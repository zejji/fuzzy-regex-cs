// Boost.Regex's half of tools/matrix/survey.py (Boost.Regex headers a640597, standalone, in WSL).
//
//     worker <rows.jsonl> <start-index>
//
// survey.py builds this in WSL (see survey._boost_worker) with
//     g++ -O2 -std=c++17 -I<boost-regex>/include survey_worker_boost.cpp
// and runs it there. It reads translated rows from a FILE, prints READY and then one JSON line per
// row, flushed, in survey_worker.pl's shape.
//
// Each row is answered in a forked child with a CPU and memory limit, so a runaway row is reported
// as "timeout" or "memory" here, before survey.py's own watchdog (which kills and restarts the
// worker) has to act. Boost's own complexity limit, which throws, is reported as an error.
//
// Operations, with Perl syntax (boost::regex::perl), no match_prev_avail and no slice (survey.py
// refuses pos/endpos rows for this engine):
//   search    regex_search
//   match     regex_search with match_continuous: the match must start at 0
//   fullmatch regex_match: anchored at both ends, found by backtracking
//   finditer  sregex_iterator, which after an empty match searches again at the same place with
//             match_not_initial_null: Perl's //g rule, and Python's
// Flags are the letters i, m and s, applied as an inline (?ims-ms) group in front of the pattern,
// with the letters that are not set switched off: Boost's Perl syntax treats ^ and $ as
// multi-line unless told otherwise (no_mod_m), which Perl and upstream do not. survey.py rewrites a
// single-line `$`, which Boost reads as the end of the buffer only.
// Spans are byte offsets; survey.py sends this engine ASCII rows only, so they are codepoints.

#define BOOST_REGEX_STANDALONE
#include <boost/regex.hpp>

#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <map>
#include <memory>
#include <sstream>
#include <string>
#include <vector>

#include <signal.h>
#include <sys/resource.h>
#include <sys/wait.h>
#include <unistd.h>

namespace {

// ------------------------------------------------------------ a small JSON reader
// Enough for survey.py's translated rows: objects, arrays, strings (with \uXXXX), integers,
// true, false and null.
struct Json {
    enum Kind { Null, Bool, Number, String, Array, Object } kind = Null;
    bool b = false;
    long long n = 0;
    std::string s;
    std::vector<Json> a;
    std::map<std::string, Json> o;

    const Json* get(const std::string& k) const {
        auto it = o.find(k);
        return it == o.end() ? nullptr : &it->second;
    }
};

void put_utf8(std::string& out, unsigned cp) {
    if (cp < 0x80) {
        out += static_cast<char>(cp);
    } else if (cp < 0x800) {
        out += static_cast<char>(0xC0 | (cp >> 6));
        out += static_cast<char>(0x80 | (cp & 0x3F));
    } else if (cp < 0x10000) {
        out += static_cast<char>(0xE0 | (cp >> 12));
        out += static_cast<char>(0x80 | ((cp >> 6) & 0x3F));
        out += static_cast<char>(0x80 | (cp & 0x3F));
    } else {
        out += static_cast<char>(0xF0 | (cp >> 18));
        out += static_cast<char>(0x80 | ((cp >> 12) & 0x3F));
        out += static_cast<char>(0x80 | ((cp >> 6) & 0x3F));
        out += static_cast<char>(0x80 | (cp & 0x3F));
    }
}

struct Parser {
    const std::string& t;
    size_t k = 0;

    void ws() {
        while (k < t.size() && (t[k] == ' ' || t[k] == '\t' || t[k] == '\r' || t[k] == '\n')) {
            ++k;
        }
    }

    unsigned hex4() {
        unsigned v = static_cast<unsigned>(std::stoul(t.substr(k, 4), nullptr, 16));
        k += 4;
        return v;
    }

    std::string str() {
        std::string out;
        ++k;  // the opening quote
        while (t.at(k) != '"') {
            char c = t[k++];
            if (c != '\\') {
                out += c;
                continue;
            }
            char e = t.at(k++);
            switch (e) {
                case 'n': out += '\n'; break;
                case 't': out += '\t'; break;
                case 'r': out += '\r'; break;
                case 'b': out += '\b'; break;
                case 'f': out += '\f'; break;
                case 'u': {
                    unsigned cp = hex4();
                    if (cp >= 0xD800 && cp < 0xDC00 && t.compare(k, 2, "\\u") == 0) {
                        k += 2;
                        cp = 0x10000 + ((cp - 0xD800) << 10) + (hex4() - 0xDC00);
                    }
                    put_utf8(out, cp);
                    break;
                }
                default: out += e; break;  // \" \\ \/
            }
        }
        ++k;
        return out;
    }

    Json value() {
        ws();
        Json v;
        char c = t.at(k);
        if (c == '{') {
            v.kind = Json::Object;
            ++k;
            ws();
            if (t.at(k) == '}') {
                ++k;
                return v;
            }
            while (true) {
                ws();
                std::string name = str();
                ws();
                ++k;  // ':'
                v.o[name] = value();
                ws();
                if (t.at(k++) == '}') {
                    return v;
                }
            }
        }
        if (c == '[') {
            v.kind = Json::Array;
            ++k;
            ws();
            if (t.at(k) == ']') {
                ++k;
                return v;
            }
            while (true) {
                v.a.push_back(value());
                ws();
                if (t.at(k++) == ']') {
                    return v;
                }
            }
        }
        if (c == '"') {
            v.kind = Json::String;
            v.s = str();
            return v;
        }
        if (t.compare(k, 4, "true") == 0) {
            v.kind = Json::Bool;
            v.b = true;
            k += 4;
            return v;
        }
        if (t.compare(k, 5, "false") == 0) {
            v.kind = Json::Bool;
            k += 5;
            return v;
        }
        if (t.compare(k, 4, "null") == 0) {
            k += 4;
            return v;
        }
        size_t used = 0;
        v.kind = Json::Number;
        v.n = std::stoll(t.substr(k), &used);
        k += used;
        return v;
    }
};

// ------------------------------------------------------------ JSON out
std::string quoted(const std::string& s) {
    std::string out = "\"";
    for (unsigned char c : s) {
        if (c == '"' || c == '\\') {
            out += '\\';
            out += static_cast<char>(c);
        } else if (c < 0x20 || c >= 0x7F) {
            char buf[8];
            std::snprintf(buf, sizeof buf, "\\u%04x", c);
            out += buf;
        } else {
            out += static_cast<char>(c);
        }
    }
    return out + "\"";
}

std::string span(long long a, long long b) {
    return "[" + std::to_string(a) + ", " + std::to_string(b) + "]";
}

// ------------------------------------------------------------ one row
constexpr boost::smatch::size_type Z = 0;  // the whole match (position(0) is ambiguous)

std::string answer(const Json& row) {
    const std::string head = "{\"i\": " + std::to_string(row.get("i")->n) + ", \"unit\": \"cp\", ";
    const std::string& letters = row.get("flags") && row.get("flags")->kind == Json::String ? row.get("flags")->s : "";
    std::string on, off;
    for (char c : std::string("ims")) {
        if (letters.find(c) != std::string::npos) {
            on += c;
        } else if (c != 'i') {
            off += c;
        }
    }
    const std::string pattern = "(?" + on + "-" + off + ")" + row.get("pattern")->s;
    const std::string& subject = row.get("subject")->s;
    const std::string& op = row.get("op")->s;
    const long long ngroups = row.get("ngroups") ? row.get("ngroups")->n : 0;

    boost::regex re;
    try {
        re.assign(pattern, boost::regex::perl);
    } catch (const std::exception& e) {
        return head + "\"status\": \"error\", \"error\": " + quoted(std::string("compile: ") + e.what()) + "}";
    }
    try {
        if (op == "finditer") {
            std::string out;
            int count = 0;
            for (boost::sregex_iterator it(subject.begin(), subject.end(), re), end; it != end; ++it) {
                if (count++) {
                    out += ", ";
                }
                out += "{\"span\": " + span(it->position(Z), it->position(Z) + it->length(Z)) + ", \"partial\": false}";
                if (count > 50) {
                    break;
                }
            }
            return head + "\"status\": \"matches\", \"matches\": [" + out + "]}";
        }
        boost::smatch m;
        bool found = op == "fullmatch" ? boost::regex_match(subject, m, re)
                     : op == "match"   ? boost::regex_search(subject, m, re, boost::match_continuous)
                                       : boost::regex_search(subject, m, re);
        if (!found) {
            return head + "\"status\": \"none\"}";
        }
        std::string groups;
        for (long long g = 1; g <= ngroups; ++g) {
            if (g > 1) {
                groups += ", ";
            }
            groups += g < static_cast<long long>(m.size()) && m[g].matched
                          ? span(m.position(static_cast<boost::smatch::size_type>(g)), m.position(static_cast<boost::smatch::size_type>(g)) + m.length(static_cast<int>(g)))
                          : std::string("null");
        }
        return head + "\"status\": \"match\", \"span\": " + span(m.position(Z), m.position(Z) + m.length(Z))
               + ", \"groups\": [" + groups + "]}";
    } catch (const std::exception& e) {
        return head + "\"status\": \"error\", \"error\": " + quoted(std::string("match: ") + e.what()) + "}";
    }
}

// Answers the row in a child process, under a CPU limit of two seconds (survey.py's own watchdog
// allows three) and an address-space limit of 1 GB.
std::string answer_in_child(const Json& row) {
    const std::string head = "{\"i\": " + std::to_string(row.get("i")->n) + ", \"unit\": \"cp\", ";
    int fds[2];
    if (pipe(fds) != 0) {
        return head + "\"status\": \"error\", \"error\": \"pipe failed\"}";
    }
    pid_t pid = fork();
    if (pid == 0) {
        close(fds[0]);
        rlimit cpu{2, 2};
        setrlimit(RLIMIT_CPU, &cpu);
        rlimit mem{1UL << 30, 1UL << 30};
        setrlimit(RLIMIT_AS, &mem);
        std::string out;
        try {
            out = answer(row);
        } catch (const std::bad_alloc&) {
            _exit(3);
        }
        size_t done = 0;
        while (done < out.size()) {
            ssize_t w = write(fds[1], out.data() + done, out.size() - done);
            if (w <= 0) {
                _exit(4);
            }
            done += static_cast<size_t>(w);
        }
        _exit(0);
    }
    close(fds[1]);
    std::string out;
    char buf[4096];
    ssize_t r;
    while ((r = read(fds[0], buf, sizeof buf)) > 0) {
        out.append(buf, static_cast<size_t>(r));
    }
    close(fds[0]);
    int status = 0;
    waitpid(pid, &status, 0);
    if (WIFSIGNALED(status) && (WTERMSIG(status) == SIGXCPU || WTERMSIG(status) == SIGKILL)) {
        return head + "\"status\": \"timeout\"}";
    }
    if ((WIFEXITED(status) && WEXITSTATUS(status) == 3) || (WIFSIGNALED(status) && WTERMSIG(status) == SIGABRT)) {
        return head + "\"status\": \"memory\"}";
    }
    if (!WIFEXITED(status) || WEXITSTATUS(status) != 0 || out.empty()) {
        return head + "\"status\": \"crash\"}";
    }
    return out;
}

}  // namespace

int main(int argc, char** argv) {
    if (argc != 3) {
        std::fprintf(stderr, "usage: worker <rows.jsonl> <start-index>\n");
        return 2;
    }
    std::ifstream in(argv[1], std::ios::binary);
    if (!in) {
        std::fprintf(stderr, "cannot read %s\n", argv[1]);
        return 2;
    }
    std::vector<std::string> lines;
    for (std::string line; std::getline(in, line);) {
        if (line.find_first_not_of(" \t\r") != std::string::npos) {
            lines.push_back(line);
        }
    }
    const size_t start = std::strtoul(argv[2], nullptr, 10);
    std::printf("READY\n");
    std::fflush(stdout);
    for (size_t k = start; k < lines.size(); ++k) {
        Parser p{lines[k]};
        Json row = p.value();
        std::printf("%s\n", answer_in_child(row).c_str());
        std::fflush(stdout);
    }
    return 0;
}
