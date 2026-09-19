// The three inputs, carried in the URL fragment so a case can be pasted into a bug report.
//
// Fragment and not query string, deliberately: "the fragment identifier is not sent to the server"
// and is dereferenced by the client alone (RFC 3986 section 3.5), so a subject typed into the demo
// never reaches GitHub's request logs. A query string would put whatever a stranger pasted -
// possibly something from their own work - into someone else's log file.

import type { Inputs } from '../types';

import { MAX_FRAGMENT_LENGTH } from './caps';

// Keys are one letter because the whole point is a link somebody can paste; `pattern=` six times
// over is six times the length for no added clarity in a string nobody reads by eye.
//
// The three v2 keys are additions and never renames: `p`, `f` and `s` still mean what they meant in
// S71, so every link already pasted into an issue still loads, and the boxes it says nothing about
// come up empty - which is the ordinary walk, and exactly what those links meant.
const KEYS: Readonly<Record<keyof Inputs, string>> = {
    pattern: 'p',
    flags: 'f',
    subject: 's',
    mode: 'm',
    replacement: 'r',
    namedLists: 'l',
};

export { MAX_FRAGMENT_LENGTH };

/**
 * A fragment with its leading `#` off.
 *
 * Exported because the caller has to tell "no fragment at all" from "a fragment that is not a
 * case": {@link decode} answers null to both, and they are different instructions to the page.
 */
export const fragmentText = (fragment: string | undefined | null): string =>
    (fragment ?? '').replace(/^#/, '');

/** Encodes the three inputs as a fragment, without the leading `#`. */
export function encode(inputs: Inputs): string {
    const parameters = new URLSearchParams();
    for (const [name, key] of Object.entries(KEYS) as [keyof Inputs, string][]) {
        // An empty box is written out too. Dropping it would make "cleared the flags" and "did not
        // say anything about the flags" the same link, and they load differently.
        parameters.set(key, inputs[name] ?? '');
    }

    return parameters.toString();
}

/**
 * Reads a fragment written by {@link encode}, or returns null when there is nothing to read.
 *
 * A fragment carrying none of the three keys is not an error and not an empty case: it is somebody
 * else's anchor (`#install`, a link into the README) and the page must leave its inputs alone.
 *
 * @param fragment The location fragment, with or without its leading `#`.
 */
export function decode(fragment: string | undefined | null): Inputs | null {
    const text = fragmentText(fragment);
    if (text === '') return null;

    const parameters = new URLSearchParams(text);
    if (!Object.values(KEYS).some((key) => parameters.has(key))) return null;

    return {
        pattern: parameters.get(KEYS.pattern) ?? '',
        flags: parameters.get(KEYS.flags) ?? '',
        subject: parameters.get(KEYS.subject) ?? '',
        mode: parameters.get(KEYS.mode) ?? '',
        replacement: parameters.get(KEYS.replacement) ?? '',
        namedLists: parameters.get(KEYS.namedLists) ?? '',
    };
}
