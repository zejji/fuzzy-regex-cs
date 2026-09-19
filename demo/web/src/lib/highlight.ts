// Turning an answer into something renderable, under a cap.
//
// Rendering is the main thread's job and the Web Worker cannot protect it (DemoEngine.cs, MaxSpans):
// the engine can answer promptly with hundreds of matches and the page can still die building a DOM
// node for each one. So the cap is applied to the MATCHES, before any segment is built, rather than
// to the segments afterwards - slicing after the work is done is a cap that costs exactly as much as
// having no cap.

import type { Span } from '../types';

import { MAX_DISPLAYED_MATCHES } from './caps';

export { MAX_DISPLAYED_MATCHES };

/** One run of the subject as the page paints it. `match` is null for the text between matches. */
export interface Segment {
    readonly text: string;
    /** The match's number in the full answer, or null for plain text. */
    readonly match: number | null;
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
    matches: readonly Span[] | undefined,
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
        pieces.push({ text: subject.slice(match.index, match.index + match.length), match: number });
        at = match.index + match.length;
    }

    if (at < subject.length) {
        pieces.push({ text: subject.slice(at), match: null });
    }

    return { segments: pieces, shown: drawn.length, total: all.length };
}
