/**
 * The flags a pattern is compiled with: the list, the two pairs that cannot both be on, and the
 * translation between the panel's tickboxes and the string the engine reads.
 *
 * The string stays the state. It is the wire format (`DemoEngine.Run` takes it as text), the `f=` key
 * in a shared link, and what each row of `examples.json` carries, so a panel that owned the state
 * instead would need a mapping at every one of those edges. A view over the string needs none: every
 * shared link and every worked example written before this panel existed loads into it unchanged.
 *
 * What is measured rather than assumed, by `tools/probes/demo-flag-pair-exclusivity.ps1` on
 * 2026-09-20 against this worktree's build:
 *
 *   - Of all 91 unordered pairs of the 14 members, the library refuses exactly two:
 *     `Unicode + Ascii` ("ASCII, LOCALE and UNICODE flags are mutually incompatible") and
 *     `Version1 + Version0` ("not a single version flag"). Every other pair compiles. So there are
 *     two radio groups and no third, and a checkbox grid cannot reach a combination the engine
 *     rejects.
 *   - Naming a default is identical to leaving it off. `Options` came back `Unicode, Version1,
 *     FullCase` for a pattern compiled with nothing, with `Version1`, with `Unicode`, and with both.
 *     That is why a radio set back to its default writes nothing into the string: the shortest
 *     string that means what the panel shows.
 */

/**
 * Every member of `FuzzyRegexOptions`, spelt as the enum spells it.
 *
 * `tests/FuzzyRegex.Tests/Gaps/Demo/DemoSnippetTests.cs` reads this array and compares it with
 * `Enum.GetNames<FuzzyRegexOptions>()`, so a member added to the library and not to this list
 * reddens the build rather than reaching a visitor as a checkbox that is missing or a name they
 * cannot compile.
 */
export const FLAG_NAMES = [
    'None',
    'IgnoreCase',
    'Multiline',
    'Singleline',
    'Unicode',
    'IgnorePatternWhitespace',
    'Ascii',
    'Version1',
    'RightToLeft',
    'Word',
    'BestMatch',
    'Version0',
    'FullCase',
    'EnhanceMatch',
    'Posix',
] as const;

export type FlagName = (typeof FLAG_NAMES)[number];

/**
 * One sentence per member, in the library's own words: the `<summary>` of each enum member of
 * `src/FuzzyRegex/FuzzyRegexOptions.cs`, with its XML tags removed.
 *
 * Copied here rather than fetched, because these sentences are part of a CONTROL and not of a help
 * panel: `help.json` is generated at build time and the page survives its absence by leaving the
 * panel shut, which would be a `(?)` button that explains nothing on a page served straight out of
 * the source tree. `DemoSnippetTests.The_flag_help_is_the_librarys_own_words` re-reads the enum and
 * fails the build when a doc comment here and there stop agreeing - the same guarantee
 * `FLAG_NAMES` above has, by the same mechanism.
 */
export const FLAG_HELP: Record<FlagName, string> = {
    None: 'No options: case-sensitive, single-line, left-to-right matching.',
    IgnoreCase: 'Case-insensitive matching. Upstream IGNORECASE / I.',
    Multiline:
        '^ and $ match at the start and end of every line, as well as of the whole subject. Upstream MULTILINE / M.',
    Singleline:
        '. matches any character including a newline. Upstream DOTALL / S; the .NET name is Singleline, which means the same thing.',
    Unicode:
        'The character classes \\w, \\W, \\s, \\S, \\d, \\D and the word boundaries \\b, \\B cover the whole of Unicode. Upstream UNICODE / U, and its default for a text pattern; the inline form is (?u).',
    IgnorePatternWhitespace:
        'Unescaped whitespace in the pattern is ignored and # starts a comment. Upstream VERBOSE / X.',
    Ascii: 'The character classes \\w, \\W, \\s, \\S, \\d, \\D and the word boundaries \\b, \\B cover ASCII only, so everything above U+007F is answered as if it were unassigned. Upstream ASCII / A; the inline form is (?a).',
    RightToLeft: 'Search backwards, from the end of the subject towards the start. Upstream REVERSE / R.',
    Word: '\\b and \\B use the default Unicode word-boundary rules (UAX #29) instead of the \\w-to-\\W transition. Upstream WORD / W; the inline form is (?w).',
    BestMatch: 'Find the best fuzzy match rather than the first one. Upstream BESTMATCH / B.',
    EnhanceMatch: 'After finding a fuzzy match, try to improve its fit. Upstream ENHANCEMATCH / E.',
    Posix: 'Leftmost-longest (POSIX) matching instead of leftmost-first. Upstream POSIX / P.',
    FullCase:
        'Use Unicode full case-folding when matching case-insensitively, so that (for example) ß matches SS and ﬁ matches fi. Upstream FULLCASE / F.',
    Version0:
        "Legacy behaviour, compatible with System.Text.RegularExpressions and Python's re: simple case-folding, and an unescaped [ inside a set is a literal. Upstream VERSION0 / V0, and upstream's own default.",
    Version1:
        'The default. Nested sets and set operations ([[a-z]--[aeiou]]), and full case folding when matching case-insensitively. Upstream VERSION1 / V1.',
};

/** One of the two choices a visitor makes with radios rather than with tickboxes. */
export interface RadioGroup {
    /** What the group as a whole is choosing, read out as the fieldset's legend. */
    readonly legend: string;
    /** The form control name, which is what makes the options one group for the keyboard. */
    readonly name: string;
    readonly options: readonly [FlagName, FlagName];
    /**
     * What the library does when the string names neither, and so what the group shows as chosen
     * before anybody has chosen. Measured, not assumed; see the note at the top of this file.
     */
    readonly fallback: FlagName;
}

export const CHARACTER_SET: RadioGroup = {
    legend: 'Character set',
    name: 'character-set',
    options: ['Unicode', 'Ascii'],
    fallback: 'Unicode',
};

export const VERSION: RadioGroup = {
    legend: 'Version',
    name: 'version',
    options: ['Version1', 'Version0'],
    fallback: 'Version1',
};

export const RADIO_GROUPS: readonly RadioGroup[] = [CHARACTER_SET, VERSION];

/**
 * The tickboxes, in the order the panel shows them.
 *
 * GOV.UK's rule for a list of options: alphabetical by default, most-used first where that helps.
 * The first five are the flags the worked examples actually set, so somebody who arrived from a
 * sample finds the flag it used at the top rather than in the middle of an alphabet.
 */
export const CHECKBOXES: readonly FlagName[] = [
    'IgnoreCase',
    'BestMatch',
    'EnhanceMatch',
    'RightToLeft',
    'Posix',
    'FullCase',
    'IgnorePatternWhitespace',
    'Multiline',
    'Singleline',
    'Word',
];

/** The order a selection is written in: the panel's own, so the row reads the way the grid looks. */
const WRITTEN_ORDER: readonly FlagName[] = [
    ...CHECKBOXES,
    ...RADIO_GROUPS.flatMap((group) => [...group.options]),
];

/** The separators the engine accepts between flag names: `DemoEngine._flagSeparators`. */
const FLAG_SEPARATORS = /[, |\t]+/;

/**
 * What `StringSplitOptions.TrimEntries` takes off a token, which is `char.IsWhiteSpace`: tab to
 * carriage return, space, NEL, no-break space, the Ogham and U+2000 blocks, the line and paragraph
 * separators, the narrow and medium spaces, and the ideographic space.
 *
 * Spelt as code points rather than as a regular expression for two reasons. `String.trim` is a
 * DIFFERENT set - it takes U+FEFF off, which .NET leaves on the token, and leaves U+0085 on, which
 * .NET takes off - and a literal U+2028 inside a regex literal ends the line it is written on.
 */
const DOTNET_WHITESPACE = new Set(
    [0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x20, 0x85, 0xa0, 0x1680, 0x2028, 0x2029, 0x202f, 0x205f, 0x3000]
        .concat([0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008, 0x2009, 0x200a])
        .map((code) => String.fromCodePoint(code)),
);

/** One token with the whitespace `TrimEntries` would have taken off it. */
function trimToken(token: string): string {
    let start = 0;
    let end = token.length;
    while (start < end && DOTNET_WHITESPACE.has(token.charAt(start))) start += 1;
    while (end > start && DOTNET_WHITESPACE.has(token.charAt(end - 1))) end -= 1;
    return token.slice(start, end);
}

/**
 * What the panel shows for a flags string: every member it names, whether or not the set is legal.
 *
 * Unknown names are dropped rather than carried. A name the engine cannot read is not a state any
 * control of ours can produce now that the text box is gone, so the only way one arrives is a
 * hand-edited link - and the engine still refuses it by name, which is the message the page shows
 * until the visitor touches the panel and the string is rewritten without it.
 *
 * Both sides of a pair is the other string the engine refuses, and both sides are kept. The page
 * does not rewrite the string it was handed, so a selection that quietly dropped one side would put
 * the panel at odds with the answer beside it: the row would read `Ascii` while the engine said the
 * pair was refused. Instead neither radio is chosen (`chosen` returns null), which says the same
 * thing the error does and lets one press repair it.
 */
export function selectionFrom(text: string): ReadonlySet<FlagName> {
    const selected = new Set<FlagName>();

    for (const raw of text.split(FLAG_SEPARATORS)) {
        const token = trimToken(raw);
        if (token === '') continue;
        const name = FLAG_NAMES.find((candidate) => candidate.toLowerCase() === token.toLowerCase());
        // `None` is the empty selection and not a member to tick: it is what the snippet prints when
        // nothing is chosen, so ticking it would mean "no options, plus these options".
        if (name !== undefined && name !== 'None') selected.add(name);
    }

    return selected;
}

/**
 * The selection as the string the engine reads, in the order the panel shows it.
 *
 * A default is left unnamed. Naming one is identical to leaving it off - measured, see the note at
 * the top of this file - so omitting it is the shortest string that means what the panel shows, and
 * it keeps a shared link and the printed C# snippet free of members that change nothing. A pair with
 * both sides selected is the exception: there the default is named too, because dropping it would
 * turn a string the engine refuses into one it accepts without the visitor asking for that.
 */
export function formatFlags(selected: Iterable<FlagName>): string {
    const present = new Set(selected);
    const unnamed = RADIO_GROUPS.filter((group) => !group.options.every((option) => present.has(option))).map(
        (group) => group.fallback,
    );
    const named = WRITTEN_ORDER.filter((name) => present.has(name) && !unnamed.includes(name));
    return named.join(', ');
}

/** The closed row's text: what is ticked, or that nothing is. */
export function summaryText(selected: ReadonlySet<FlagName>): string {
    const written = formatFlags(selected);
    return written === '' ? 'none' : written;
}

/**
 * Which option of a group is chosen: the one named, the library's default while neither is, and
 * NEITHER while a string names both - the state the engine refuses, which no radio should claim.
 */
export function chosen(selected: ReadonlySet<FlagName>, group: RadioGroup): FlagName | null {
    if (group.options.every((option) => selected.has(option))) return null;
    return group.options.find((option) => option !== group.fallback && selected.has(option)) ?? group.fallback;
}

/** The selection with one tickbox ticked or unticked. */
export function withCheckbox(
    selected: ReadonlySet<FlagName>,
    name: FlagName,
    ticked: boolean,
): ReadonlySet<FlagName> {
    const next = new Set(selected);
    if (ticked) next.add(name);
    else next.delete(name);
    return next;
}

/** The selection with one option of a group chosen, which is what unchooses the other. */
export function withRadio(
    selected: ReadonlySet<FlagName>,
    group: RadioGroup,
    option: FlagName,
): ReadonlySet<FlagName> {
    const next = new Set(selected);
    for (const member of group.options) next.delete(member);
    next.add(option);
    return next;
}
