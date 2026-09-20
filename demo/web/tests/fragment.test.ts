// The fragment round trip. These parts are pure, so they are tested here rather than in a browser;
// what genuinely needs one is the .NET web root's checks.html. See demo/README.md.
import { expect, test } from 'vitest';

import { MAX_FRAGMENT_LENGTH, decode, encode } from '../src/lib/fragment';
import type { Inputs } from '../src/types';

import { walk } from './inputs';

const roundTrip = (inputs: Inputs) => decode('#' + encode(inputs));

test('a case survives being pasted into a bug report and back', () => {
    const inputs = walk('(?:colour){e<=2}', 'BestMatch', 'the color of the collar');
    expect(roundTrip(inputs)).toEqual(inputs);
});

test('the characters a regex is made of survive', () => {
    // Every one of these has a meaning in a URL: & and = split parameters, # ends the fragment,
    // + is a space to URLSearchParams, % starts an escape. A subject is arbitrary text, so all of
    // them arrive sooner or later.
    const inputs = walk('a+b&c=d#e%20f', '', '100% + 50% & more #tags');
    expect(roundTrip(inputs)).toEqual(inputs);
});

test('non-ASCII text survives', () => {
    const inputs = walk('\\p{Greek}+', '', 'alpha then αβγ then 🙂 then beta');
    expect(roundTrip(inputs)).toEqual(inputs);
});

test('an empty box is a value and not an absence', () => {
    // "cleared the flags" and "said nothing about the flags" must not be the same link.
    const inputs = walk('a', '', '');
    expect(roundTrip(inputs)).toEqual(inputs);
});

test('the mode, the template and the word lists travel in the link too', () => {
    // A replace sample is not shareable at all unless the template goes with it: the same pattern
    // and subject under an empty template is a different answer, and a link that dropped it would
    // hand someone else a case that looks like the one on screen and is not.
    const inputs: Inputs = {
        pattern: '(?<year>\\d{4})-(?<month>\\d{2})',
        flags: '',
        subject: '2026-09 and 1999-12',
        mode: 'replace',
        replacement: '\\g<month>/\\g<year>',
        namedLists: '',
    };
    expect(roundTrip(inputs)).toEqual(inputs);

    const lists: Inputs = {
        pattern: '\\b(?:\\L<fruit>){e<=1}\\b',
        flags: '',
        subject: 'aple bananna cherry',
        mode: '',
        replacement: '',
        namedLists: 'fruit: apple, banana, cherry',
    };
    expect(roundTrip(lists)).toEqual(lists);
});

test('a link written by v1 still loads, with the three new boxes empty', () => {
    // S71's links are in issues and chat logs already, and they name three keys. Empty is the
    // ordinary walk, which is exactly what v1 meant.
    expect(decode('#p=a%2Bb&f=IgnoreCase&s=aab')).toEqual(walk('a+b', 'IgnoreCase', 'aab'));
});

test("somebody else's anchor is left alone", () => {
    // A link to #install carries no case, and the page must keep whatever the boxes already hold
    // rather than blanking them.
    expect(decode('#install')).toBeNull();
    expect(decode('#')).toBeNull();
    expect(decode('')).toBeNull();
    expect(decode(undefined)).toBeNull();
});

test('a partial fragment fills what it names and empties the rest', () => {
    expect(decode('#p=abc')).toEqual(walk('abc', '', ''));
});

test('the cap is long enough for a real case and short of a browser limit', () => {
    // A guard on the constant itself: the demo's subject cap is 100,000 characters, so the page
    // must decide what to do above this rather than write a link that will be truncated in transit.
    expect(MAX_FRAGMENT_LENGTH).toBeGreaterThan(1000);
    expect(MAX_FRAGMENT_LENGTH).toBeLessThanOrEqual(8000);
});

test('a long subject produces a fragment the page can recognise as too long', () => {
    const fragment = encode(walk('a', '', 'x'.repeat(20000)));
    expect(fragment.length).toBeGreaterThan(MAX_FRAGMENT_LENGTH);
    // And it still decodes, because the cap is the page's policy about sharing, not a parse limit.
    expect(decode('#' + fragment)?.subject.length).toBe(20000);
});
