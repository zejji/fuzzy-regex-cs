// The C# the panel shows, asserted whole.
//
// Whole snippets and not fragments: what a visitor copies is a file they paste into a console
// project, so the thing under test is the text in full - the using, the blank lines, the closing
// bracket. A test that checked `toContain('EnumerateMatches')` would pass on a snippet missing its
// constructor.
//
// One of these snippets is compiled for real during the sitting that writes it and its output
// quoted in the closing notes, because a snippet nobody has compiled is a guess. These tests then
// hold that shape in place.

import { expect, test } from 'vitest';

import { MATCH_TIMEOUT_SECONDS } from '../src/lib/caps';
import { toCSharp, tokenize } from '../src/lib/snippet';
import type { Inputs } from '../src/types';

import { walk } from './inputs';

const inputs = (extra: Partial<Inputs>): Inputs => ({
    pattern: 'a',
    flags: '',
    subject: 'b',
    mode: '',
    replacement: '',
    namedLists: '',
    ...extra,
});

test('the default case prints the walk, whole', () => {
    expect(toCSharp(walk('(?:colour){e<=2}', '', 'the color of the collar'))).toBe(
        `// dotnet add package FuzzyRegex
using Fuzzy.Text.RegularExpressions;

FuzzyRegex regex = new(
    @"(?:colour){e<=2}",
    FuzzyRegexOptions.None,
    TimeSpan.FromSeconds(2)); // the demo's own timeout

foreach (Match match in regex.EnumerateMatches(@"the color of the collar"))
{
    FuzzyCounts counts = match.FuzzyCounts;
    Console.WriteLine($"{match.Index}+{match.Length} s={counts.Substitutions} i={counts.Insertions} d={counts.Deletions}");
}
`,
    );
});

test('partial mode asks for one match and checks it', () => {
    expect(toCSharp(inputs({ pattern: '\\d{4}-\\d{2}-\\d{2}', subject: '2026-09', mode: 'partial' }))).toBe(
        `// dotnet add package FuzzyRegex
using Fuzzy.Text.RegularExpressions;

FuzzyRegex regex = new(
    @"\\d{4}-\\d{2}-\\d{2}",
    FuzzyRegexOptions.None,
    TimeSpan.FromSeconds(2)); // the demo's own timeout

Match match = regex.Match(@"2026-09", partial: true);
if (match.Success)
{
    Console.WriteLine($"{match.Index}+{match.Length} partial={match.PartialMatch}");
}
`,
    );
});

// No `count:` in the expected text, and that omission is the decision: DemoEngine passes
// `count: MaxMatches`, but 1,000 is the page's own display cap - which the page says out loud when
// it hits one - and a visitor's Replace should rewrite the whole subject rather than inherit a
// limit that belongs to this page. Asserting the whole snippet is what holds it: a separate
// `not.toContain('count:')` could not fail unless this test failed first.
test('replace mode prints the rewritten subject', () => {
    expect(
        toCSharp(
            inputs({
                pattern: '(?<year>\\d{4})-(?<month>\\d{2})',
                subject: '2026-09 and 1999-12',
                mode: 'replace',
                replacement: '\\g<month>/\\g<year>',
            }),
        ),
    ).toBe(
        `// dotnet add package FuzzyRegex
using Fuzzy.Text.RegularExpressions;

FuzzyRegex regex = new(
    @"(?<year>\\d{4})-(?<month>\\d{2})",
    FuzzyRegexOptions.None,
    TimeSpan.FromSeconds(2)); // the demo's own timeout

string replaced = regex.Replace(@"2026-09 and 1999-12", @"\\g<month>/\\g<year>");
Console.WriteLine(replaced);
`,
    );
});

// A mode the page does not offer is the walk, which is what the ENGINE does with an empty one. The
// page only shows the panel for a case the engine answered, so this is the pure function refusing
// to invent a fourth shape rather than a path a visitor can reach.
test('a mode nobody offers prints the walk', () => {
    expect(toCSharp(inputs({ mode: 'sideways' }))).toContain('regex.EnumerateMatches(@"b")');
});

// The panel recomputes the snippet on every keystroke, and neither the flag box nor the mode is
// length-capped, so a trim that is not linear is a frozen tab. Written as a regex with an
// unanchored `[ws]+$` alternative it was quadratic - measured 66 ms, 253 ms, 986 ms and 3,927 ms
// for 12.5k, 25k, 50k and 100k characters of U+00A0 in the flag box on 2026-09-19 - and the index
// walk that replaced it does the same 100k in a millisecond. The budget is loose enough not to
// flake on a busy machine and tight enough that the quadratic version could not pass it.
test('a long run of whitespace does not freeze the panel', () => {
    const flags = `a${'\u{a0}'.repeat(100_000)}b`;
    const started = performance.now();
    toCSharp({ ...walk('a', flags, 's'), namedLists: '' });

    expect(performance.now() - started).toBeLessThan(200);
});

// DemoEngine.TryParseMode reads `mode.Trim().ToLowerInvariant()`, and the fragment hands the mode
// over as typed: `#m=Partial` is a case the engine answers as partial. Compared exactly, the panel
// would show a walk beside a partial answer.
test('the mode is read the way the engine reads it, trimmed and in any case', () => {
    expect(toCSharp(inputs({ mode: 'Partial' }))).toContain('regex.Match(@"b", partial: true)');
    expect(toCSharp(inputs({ mode: ' partial ' }))).toContain('regex.Match(@"b", partial: true)');
    expect(toCSharp(inputs({ mode: 'REPLACE', replacement: 'x' }))).toContain('regex.Replace(@"b", @"x")');
});

// `String.Trim()` is not JavaScript's `trim()`. Measured with `char.IsWhiteSpace` over the whole
// BMP on 2026-09-19: .NET trims U+0085 and JavaScript does not, JavaScript trims U+FEFF and .NET
// does not, and the other twenty-four characters are the same. Every box the engine reads it trims
// this way - the mode, each flag token, a list's name and its words - so the snippet must too.
test('the trims are the engine’s trims, not JavaScript’s', () => {
    expect(toCSharp(inputs({ mode: 'partial\u{85}' }))).toContain('regex.Match(@"b", partial: true)');
    expect(toCSharp(walk('a', 'BestMatch\u{85}', 'b'))).toContain('FuzzyRegexOptions.BestMatch,');
    expect(toCSharp(inputs({ namedLists: 'fruit\u{85}: apple\u{85}' }))).toContain('[@"fruit"] = [@"apple"],');
    // The other way round: C# keeps a U+FEFF, so the snippet keeps it and the dictionary key is the
    // key the engine built. Invisible in the panel, and the same string underneath.
    expect(toCSharp(inputs({ namedLists: 'fruit\u{feff}: apple' }))).toContain('[@"fruit\u{feff}"] = [@"apple"],');
});

test('no flags is None, and every flag named in full on its own line', () => {
    expect(toCSharp(walk('a', '', 'b'))).toContain('    FuzzyRegexOptions.None,\n');
    expect(toCSharp(walk('a', 'BestMatch', 'b'))).toContain('    FuzzyRegexOptions.BestMatch,\n');
    expect(toCSharp(walk('a', 'IgnoreCase, BestMatch', 'b'))).toContain(
        '    FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.BestMatch,\n',
    );
});

// The engine reads flag names case-insensitively (DemoEngine.TryParseFlags), so a case the engine
// answered can hold "bestmatch" - and `FuzzyRegexOptions.bestmatch` does not compile. The printed
// name is the enum's own.
test('a flag typed in any case prints as the member is spelt', () => {
    expect(toCSharp(walk('a', 'bestmatch|IGNORECASE', 'b'))).toContain(
        'FuzzyRegexOptions.BestMatch | FuzzyRegexOptions.IgnoreCase,',
    );
});

// The four separators of DemoEngine._flagSeparators, so the snippet reads the flag box the way the
// engine reads it rather than the way a second parser would.
test('flags separate on a comma, a space, a pipe or a tab', () => {
    expect(toCSharp(walk('a', 'Posix,Word Unicode|Ascii\tMultiline', 'b'))).toContain(
        'FuzzyRegexOptions.Posix | FuzzyRegexOptions.Word | FuzzyRegexOptions.Unicode | ' +
            'FuzzyRegexOptions.Ascii | FuzzyRegexOptions.Multiline,',
    );
});

// Passed through and not dropped: the page shows the panel only for a case the engine accepted, so
// this cannot be reached from the page, and a snippet that silently matched with a different flag
// set than the one on screen would be worse than one that does not compile.
test('a flag the enum does not have is printed as it was typed', () => {
    expect(toCSharp(walk('a', 'Sideways', 'b'))).toContain('FuzzyRegexOptions.Sideways,');
});

test('a quote in a literal is doubled, and a trailing backslash needs nothing', () => {
    expect(toCSharp(walk('say "hi"', '', 'a\\'))).toContain('@"say ""hi""",');
    // Verbatim, so the backslash is not an escape and the literal ends where it looks like it ends.
    expect(toCSharp(walk('say "hi"', '', 'a\\'))).toContain('regex.EnumerateMatches(@"a\\")');
});

test('a value holding a newline is a raw string literal, indented to its closing fence', () => {
    expect(toCSharp(walk('a', '', 'one\ntwo'))).toContain(
        `foreach (Match match in regex.EnumerateMatches("""
    one
    two
    """))`,
    );
    expect(toCSharp(walk('one\ntwo', '', 'b'))).toContain(
        `FuzzyRegex regex = new(
    """
    one
    two
    """,
    FuzzyRegexOptions.None,`,
    );
});

// A run of three quotes inside the content would close a three-quote fence, so the fence grows past
// the longest run. This is the case the spec's own hunt list names.
test('three quotes in the content lengthen the fence', () => {
    expect(toCSharp(walk('a', '', 'say """this"""\nand that'))).toContain(
        `""""
    say """this"""
    and that
    """"`,
    );
});

// The blank line is content, not layout: a raw literal drops only the newline after the opening
// fence and the one before the closing fence.
test('a trailing newline and a blank line survive the round trip', () => {
    expect(toCSharp(walk('a', '', 'one\n\nthree\n'))).toContain(
        `regex.EnumerateMatches("""
    one

    three

    """)`,
    );
});

// A raw string literal's lines end where the COMPILER says they do, and C# ends a line on a
// carriage return, U+0085, U+2028 or U+2029 as readily as on a newline. Splitting on them and
// joining with a newline loses the character: `tools/probes/demo-snippet-compiles.mjs` compiled
// `a\r\nb` as a raw literal and the value came back DIFFERENT, and a U+2028 inside one is
// `error CS8999: Line does not start with the same whitespace as the closing line of the raw
// string literal`. The subject box cannot hold a carriage return, but `#s=a%0D%0Ab` can.
test('a line terminator a raw literal cannot hold is escaped instead', () => {
    expect(toCSharp(walk('a', '', 'a\r\nb'))).toContain(String.raw`regex.EnumerateMatches("a\r\nb")`);
    expect(toCSharp(walk('a\rb', '', 'c'))).toContain(String.raw`    "a\rb",`);
    expect(toCSharp(walk('a', '', 'one\ntwo\u{2028}three'))).toContain(
        String.raw`regex.EnumerateMatches("one\ntwo\u2028three")`,
    );
    expect(toCSharp(walk('a', '', 'x\u0085y\u{2029}z'))).toContain(
        String.raw`regex.EnumerateMatches("x\u0085y\u2029z")`,
    );
});

// The escaped form is a C# string literal, so its own metacharacters need spelling out too.
test('the escaped form escapes the backslash, the quote and the control characters', () => {
    expect(toCSharp(walk('a', '', 'he said "hi"\rand left\\'))).toContain(
        String.raw`regex.EnumerateMatches("he said \"hi\"\rand left\\")`,
    );
    expect(toCSharp(walk('a', '', 'tab\there\rbell\u0007'))).toContain(String.raw`("tab\there\rbell\u0007")`);
});

test('named lists become the fourth constructor argument', () => {
    expect(
        toCSharp(
            inputs({
                pattern: '\\b(?:\\L<fruit>){e<=1}\\b',
                subject: 'aple bananna cherry',
                namedLists: 'fruit: apple, banana; cherry',
            }),
        ),
    ).toContain(
        `    TimeSpan.FromSeconds(2), // the demo's own timeout
    new Dictionary<string, IReadOnlyCollection<string>>
    {
        [@"fruit"] = [@"apple", @"banana", @"cherry"],
    });
`,
    );
});

// DemoEngine.TryParseNamedLists reads the block this way: lines in three forms, words trimmed,
// empties dropped, and the first definition of a name is the one that stands (Dictionary.TryAdd).
test('the list block is read the way the engine reads it', () => {
    const emitted = toCSharp(inputs({ namedLists: 'a: one,two\r\n   \nb: three\ra: four' }));
    expect(emitted).toContain('[@"a"] = [@"one", @"two"],\n        [@"b"] = [@"three"],\n');
    expect(emitted).not.toContain('four');
});

test('the printed timeout is the demo’s own constant', () => {
    expect(toCSharp(walk('a', '', 'b'))).toContain(`TimeSpan.FromSeconds(${MATCH_TIMEOUT_SECONDS})`);
});

// --- the tokenizer -----------------------------------------------------------------------------

const kinds = (source: string) => tokenize(source).map((token) => `${token.kind}:${token.text}`);

test('the tokenizer puts every character back', () => {
    for (const source of [
        toCSharp(walk('(?:colour){e<=2}', 'IgnoreCase', 'the color of the collar')),
        toCSharp(inputs({ subject: 'one\ntwo', mode: 'replace', replacement: '"x"' })),
        toCSharp(inputs({ namedLists: 'fruit: apple' })),
    ]) {
        expect(tokenize(source).reduce((text, token) => text + token.text, '')).toBe(source);
    }
});

test('the five classes are what they say', () => {
    expect(kinds('// a note\n')).toEqual(['comment:// a note', 'other:\n']);
    // One span per run, so a line of ordinary code is one element and not one per character.
    expect(kinds('using Fuzzy;')).toEqual(['keyword:using', 'other: Fuzzy;']);
    expect(kinds('FromSeconds(2)')).toEqual(['other:FromSeconds(', 'number:2', 'other:)']);
    // A digit inside a name is part of the name: `Version1` is one identifier, not a word and a 1.
    expect(kinds('FuzzyRegexOptions.Version1')).toEqual(['other:FuzzyRegexOptions.Version1']);
    expect(kinds('@"a""b"')).toEqual(['string:@"a""b"']);
    expect(kinds('$"{x} y"')).toEqual(['string:$"{x} y"']);
});

// A backslash is not an escape in a verbatim string, so `@"c:\"` ends at that quote. Treating it as
// one swallowed the constructor's remaining arguments and the whole walk into a single string run.
test('a trailing backslash ends a verbatim string where the compiler ends it', () => {
    expect(kinds(String.raw`@"c:\", x`)).toEqual([String.raw`string:@"c:\"`, 'other:, x']);
    // An escaped one is still an escape in an interpolated or ordinary string.
    expect(kinds(String.raw`"c:\\" + $"a\"b"`)).toEqual([String.raw`string:"c:\\"`, 'other: + ', String.raw`string:$"a\"b"`]);
});

// The fences are part of the string, and a raw literal holding a `//` or a `"` is still one token.
// A tokenizer that stopped at the first quote would colour the rest of the file as code.
test('a raw string literal is one token, fence and all', () => {
    expect(kinds('"""\n// not a comment\n"""')).toEqual(['string:"""\n// not a comment\n"""']);
    expect(kinds('""""\nsay """this"""\n""""')).toEqual(['string:""""\nsay """this"""\n""""']);
});
