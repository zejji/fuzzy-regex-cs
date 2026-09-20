/**
 * The flags control's state: the flag list, the two exclusive pairs, and the string the engine reads.
 *
 * The page keeps the flags as the string it always was - that string is the wire format, the `f=` key
 * in a shared link, and what `examples.json` carries - and the checkbox panel is a view over it. So
 * everything worth pinning is here, in pure functions, and `page.test.ts` only has to check that the
 * boxes are wired to them.
 *
 * The exclusive pairs and the two defaults are not read off the enum: they were measured against the
 * library by `tools/probes/demo-flag-pair-exclusivity.ps1` on 2026-09-20. All 91 unordered pairs of
 * the 14 members were compiled, exactly two were refused - `Unicode + Ascii` ("ASCII, LOCALE and
 * UNICODE flags are mutually incompatible") and `Version1 + Version0` ("not a single version flag")
 * - and naming either default was identical to naming nothing: `Options = Unicode, Version1,
 * FullCase` for none, for `Version1`, for `Unicode` and for both.
 */
import { describe, expect, it } from 'vitest';

import {
    CHARACTER_SET,
    CHECKBOXES,
    FLAG_HELP,
    FLAG_NAMES,
    RADIO_GROUPS,
    VERSION,
    chosen,
    formatFlags,
    selectionFrom,
    summaryText,
    withCheckbox,
    withRadio,
    type FlagName,
} from '../src/lib/flags';

/** Every member a visitor can choose: the enum, less `None`, which is the empty selection. */
const CHOOSABLE = FLAG_NAMES.filter((name) => name !== 'None');

describe('the control offers every flag', () => {
    it('covers each member exactly once, as a checkbox or as a radio option', () => {
        const offered = [...CHECKBOXES, ...RADIO_GROUPS.flatMap((group) => [...group.options])];

        expect([...offered].sort()).toEqual([...CHOOSABLE].sort());
        expect(new Set(offered).size, 'no member is offered twice').toBe(offered.length);
    });

    it('never offers None, which is what an empty selection means', () => {
        expect(CHECKBOXES).not.toContain('None');
        expect(RADIO_GROUPS.flatMap((group) => [...group.options])).not.toContain('None');
    });

    it('puts the flags the worked examples use first, then the rest alphabetically', () => {
        // GOV.UK's rule: alphabetical by default, most-used first where that helps. The first five
        // are the flags examples.json actually sets.
        const used: readonly FlagName[] = ['IgnoreCase', 'BestMatch', 'EnhanceMatch', 'RightToLeft', 'Posix'];
        const rest = CHECKBOXES.filter((name) => !used.includes(name));

        expect(CHECKBOXES.slice(0, used.length)).toEqual(used);
        expect(rest).toEqual([...rest].sort());
    });

    it('explains every member in the words the library uses', () => {
        for (const name of FLAG_NAMES) {
            expect(FLAG_HELP[name], `${name} has a help sentence`).toMatch(/\S/);
        }
    });
});

describe('reading a flags string', () => {
    it('round trips every member that says something', () => {
        const defaults = RADIO_GROUPS.map((group) => group.fallback);
        for (const name of CHOOSABLE.filter((candidate) => !defaults.includes(candidate))) {
            expect(formatFlags(selectionFrom(name))).toBe(name);
        }
    });

    it('reads a named default, and writes it back as nothing', () => {
        // Not a round trip, deliberately: `Unicode` and `Version1` are what the library does anyway,
        // so the panel shows them chosen and the string says nothing. An older shared link that
        // names one still loads, and still means what it meant.
        for (const group of RADIO_GROUPS) {
            expect([...selectionFrom(group.fallback)]).toEqual([group.fallback]);
            expect(chosen(selectionFrom(group.fallback), group)).toBe(group.fallback);
            expect(formatFlags(selectionFrom(group.fallback))).toBe('');
        }
    });

    it('reads the separators the engine reads, and the case it reads them in', () => {
        // DemoEngine._flagSeparators is comma, space, pipe and tab, and TryParseFlags compares
        // names case-insensitively, so a shared link that predates this control still loads.
        for (const text of ['IgnoreCase,BestMatch', 'IgnoreCase BestMatch', 'IgnoreCase|BestMatch', 'IgnoreCase\tBestMatch']) {
            expect([...selectionFrom(text)].sort()).toEqual(['BestMatch', 'IgnoreCase']);
        }

        expect([...selectionFrom('ignorecase, BESTMATCH')].sort()).toEqual(['BestMatch', 'IgnoreCase']);
    });

    it('drops a name it does not know, and None, rather than ticking something else', () => {
        // A hand-edited link is the only way one arrives now that the text box is gone. The engine
        // still refuses it - `Unknown flag 'Nonsense'` - and that refusal is what the page shows
        // until the visitor touches the control, which rewrites the string without it.
        expect([...selectionFrom('IgnoreCase, Nonsense')]).toEqual(['IgnoreCase']);
        expect([...selectionFrom('None')]).toEqual([]);
        expect([...selectionFrom('')]).toEqual([]);
    });

    it('keeps both sides of a pair the engine refuses, because both are what it was given', () => {
        // Both named is a string no control of ours can produce, and one the engine rejects with
        // "ASCII, LOCALE and UNICODE flags are mutually incompatible". The page does not rewrite the
        // string it was handed, so hiding one side here would put the panel and the engine at odds:
        // the row would read `Ascii` while the answer said the pair was refused.
        expect([...selectionFrom('Ascii, Unicode')].sort()).toEqual(['Ascii', 'Unicode']);
        expect([...selectionFrom('Unicode, Ascii')].sort()).toEqual(['Ascii', 'Unicode']);
        expect([...selectionFrom('Version0, Version1')].sort()).toEqual(['Version0', 'Version1']);
    });

    it('trims a token the way the engine does, and splits where the engine splits', () => {
        // `TryParseFlags` splits on those four separators with `StringSplitOptions.TrimEntries`, so
        // "IgnoreCase\n" is one token that parses and "IgnoreCase\nBestMatch" is one token that does
        // not. A newline reaches the page from a hand-edited link and from a copied one that wrapped.
        expect([...selectionFrom('IgnoreCase\n')]).toEqual(['IgnoreCase']);
        expect([...selectionFrom('IgnoreCase, BestMatch')].sort()).toEqual(['BestMatch', 'IgnoreCase']);
        expect([...selectionFrom('IgnoreCase\nBestMatch')], 'not a separator, so not two flags').toEqual([]);
    });

    it('trims what .NET calls whitespace, which is not what JavaScript calls whitespace', () => {
        // The two sets differ in both directions, and `String.trim` is wrong in both. Measured with
        // pwsh on 2026-09-20: `char.IsWhiteSpace(U+0085)` is True and `char.IsWhiteSpace(U+FEFF)` is
        // False, so `"IgnoreCase" + U+0085` splits with TrimEntries to a token of 10 characters that
        // parses, while `"IgnoreCase" + U+FEFF` stays 11 characters and the engine answers
        // `Unknown flag`. Written as code points because both characters are invisible in a source
        // file - one of them is the byte-order mark.
        const nel = String.fromCodePoint(0x85);
        const bom = String.fromCodePoint(0xfeff);

        expect([...selectionFrom(`IgnoreCase${nel}`)], 'the engine trims NEL, so the box is ticked').toEqual([
            'IgnoreCase',
        ]);
        expect([...selectionFrom(`IgnoreCase${bom}`)], 'the engine refuses this token, so no box is').toEqual([]);
    });
});

describe('writing a flags string', () => {
    it('writes the members in the order the panel shows them', () => {
        const selected = selectionFrom('Word, Posix, Ascii, IgnoreCase');
        expect(formatFlags(selected)).toBe('IgnoreCase, Posix, Word, Ascii');
    });

    it('writes both members of a pair when both were given, and no control can give both', () => {
        // The string is the state, so writing one side of a pair the visitor never repaired would
        // send the engine something other than what the row says. Only a radio repairs it, and a
        // radio cannot produce the pair: `withRadio` clears the group before it adds.
        for (const group of RADIO_GROUPS) {
            const both = new Set<FlagName>(group.options);
            const written = formatFlags(both);
            expect(group.options.every((option) => written.includes(option)), `wrote "${written}"`).toBe(true);

            for (const option of group.options) {
                const repaired = formatFlags(withRadio(both, group, option));
                const named = group.options.filter((other) => repaired.includes(other));
                expect(named.length, `${group.legend} wrote "${repaired}"`).toBeLessThanOrEqual(1);
            }
        }
    });

    it('leaves a default unnamed, because naming it changes nothing', () => {
        // Measured: `Options` is `Unicode, Version1, FullCase` whether they are named or not. So the
        // shortest string that means what the panel shows is the one that omits them, which keeps a
        // shared link short and the printed snippet honest.
        expect(formatFlags(withRadio(selectionFrom('Ascii'), CHARACTER_SET, 'Unicode'))).toBe('');
        expect(formatFlags(withRadio(selectionFrom('Version0'), VERSION, 'Version1'))).toBe('');
    });
});

describe('the summary row', () => {
    it('says what is selected, and says so when nothing is', () => {
        expect(summaryText(selectionFrom(''))).toBe('none');
        expect(summaryText(selectionFrom('BestMatch'))).toBe('BestMatch');
        expect(summaryText(selectionFrom('BestMatch, IgnoreCase'))).toBe('IgnoreCase, BestMatch');
    });

    it('says both sides of a refused pair, because that is what the engine was given', () => {
        expect(summaryText(selectionFrom('Unicode, Ascii'))).toBe('Unicode, Ascii');
        expect(summaryText(selectionFrom('Version0, Version1'))).toBe('Version1, Version0');
    });

    it('agrees with the string the engine is given', () => {
        for (const text of ['', 'IgnoreCase', 'Ascii, Posix, Word', 'Unicode, Ascii']) {
            const selected = selectionFrom(text);
            const written = formatFlags(selected);
            expect(summaryText(selected)).toBe(written === '' ? 'none' : written);
        }
    });
});

describe('changing the selection', () => {
    it('ticks and unticks a checkbox', () => {
        const none = selectionFrom('');
        expect(formatFlags(withCheckbox(none, 'BestMatch', true))).toBe('BestMatch');
        expect(formatFlags(withCheckbox(selectionFrom('BestMatch'), 'BestMatch', false))).toBe('');
    });

    it('replaces the other side when a radio is picked', () => {
        expect(formatFlags(withRadio(selectionFrom('IgnoreCase'), CHARACTER_SET, 'Ascii'))).toBe('IgnoreCase, Ascii');
        expect(formatFlags(withRadio(selectionFrom('Ascii'), CHARACTER_SET, 'Unicode'))).toBe('');
        expect(formatFlags(withRadio(selectionFrom('Version0'), VERSION, 'Version0'))).toBe('Version0');
    });

    it('shows the library default as chosen while nothing names it', () => {
        expect(chosen(selectionFrom(''), CHARACTER_SET)).toBe('Unicode');
        expect(chosen(selectionFrom(''), VERSION)).toBe('Version1');
        expect(chosen(selectionFrom('Ascii'), CHARACTER_SET)).toBe('Ascii');
        expect(chosen(selectionFrom('Version0'), VERSION)).toBe('Version0');
    });

    it('chooses neither while a link names both, so one press repairs it', () => {
        // Nothing chosen is the only honest answer for a string the engine refuses, and it is also
        // what makes the repair work: a radio that is already checked fires no `change`, so a panel
        // showing `Ascii` for `Unicode,Ascii` would ignore the press the visitor makes on `Ascii`.
        expect(chosen(selectionFrom('Unicode, Ascii'), CHARACTER_SET)).toBeNull();
        expect(chosen(selectionFrom('Version1, Version0'), VERSION)).toBeNull();
        expect(formatFlags(withRadio(selectionFrom('Unicode, Ascii'), CHARACTER_SET, 'Ascii'))).toBe('Ascii');
        expect(formatFlags(withRadio(selectionFrom('Unicode, Ascii'), CHARACTER_SET, 'Unicode'))).toBe('');
    });

    it('leaves the rest of the selection alone', () => {
        const selected = selectionFrom('IgnoreCase, BestMatch, Version0');
        expect(formatFlags(withRadio(selected, CHARACTER_SET, 'Ascii'))).toBe(
            'IgnoreCase, BestMatch, Ascii, Version0',
        );
    });
});
