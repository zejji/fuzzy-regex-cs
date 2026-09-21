// The case on screen, as the C# that produced it.
//
// The demo is an argument that this library is usable, and the question a visitor asks next is
// "what do I write?". The answer is the code the demo itself runs: every call below is one
// DemoEngine.cs makes (its constructor, EnumerateMatches, Match and Replace), with the same
// arguments in the same order, so a snippet that is wrong is a demo that is wrong about itself.
// The one thing left out is the page's display caps, which are this page's and not the library's -
// see `body`.
//
// A pure function over `Inputs` and nothing else. No DOM, no engine, no clipboard: what it returns
// is text, which is what makes the whole of it testable and what lets `page.test.ts` assert on the
// panel without owning the language rules.
//
// It is NOT linted by `copy.test.ts` (see `tests/copy-sources.ts`): this file is C# with a comment
// in it, and interface-copy rules applied to code would ban words the code needs.

import type { Inputs } from '../types';

import { MATCH_TIMEOUT_SECONDS } from './caps';
import { FLAG_NAMES } from './flags';

/**
 * Every name from this library that the snippet prints, in one place.
 *
 * TypeScript has no `nameof`, so a type or member the generator names is a string literal here, and
 * a rename in the library would otherwise ship a snippet that does not compile - silently, because
 * nothing on the page reads the panel's text. The templates below interpolate these rather than
 * spelling the names out, which is what makes this record the only copy.
 *
 * `DemoSnippetTests.The_snippet_names_the_library_the_library_names_itself` holds each entry against
 * `nameof(...)` on the real API, and the `partial` argument against the method's own
 * `ParameterInfo`, so a rename fails the build rather than the browser. The keys are roles and not
 * spellings: `matchType` and `matchMethod` are the same word today and a rename could move one
 * without the other.
 *
 * What is NOT here: `Console`, `TimeSpan`, `Dictionary`, `IReadOnlyCollection` and `string`. Those
 * are the framework's and nothing in this repository can rename them.
 */
export const CSHARP_API = {
    // The NuGet package, pinned against `<PackageId>` in src/FuzzyRegex/FuzzyRegex.csproj. The same
    // word as the type today and a separate fact: renaming the type does not rename the package.
    packageId: 'FuzzyRegex',
    namespace: 'Fuzzy.Text.RegularExpressions',
    regexType: 'FuzzyRegex',
    optionsType: 'FuzzyRegexOptions',
    matchType: 'Match',
    countsType: 'FuzzyCounts',
    enumerateMatches: 'EnumerateMatches',
    matchMethod: 'Match',
    replace: 'Replace',
    partialParameter: 'partial',
    success: 'Success',
    index: 'Index',
    length: 'Length',
    partialMatch: 'PartialMatch',
    fuzzyCounts: 'FuzzyCounts',
    substitutions: 'Substitutions',
    insertions: 'Insertions',
    deletions: 'Deletions',
} as const;

/**
 * What `String.Trim()` strips, which is not what JavaScript's `trim()` strips.
 *
 * Measured on 2026-09-19 by walking the whole BMP: `char.IsWhiteSpace` is true for U+0085 and false
 * for U+FEFF, and JavaScript's `trim()` is the other way round on both. The twenty-four other
 * characters agree. Every box the engine reads it trims this way - the mode (`Trim()`), each flag
 * token and each list word (`StringSplitOptions.TrimEntries`), a list's name (`Trim()`) - so a
 * snippet that used the language's own `trim()` would read `#m=partial%C2%85` as a walk while the
 * page above it showed a partial answer.
 */
const IS_DOTNET_WHITESPACE = /^[\t-\r \u0085\u{a0}\u{1680}\u{2000}-\u{200a}\u{2028}\u{2029}\u{202f}\u{205f}\u{3000}]$/u;

/**
 * `String.Trim()`, spelt out because the two languages disagree about two characters.
 *
 * An index walk from each end, which is what `Trim()` itself does, and not one regex over the whole
 * value: written as `^[ws]+|[ws]+$` the trailing alternative restarts inside every interior run of
 * whitespace, which made a 100,000-character flag box take 3.9 seconds - and the panel recomputes
 * the snippet on every keystroke. `tests/snippet.test.ts` holds the budget.
 */
function trimmed(value: string): string {
    let start = 0;
    let end = value.length;

    while (start < end && IS_DOTNET_WHITESPACE.test(value[start] as string)) start += 1;
    while (end > start && IS_DOTNET_WHITESPACE.test(value[end - 1] as string)) end -= 1;

    return value.slice(start, end);
}

/** The separators the engine accepts between flag names: `DemoEngine._flagSeparators`. */
const FLAG_SEPARATORS = /[, |\t]+/;

/** The separators a named list's words may use: `DemoEngine._wordSeparators`. */
const WORD_SEPARATORS = /[,;]+/;

/** The line separators a named-list block may use: `DemoEngine._lineSeparators`. */
const LINE_SEPARATORS = /\r\n|\n|\r/;

/**
 * A line terminator no raw string literal can carry intact.
 *
 * C# ends a line on a carriage return, U+0085, U+2028 or U+2029 exactly as it does on a newline, so
 * a raw literal holding one is a literal whose lines are not the lines this generator laid out:
 * `a\r\nb` came back from the compiler as `a\nb` (DIFFERENT), and a U+2028 inside an indented one is
 * `error CS8999: Line does not start with the same whitespace as the closing line of the raw string
 * literal`. Both measured by `tools/probes/demo-snippet-compiles.mjs` on 2026-09-19.
 */
const RAW_HOSTILE = /[\r\u0085\u{2028}\u{2029}]/u;

/** What an escaped literal spells out, plus every control character, which becomes `\uXXXX`. */
const NEEDS_ESCAPE = /["\\\n\r\t\u0000-\u001f\u007f-\u009f\u0085\u{2028}\u{2029}]/gu;
const ESCAPES = new Map([
    ['\\', '\\\\'],
    ['"', '\\"'],
    ['\n', '\\n'],
    ['\r', '\\r'],
    ['\t', '\\t'],
]);

/**
 * The ordinary C# string literal, with every character spelt out.
 *
 * The form of last resort, and the only one that does not care how an editor stores a line: it is
 * one line of source whatever the value holds. Escaped per UTF-16 code unit, so a surrogate pair
 * needing escapes becomes two `\uXXXX` - which is a pair again by the time the compiler is done.
 */
function escaped(value: string): string {
    const body = value.replaceAll(
        NEEDS_ESCAPE,
        (char) => ESCAPES.get(char) ?? `\\u${char.charCodeAt(0).toString(16).padStart(4, '0')}`,
    );
    return `"${body}"`;
}

/**
 * A string as C# source.
 *
 * Three forms, chosen by the value and not by preference. A verbatim string carries anything on one
 * line: a backslash is not an escape in one, so a pattern ending in `\` needs no care at all, and
 * the only character with meaning is `"`, which is written twice. A value holding a newline cannot
 * be verbatim without putting the rest of the page's indentation inside the string, so it becomes a
 * raw string literal, whose closing fence sets the indentation stripped from every line. A value
 * holding a line terminator a raw literal cannot carry (see `RAW_HOSTILE`) is escaped instead -
 * less readable, and the only form that gives the compiler back the string the page holds.
 *
 * @param indent The column the closing fence sits at, which is the indentation the compiler removes
 *   from each content line. The caller knows it; the literal cannot.
 */
export function literal(value: string, indent = 0): string {
    if (RAW_HOSTILE.test(value)) return escaped(value);
    if (!value.includes('\n')) return `@"${value.replaceAll('"', '""')}"`;

    // Longer than the longest run of quotes inside it, and never shorter than three: a fence the
    // content also contains is a literal that ends in the middle of itself.
    const longest = [...value.matchAll(/"+/g)].reduce((most, run) => Math.max(most, run[0].length), 0);
    const fence = '"'.repeat(Math.max(3, longest + 1));
    const pad = ' '.repeat(indent);

    // The newline after the opening fence and the one before the closing fence are the literal's
    // own, so an empty content line is an empty line in the value and a value ending in a newline
    // is one blank line before the fence. An empty line is left empty rather than padded: the
    // compiler allows a blank line to be shorter than the fence, and trailing whitespace is the
    // first thing an editor strips.
    return [
        fence,
        ...value.split(LINE_SEPARATORS).map((line) => (line === '' ? '' : pad + line)),
        pad + fence,
    ].join('\n');
}

/** The flag box as a `FuzzyRegexOptions` expression, with every member named in full. */
function options(flags: string): string {
    const named = flags
        .split(FLAG_SEPARATORS)
        .map((token) => trimmed(token))
        .filter((token) => token !== '')
        // Unknown tokens are printed as they were typed. The panel only opens on a case the engine
        // answered, so an unknown flag cannot arrive from the page; inventing a different flag set,
        // or quietly dropping one, would be a snippet that matches something else.
        .map((token) => FLAG_NAMES.find((name) => name.toLowerCase() === token.toLowerCase()) ?? token);

    return (named.length === 0 ? ['None'] : named)
        .map((name) => `${CSHARP_API.optionsType}.${name}`)
        .join(' | ');
}

/**
 * The named-list block as a dictionary initialiser, read exactly as `DemoEngine.TryParseNamedLists`
 * reads it: one list per line, words trimmed, empties dropped, and the first definition of a name
 * the one that stands (`Dictionary.TryAdd`).
 *
 * Returns null when there is nothing to pass, which is the engine's own choice too - it hands the
 * constructor a null rather than an empty dictionary.
 */
function namedLists(block: string): string | null {
    const lists = new Map<string, readonly string[]>();
    for (const line of block.split(LINE_SEPARATORS)) {
        const colon = line.indexOf(':');
        if (colon <= 0) continue;

        const name = trimmed(line.slice(0, colon));
        const words = line
            .slice(colon + 1)
            .split(WORD_SEPARATORS)
            .map((word) => trimmed(word))
            .filter((word) => word !== '');
        if (words.length === 0 || lists.has(name)) continue;
        lists.set(name, words);
    }
    if (lists.size === 0) return null;

    const rows = [...lists].map(
        ([name, words]) => `        [${literal(name, 8)}] = [${words.map((word) => literal(word, 8)).join(', ')}],`,
    );
    return ['    new Dictionary<string, IReadOnlyCollection<string>>', '    {', ...rows, '    }'].join('\n');
}

/**
 * What the page does with the answer, per mode, in the demo's own order of interest.
 *
 * The walk prints the span and the three error counts, because those are the columns the table
 * shows. Partial mode prints whether the subject ran out, because that is the whole point of it.
 * Replace prints the rewritten subject. Anything that is not a mode the page offers is the walk,
 * which is what an unknown mode means to the engine.
 */
function body(inputs: Inputs): string {
    const subject = literal(inputs.subject, 4);
    // Destructured so that each template below reads as the C# it prints. The names on the left are
    // the roles; the values are what the library calls them today.
    const {
        matchType: Match,
        countsType: FuzzyCounts,
        enumerateMatches: EnumerateMatches,
        matchMethod: MatchMethod,
        replace: Replace,
        partialParameter,
        success: Success,
        index: Index,
        length: Length,
        partialMatch: PartialMatch,
        fuzzyCounts: FuzzyCountsOf,
        substitutions: Substitutions,
        insertions: Insertions,
        deletions: Deletions,
    } = CSHARP_API;

    // Read as `DemoEngine.TryParseMode` reads it - `mode.Trim().ToLowerInvariant()` - because the
    // fragment carries the mode as typed and `#m=Partial` is a case the engine answers. Compared
    // exactly, the page would show a partial answer with a walk in the panel beneath it.
    const mode = trimmed(inputs.mode).toLowerCase();

    if (mode === 'partial') {
        return `${Match} match = regex.${MatchMethod}(${subject}, ${partialParameter}: true);
if (match.${Success})
{
    Console.WriteLine($"{match.${Index}}+{match.${Length}} partial={match.${PartialMatch}}");
}
`;
    }

    // No `count:`, though DemoEngine passes `count: MaxMatches`: 1,000 is the page's own display
    // cap - it says out loud when it hits one - and a visitor's Replace should rewrite the whole
    // subject rather than inherit a limit that belongs to this page. The walk below omits the same
    // cap for the same reason. The timeout is not dropped, only moved: it is on the regex above.
    if (mode === 'replace') {
        return `string replaced = regex.${Replace}(${subject}, ${literal(inputs.replacement, 4)});
Console.WriteLine(replaced);
`;
    }

    return `foreach (${Match} match in regex.${EnumerateMatches}(${subject}))
{
    ${FuzzyCounts} counts = match.${FuzzyCountsOf};
    Console.WriteLine($"{match.${Index}}+{match.${Length}} s={counts.${Substitutions}} i={counts.${Insertions}} d={counts.${Deletions}}");
}
`;
}

/**
 * The whole snippet for a case: paste it into a console project and it prints what the page shows.
 *
 * One argument per line, and the flags enum never abbreviated, because the owner's rule for this
 * panel is that the call and its options are obvious at a glance. The timeout is the demo's own
 * (`MATCH_TIMEOUT_SECONDS`, which mirrors `DemoEngine.MatchTimeout`) and says so, so that a visitor
 * sees a timeout as the ordinary way to call this library rather than as something the demo needed.
 *
 * `using Fuzzy.Text.RegularExpressions;` is the only using, and `System` is not among them: a
 * console project has implicit usings, which is where `Console`, `TimeSpan` and `Dictionary` come
 * from. Verified by compiling an emitted snippet - see the S73 sitting notes.
 */
export function toCSharp(inputs: Inputs): string {
    const lists = namedLists(inputs.namedLists);
    const timeout = `    TimeSpan.FromSeconds(${MATCH_TIMEOUT_SECONDS})${lists === null ? ');' : ','} // the demo's own timeout`;

    return [
        `// dotnet add package ${CSHARP_API.packageId}`,
        `using ${CSHARP_API.namespace};`,
        '',
        `${CSHARP_API.regexType} regex = new(`,
        `    ${literal(inputs.pattern, 4)},`,
        `    ${options(inputs.flags)},`,
        timeout,
        ...(lists === null ? [] : [lists + ');']),
        '',
        body(inputs),
    ].join('\n');
}

/** What a run of characters is, for colour. Five classes and no grammar. */
export type TokenKind = 'keyword' | 'string' | 'comment' | 'number' | 'other';

export interface Token {
    readonly kind: TokenKind;
    readonly text: string;
}

/** The keywords this generator can emit. A list, not a language: nothing else reaches the panel. */
const KEYWORDS = new Set(['using', 'new', 'foreach', 'in', 'if', 'string', 'true', 'false']);

/**
 * Splits C# into coloured runs, for a panel that renders spans from an array.
 *
 * Deliberately a tokenizer of about sixty lines and not a highlighter. highlight.js, Prism and
 * Shiki each weigh more than this whole page, and what is being coloured is one language and one
 * generator's output, which is the narrowest possible input. The output is spans with text in them:
 * nothing here is ever handed to `v-html`, so a subject holding `<script>` is text on the screen.
 *
 * Every character comes back out - `tests/snippet.test.ts` asserts the round trip - so a construct
 * this does not know becomes uncoloured text rather than text that disappears.
 */
export function tokenize(source: string): Token[] {
    const tokens: Token[] = [];
    let other = '';
    let at = 0;

    const flush = (): void => {
        if (other !== '') tokens.push({ kind: 'other', text: other });
        other = '';
    };
    const take = (kind: TokenKind, text: string): void => {
        flush();
        tokens.push({ kind, text });
        at += text.length;
    };

    while (at < source.length) {
        const rest = source.slice(at);
        const before = at === 0 ? '' : source[at - 1];

        // A raw string literal first: its fence is three or more quotes, and inside it a `//` is
        // text and a shorter run of quotes is text. Unterminated, it runs to the end, which is what
        // the compiler does with it too.
        const raw = /^("{3,})/.exec(rest);
        if (raw !== null) {
            const fence = raw[1] as string;
            const end = source.indexOf(fence, at + fence.length);
            take('string', end === -1 ? rest : source.slice(at, end + fence.length));
            continue;
        }

        // A verbatim or interpolated string. `""` inside a verbatim one is an escaped quote, so the
        // scan skips pairs; an interpolated string here never spans a line. A backslash is an
        // escape in every form BUT the verbatim one, where `@"c:\"` ends at that quote - read as an
        // escape it swallowed the rest of the snippet into one string run.
        const quoted = /^(@|\$)?"/.exec(rest);
        if (quoted !== null) {
            const verbatim = quoted[1] === '@';
            const start = (quoted[1] ?? '').length + 1;
            let end = at + start;
            while (end < source.length && source[end] !== '"') end += !verbatim && source[end] === '\\' ? 2 : 1;
            while (source[end + 1] === '"') {
                end += 2;
                while (end < source.length && source[end] !== '"') end += 1;
            }
            take('string', source.slice(at, Math.min(end + 1, source.length)));
            continue;
        }

        if (rest.startsWith('//')) {
            const line = /^[^\n]*/.exec(rest) as RegExpExecArray;
            take('comment', line[0]);
            continue;
        }

        const word = /^[A-Za-z_][A-Za-z0-9_]*/.exec(rest);
        if (word !== null) {
            const text = word[0];
            if (KEYWORDS.has(text)) take('keyword', text);
            else {
                other += text;
                at += text.length;
            }
            continue;
        }

        // A digit that follows a letter belongs to the name it is in: `Version1` is one identifier.
        const digits = /^[0-9]+/.exec(rest);
        if (digits !== null && !/[A-Za-z0-9_]/.test(before ?? '')) {
            take('number', digits[0]);
            continue;
        }

        other += source[at];
        at += 1;
    }

    flush();
    return tokens;
}
