// The three inputs, carried in the URL fragment so a case can be pasted into a bug report.
//
// Fragment and not query string, deliberately: "the fragment identifier is not sent to the server"
// and is dereferenced by the client alone (RFC 3986 section 3.5), so a subject typed into the demo
// never reaches GitHub's request logs. A query string would put whatever a stranger pasted -
// possibly something from their own work - into someone else's log file.

import { MAX_FRAGMENT_LENGTH } from './caps.js';

// Keys are one letter because the whole point is a link somebody can paste; `pattern=` three times
// over is three times the length for no added clarity in a string nobody reads by eye.
const KEYS = { pattern: 'p', flags: 'f', subject: 's' };

export { MAX_FRAGMENT_LENGTH };

/**
 * Encodes the three inputs as a fragment, without the leading `#`.
 *
 * @param {{pattern: string, flags: string, subject: string}} inputs
 * @returns {string}
 */
export function encode(inputs) {
    const parameters = new URLSearchParams();
    for (const [name, key] of Object.entries(KEYS)) {
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
 * @param {string} fragment The location fragment, with or without its leading `#`.
 * @returns {{pattern: string, flags: string, subject: string} | null}
 */
export function decode(fragment) {
    const text = (fragment ?? '').replace(/^#/, '');
    if (text === '') return null;

    const parameters = new URLSearchParams(text);
    if (!Object.values(KEYS).some((key) => parameters.has(key))) return null;

    return {
        pattern: parameters.get(KEYS.pattern) ?? '',
        flags: parameters.get(KEYS.flags) ?? '',
        subject: parameters.get(KEYS.subject) ?? '',
    };
}
