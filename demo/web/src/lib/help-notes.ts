/**
 * One note per input heading: what the box is for, and where the documentation says more.
 *
 * The owner read the page and could not tell what "Named lists" was for (S75, item 2). A heading is
 * a name, and a name only helps somebody who already knows the thing - so each of the six carries a
 * `(?)` button with a sentence or two behind it, and the sentence ends with a press that opens the
 * help tab at the section of `docs/COMPARISON.md` that covers it.
 *
 * Data rather than markup, for the reason `FLAG_HELP` is: the copy linter reads this file, so every
 * sentence here is held to the same rules as the rest of the page, and `help-notes.test.ts` checks
 * each `helpKey` against the keys `tools/build-demo-help.ps1` actually writes a panel for. A link
 * that opens an empty tab is then a red build rather than a dead end on the page.
 */

/** One heading's note: the button's name, the sentence, and the panel the sentence points at. */
export interface HeadingNote {
    /** Short name, used to build the button's and the note's ids. Unique across the six. */
    readonly id: string;
    /** The heading as the page spells it, so the note and the label cannot drift from it. */
    readonly heading: string;
    /** What the button is called for a screen reader, which reads the label and not the "?". */
    readonly label: string;
    readonly note: string;
    /** The words on the press that opens the help tab. */
    readonly linkText: string;
    /** Which panel that press opens: a feature key `tools/build-demo-help.ps1` maps. */
    readonly helpKey: string;
}

/** The id the `(?)` button's `aria-controls` and the note itself agree on. */
export const noteId = (id: string): string => `heading-help-${id}`;

/** The id of the button that opens one note. */
export const noteButtonId = (id: string): string => `heading-help-button-${id}`;

/**
 * The six, in the order the input pane shows them.
 *
 * Each names a real section of the documentation rather than a general one: the Subject note points
 * at the UTF-16 rule because that is the fact about the subject box a visitor gets wrong, and the
 * Flags note points at the version default because that is the one flag choice already made for
 * them before they touch the panel.
 */
export const HEADING_NOTES: readonly HeadingNote[] = [
    {
        id: 'pattern',
        heading: 'Pattern',
        label: 'What Pattern is for',
        note: 'The expression the engine runs. A fuzzy budget goes after the part it applies to: (?:colour){e<=2} allows up to two errors inside that word.',
        linkText: 'How a fuzzy budget is written',
        helpKey: 'fuzzy',
    },
    {
        id: 'subject',
        heading: 'Subject',
        label: 'What Subject is for',
        note: 'The text the pattern is run against. Every index and length on this page counts UTF-16 code units, so a character outside the Basic Multilingual Plane, an emoji among them, counts as two.',
        linkText: 'How indices are counted',
        helpKey: 'indices',
    },
    {
        id: 'flags',
        heading: 'Flags',
        label: 'What Flags is for',
        note: 'The options the pattern is compiled with, such as IgnoreCase. Version 1 applies until you choose Version 0, which matches the behaviour of Python re.',
        linkText: 'What Version 1 changes',
        helpKey: 'version',
    },
    {
        id: 'mode',
        heading: 'Mode',
        label: 'What Mode is for',
        note: 'Which question the engine is asked: every match in the subject, a partial match that runs out of subject before the pattern finishes, or the subject with every match rewritten.',
        linkText: 'What a partial match means',
        helpKey: 'partial',
    },
    {
        id: 'replacement',
        heading: 'Replacement template',
        label: 'What Replacement template is for',
        note: 'The text each match is rewritten as, in replace mode. Groups go in upstream style: \\1 by number, \\g<name> by name.',
        linkText: 'How a replacement template is written',
        helpKey: 'replace',
    },
    {
        id: 'named-lists',
        heading: 'Named lists',
        label: 'What Named lists is for',
        // The owner's own words, written into the slice spec to set the standard for the other five.
        // One change to them: the spec said "one word per line", and the box takes one LIST per line,
        // as `name: word, word` - `namedLists` in lib/snippet.ts and `DemoEngine.TryParseNamedLists`
        // both read it that way, and the hint under the box already says so.
        note: 'A named list is a set of words the pattern can match as one alternative, written \\L<name>. Give the list here, one list per line, and the pattern refers to it by name. Fuzzy budgets apply to the list as a whole.',
        linkText: 'How a named list is matched',
        helpKey: 'namedlists',
    },
];

/**
 * One note by its id.
 *
 * Throws on an id nothing defines, rather than returning undefined: every caller is a heading that
 * must have a note, so the alternative is a `(?)` rendered beside an empty sentence and nothing said
 * about why.
 */
export const headingNote = (id: string): HeadingNote => {
    const note = HEADING_NOTES.find((candidate) => candidate.id === id);
    if (!note) throw new Error(`No heading note is defined for '${id}'.`);
    return note;
};
