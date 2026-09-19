// Everything that arrives from outside the bundle, checked before it is believed.
//
// Two things cross that line: the engine's reply, which worker.js hands over as a string of JSON,
// and examples.json, which is fetched at runtime. A cast is a claim about both and not a check -
// `JSON.parse(...) as Reply` type-checks against anything - so a worker left over from an older
// deployment, or an examples file edited by hand, satisfies the compiler and then fails far away:
// in a template, as `undefined` rendered into the page or `Cannot read properties of undefined`
// thrown out of a render. The guards below turn that into one message at the boundary it entered.

import type { Example, Help, Reply } from '../types';

import { MAX_PATTERN_LENGTH } from './caps';

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

// A member that is allowed to be absent, and must be of its own type when it is present. Every
// optional member below is optional because the engine omits it rather than sending a null, so
// "absent" is a real answer and "present but the wrong type" is a reply from something else.
const optional = (value: unknown, kind: 'string' | 'number' | 'boolean'): boolean =>
    value === undefined || typeof value === kind;

// Checked to the depth the page reads it: every member the tables and the highlighter touch. A
// shallower check would pass a match with no `counts` straight into the render that needs it.
const isMatch = (value: unknown): boolean =>
    isSpan(value) &&
    isObject(value) &&
    isCounts(value.counts) &&
    Array.isArray(value.groups) &&
    value.groups.every(isGroup) &&
    optional(value.partialMatch, 'boolean');

// The position a parse failed at, checked as an INDEX and not merely as a number, because the page
// executes it rather than printing it: App.vue draws the caret with `' '.repeat(errorOffset)`, so a
// negative one throws RangeError out of the render - which is a blank page and a console error, not
// a misplaced hat - and a large one allocates that many characters on the main thread. The upper
// bound is the longest pattern the engine will parse at all, so every honest offset fits.
const isOffset = (value: unknown): boolean =>
    value === undefined ||
    (typeof value === 'number' &&
        Number.isInteger(value) &&
        value >= 0 &&
        value <= MAX_PATTERN_LENGTH);

// `aborted` is rejected rather than ignored. It is the POOL's own field - "we killed this
// worker" - and the page turns it into "Stopped." on screen, so a worker that sent one would make
// the page report a stop nobody performed, with neither a match nor an error to contradict it.
// Rejected and not stripped, because a reply that claims it is not a reply this page understands.
const isReply = (value: unknown): value is Reply =>
    isObject(value) &&
    value.aborted === undefined &&
    (value.matches === undefined || (Array.isArray(value.matches) && value.matches.every(isMatch))) &&
    optional(value.truncated, 'boolean') &&
    optional(value.error, 'string') &&
    optional(value.replaced, 'string') &&
    isOffset(value.errorOffset);

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

    if (!isReply(value)) return { error: "the engine's reply could not be read: it arrived in the wrong shape" };
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
            typeof item.subject === 'string' &&
            // The four v2 members. A row omits the ones it does not need - the page empties those
            // boxes - but a row that names one and gives it a number is a file that would drive the
            // engine with something that is not an input.
            optional(item.key, 'string') &&
            optional(item.mode, 'string') &&
            optional(item.replacement, 'string') &&
            optional(item.namedLists, 'string'),
    );

const isRun = (value: unknown): boolean =>
    isObject(value) && typeof value.code === 'boolean' && typeof value.text === 'string';

// The two kinds the generator emits, and no others: a third kind is the generated shape drifting
// away from the page that renders it, and it would reach the screen as a blank space in the middle
// of an explanation rather than as anything anybody could report.
const isBlock = (value: unknown): boolean =>
    isObject(value) &&
    ((value.kind === 'paragraph' && Array.isArray(value.runs) && value.runs.every(isRun)) ||
        (value.kind === 'code' && typeof value.language === 'string' && typeof value.text === 'string'));

// A section with no blocks is refused for the reason a key with no sections is (see `isHelp`): the
// generator maps a heading whose prose has since moved under a sub-heading, writes the section with
// an empty body, and the panel opens onto nothing. `tools/build-demo-help.ps1` fails on it at build
// time; this is the same claim checked again at the moment the page believes the file.
const isSection = (value: unknown): boolean =>
    isObject(value) &&
    Array.isArray(value.heading) &&
    value.heading.every(isRun) &&
    Array.isArray(value.blocks) &&
    value.blocks.length > 0 &&
    value.blocks.every(isBlock);

/**
 * Whether a fetched `help.json` is what `tools/build-demo-help.ps1` writes.
 *
 * A key with no sections is refused rather than accepted: on screen it is a disclosure that opens
 * onto nothing, which is indistinguishable from a feature nobody documented. The build-time
 * guarantee is that a renamed heading in `docs/COMPARISON.md` reddens the build; this is the same
 * claim checked again at the moment the page believes the file.
 */
export const isHelp = (value: unknown): value is Help =>
    isObject(value) &&
    typeof value.source === 'string' &&
    typeof value.note === 'string' &&
    isObject(value.entries) &&
    Object.keys(value.entries).length > 0 &&
    Object.values(value.entries).every(
        (sections) => Array.isArray(sections) && sections.length > 0 && sections.every(isSection),
    );
