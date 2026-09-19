<script setup lang="ts">
// The page. Its state is demo.ts, which is testable without a DOM; what is here is layout only.
import { computed, nextTick, onMounted, onUnmounted, proxyRefs, ref, useTemplateRef, watch } from 'vue';

import { spawnEngineWorker, useDemo } from './demo';
import { copyText } from './lib/clipboard';
import { createPool } from './lib/pool';
import { toCSharp, tokenize } from './lib/snippet';
import type { Example } from './types';

const demo = useDemo();
const {
    pattern,
    flags,
    subject,
    mode,
    replacement,
    namedLists,
    answeredSubject,
    answeredPattern,
    examples,
    helpSections,
    engine,
    engineError,
    busy,
    answer,
    failure,
    failureOffset,
    elapsedMs,
    selected,
    shareable,
    matches,
    view,
    capped,
    current,
    replaced,
    partial,
    maxSubjectLength,
    load,
    select,
    stop,
} = demo;

/**
 * The three modes the engine answers, in the order the page offers them.
 *
 * Radios and not a `<select>`: there are three, all three fit, and the one in force is readable
 * without opening anything. `''` is the ordinary walk, which is what an empty mode means to the
 * engine - so the default choice needs no special case anywhere.
 */
const MODES = [
    { value: '', id: 'mode-walk', label: 'Find every match' },
    { value: 'partial', id: 'mode-partial', label: 'Partial match' },
    { value: 'replace', id: 'mode-replace', label: 'Replace' },
];

// --- the shell's one breakpoint ----------------------------------------------------------------

/**
 * The window the two-pane shell needs, in the exact words `styles.css` uses for the same gate.
 *
 * Written once here and once there, because CSS cannot hand a media query to a script and a script
 * that re-derived the layout would be a second opinion about it. `layout.test.ts` compares the two
 * strings, so the copies cannot drift apart unnoticed.
 *
 * Both halves matter. Under 64rem there is one column. Under 600 px of height - a phone held
 * sideways, or a laptop at 200 % zoom, where the CSS viewport halves in both directions - a frame
 * that refuses to scroll is a frame whose last control cannot be reached.
 */
const SHELL_QUERY = '(min-width: 64rem) and (min-height: 600px)';

/**
 * True while the window can carry the two-pane shell.
 *
 * Defaults to true where there is no `matchMedia` at all, which is jsdom: the tests that care
 * about the narrow layout stub it, and everything else should see the layout this page is for.
 */
function useWide(query: string) {
    const list = window.matchMedia?.(query) ?? null;
    const wide = ref(list?.matches ?? true);
    const update = (event: MediaQueryListEvent): void => {
        wide.value = event.matches;
    };
    list?.addEventListener('change', update);
    onUnmounted(() => list?.removeEventListener('change', update));
    return wide;
}

const wide = useWide(SHELL_QUERY);

/**
 * The two things a narrow window keeps closed, and a wide one never does.
 *
 * On one column every open thing above the answer is a screenful between a visitor and the answer,
 * which is the fault this slice exists to fix. On two columns neither costs anything: the input
 * pane has its own scroll and the answer is beside it, not below it.
 */
const advanced = ref(false);
const panelOpen = ref(false);

const advancedRegion = useTemplateRef<HTMLElement>('advancedRegion');
const samplesRegion = useTemplateRef<HTMLElement>('samplesRegion');

/**
 * A window that narrows keeps the focus where the visitor put it.
 *
 * Both regions above are open on two columns and closed on one, so a window that crosses the gate
 * while a control inside one of them has focus hides that control, and the browser drops focus to
 * `<body>`. The visitor is then at the top of the document, by an act they did not perform - which
 * is what WCAG 3.2.2 On Input is about.
 *
 * The watcher's default pre-flush timing is what makes this cheap: it runs before the DOM is
 * updated, so `document.activeElement` is still the focused element and opening its disclosure
 * means the element is never hidden at all. Nothing has to be re-focused afterwards.
 *
 * What the gate opens, the gate closes: a region left open would be back on the next narrowing with
 * nothing in it, which is the screenful above the answer this slice is removing. A region the
 * visitor opened is not the gate's to close, so which one it was is remembered.
 */
const openedByGate = ref<'advanced' | 'samples' | null>(null);

/**
 * The visitor's own press of a disclosure, which also takes the region back from the gate.
 *
 * Both halves matter. Without the first, the gate cannot undo what it did; without the second, a
 * region the gate opened and the visitor then re-opened for themselves is still closed on the next
 * widening, and they lose a panel they asked for.
 */
function toggleDisclosure(which: 'advanced' | 'samples'): void {
    if (openedByGate.value === which) openedByGate.value = null;
    if (which === 'advanced') advanced.value = !advanced.value;
    else panelOpen.value = !panelOpen.value;
}

watch(wide, (isWide) => {
    if (isWide) {
        if (openedByGate.value === 'advanced') advanced.value = false;
        if (openedByGate.value === 'samples') panelOpen.value = false;
        openedByGate.value = null;
        return;
    }

    const focused = document.activeElement;
    if (focused === null) return;
    if (advancedRegion.value?.contains(focused) === true && !advanced.value) {
        advanced.value = true;
        openedByGate.value = 'advanced';
    }
    if (samplesRegion.value?.contains(focused) === true && !panelOpen.value) {
        panelOpen.value = true;
        openedByGate.value = 'samples';
    }
});

// --- examples and help, as one tab set ---------------------------------------------------------

/**
 * Two tabs and no more, to NN/g's rules: one row, one always selected, one-word labels, the panel
 * below the list.
 *
 * The ids are fixed strings rather than generated, because each tab names its panel and each panel
 * names its tab, and a page with one tab set needs no uniqueness beyond that.
 */
const TABS = [
    { value: 'examples', id: 'tab-examples', panel: 'panel-examples', label: 'Examples' },
    { value: 'help', id: 'tab-help', panel: 'panel-help', label: 'Help' },
] as const;

const tab = ref<(typeof TABS)[number]['value']>('examples');

/**
 * Arrow keys across the tabs, with the selection following the focus.
 *
 * ARIA's authoring practices ask for automatic activation wherever the panels render without
 * noticeable latency, which these do - they are already in memory. The ends hold rather than
 * wrapping, which is what the match highlights and the table rows do, so the whole page answers an
 * arrow key the same way.
 */
function onTabKeydown(event: KeyboardEvent): void {
    const at = TABS.findIndex((item) => item.value === tab.value);
    const to =
        event.key === 'ArrowLeft'
            ? Math.max(at - 1, 0)
            : event.key === 'ArrowRight'
              ? Math.min(at + 1, TABS.length - 1)
              : event.key === 'Home'
                ? 0
                : event.key === 'End'
                  ? TABS.length - 1
                  : null;
    if (to === null) return;

    event.preventDefault();
    const destination = TABS[to];
    if (destination === undefined) return;
    tab.value = destination.value;
    void nextTick(() => document.getElementById(destination.id)?.focus());
}

/**
 * Loads a sample and shows what it demonstrates.
 *
 * Clicking a sample is a visitor asking what it does, and the documentation's own answer is behind
 * the other tab - so the click opens it. A sample with no documented section leaves the tabs where
 * they were rather than opening a panel onto nothing, and the examples are one click back either
 * way. Nothing moves for a visitor who did not click.
 */
function loadExample(example: Example): void {
    load(example);
    if (helpSections.value.length > 0) tab.value = 'help';
}

// --- linking a highlight to its row ----------------------------------------------------------
//
// Which of six numbers in a table belongs to which highlight is the question the table raises, and
// pointing at either is the cheapest answer. It stays here rather than in demo.ts because it is
// view state and pointer-only: it changes nothing the engine is asked, and the keyboard's
// equivalent - the selection, which the arrows drive - already exists.
//
// Hovering deliberately does NOT select: the selection drives the group table below, and a pointer
// crossing the pane on its way elsewhere must not rewrite what is being examined.
const linked = ref<number | null>(null);

/** What a screen reader is told a highlight is: its number, its text, and whether it is partial. */
const markLabel = (index: number, text: string): string =>
    `match ${index + 1}, ${text === '' ? 'empty' : text}` +
    (matches.value[index]?.partialMatch === true ? ', partial' : '');

/**
 * The second line of the caret box: spaces up to the character the engine blamed, then the hat.
 *
 * One `<pre>` holds both lines, because the caret's whole job is to line up with the text above it:
 * two boxes would be two monospace boxes to keep in step, and any padding or wrapping applied to
 * one of them moves the hat off its character. Only this row is a `<span>`, and only so that it can
 * be hidden from a screen reader - read out, a row of spaces and a `^` is nothing at all.
 *
 * Drawn against `answeredPattern` and never the live box - the offset indexes the string the engine
 * was given, which for the debounce plus the round trip is not what the field says. `errorOffset`
 * is bounded to a real index by `shapes.ts` before it gets here, so `repeat` can neither throw nor
 * build a string worth noticing.
 */
const caretRow = computed(() =>
    failureOffset.value === null ? '' : '\n' + ' '.repeat(failureOffset.value) + '^',
);

// --- one tab stop per group of matches, not one per match ------------------------------------
//
// WAI-ARIA's roving tabindex: the selected match is the only tab stop in the subject, and the
// selected row the only one in the table, with the arrow keys moving between them. A tab stop per
// match reads as accessible and is not: a 200-match answer put 200 stops in the subject and
// another 200 in the table, so Tab stopped being a way to cross the page at all.
const subjectPane = useTemplateRef<HTMLElement>('subjectPane');
const matchRows = useTemplateRef<HTMLElement>('matchRows');

/**
 * The answer's two halves, and how to find one match in each of them.
 *
 * Written once, because the keyboard and the linking both have to find the same element: the
 * arrows move the focus inside the half being driven, and a click in either half moves the other
 * one to the same match. Two copies of these selectors would be two chances for the two features
 * to disagree about which element is match 3.
 *
 * `focusable` and `reveal` differ on the table side and that is deliberate: the keyboard's control
 * is the little button in the first cell, while the thing worth scrolling to is the whole row -
 * the row carries the answer, and `nearest` on a 24 px control in its first cell is satisfied by
 * a sliver of the row at the edge of the pane, with the five columns of numbers still out of it.
 *
 * Every lookup is by `data-match` and never by position. Under RightToLeft the highlights sit in
 * subject order while the rows sit in the answer's order, so the two halves disagree about which
 * element is third.
 */
const HALVES = {
    subject: { within: subjectPane, focusable: 'mark.hit', reveal: 'mark.hit' },
    rows: { within: matchRows, focusable: 'button.row-select', reveal: 'tr' },
} as const;

type Half = keyof typeof HALVES;

/** The element in one half that stands for a given match, or null while the answer has no such row. */
function elementFor(half: Half, kind: 'focusable' | 'reveal', index: number): HTMLElement | null {
    const { within } = HALVES[half];
    return within.value?.querySelector<HTMLElement>(`${HALVES[half][kind]}[data-match="${index}"]`) ?? null;
}

/**
 * How a reveal scrolls, which is the visitor's setting rather than this page's taste.
 *
 * A scroll nobody asked to watch is motion triggered by an interaction, and `prefers-reduced-motion`
 * is how a browser passes on a visitor who does not want it (WCAG 2.3.3 Animation from
 * Interactions). Asked through `matchMedia`, optionally, for the same reason the shell's own gate
 * is: jsdom has none.
 */
const REDUCED_MOTION = '(prefers-reduced-motion: reduce)';

/**
 * Brings the OTHER view of a match into view: the row for a highlight, the highlight for a row.
 *
 * Which of six numbers in a table belongs to which highlight is the question the table raises, and
 * moving the counterpart into view is the answer that survives a long subject, where the two halves
 * are a scroll apart. The half the visitor acted in is left where it is: nobody needs the thing
 * under their pointer moved.
 *
 * `nearest` in both axes, so a counterpart already on screen does not jump - which is the
 * commonest case of all, a short subject with both halves visible at once.
 *
 * @param origin The half the visitor acted in.
 * @param index The match, numbered as the answer numbers it.
 */
function reveal(origin: Half, index: number): void {
    elementFor(origin === 'subject' ? 'rows' : 'subject', 'reveal', index)?.scrollIntoView({
        block: 'nearest',
        inline: 'nearest',
        behavior: window.matchMedia?.(REDUCED_MOTION).matches === true ? 'auto' : 'smooth',
    });
}

/** Selects a match from one half of the answer and brings the other half to it. */
function selectFrom(origin: Half, index: number): void {
    select(index);
    reveal(origin, index);
}

/**
 * Moves the selection with an arrow key and takes the focus with it.
 *
 * @param back The key that moves towards the first match, which differs by the half's direction:
 *   the highlights run along the subject and the rows run down the table.
 * @param origin The half being driven, so the focus stays inside it.
 */
function rove(event: KeyboardEvent, back: string, forward: string, origin: Half): void {
    const last = view.value.shown - 1;
    if (last < 0) return;

    const to =
        event.key === back
            ? Math.max(selected.value - 1, 0)
            : event.key === forward
              ? Math.min(selected.value + 1, last)
              : event.key === 'Home'
                ? 0
                : event.key === 'End'
                  ? last
                  : null;
    if (to === null) return;

    // The ends hold rather than wrapping, and the page does not scroll: every one of these keys
    // scrolls something by default, and the table's own scroll container is right underneath.
    event.preventDefault();

    // Selection only: an arrow does NOT bring the other half over. Both halves share one scroll
    // container, so when they are a screen apart only one of them can be on screen, and the one
    // that must be is the one holding the focus (WCAG 2.4.3 Focus Order). Measured in Chrome
    // 2026-09-19, 80 matches at 1366x768: with a reveal here, ArrowRight scrolled the pane away to
    // the counterpart row and the focus call on the next tick scrolled it straight back, so the
    // reveal was a cancelled animation and nothing else. Enter, Space and a click keep focus put,
    // and there the reveal survives - which is where the spec's two-way linking lives.
    select(to);

    // On the next render, because the destination is a tab stop only after it: moving focus first
    // would leave the focused control holding `tabindex="-1"` for a frame, and Tab out of it then
    // resumes from the wrong place. The focus is its own reveal - a browser scrolls what it focuses.
    void nextTick(() => elementFor(origin, 'focusable', to)?.focus());
}

const onSubjectKeydown = (event: KeyboardEvent): void =>
    rove(event, 'ArrowLeft', 'ArrowRight', 'subject');

const onRowsKeydown = (event: KeyboardEvent): void => rove(event, 'ArrowUp', 'ArrowDown', 'rows');

// --- the case as C# ----------------------------------------------------------------------------

/**
 * The snippet panel: shut until asked for, and generated from the boxes as they stand.
 *
 * Shut by default because the page's job is the answer, and a block of code above the fold is a
 * screenful between a visitor and it. Generated from the live inputs rather than the answered ones
 * so that what it hands over is the case the visitor is looking at - `answeredPattern` exists for
 * the caret, which has to index the exact string the engine was given, and code to paste has no
 * such constraint.
 */
const snippetOpen = ref(false);
const snippetToggle = useTemplateRef<HTMLElement>('snippetToggle');
const snippetCode = useTemplateRef<HTMLElement>('snippetCode');

const snippet = computed(() =>
    toCSharp({
        pattern: pattern.value,
        flags: flags.value,
        subject: subject.value,
        mode: mode.value,
        replacement: replacement.value,
        namedLists: namedLists.value,
    }),
);

/** The same text as coloured runs. Spans with text in them, so nothing here can become markup. */
const snippetTokens = computed(() => tokenize(snippet.value));

/** What the last copy did, said in the region that announces the answer. Cleared on every open. */
const copyNote = ref('');

/**
 * Opens the panel and stands in it; closes it and goes back to the button.
 *
 * The focus move is the whole of the keyboard story here. Revealing a region and leaving the focus
 * on the button means tabbing through the code to reach the copy control, and closing it without
 * putting the focus back drops the visitor at the top of the document (WCAG 2.4.3 Focus Order).
 */
function toggleSnippet(): void {
    snippetOpen.value = !snippetOpen.value;
    copyNote.value = '';
    if (!snippetOpen.value) {
        snippetToggle.value?.focus();
        return;
    }
    // After the render, because a hidden element cannot take focus.
    void nextTick(() => snippetCode.value?.focus());
}

/** Escape, from anywhere inside the panel. */
function closeSnippet(): void {
    if (!snippetOpen.value) return;
    snippetOpen.value = false;
    copyNote.value = '';
    snippetToggle.value?.focus();
}

/** Copies, or selects the code and says which happened. `copyText` never throws. */
async function copySnippet(): Promise<void> {
    const outcome = await copyText(snippet.value, snippetCode.value);
    copyNote.value =
        outcome === 'copied'
            ? 'copied to the clipboard'
            : 'the browser refused the clipboard, so the code is selected';
}

onMounted(demo.initialise);
// The page's lifetime is the document's, so this never runs in production. It runs in the tests,
// where a mount that left its `hashchange` listener and its two workers behind would answer the
// next test's questions.
onUnmounted(demo.dispose);

// The page's own state, for a driver to read. Same contract as S70's window.__harness: a machine
// can wait on it and read what the page believes rather than scraping rendered text.
//
// `proxyRefs` and not the value `app.mount()` returns: this is the only form that unwraps the refs
// for a reader AND writes through to them for a driver, and it says so at the assignment rather
// than depending on what mount happens to hand back for a component using `<script setup>`.
window.__demo = proxyRefs(demo);

// The two pieces checks.html needs to build a pool of its own, for the respawn measurement. It
// cannot import them: the bundle is one hashed file per build, so there is no stable module URL to
// import from. Published here rather than reached for through the bundle.
window.__demoInternals = { createPool, spawnEngineWorker };
</script>

<template>
    <div class="shell">
        <header class="shell-header">
            <h1 class="text-[1.75rem] leading-none font-semibold tracking-tight">FuzzyRegex</h1>
            <p class="text-xs leading-relaxed text-shell-muted">
                A C# port of Python's <code class="font-mono">regex</code> module, running in your
                browser on WebAssembly. Edit any box and the answer follows.
            </p>
            <!--
              The repository, from the top of the page. The same link is in the footer, which the
              fixed shell put beyond reach of a visitor who never scrolls the input pane.

              Named by the repository rather than by "GitHub": the destination is this library, and
              a visitor deciding whether to follow a link is owed which one it is (WCAG 2.4.4). It
              opens in this tab - the case is in the address bar, so Back brings all of it home.
            -->
            <a class="header-link" href="https://github.com/zejji/fuzzy-regex-cs">
                <svg class="size-4" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true">
                    <path
                        d="M8 0a8 8 0 0 0-2.53 15.59c.4.07.55-.17.55-.38l-.01-1.34c-2.23.48-2.7-1.07-2.7-1.07-.36-.93-.89-1.18-.89-1.18-.73-.5.05-.49.05-.49.8.06 1.23.83 1.23.83.72 1.23 1.88.87 2.34.67.07-.52.28-.87.5-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.01.08-2.12 0 0 .67-.21 2.2.82a7.6 7.6 0 0 1 4 0c1.53-1.03 2.2-.82 2.2-.82.44 1.11.16 1.92.08 2.12.51.56.82 1.28.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48l-.01 2.2c0 .21.14.46.55.38A8 8 0 0 0 8 0Z"
                    />
                </svg>
                zejji/fuzzy-regex-cs
            </a>
        </header>

        <main class="shell-body">
            <div class="input-pane">
                <!-- One column, labels above their field, hints below it and persistent. -->
                <section class="flex flex-col gap-4" aria-label="The case">
                    <div>
                        <label class="field-label" for="pattern">Pattern</label>
                        <input
                            id="pattern"
                            v-model="pattern"
                            class="field"
                            spellcheck="false"
                            autocapitalize="off"
                            autocomplete="off"
                        />
                        <!--
                          The failure that has a place in the pattern goes UNDER the pattern, with a
                          caret at the character the engine named; everything else goes to the block
                          in the results region. Said in one place or the other, never both: a
                          message repeated twice reads as two problems.
                        -->
                        <div v-if="failureOffset !== null" class="parse-error" role="status">
                            <p>{{ failure }}</p>
                            <!--
                              A scroll region with a tab stop, a role and a name, exactly as the
                              two tables have (S71): the box does not wrap - a wrapped pattern puts
                              the hat under a character on a different line - so a pattern wider
                              than the pane can only be read by scrolling, and a region only a
                              pointer can drag fails WCAG 2.1.1.

                              The whole <pre> was `aria-hidden` before this became focusable, and
                              the two cannot both be true: an element that takes focus and is
                              hidden from assistive technology is a stop a screen reader lands on
                              and is told nothing about. So the CARET ROW alone is hidden - read
                              out, a row of spaces and a hat is nothing at all - and the sentence
                              underneath gives the position in words.
                            -->
                            <pre
                                tabindex="0"
                                role="region"
                                aria-label="The pattern the engine was given"
                                aria-describedby="caret-hint"
                            >{{ answeredPattern }}<span aria-hidden="true">{{ caretRow }}</span></pre>
                            <!-- Not `.field-hint`: its grey is measured against the ink pane, not
                                 against this red. The sentence inherits the box's own colour. -->
                            <p id="caret-hint" class="mt-1 text-xs leading-relaxed">
                                The caret is under character {{ failureOffset + 1 }}. The line
                                scrolls sideways.
                            </p>
                        </div>
                    </div>

                    <div>
                        <label class="field-label" for="subject">Subject</label>
                        <textarea
                            id="subject"
                            v-model="subject"
                            class="field min-h-28 resize-y"
                            spellcheck="false"
                            aria-describedby="subject-hint"
                        ></textarea>
                        <p id="subject-hint" class="field-hint">
                            Up to {{ maxSubjectLength.toLocaleString() }} characters. Currently
                            {{ subject.length.toLocaleString() }}.
                        </p>
                    </div>

                    <!--
                      On one column the four remaining boxes sit between the subject and the answer,
                      so they fold away until they are wanted. On two columns they are always open:
                      the pane has its own scroll and nothing below it is waiting for the space.
                    -->
                    <button
                        v-if="!wide"
                        class="disclosure"
                        type="button"
                        :aria-expanded="advanced"
                        aria-controls="advanced-inputs"
                        @click="toggleDisclosure('advanced')"
                    >
                        Flags, mode and lists
                        <span aria-hidden="true" :class="advanced ? 'chevron chevron-open' : 'chevron'">&#9662;</span>
                    </button>

                    <!--
                      Hidden rather than dropped, so that `aria-controls` above names something that
                      is there: an id that resolves to nothing is a reference to nothing. `hidden`
                      takes the element out of the accessibility tree as well as off the screen
                      (HTML-AAM maps it to "not exposed"), so a closed disclosure is closed for a
                      screen reader too, and Tailwind's own reset gives it `!important` so a display
                      utility on the same element cannot reopen it.
                    -->
                    <div
                        id="advanced-inputs"
                        ref="advancedRegion"
                        class="flex flex-col gap-4"
                        :hidden="!wide && !advanced"
                    >
                        <div>
                            <label class="field-label" for="flags">Flags</label>
                            <input
                                id="flags"
                                v-model="flags"
                                class="field"
                                spellcheck="false"
                                autocapitalize="off"
                                autocomplete="off"
                                aria-describedby="flags-hint"
                            />
                            <p id="flags-hint" class="field-hint">
                                FuzzyRegexOptions names, separated by commas or spaces:
                                <code class="font-mono">IgnoreCase, BestMatch</code>. A typo is an
                                error. Version1 is the default.
                            </p>
                        </div>

                        <!--
                          A fieldset and a legend, which is how a set of radios is named: without
                          one, each radio is announced with its own label and nothing says what the
                          three of them together are choosing.
                        -->
                        <fieldset aria-describedby="mode-hint">
                            <legend class="field-label">Mode</legend>
                            <div class="flex flex-wrap gap-x-6 gap-y-2">
                                <div
                                    v-for="option in MODES"
                                    :key="option.id"
                                    class="flex items-center gap-2"
                                >
                                    <input
                                        :id="option.id"
                                        v-model="mode"
                                        class="size-4 accent-accent-bright"
                                        type="radio"
                                        name="mode"
                                        :value="option.value"
                                    />
                                    <label :for="option.id" class="text-sm">{{ option.label }}</label>
                                </div>
                            </div>
                            <p id="mode-hint" class="field-hint">
                                Partial asks for one match and says whether the subject ran out
                                first. Replace rewrites every match with the template.
                            </p>
                        </fieldset>

                        <!--
                          The template belongs to replace mode and appears with it. A box that is
                          shown in every mode is a box somebody fills in and then wonders why it was
                          ignored.
                        -->
                        <div v-if="mode === 'replace'">
                            <label class="field-label" for="replacement">Replacement template</label>
                            <input
                                id="replacement"
                                v-model="replacement"
                                class="field"
                                spellcheck="false"
                                autocapitalize="off"
                                autocomplete="off"
                                aria-describedby="replacement-hint"
                            />
                            <p id="replacement-hint" class="field-hint">
                                Upstream's syntax: <code class="font-mono">\1</code> or
                                <code class="font-mono">\g&lt;name&gt;</code> for a group,
                                <code class="font-mono">\g&lt;0&gt;</code> for the whole match.
                            </p>
                        </div>

                        <div>
                            <label class="field-label" for="named-lists">Named lists</label>
                            <textarea
                                id="named-lists"
                                v-model="namedLists"
                                class="field min-h-16 resize-y"
                                spellcheck="false"
                                autocapitalize="off"
                                aria-describedby="named-lists-hint"
                            ></textarea>
                            <p id="named-lists-hint" class="field-hint">
                                For <code class="font-mono">\L&lt;name&gt;</code> in a pattern: one
                                list per line, as <code class="font-mono">name: word, word</code>.
                            </p>
                        </div>
                    </div>
                </section>

                <!--
                  The tab set sits at the foot of the pane at every width (owner decision: one
                  layout to maintain), and `mt-auto` is what puts it there when the inputs are
                  shorter than the pane.
                -->
                <section class="mt-auto flex flex-col" aria-label="Examples and help">
                    <button
                        v-if="!wide"
                        class="disclosure"
                        type="button"
                        :aria-expanded="panelOpen"
                        aria-controls="examples-and-help"
                        @click="toggleDisclosure('samples')"
                    >
                        Examples and help
                        <span aria-hidden="true" :class="panelOpen ? 'chevron chevron-open' : 'chevron'">&#9662;</span>
                    </button>

                    <!--
                      The disclosure controls the whole set, tabs included, so the id it names is
                      this box rather than whichever panel happens to be open. It was naming
                      `panel-examples`, which is not what it opens and, on the Help tab, was not in
                      the page at all.
                    -->
                    <div
                        id="examples-and-help"
                        ref="samplesRegion"
                        class="flex flex-col"
                        :hidden="!wide && !panelOpen"
                    >
                        <div
                            class="tab-list"
                            role="tablist"
                            aria-label="Examples and help"
                            @keydown="onTabKeydown"
                        >
                            <button
                                v-for="item in TABS"
                                :id="item.id"
                                :key="item.value"
                                class="tab"
                                :class="{ 'tab-selected': tab === item.value }"
                                type="button"
                                role="tab"
                                :aria-selected="tab === item.value"
                                :aria-controls="item.panel"
                                :tabindex="tab === item.value ? 0 : -1"
                                @click="tab = item.value"
                            >
                                {{ item.label }}
                            </button>
                        </div>

                        <!--
                          Both panels are in the page and the unselected one is `hidden`, because
                          each tab has to name the panel it controls (APG, Tabs: "Each element with
                          role tab has the property aria-controls referring to its associated
                          tabpanel element") and an id that resolves to nothing names nothing. It
                          was rendering one panel, so the unselected tab pointed into thin air.
                          `hidden` keeps the closed panel out of the accessibility tree, which is
                          what the old comment here wanted and is why nothing can wander into it.
                        -->
                        <div
                            id="panel-examples"
                            class="tab-panel"
                            role="tabpanel"
                            aria-labelledby="tab-examples"
                            :hidden="tab !== 'examples'"
                        >
                                <button
                                    v-for="example in examples"
                                    :key="example.title"
                                    class="example-button"
                                    type="button"
                                    @click="loadExample(example)"
                                >
                                    <span class="block text-sm font-semibold text-shell-text">
                                        {{ example.title }}
                                    </span>
                                    <span class="mt-1 block text-xs leading-relaxed text-shell-muted">
                                        {{ example.note }}
                                    </span>
                                </button>
                                <p class="field-hint">
                                    Every box stays editable. The answers are checked against
                                    Python's <code class="font-mono">regex</code> module in this
                                    project's test suite. The address bar holds the current case, so
                                    you can link to what you see.
                                </p>
                        </div>

                        <!--
                          The documentation's own words, beside the sample that led here.

                          <details>, so it is operable from a keyboard by construction rather
                          than by a handler - and never a hover panel, which is a control a
                          keyboard cannot reach at all. Every run is interpolated and none of it
                          is `v-html`: help.json is generated from a markdown file, and a file
                          that could put markup into this page could put a script here.

                          `tabindex="0"` only while there is nothing to load - with no sample
                          loaded the panel is one paragraph and a keyboard cannot reach it at all,
                          which is the case APG's Tabs note 4 names ("When the tabpanel does not
                          contain any focusable elements ... the tabpanel should set tabindex=0").
                          With sections there are summaries and code boxes to tab to, and a stop
                          on the panel itself would be one press in the way.
                        -->
                        <div
                            id="panel-help"
                            class="tab-panel"
                            role="tabpanel"
                            aria-labelledby="tab-help"
                            :tabindex="helpSections.length ? undefined : 0"
                            :hidden="tab !== 'help'"
                        >
                                <details
                                    v-for="(section, s) in helpSections"
                                    :key="s"
                                    class="help-panel"
                                    :open="s === 0"
                                >
                                    <summary><template v-for="(run, r) in section.heading" :key="r"><code
                                                v-if="run.code"
                                                class="font-mono"
                                            >{{ run.text }}</code><template v-else>{{ run.text }}</template></template></summary>
                                    <div class="help-body">
                                        <template v-for="(block, b) in section.blocks" :key="b">
                                            <p v-if="block.kind === 'paragraph'"><template
                                                    v-for="(run, r) in block.runs"
                                                    :key="r"
                                                ><code v-if="run.code" class="font-mono">{{ run.text }}</code><template
                                                        v-else
                                                    >{{ run.text }}</template></template></p>
                                            <!-- A fenced sample does not wrap either, so it is one
                                                 more scroll region and gets the same tab stop, role
                                                 and name. The name carries the hint rather than a
                                                 sentence under every code block: one visible line
                                                 per sample would be more hint than help, and what a
                                                 keyboard user needs is to be able to land on the
                                                 box at all. -->
                                            <pre
                                                v-else
                                                class="help-code"
                                                tabindex="0"
                                                role="region"
                                                aria-label="Code sample, scrollable sideways"
                                            >{{ block.text }}</pre>
                                        </template>
                                    </div>
                                </details>
                                <p v-if="helpSections.length" class="field-hint">
                                    Lifted from <code class="font-mono">docs/COMPARISON.md</code>
                                    when the page was built.
                                </p>
                                <p v-else class="field-hint">
                                    Load a sample to read what it shows.
                                </p>
                        </div>
                    </div>
                </section>
            </div>

            <section class="results-pane" aria-label="The answer">
                <!--
                  Announced politely: the answer arrives without anyone pressing anything, so a
                  screen reader is never told the count changed unless this region says so. Polite
                  and not assertive - it is typed over constantly, and assertive would interrupt the
                  visitor mid-word, every word.
                -->
                <div class="status" aria-live="polite">
                    <span v-if="engine === 'starting'" class="pill pill-busy">starting the engine...</span>
                    <span v-else-if="engine === 'failed'" class="pill pill-alert">engine failed to load</span>
                    <!--
                      `busy`, not `running`: during the 250 ms debounce nothing is with the worker yet,
                      and a pill that went back to "3 matches" in that window would be labelling the
                      previous case's answer with the current case's inputs.
                    -->
                    <span v-else-if="busy" class="pill pill-busy">matching...</span>
                    <!--
                      The count is the answer, so it is the biggest thing in the region and the
                      first. Everything qualifying it stays a pill beside it.
                    -->
                    <template v-else-if="answer">
                        <span class="answer-count">
                            {{ view.total }} match{{ view.total === 1 ? '' : 'es' }}
                        </span>
                        <span v-if="elapsedMs !== null" class="pill">in {{ Math.round(elapsedMs) }} ms</span>
                    </template>

                    <button v-if="busy" class="button button-primary" type="button" @click="stop">Stop</button>

                    <span v-if="answer && answer.truncated" class="pill pill-alert">
                        the engine hit its own cap, so this is part of the answer
                    </span>
                    <span v-if="partial" class="pill">
                        partial: the subject ran out before the pattern did
                    </span>
                    <span v-if="capped" class="pill">showing the first {{ view.shown }} of {{ view.total }}</span>
                    <span v-if="!shareable" class="pill">too long for the address bar, so no link</span>
                    <!-- What the copy button did. Here and not beside the button, because this is
                         the region a screen reader is already listening to. -->
                    <span v-if="copyNote" class="pill">{{ copyNote }}</span>
                </div>

                <!-- Everything the pattern's own text cannot be pointed at for. A failure WITH a
                     position is shown under the pattern field instead, not in both places. -->
                <p
                    v-if="engine === 'failed' || (failure && failureOffset === null)"
                    class="rounded-md border border-red-300 bg-red-50 p-4 text-sm leading-relaxed text-red-900"
                    role="status"
                >
                    {{ engine === 'failed' ? engineError : failure }}
                </p>

                <template v-if="answer && !failure">
                    <section>
                        <h2 class="section-label">Subject</h2>
                        <!--
                          Each highlight is a control, so it says so and answers a keyboard:
                          `role="button"`, Enter and Space, the arrows between them, and an
                          `aria-label` because the text inside it is the subject and not a name - a
                          zero-length match has no text at all. WCAG 2.1.1 Keyboard: the group table
                          below is only reachable by choosing a match, and before this it was only
                          reachable with a pointer.

                          `aria-busy` while the page is waiting: the text here is the subject the
                          answer belongs to, which for the debounce plus the round trip is not what
                          the box says. Dimmed for the same reason - the pane is out of date and
                          says so both ways.
                        -->
                        <p
                            ref="subjectPane"
                            class="subject-pane"
                            :class="{ 'opacity-60': busy }"
                            :aria-busy="busy"
                            @keydown="onSubjectKeydown"
                        ><template v-for="(part, i) in view.segments" :key="i"><mark
                                    v-if="part.match !== null"
                                    class="hit"
                                    :class="{
                                        'hit-alt': part.match % 2 === 1,
                                        'hit-empty': part.text === '',
                                        'hit-current': part.match === selected,
                                        'hit-linked': part.match === linked,
                                        'hit-partial': matches[part.match]?.partialMatch === true,
                                    }"
                                    role="button"
                                    :data-match="part.match"
                                    :tabindex="part.match === selected ? 0 : -1"
                                    :aria-current="part.match === selected"
                                    :aria-label="markLabel(part.match, part.text)"
                                    :title="'match ' + (part.match + 1)"
                                    @click="selectFrom('subject', part.match)"
                                    @mouseenter="linked = part.match"
                                    @mouseleave="linked = null"
                                    @keydown.enter="selectFrom('subject', part.match)"
                                    @keydown.space.prevent="selectFrom('subject', part.match)"
                                >{{ part.text }}</mark><template v-else>{{ part.text }}</template></template></p>
                        <p v-if="view.total === 0" class="note">No matches.</p>
                    </section>

                    <!--
                      `replaced !== null` and not a truth test: replacing every match with nothing
                      gives an empty string, which is a complete answer and the commonest way to see
                      what a pattern really covers. A pane that vanished for it would look like a
                      broken engine.
                    -->
                    <section v-if="replaced !== null">
                        <h2 class="section-label">Replaced</h2>
                        <p class="replaced-pane" :class="{ 'opacity-60': busy }" :aria-busy="busy">{{ replaced }}</p>
                        <p class="note">
                            Every match rewritten with the template. The highlights above show where
                            they were.
                        </p>
                    </section>

                    <template v-if="matches.length">
                        <section>
                            <h2 class="section-label">Matches</h2>
                            <!--
                              A scroll container with a tab stop, a role and a name. Six columns do
                              not fit 390 px (docs/demo/page-390.png), and a region that can only be
                              scrolled by dragging it hides its last column from anyone without a
                              pointer. WCAG 2.1.1 again, and the hint below is the visible half of
                              the same fix.
                            -->
                            <div
                                class="table-scroll"
                                tabindex="0"
                                role="region"
                                aria-label="Matches, scrollable sideways"
                                aria-describedby="matches-scroll-hint"
                            >
                                <table class="data-table">
                                    <thead>
                                        <tr>
                                            <th scope="col">#</th>
                                            <th scope="col">Index</th>
                                            <th scope="col">Length</th>
                                            <th scope="col">Substitutions</th>
                                            <th scope="col">Insertions</th>
                                            <th scope="col">Deletions</th>
                                        </tr>
                                    </thead>
                                    <tbody ref="matchRows" class="match-rows" @keydown="onRowsKeydown">
                                        <!--
                                          The row stays a row: a `role="button"` on a <tr> replaces
                                          the row semantics the rest of the answer is read by. The
                                          keyboard control is a real button in the first cell, which
                                          needs Enter and Space from nobody - and, as one of a set,
                                          the same roving tabindex and arrow keys as the highlights.
                                        -->
                                        <tr
                                            v-for="(match, i) in matches.slice(0, view.shown)"
                                            :key="i"
                                            class="cursor-pointer"
                                            :class="{ 'font-semibold': i === selected, 'row-linked': i === linked }"
                                            :data-match="i"
                                            @click="selectFrom('rows', i)"
                                            @mouseenter="linked = i"
                                            @mouseleave="linked = null"
                                        >
                                            <td>
                                                <!--
                                                  `aria-current`, the same word the highlight uses:
                                                  one selected match said two ways - pressed here,
                                                  current there - is two different claims about the
                                                  same state.
                                                -->
                                                <!-- No click handler of its own: the press bubbles
                                                     to the row, whose handler selects and reveals.
                                                     A second one here would run for the same
                                                     press and ask for the same scroll twice. -->
                                                <button
                                                    class="row-select"
                                                    type="button"
                                                    :data-match="i"
                                                    :tabindex="i === selected ? 0 : -1"
                                                    :aria-current="i === selected"
                                                    :aria-label="'match ' + (i + 1)"
                                                >
                                                    {{ i + 1 }}
                                                </button>
                                            </td>
                                            <td class="font-mono">{{ match.index }}</td>
                                            <td class="font-mono">{{ match.length }}</td>
                                            <!--
                                              One hue per kind of edit, and one letter per kind of
                                              edit. The letter is `aria-hidden` because the column
                                              header already names the kind: it is there for a
                                              reader who cannot separate the amber from the red,
                                              which is roughly one man in twelve.
                                            -->
                                            <td>
                                                <span
                                                    class="count"
                                                    :class="match.counts.substitutions ? 'count-sub' : 'count-zero'"
                                                >{{ match.counts.substitutions
                                                }}<span aria-hidden="true">s</span></span>
                                            </td>
                                            <td>
                                                <span
                                                    class="count"
                                                    :class="match.counts.insertions ? 'count-ins' : 'count-zero'"
                                                >{{ match.counts.insertions
                                                }}<span aria-hidden="true">i</span></span>
                                            </td>
                                            <td>
                                                <span
                                                    class="count"
                                                    :class="match.counts.deletions ? 'count-del' : 'count-zero'"
                                                >{{ match.counts.deletions
                                                }}<span aria-hidden="true">d</span></span>
                                            </td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                            <!--
                              Named by the region above through `aria-describedby`, so the hint is
                              read out when the region takes focus. Unattached it was a sentence
                              only a sighted visitor on a narrow window ever met, which is not who
                              needs to be told the region scrolls.
                            -->
                            <p id="matches-scroll-hint" class="note sm:hidden">
                                Scroll the table sideways for the rest of the columns.
                            </p>
                        </section>

                        <section v-if="current">
                            <h2 class="section-label">Groups in match {{ selected + 1 }}</h2>
                            <div
                                class="table-scroll"
                                tabindex="0"
                                role="region"
                                :aria-label="'Groups in match ' + (selected + 1) + ', scrollable sideways'"
                                aria-describedby="groups-scroll-hint"
                            >
                                <table class="data-table">
                                    <thead>
                                        <tr>
                                            <th scope="col">Group</th>
                                            <th scope="col">Index</th>
                                            <th scope="col">Length</th>
                                            <th scope="col">Text</th>
                                            <th scope="col">Captures</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr v-for="group in current.groups" :key="group.number">
                                            <td>
                                                {{ group.number
                                                }}<template v-if="group.name !== String(group.number)">
                                                    ({{ group.name }})</template
                                                >
                                            </td>
                                            <!--
                                              A group that did not take part shows as such and not as an
                                              empty string: those are two different answers, and showing
                                              them the same way is the confusion the demo exists to
                                              remove.
                                            -->
                                            <td v-if="!group.success" colspan="3" class="text-slate-500">
                                                <em>did not participate</em>
                                            </td>
                                            <template v-else>
                                                <td class="font-mono">{{ group.index }}</td>
                                                <td class="font-mono">{{ group.length }}</td>
                                                <td>
                                                    <!--
                                                      `answeredSubject` and never the live box: these
                                                      offsets are into the text the engine was given,
                                                      which stops being the text on screen the moment
                                                      somebody types.
                                                    -->
                                                    <code class="font-mono">{{
                                                        answeredSubject.slice(group.index, group.index + group.length)
                                                    }}</code>
                                                </td>
                                            </template>
                                            <!--
                                              Every capture there is, and "-" only when there are
                                              none - which happens exactly when the group did not
                                              participate. The old test was `> 1`, so the ordinary
                                              one-capture group, which is most of them, showed a
                                              dash: the same mark the row above uses for "captured
                                              nothing".
                                            -->
                                            <td>
                                                <template v-if="group.captures.length >= 1">
                                                    <code v-for="(capture, i) in group.captures" :key="i" class="font-mono"
                                                        >{{ i ? ', ' : ''
                                                        }}{{
                                                            answeredSubject.slice(
                                                                capture.index,
                                                                capture.index + capture.length,
                                                            )
                                                        }}</code
                                                    >
                                                </template>
                                                <span v-else class="text-slate-500">-</span>
                                            </td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                            <p id="groups-scroll-hint" class="note sm:hidden">
                                Scroll the table sideways for the rest of the columns.
                            </p>
                            <p class="note">
                                A repeated group keeps every capture it made. Python's standard
                                <code class="font-mono">re</code> keeps only the last.
                            </p>
                        </section>
                    </template>

                    <!--
                      The case as code, at the foot of the answer: the question a visitor asks after
                      "what does this pattern do" is "what do I write", and this is the demo's own
                      call with their inputs in it. Shut until asked for, because a block of code
                      above the answer is a screenful between them and what they came for.
                    -->
                    <section class="snippet-region" aria-label="The case as C#">
                        <button
                            ref="snippetToggle"
                            class="button snippet-toggle"
                            type="button"
                            :aria-expanded="snippetOpen"
                            aria-controls="snippet-panel"
                            @click="toggleSnippet"
                        >
                            C# for this case
                            <span aria-hidden="true" :class="snippetOpen ? 'chevron chevron-open' : 'chevron'">&#9662;</span>
                        </button>

                        <!--
                          Rendered whether it is open or not and `hidden` when it is shut, because
                          the button above names it and an id that resolves to nothing names nothing
                          - the same fault the tab set had. `hidden` also keeps the closed panel out
                          of the accessibility tree and out of the tab order.
                        -->
                        <div
                            id="snippet-panel"
                            class="snippet-panel"
                            :hidden="!snippetOpen"
                            @keydown.esc="closeSnippet"
                        >
                            <!--
                              A tab stop, a role and a name, as the tables and the caret line are
                              (S71): code does not wrap, so this is one more region only a pointer
                              could scroll otherwise. It is also where the focus lands on opening.
                            -->
                            <pre
                                ref="snippetCode"
                                class="snippet"
                                tabindex="0"
                                role="region"
                                aria-label="C# for this case, scrollable sideways"
                            ><span
                                v-for="(token, i) in snippetTokens"
                                :key="i"
                                :class="'tok tok-' + token.kind"
                            >{{ token.text }}</span></pre>
                            <div class="snippet-actions">
                                <button class="button snippet-copy" type="button" @click="copySnippet">
                                    Copy
                                </button>
                                <p class="note">
                                    Paste it into a console project after
                                    <code class="font-mono">dotnet add package FuzzyRegex</code>.
                                </p>
                            </div>
                        </div>
                    </section>
                </template>
            </section>
        </main>

        <footer class="shell-footer">
            <a href="https://github.com/zejji/fuzzy-regex-cs">Source and documentation</a>. The
            library is a port of
            <a href="https://github.com/mrabarnett/mrab-regex">mrab-regex</a>.
        </footer>
    </div>
</template>
