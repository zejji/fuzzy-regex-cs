// The fragment round trip, under `node --test`. These parts are pure, so they are tested here rather
// than in a browser; what genuinely needs one is wwwroot/checks.html. See demo/README.md.
import assert from 'node:assert/strict';
import test from 'node:test';

import { MAX_FRAGMENT_LENGTH, decode, encode } from '../FuzzyRegex.Demo.Wasm/wwwroot/lib/fragment.js';

const roundTrip = (inputs) => decode('#' + encode(inputs));

test('a case survives being pasted into a bug report and back', () => {
    const inputs = { pattern: '(?:colour){e<=2}', flags: 'BestMatch', subject: 'the color of the collar' };
    assert.deepEqual(roundTrip(inputs), inputs);
});

test('the characters a regex is made of survive', () => {
    // Every one of these has a meaning in a URL: & and = split parameters, # ends the fragment,
    // + is a space to URLSearchParams, % starts an escape. A subject is arbitrary text, so all of
    // them arrive sooner or later.
    const inputs = { pattern: 'a+b&c=d#e%20f', flags: '', subject: '100% + 50% & more #tags' };
    assert.deepEqual(roundTrip(inputs), inputs);
});

test('non-ASCII text survives', () => {
    const inputs = { pattern: '\\p{Greek}+', flags: '', subject: 'alpha then αβγ then 🙂 then beta' };
    assert.deepEqual(roundTrip(inputs), inputs);
});

test('an empty box is a value and not an absence', () => {
    // "cleared the flags" and "said nothing about the flags" must not be the same link.
    const inputs = { pattern: 'a', flags: '', subject: '' };
    assert.deepEqual(roundTrip(inputs), inputs);
});

test('somebody else\'s anchor is left alone', () => {
    // A link to #install carries no case, and the page must keep whatever the boxes already hold
    // rather than blanking them.
    assert.equal(decode('#install'), null);
    assert.equal(decode('#'), null);
    assert.equal(decode(''), null);
    assert.equal(decode(undefined), null);
});

test('a partial fragment fills what it names and empties the rest', () => {
    assert.deepEqual(decode('#p=abc'), { pattern: 'abc', flags: '', subject: '' });
});

test('the cap is long enough for a real case and short of a browser limit', () => {
    // A guard on the constant itself: the demo's subject cap is 100,000 characters, so the page
    // must decide what to do above this rather than write a link that will be truncated in transit.
    assert.ok(MAX_FRAGMENT_LENGTH > 1000, 'a realistic shared case has to fit');
    assert.ok(MAX_FRAGMENT_LENGTH <= 8000, 'RFC 7230 asks for 8000 octets of request line; beyond that, links get cut');
});

test('a long subject produces a fragment the page can recognise as too long', () => {
    const fragment = encode({ pattern: 'a', flags: '', subject: 'x'.repeat(20000) });
    assert.ok(fragment.length > MAX_FRAGMENT_LENGTH);
    // And it still decodes, because the cap is the page's policy about sharing, not a parse limit.
    assert.equal(decode('#' + fragment).subject.length, 20000);
});
