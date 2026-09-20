// Turning an answer into something renderable, under a cap.
//
// Rendering is the main thread's job and the Web Worker cannot protect it (DemoEngine.cs, MaxSpans):
// the engine can answer promptly with hundreds of matches and the page can still die building a DOM
// node for each one. So the cap is applied to the MATCHES, before any segment is built, rather than
// to the segments afterwards - slicing after the work is done is a cap that costs exactly as much as
// having no cap.

import type { Edits, Span } from '../types';

import { MAX_DISPLAYED_MATCHES } from './caps';

export { MAX_DISPLAYED_MATCHES };

/** The kind of error a fuzzy match spent at one place. The three members of {@link Edits}. */
export type EditKind = 'sub' | 'ins' | 'del';

/**
 * One run inside a highlight: a stretch of matched text, or the error the match spent there.
 *
 * A `del` run has no text of its own. Nothing was matched there - a character the pattern asked for
 * is missing from the subject - so what the page draws is a mark between two characters.
 */
export interface EditRun {
    readonly text: string;
    readonly kind: EditKind | null;
    /**
     * How many errors of this kind the run stands for: the characters it covers on a `sub` or `ins`
     * run, and the characters missing from one place on a `del` run. 1 on plain text.
     *
     * Both cases are the same fact - one mark for neighbouring errors of one kind - and only the
     * deletion draws the number. `(foobar){e}` ends with a match missing five characters and an
     * empty match missing six; a mark each put those five on top of each other, 3 px of dashed
     * border apart with their letters overlapping, and the gap has no characters of its own to show
     * how wide the hole is. A run of six substitutions does (S75).
     */
    readonly count: number;
    /**
     * Where the run starts in the subject, in UTF-16 code units, as the engine counts.
     *
     * The page names the position when a pointer rests on a mark, and a `del` run is the one that
     * needs saying: the gap is drawn between two characters and stands for characters that are not
     * there at all, so "deletion before index 6" is the only way to read it off the page.
     */
    readonly index: number;
}

/** One run of the subject as the page paints it. `match` is null for the text between matches. */
export interface Segment {
    readonly text: string;
    /** The match's number in the full answer, or null for plain text. */
    readonly match: number | null;
    /**
     * The match broken into its errors, for a fuzzy match that spent any. Absent everywhere else,
     * so the ordinary highlight stays one run of text and one text node.
     */
    readonly runs?: readonly EditRun[];
}

/** What {@link segments} needs of a match: its span, and where it spent its errors. */
export interface Highlighted extends Span {
    readonly edits?: Edits;
}

/** The subject split into paintable runs, and how much of the answer they cover. */
export interface View {
    readonly segments: readonly Segment[];
    readonly shown: number;
    readonly total: number;
}

/**
 * Splits the subject into the runs the page paints: plain text, and one run per highlighted match.
 *
 * @param subject The text that was searched.
 * @param matches The engine's matches, in order.
 * @param cap The most matches to draw. Defaults to {@link MAX_DISPLAYED_MATCHES}.
 */
export function segments(
    subject: string,
    matches: readonly Highlighted[] | undefined,
    cap: number = MAX_DISPLAYED_MATCHES,
): View {
    const all = matches ?? [];
    const drawn = all.slice(0, Math.max(0, cap));
    const pieces: Segment[] = [];
    let at = 0;

    // The answer's order is NOT always the subject's order: a RightToLeft search starts at the end,
    // so the match numbered 1 is the last one in the text. The page paints the subject, so the runs
    // are built in subject order while each run keeps the number the answer gave it - which is what
    // the table, the selection, the labels and the alternating tones all key off.
    //
    // Two matches CAN start at the same index, and that is an ordinary engine answer rather than a
    // malformed one: a reverse search over "baa" for `a*` returns (1,3), (1,1), (0,0) upstream as
    // start/end pairs (regex 2026.9.10, 2026-09-19) - a two-character match and an empty one at its
    // start, which this port answers as the spans [1,2], [1,0], [0,0].
    // The shorter goes first, because an empty match fits before a longer one begins and would
    // otherwise be swallowed by the overlap guard below.
    const inSubjectOrder = [...drawn.entries()].sort(
        ([, a], [, b]) => a.index - b.index || a.length - b.length,
    );

    for (const [number, match] of inSubjectOrder) {
        // Two matches that share a CHARACTER cannot both be painted in one flat run of text (two
        // that share only a boundary can, which is the sort above). An engine walk does not overlap
        // that way, so this cannot fire from the engine; it can fire from a hand-written fragment or
        // the console, and a negative slice length renders as an empty string rather than as an
        // error - the kind of silent wrongness the demo exists to not have.
        if (match.index < at) continue;

        if (match.index > at) {
            pieces.push({ text: subject.slice(at, match.index), match: null });
        }

        // A zero-length match (`a*` against "bbb") still needs its own segment: it is the answer,
        // and a page that skipped it would show nothing where the group table shows a match.
        const end = match.index + match.length;
        const text = subject.slice(match.index, end);
        const runs = match.edits === undefined ? null : editRuns(subject, match.index, end, match.edits);
        pieces.push(runs === null ? { text, match: number } : { text, match: number, runs });
        at = end;
    }

    if (at < subject.length) {
        pieces.push({ text: subject.slice(at), match: null });
    }

    return { segments: pieces, shown: drawn.length, total: all.length };
}

/**
 * One match's text, broken into the errors it spent, or null when it spent none inside its own span.
 *
 * Positions outside the match are dropped rather than drawn. The engine cannot produce one - they
 * come from the match's own walk - but the answer arrives as JSON from a worker, and a run sliced
 * beyond the match would paint the subject around it as though it were inside it.
 */
function editRuns(subject: string, start: number, end: number, edits: Edits): readonly EditRun[] | null {
    const kinds = new Map<number, EditKind>();
    const carets = new Map<number, number>();

    // Where the walk below will actually stand when it reaches this position. Widening a position
    // to the start of its character can put it before the match - the pair straddles the span's
    // edge - and a run keyed there is one the walk never visits, so the error would simply vanish.
    const runFrom = (at: number): number => Math.max(startOfCharacter(subject, at), start);

    const mark = (at: number, kind: EditKind): void => {
        if (at < start || at >= end) return;
        // First kind wins. Two errors on one character is not an answer the engine gives, and the
        // alternative - the later kind overwriting the earlier - is no more true than this one.
        const from = runFrom(at);
        if (!kinds.has(from)) kinds.set(from, kind);
    };

    for (const at of edits.substitutions) mark(at, 'sub');
    for (const at of edits.insertions) mark(at, 'ins');
    for (const at of edits.deletions) {
        // `<= end` and not `< end`: a deletion at the end of the match is the ordinary case of a
        // pattern that asked for one more character than the subject had.
        if (at < start || at > end) continue;
        const from = at === end ? end : runFrom(at);
        carets.set(from, (carets.get(from) ?? 0) + 1);
    }

    if (kinds.size === 0 && carets.size === 0) return null;

    const runs: EditRun[] = [];
    let plainFrom = start;
    const flush = (upto: number): void => {
        if (upto > plainFrom) {
            runs.push({ text: subject.slice(plainFrom, upto), kind: null, count: 1, index: plainFrom });
        }
        plainFrom = upto;
    };

    // ONE run for neighbouring errors of the same kind, carrying how many characters it covers.
    // The letter marks the run, so a character each drew six `s` letters in a row under the owner's
    // first match, where the six characters are one substituted word (S75, spec line 40).
    let openFrom = start;
    let openKind: EditKind | null = null;
    let openCount = 0;
    const close = (upto: number): void => {
        if (openKind === null) return;
        runs.push({ text: subject.slice(openFrom, upto), kind: openKind, count: openCount, index: openFrom });
        plainFrom = upto;
        openKind = null;
        openCount = 0;
    };

    for (let at = start; at <= end; ) {
        // ONE mark for all the deletions in one place, carrying how many there are. Two characters
        // missing from one place is still twice the story one is, which is why the count is drawn;
        // what it is not is two marks, because a deletion has no width of its own to separate them
        // by and five of them landed on one x-position (S75, the owner's `(foobar){e}` case).
        const missing = carets.get(at) ?? 0;
        if (missing > 0) {
            // A gap is drawn between the characters either side of it, so a run of one kind cannot
            // span it: the two halves are two marks with a hole between them.
            close(at);
            flush(at);
            runs.push({ text: '', kind: 'del', count: missing, index: at });
        }

        if (at === end) break;

        const kind = kinds.get(at);
        // Never past the end of the match: a pair whose second half is outside the span would take
        // the run with it, and the segment after this one paints that half again.
        const upto = Math.min(at + widthOfCharacter(subject, at), end);
        if (kind === undefined) {
            close(at);
        } else {
            if (kind !== openKind) {
                close(at);
                flush(at);
                openFrom = at;
                openKind = kind;
            }
            openCount += 1;
        }

        at = upto;
    }

    close(end);
    flush(end);
    return runs;
}

/**
 * Where the character at `at` starts: one back, when `at` points at the low half of a surrogate pair.
 *
 * The engine counts UTF-16 code units, so a position it reports can land on half a character.
 * Slicing there puts a lone surrogate in a text node, which a browser paints as U+FFFD - the demo
 * corrupting the subject it exists to show.
 */
function startOfCharacter(subject: string, at: number): number {
    const code = subject.charCodeAt(at);
    const before = at > 0 ? subject.charCodeAt(at - 1) : 0;
    const low = code >= 0xdc00 && code <= 0xdfff;
    const high = before >= 0xd800 && before <= 0xdbff;
    return low && high ? at - 1 : at;
}

/** How many code units the character at `at` takes. Two for a surrogate pair, one for everything else. */
function widthOfCharacter(subject: string, at: number): number {
    const codepoint = subject.codePointAt(at);
    return codepoint !== undefined && codepoint > 0xffff ? 2 : 1;
}
