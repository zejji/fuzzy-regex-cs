// The fragment round trip. These parts are pure, so they are tested here rather than in a browser;
// what genuinely needs one is the .NET web root's checks.html. See demo/README.md.
import { expect, test } from 'vitest';

import { MAX_FRAGMENT_LENGTH, decode, encode } from '../src/lib/fragment';
import type { Inputs } from '../src/types';

const roundTrip = (inputs: Inputs) => decode('#' + encode(inputs));

test('a case survives being pasted into a bug report and back', () => {
    const inputs: Inputs = {
        pattern: '(?:colour){e<=2}',
        flags: 'BestMatch',
        subject: 'the color of the collar',
    };
    expect(roundTrip(inputs)).toEqual(inputs);
});

test('the characters a regex is made of survive', () => {
    // Every one of these has a meaning in a URL: & and = split parameters, # ends the fragment,
    // + is a space to URLSearchParams, % starts an escape. A subject is arbitrary text, so all of
    // them arrive sooner or later.
    const inputs: Inputs = { pattern: 'a+b&c=d#e%20f', flags: '', subject: '100% + 50% & more #tags' };
    expect(roundTrip(inputs)).toEqual(inputs);
});

test('non-ASCII text survives', () => {
    const inputs: Inputs = { pattern: '\\p{Greek}+', flags: '', subject: 'alpha then αβγ then 🙂 then beta' };
    expect(roundTrip(inputs)).toEqual(inputs);
});

test('an empty box is a value and not an absence', () => {
    // "cleared the flags" and "said nothing about the flags" must not be the same link.
    const inputs: Inputs = { pattern: 'a', flags: '', subject: '' };
    expect(roundTrip(inputs)).toEqual(inputs);
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
    expect(decode('#p=abc')).toEqual({ pattern: 'abc', flags: '', subject: '' });
});

test('the cap is long enough for a real case and short of a browser limit', () => {
    // A guard on the constant itself: the demo's subject cap is 100,000 characters, so the page
    // must decide what to do above this rather than write a link that will be truncated in transit.
    expect(MAX_FRAGMENT_LENGTH).toBeGreaterThan(1000);
    expect(MAX_FRAGMENT_LENGTH).toBeLessThanOrEqual(8000);
});

test('a long subject produces a fragment the page can recognise as too long', () => {
    const fragment = encode({ pattern: 'a', flags: '', subject: 'x'.repeat(20000) });
    expect(fragment.length).toBeGreaterThan(MAX_FRAGMENT_LENGTH);
    // And it still decodes, because the cap is the page's policy about sharing, not a parse limit.
    expect(decode('#' + fragment)?.subject.length).toBe(20000);
});
