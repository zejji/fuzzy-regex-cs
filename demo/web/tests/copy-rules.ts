/**
 * The copy rules the demo's user-facing strings are held to, as data.
 *
 * One exported array, so the linter, its self-test and anybody adding a string all read the same
 * list. Each rule carries the source that asked for it, because a banned word with no reason
 * attached is a rule the next person deletes.
 *
 * The sources, all read 2026-09-19:
 *   - Wikipedia:Signs of AI writing - negative parallelism (WP:AIPARALLEL), puffery (WP:AIPUFFERY),
 *     the vocabulary list (WP:AIVOCAB), the trailing -ing summary (WP:SUPERFICIAL), rule of three.
 *   - GOV.UK's A to Z style guide - words to avoid.
 *
 * Deliberately narrow where a broad rule would be noisy. "Restating the obvious" is in the slice's
 * ban list and is NOT here: no pattern distinguishes a sentence that repeats its label from one
 * that adds the fact the label leaves out, so that one is a review question. Every rule here fires
 * on a form, never on a judgement.
 */

export interface CopyRule {
    /** Short name, quoted in the failure message so a violation names the rule it broke. */
    readonly id: string;
    /** What the rule bans and who asked for it. */
    readonly why: string;
    /** True when the string breaks the rule. */
    readonly broken: (text: string) => boolean;
}

/** Any of the patterns matching means the rule is broken. */
const anyOf =
    (...patterns: readonly RegExp[]) =>
    (text: string): boolean =>
        patterns.some((pattern) => pattern.test(text));

/** A word list as one alternation, matched on word boundaries and case-insensitively. */
const words = (...list: readonly string[]): RegExp =>
    new RegExp(String.raw`\b(${list.join('|')})\b`, 'i');

/**
 * Words shaped like adjectives, for the triad rule.
 *
 * Suffixes only, plus a short list of the common adjectives no suffix catches. `-y`, `-ed` and
 * `-ing` are left out on purpose: they would make "memory, library and query" a triad of
 * adjectives, which is the false positive that gets a rule like this switched off.
 */
const ADJECTIVE = /^(?:\w+(?:ive|ous|ful|ent|ant|able|ible|less|ish|ic|al)|robust|fast|slow|simple|clear|clean|easy|hard|small|big|short|long|plain|quick|smart|modern|rich|deep|broad|strong|sharp|light|dark|free|safe|exact|fuzzy|strict|loose)$/i;

/** Three comma-separated words in a row, with or without a trailing "and". */
const TRIAD = /\b(\w+),\s+(\w+),?\s+(?:and\s+)?(\w+)\b/g;

export const COPY_RULES: readonly CopyRule[] = [
    {
        id: 'triad',
        why: 'Three adjectives in a row. Wikipedia names the rule of three as the commonest tell.',
        broken: (text) => {
            for (const [, first, second, third] of text.matchAll(TRIAD)) {
                if ([first, second, third].every((word) => word !== undefined && ADJECTIVE.test(word))) return true;
            }
            return false;
        },
    },
    {
        id: 'negative-parallelism',
        why: '"not X but Y", "it is not X, it is Y", "no X, no Y". WP:AIPARALLEL.',
        broken: anyOf(
            /\bnot\s+[^.,;:]{1,40},?\s+but\s/i,
            /\bit'?s not\s+[^.]{1,40},\s*it'?s\b/i,
            /\bno\s+\w+,\s*no\s+\w+/i,
        ),
    },
    {
        id: 'says-what-it-is-not',
        why: 'Defining a thing by what it is not, or correcting a misconception the reader never had.',
        broken: anyOf(/,\s*(?:and\s+)?not\s+\S/i, /\band not\b/i, /\b(?:is|are|was|were)\s+not\s+an?\b/i, /\bdoes not mean\b/i),
    },
    {
        id: 'rhetorical',
        why: 'A set-up, or a question asked in order to answer it.',
        broken: anyOf(
            /\b(?:what this means is|here'?s the thing|the good news is|why does this matter|but here'?s)\b/i,
            // A question answered by the words after it. The sentence has to OPEN with a question
            // word: a bare `?` followed by text is not a question. `TimeSpan? timeout` is the case
            // that made this narrower - C# strings are linted too, and nullable types are common.
            /(?:^|[.!?]\s+)(?:what|why|how|when|where|who|which|is|are|can|could|should|would|will|do|does|did)\b[^.!?]*\?\s+\S/i,
        ),
    },
    {
        id: 'puffery',
        why: 'WP:AIPUFFERY: praise in place of a fact.',
        broken: anyOf(
            words(
                'seamless',
                'seamlessly',
                'robust',
                'powerful',
                'comprehensive',
                'cutting-edge',
                'blazing',
                'elegant',
                'intuitive',
                'simply',
            ),
            /\brich(?:ly)?\b/i,
        ),
    },
    {
        id: 'corporate',
        why: "GOV.UK's words to avoid.",
        broken: anyOf(
            words(
                'leverage',
                'leverages',
                'utilise',
                'utilize',
                'facilitate',
                'empower',
                'empowers',
                'streamline',
                'streamlines',
                'foster',
                'fosters',
                'transform',
                'transforms',
                'going forward',
            ),
            // "deliver" of anything but a parcel, and "drive" of anything but a car.
            /\bdeliver(?:s|ed|ing|y)?\b(?!\s+(?:a parcel|the post))/i,
            /\bdrives?\b(?!\s+(?:a car|home))/i,
            /\bkey\s+(?:feature|benefit|point|part|difference|aspect|component|takeaway)/i,
            // "deploy" of anything but software; the demo's own deployment prose is allowed.
            /\bdeploy(?:s|ed|ing|ment)?\b(?!\s+(?:to (?:GitHub )?Pages|the (?:page|site|demo|bundle)))/i,
        ),
    },
    {
        id: 'llm-vocabulary',
        why: 'WP:AIVOCAB, plus the openers that give an LLM away.',
        broken: anyOf(
            words(
                'delve',
                'delves',
                'crucial',
                'pivotal',
                'meticulous',
                'meticulously',
                'intricate',
                'tapestry',
                'testament',
                'showcase',
                'showcases',
                'garner',
                'garners',
                'enhance',
                'enhances',
                'ensure',
                'ensures',
                'boasts',
            ),
            /\bdives? into\b/i,
            /\bdesigned to\b/i,
            /\bserves as\b/i,
            /\bstands as\b/i,
            /(?:^|[.!?]\s+)(?:Additionally|Moreover|Furthermore)\b/,
            /\bit'?s important to note\b/i,
        ),
    },
    {
        id: 'trailing-ing',
        why: 'WP:SUPERFICIAL: a participle clause that restates the sentence it hangs off.',
        broken: anyOf(
            /,\s+(?:highlighting|underscoring|ensuring|showcasing|demonstrating|illustrating|reflecting|emphasi[sz]ing|allowing you|enabling)\b/i,
        ),
    },
    {
        id: 'em-dash',
        why: 'Em and en dashes in interface copy. A hyphen or a full stop instead.',
        broken: anyOf(/[—–]/),
    },
];

/** Every rule the string breaks, in the order they are declared. */
export const violations = (text: string): readonly CopyRule[] =>
    COPY_RULES.filter((rule) => rule.broken(text));
