// Everything that arrives from outside the bundle, checked before it is believed.
//
// Two things cross that line: the engine's reply, which worker.js hands over as a string of JSON,
// and examples.json, which is fetched at runtime. A cast is a claim about both and not a check -
// `JSON.parse(...) as Reply` type-checks against anything - so a worker left over from an older
// deployment, or an examples file edited by hand, satisfies the compiler and then fails far away:
// in a template, as `undefined` rendered into the page or `Cannot read properties of undefined`
// thrown out of a render. The guards below turn that into one message at the boundary it entered.

import type { Example, Reply } from '../types';

const isObject = (value: unknown): value is Record<string, unknown> =>
    typeof value === 'object' && value !== null;

const isSpan = (value: unknown): boolean =>
    isObject(value) && typeof value.index === 'number' && typeof value.length === 'number';

const isCounts = (value: unknown): boolean =>
    isObject(value) &&
    typeof value.substitutions === 'number' &&
    typeof value.insertions === 'number' &&
    typeof value.deletions === 'number';

const isGroup = (value: unknown): boolean =>
    isObject(value) &&
    typeof value.number === 'number' &&
    typeof value.name === 'string' &&
    typeof value.success === 'boolean' &&
    isSpan(value) &&
    Array.isArray(value.captures) &&
    value.captures.every(isSpan);

// Checked to the depth the page reads it: every member the tables and the highlighter touch. A
// shallower check would pass a match with no `counts` straight into the render that needs it.
const isMatch = (value: unknown): boolean =>
    isSpan(value) &&
    isObject(value) &&
    isCounts(value.counts) &&
    Array.isArray(value.groups) &&
    value.groups.every(isGroup);

// `aborted` is rejected rather than ignored. It is the POOL's own field - "we killed this
// worker" - and the page turns it into "Stopped." on screen, so a worker that sent one would make
// the page report a stop nobody performed, with neither a match nor an error to contradict it.
// Rejected and not stripped, because a reply that claims it is not a reply this page understands.
const isReply = (value: unknown): value is Reply =>
    isObject(value) &&
    value.aborted === undefined &&
    (value.matches === undefined || (Array.isArray(value.matches) && value.matches.every(isMatch))) &&
    (value.truncated === undefined || typeof value.truncated === 'boolean') &&
    (value.error === undefined || typeof value.error === 'string');

/**
 * Reads what a worker sent, or returns the failure as an answer.
 *
 * Never throws: this runs inside a `message` listener, where a throw is an unhandled error that
 * settles nothing, and the question it was answering then waits for ever behind a "matching..."
 * that never clears.
 */
export function parseReply(json: string): Reply {
    let value: unknown;
    try {
        value = JSON.parse(json) as unknown;
    } catch (error) {
        return { error: `the engine's reply could not be read: ${error instanceof Error ? error.message : String(error)}` };
    }

    if (!isReply(value)) return { error: "the engine's reply could not be read: it is not an answer" };
    return value;
}

/** Whether a fetched `examples.json` is the guided tour, and not something else with a .json name. */
export const isExampleList = (value: unknown): value is readonly Example[] =>
    Array.isArray(value) &&
    value.every(
        (item) =>
            isObject(item) &&
            typeof item.title === 'string' &&
            typeof item.note === 'string' &&
            typeof item.pattern === 'string' &&
            typeof item.flags === 'string' &&
            typeof item.subject === 'string',
    );
