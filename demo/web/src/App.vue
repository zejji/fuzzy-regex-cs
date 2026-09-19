<script setup lang="ts">
// The page. Its state is demo.ts, which is testable without a DOM; what is here is layout only.
import { computed, nextTick, onMounted, onUnmounted, proxyRefs, ref, useTemplateRef, type ShallowRef } from 'vue';

import { spawnEngineWorker, useDemo } from './demo';
import { createPool } from './lib/pool';
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
const openTab = computed(() => TABS.find((item) => item.value === tab.value) ?? TABS[0]);

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
 * Moves the selection with an arrow key and takes the focus with it.
 *
 * @param back The key that moves towards the first match, which differs by the group's direction:
 *   the highlights run along the subject and the rows run down the table.
 * @param within The element holding the controls, so the focus stays inside the group that was
 *   being driven.
 * @param control A selector for those controls. The destination is found by its `data-match`
 *   attribute and never by position: under RightToLeft the highlights sit in subject order while
 *   the rows sit in the answer's order, so the two groups disagree about which element is third.
 */
function rove(
    event: KeyboardEvent,
    back: string,
    forward: string,
    within: Readonly<ShallowRef<HTMLElement | null>>,
    control: string,
): void {
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
    select(to);

    // On the next render, because the destination is a tab stop only after it: moving focus first
    // would leave the focused control holding `tabindex="-1"` for a frame, and Tab out of it then
    // resumes from the wrong place.
    void nextTick(() =>
        within.value?.querySelector<HTMLElement>(`${control}[data-match="${to}"]`)?.focus(),
    );
}

const onSubjectKeydown = (event: KeyboardEvent): void =>
    rove(event, 'ArrowLeft', 'ArrowRight', subjectPane, 'mark.hit');

const onRowsKeydown = (event: KeyboardEvent): void =>
    rove(event, 'ArrowUp', 'ArrowDown', matchRows, 'button.row-select');

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
                        @click="advanced = !advanced"
                    >
                        Flags, mode and lists
                        <span aria-hidden="true" :class="advanced ? 'chevron chevron-open' : 'chevron'">&#9662;</span>
                    </button>

                    <div v-if="wide || advanced" id="advanced-inputs" class="flex flex-col gap-4">
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
                        aria-controls="panel-examples"
                        @click="panelOpen = !panelOpen"
                    >
                        Examples and help
                        <span aria-hidden="true" :class="panelOpen ? 'chevron chevron-open' : 'chevron'">&#9662;</span>
                    </button>

                    <template v-if="wide || panelOpen">
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
                          One panel in the page at a time: the unselected tab's content is not
                          hidden markup a screen reader can wander into, it is not rendered.
                        -->
                        <div
                            :id="openTab.panel"
                            class="tab-panel"
                            role="tabpanel"
                            :aria-labelledby="openTab.id"
                        >
                            <template v-if="tab === 'examples'">
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
                            </template>

                            <!--
                              The documentation's own words, beside the sample that led here.

                              <details>, so it is operable from a keyboard by construction rather
                              than by a handler - and never a hover panel, which is a control a
                              keyboard cannot reach at all. Every run is interpolated and none of it
                              is `v-html`: help.json is generated from a markdown file, and a file
                              that could put markup into this page could put a script here.
                            -->
                            <template v-else>
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
                            </template>
                        </div>
                    </template>
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
                                    @click="select(part.match)"
                                    @mouseenter="linked = part.match"
                                    @mouseleave="linked = null"
                                    @keydown.enter="select(part.match)"
                                    @keydown.space.prevent="select(part.match)"
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
                                            @click="select(i)"
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
                                                <button
                                                    class="row-select"
                                                    type="button"
                                                    :data-match="i"
                                                    :tabindex="i === selected ? 0 : -1"
                                                    :aria-current="i === selected"
                                                    :aria-label="'match ' + (i + 1)"
                                                    @click="select(i)"
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
