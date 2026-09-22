using System.Globalization;
using System.Text;

// CA5394 and S2245 ("use a cryptographically strong generator") are disapplied for this file and no
// other, because they cannot apply: every value here must be reproducible from a seed, and a
// cryptographic generator cannot be seeded. The data is benchmark input; none of it is a secret.
#pragma warning disable CA5394, S2245

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>
/// Many short inputs, for <see cref="ManyInputsBenchmarks"/>: the shape of use the single-subject
/// <see cref="Corpus"/> cannot represent, where the cost that matters is per call rather than per
/// character.
/// </summary>
/// <remarks>
/// <para>
/// Every collection is generated from a fixed seed, so every run - and the Python side of the v1.0
/// gate, which reads what <see cref="Emit"/> writes - sees the same lines. <see cref="Random"/> with
/// a seed is deterministic across runs and across .NET versions, which is the only property wanted
/// from it here.
/// </para>
/// <para>
/// The record set is a fuzzy phrase search at volume: 100,000 records of 40 to 50 characters, most
/// of them noise, a few carrying one of a handful of known phrases of 19 or 20 characters with up to two
/// typing errors, and some carrying near misses that share words with a phrase but are further than
/// two edits from it. Searched case-insensitively with <c>{e&lt;=2}</c>, it exercises the per-call
/// cost of a fuzzy pattern that fails on almost every input, which is the expensive case.
/// </para>
/// </remarks>
internal static class UsageCorpus
{
    /// <summary>How many inputs each collection holds.</summary>
    public const int Size = 100_000;

    /// <summary>
    /// The phrases searched for, lower case, 19 or 20 characters each - the top of a 10-20 range, because a longer phrase is more work at every position. Records carry them upper case,
    /// so a search must be case-insensitive to find any.
    /// </summary>
    public static readonly string[] Phrases = ["amber lantern works", "copper field studio", "violet stone archive"];

    /// <summary>
    /// Near misses: text that shares words with a phrase but is further than two edits from it, so a
    /// correct engine must reject it - and must do the work of finding that out.
    /// </summary>
    private static readonly string[] _nearMisses = ["AMBER LANTERN PARK", "COPPER FIELD HOUSE", "VIOLET STONE BRIDGE"];

    /// <summary>Noise words for the other records, upper case to match the phrases' case.</summary>
    private static readonly string[] _noise =
    [
        "ORBIT",
        "PARCEL",
        "HARBOUR",
        "MEADOW",
        "SIGNAL",
        "GRANITE",
        "LEDGER",
        "CANVAS",
        "TIMBER",
        "FALCON",
        "RIPPLE",
        "SUMMIT",
        "BEACON",
        "MARBLE",
        "THISTLE",
    ];

    /// <summary>
    /// 100,000 records of 40 to 50 characters, uniformly, mostly letters: words to within about six
    /// characters of the end and a short numeric reference. About 5% carry a phrase (2% the first, 1.5% each of the others) with
    /// 0, 1 or 2 typos, at a random position among the words; 1.5% carry a near miss.
    /// </summary>
    public static readonly string[] Records = BuildRecords();

    /// <summary>
    /// 100,000 short strings for a validation-style check. 70% get a valid domain and 5% of all
    /// lose their <c>@</c>, so about 66.5% are valid.
    /// </summary>
    public static readonly string[] Emails = BuildEmails();

    /// <summary>100,000 log lines in one fixed layout, for group extraction.</summary>
    public static readonly string[] LogLines = BuildLogLines();

    /// <summary>100,000 lines of non-ASCII text, about 5% holding one of two words that fold.</summary>
    public static readonly string[] Accented = BuildAccented();

    /// <summary>
    /// Writes every collection as UTF-8, one input per line, so the Python side of the gate measures
    /// exactly these inputs rather than a second generator's idea of them.
    /// </summary>
    /// <param name="directory">Where to write the four files.</param>
    public static void Emit(string directory)
    {
        // LF, not File.WriteAllLines' Environment.NewLine: that is CRLF on Windows and LF elsewhere,
        // so the same seed would write different files on different machines. Readers must split on
        // line breaks WITHOUT stripping: most of the accented lines end in a space.
        Directory.CreateDirectory(directory);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Write("records.txt", Records);
        Write("emails.txt", Emails);
        Write("log-lines.txt", LogLines);
        Write("accented.txt", Accented);

        void Write(string name, string[] lines) =>
            File.WriteAllText(Path.Combine(directory, name), string.Join('\n', lines) + "\n", utf8);
    }

    private static string[] BuildRecords()
    {
        var random = new Random(20260922);
        var records = new string[Size];
        for (int i = 0; i < Size; i++)
        {
            double roll = random.NextDouble();
            string? carried = roll switch
            {
                < 0.020 => WithTypos(Phrases[0].ToUpperInvariant(), random),
                < 0.035 => WithTypos(Phrases[1].ToUpperInvariant(), random),
                < 0.050 => WithTypos(Phrases[2].ToUpperInvariant(), random),
                < 0.065 => _nearMisses[random.Next(_nearMisses.Length)],
                _ => null,
            };

            // Mostly letters, as a record of names and words is. Digits would flatter the fuzzy rows:
            // a lettered phrase rejects a run of digits within two characters, because each one is an
            // error, while letters and spaces keep partial alignments alive, which is where the work
            // is. So words fill the record, a carried phrase sits among them at a random position, and
            // only the reference at the end is numeric.
            int total = 40 + random.Next(11);
            int wordsUpTo = total - 6;
            var words = new List<string>();
            while (words.Sum(static w => w.Length + 1) < wordsUpTo)
            {
                words.Add(_noise[random.Next(_noise.Length)]);
            }
            if (carried is not null)
            {
                words.Insert(random.Next(words.Count + 1), carried);
            }

            // Noise words go first when the record runs long; a carried phrase is never cut.
            while (words.Count > 1 && words.Sum(static w => w.Length + 1) > wordsUpTo + 1)
            {
                int drop = words.FindLastIndex(w => !ReferenceEquals(w, carried));
                words.RemoveAt(drop);
            }

            var record = new StringBuilder(string.Join(' ', words)).Append(' ');
            while (record.Length < total)
            {
                record.Append((char)('0' + random.Next(10)));
            }
            records[i] = record.ToString(0, total);
        }
        return records;
    }

    /// <summary>
    /// Plants 0, 1 or 2 edits, a third of records each, as a substitution, an insertion or a deletion -
    /// the three kinds a fuzzy budget counts - so every planted phrase is within <c>{e&lt;=2}</c>.
    /// </summary>
    private static string WithTypos(string phrase, Random random)
    {
        var text = new StringBuilder(phrase);
        int edits = random.Next(3);
        for (int e = 0; e < edits; e++)
        {
            int at = random.Next(1, text.Length - 1);
            char letter = (char)('A' + random.Next(26));
            switch (random.Next(3))
            {
                case 0:
                    text[at] = letter;
                    break;
                case 1:
                    text.Insert(at, letter);
                    break;
                default:
                    text.Remove(at, 1);
                    break;
            }
        }
        return text.ToString();
    }

    private static string[] BuildEmails()
    {
        var random = new Random(20260923);
        string[] names = ["j.smith", "alice", "bob_99", "priya.k", "o'neil", "x", "ma+tag"];
        string[] domains =
        [
            "example.com",
            "mail.co.uk",
            "sub.domain.org",
            "localhost",
            "exa mple.com",
            "bad..dots.com",
        ];
        var emails = new string[Size];
        for (int i = 0; i < Size; i++)
        {
            string name = names[random.Next(names.Length)];
            string domain = random.NextDouble() < 0.7 ? domains[random.Next(3)] : domains[3 + random.Next(3)];
            emails[i] = random.NextDouble() < 0.05 ? name + domain : name + "@" + domain;
        }
        return emails;
    }

    private static string[] BuildLogLines()
    {
        var random = new Random(20260924);
        string[] levels = ["INFO", "WARN", "ERROR", "DEBUG"];
        string[] messages =
        [
            "request served in 12 ms",
            "cache miss for key user:4411",
            "retrying upstream call",
            "connection reset by peer",
        ];
        var lines = new string[Size];
        for (int i = 0; i < Size; i++)
        {
            lines[i] = string.Create(
                CultureInfo.InvariantCulture,
                $"2026-{random.Next(1, 13):00}-{random.Next(1, 29):00} {levels[random.Next(levels.Length)]} {messages[random.Next(messages.Length)]}"
            );
        }
        return lines;
    }

    private static string[] BuildAccented()
    {
        var random = new Random(20260925);
        string[] words = ["Größe", "naïve", "résumé", "Ångström", "façade", "Øresund", "jalapeño", "Zürich"];
        string[] targets = ["STRASSE", "Café"];
        var lines = new string[Size];
        for (int i = 0; i < Size; i++)
        {
            var line = new StringBuilder();
            for (int w = 0; w < 5; w++)
            {
                line.Append(words[random.Next(words.Length)]).Append(' ');
            }
            if (random.NextDouble() < 0.05)
            {
                line.Append(targets[random.Next(targets.Length)]);
            }
            lines[i] = line.ToString();
        }
        return lines;
    }
}
