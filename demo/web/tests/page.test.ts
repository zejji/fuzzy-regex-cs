// The page as a browser assembles it: App.vue mounted into jsdom, over the same fake worker the
// state machine's tests use.
//
// demo.test.ts covers what the page BELIEVES; this file covers what it PUTS ON SCREEN, which is
// where a keyboard user and a screen reader meet it. There is no @vue/test-utils in this project
// and this needs none: `createApp().mount()` is the same call main.ts makes, and the assertions
// below are ordinary DOM queries. What still needs a real browser - that the runtime boots, that
// focus is visible, that the layout holds at 390 px - is checks.html and the screenshots.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { createApp, nextTick, type App as VueApp } from 'vue';

// The stylesheet as text, because jsdom applies no stylesheet at all: a rule that only a real
// browser ever runs can still be asserted about as source, which is what DemoCapsTests.cs does to
// caps.ts for the same reason. Read from disk and not imported: Vitest disables CSS processing, so
// `import '../src/styles.css?raw'` resolves to the empty string (measured, 2026-09-19).
const styles = readFileSync(join(import.meta.dirname, '../src/styles.css'), 'utf8');

import App from '../src/App.vue';
import { CHECKBOXES, FLAG_HELP, FLAG_NAMES, RADIO_GROUPS } from '../src/lib/flags';
import { toCSharp } from '../src/lib/snippet';
import type { Group, Inputs, Match } from '../src/types';

import { FakeWorker } from './fake-worker';

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Waits for something the page reaches on its own, rather than for a fixed slice of time.
 *
 * A fixed sleep is a race with the machine: the page debounces for 250 ms and the fake worker
 * answers immediately, so a 400 ms wait had 150 ms of margin, and on a loaded machine the page's
 * own first answer landed AFTER a test had injected its answer and overwrote it - which reads as a
 * mismatched set of highlights rather than as a timing failure.
 */
async function until(reached: () => boolean, what: string, limitMs = 5000): Promise<void> {
    const deadline = Date.now() + limitMs;
    while (!reached()) {
        if (Date.now() > deadline) throw new Error(`the page never ${what} within ${limitMs} ms`);
        await sleep(5);
    }
    await nextTick();
}

/** Asserts a query found something, and narrows it, without a non-null assertion. */
function found<T>(value: T | null | undefined, what: string): T {
    if (value === null || value === undefined) throw new Error(`the page has no ${what}`);
    return value;
}

const match = (index: number, length: number, groups: readonly Group[] = []): Match => ({
    index,
    length,
    counts: { substitutions: 0, insertions: 0, deletions: 0 },
    groups,
});

let app: VueApp<Element> | null = null;
let host: HTMLDivElement | null = null;

/** One call the page made to `scrollIntoView`: what it scrolled to, and how it asked. */
interface Reveal {
    readonly target: Element;
    readonly options: ScrollIntoViewOptions | boolean | undefined;
}

/**
 * Every reveal the page asked for, in order.
 *
 * jsdom implements no scrolling at all: `Element.prototype.scrollIntoView` is undefined in jsdom
 * 30.1.0 and calling it throws `TypeError: e.scrollIntoView is not a function` (measured
 * 2026-09-19). So the method is installed here rather than guarded in the page - every browser has
 * had it for a decade, and a `?.()` in `App.vue` would be the page apologising for the test
 * environment. It is installed for every test in this file because any click on a row or a
 * highlight reaches it.
 */
let reveals: Reveal[] = [];

beforeEach(() => {
    location.hash = '';
    reveals = [];
    Object.defineProperty(Element.prototype, 'scrollIntoView', {
        configurable: true,
        writable: true,
        value: function (this: Element, options?: ScrollIntoViewOptions | boolean): void {
            reveals.push({ target: this, options });
        },
    });
    vi.stubGlobal('Worker', FakeWorker);
    // Routed by URL: the mount path fetches examples.json AND help.json, and one body for both hands
    // the empty tour to the help guard, which rejects it and writes a console error no test is about.
    vi.stubGlobal(
        'fetch',
        vi.fn((input: unknown) =>
            Promise.resolve(
                new Response(String(input).includes('help.json') ? EMPTY_HELP : '[]', { status: 200 }),
            ),
        ),
    );
});

/** The smallest `help.json` that satisfies `isHelp`, for a page no help test is driving. */
const EMPTY_HELP = JSON.stringify({
    source: 'docs/COMPARISON.md',
    note: 'generated',
    entries: {
        fuzzy: [
            {
                heading: [{ code: false, text: 'Fuzzy matching' }],
                // One block, because `isHelp` refuses a section with none: an empty body is a
                // panel that opens onto nothing.
                blocks: [{ kind: 'paragraph', runs: [{ code: false, text: 'Up to n errors.' }] }],
            },
        ],
    },
});

afterEach(() => {
    app?.unmount();
    host?.remove();
    app = null;
    host = null;
    Reflect.deleteProperty(Element.prototype, 'scrollIntoView');
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
});

/** Mounts the page, waits for its first answer, and hands back the page and the state behind it. */
async function mountPage() {
    host = document.createElement('div');
    document.body.append(host);
    app = createApp(App);
    app.mount(host);
    await until(
        () => window.__demo !== undefined && window.__demo.answer !== null && !window.__demo.busy,
        'answered its first question',
    );

    return { page: host, demo: found(window.__demo, 'state on window.__demo') };
}

const keydown = (key: string) => new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });

/** Lets a render, and the focus move queued behind it, both happen. */
const settle = async (): Promise<void> => {
    await nextTick();
    await nextTick();
};

test('the match highlights are one tab stop, and the arrows move between them', async () => {
    const { page, demo } = await mountPage();
    demo.answer = { matches: [match(0, 3), match(4, 3), match(8, 0)], truncated: false };
    demo.answeredSubject = 'abc abc ';
    await nextTick();

    const marks = () => [...page.querySelectorAll<HTMLElement>('mark.hit')];
    expect(marks()).toHaveLength(3);

    // A roving tabindex: one stop for the whole set, on the selected match. A tab stop per
    // highlight puts 200 of them between the subject and the rest of the page for a 200-match
    // answer, and another 200 in the table below it, which is a keyboard trap made of controls.
    const stops = () => marks().filter((mark) => mark.getAttribute('tabindex') === '0');
    expect(stops()).toHaveLength(1);
    expect(stops()[0]).toBe(marks()[0]);
    expect(marks()[0]?.getAttribute('role')).toBe('button');

    // The matched text is in the label, because `aria-label` REPLACES the text inside the mark:
    // "match 2" alone reads out everything except the one thing a highlight is for.
    expect(marks()[0]?.getAttribute('aria-label')).toBe('match 1, abc');
    expect(marks()[2]?.getAttribute('aria-label')).toBe('match 3, empty');

    // One way of saying "this is the selected one", here and in the table: aria-current on both.
    expect(marks()[0]?.getAttribute('aria-current')).toBe('true');
    expect(marks()[1]?.getAttribute('aria-current')).toBe('false');
    expect(page.querySelector('[aria-pressed]')).toBeNull();

    found(marks()[0], 'first match highlight').focus();
    found(marks()[0], 'first match highlight').dispatchEvent(keydown('ArrowRight'));
    await settle();
    expect(demo.selected).toBe(1);
    expect(document.activeElement).toBe(marks()[1]); // the focus goes with the selection
    expect(marks()[1]?.getAttribute('tabindex')).toBe('0');
    expect(marks()[0]?.getAttribute('tabindex')).toBe('-1');

    found(marks()[1], 'second match highlight').dispatchEvent(keydown('End'));
    await settle();
    expect(demo.selected).toBe(2);
    expect(document.activeElement).toBe(marks()[2]);

    found(marks()[2], 'third match highlight').dispatchEvent(keydown('Home'));
    await settle();
    expect(demo.selected).toBe(0);

    found(marks()[0], 'first match highlight').dispatchEvent(keydown('ArrowLeft'));
    await settle();
    expect(demo.selected).toBe(0); // the ends hold rather than wrapping
});

test('a right-to-left answer paints every match, and the arrows still follow the numbering', async () => {
    // RightToLeft numbers the last match in the subject first, so the answer's order is the reverse
    // of the subject's - the shape of upstream's answer for \w+ with REVERSE (DemoExamplesTests,
    // from regex 2026.9.10). FOUR words rather than the sample's three: with three, the middle mark
    // sits at the same position as its number and a focus move by position passes by coincidence.
    const { page, demo } = await mountPage();
    demo.answer = {
        matches: [match(14, 4), match(8, 5), match(4, 3), match(0, 3)],
        truncated: false,
    };
    demo.answeredSubject = 'one two three four';
    await nextTick();

    const marks = () => [...page.querySelectorAll<HTMLElement>('mark.hit')];
    expect(marks().map((mark) => mark.textContent)).toEqual(['one', 'two', 'three', 'four']);

    // The subject reads left to right and the answer is numbered right to left, so the last
    // highlight in the pane is match 1. Both have to be true at once, or the table and the
    // highlights are describing different answers.
    expect(marks().map((mark) => mark.getAttribute('aria-label'))).toEqual([
        'match 4, one',
        'match 3, two',
        'match 2, three',
        'match 1, four',
    ]);

    // Selection follows the NUMBER, which is why the focus is found by data-match and not by
    // position: ArrowRight from match 1 ("four", last in the pane) lands on match 2 ("three"),
    // where by position it would land on "two".
    found(marks()[3], 'highlight of match 1').focus();
    found(marks()[3], 'highlight of match 1').dispatchEvent(keydown('ArrowRight'));
    await settle();
    expect(demo.selected).toBe(1);
    expect(document.activeElement).toBe(marks()[2]);
    expect(marks()[2]?.getAttribute('tabindex')).toBe('0');
});

test('an empty match sharing a start with a longer one still gets its own tab stop', async () => {
    // A reverse search answers with both: upstream, regex 2026.9.10, 2026-09-19,
    //   regex.finditer(r'a*', 'baa', flags=regex.REVERSE|regex.VERSION1) -> [(1, 3), (1, 1), (0, 0)]
    // and this port answers the same spans. Dropping the empty one leaves the table listing a match
    // the pane cannot show, and arrowing onto it then strands the focus on a control that is no
    // longer a tab stop - which is what happened before the highlight sorted by length as well as
    // by index.
    const { page, demo } = await mountPage();
    demo.answer = { matches: [match(1, 2), match(1, 0), match(0, 0)], truncated: false };
    demo.answeredSubject = 'baa';
    await nextTick();

    const marks = () => [...page.querySelectorAll<HTMLElement>('mark.hit')];
    expect(marks().map((mark) => mark.dataset['match'])).toEqual(['2', '1', '0']);

    found(marks()[2], 'highlight of match 1').focus();
    found(marks()[2], 'highlight of match 1').dispatchEvent(keydown('ArrowRight'));
    await settle();
    expect(demo.selected).toBe(1);
    expect(document.activeElement).toBe(marks()[1]);
    expect(marks()[1]?.getAttribute('tabindex')).toBe('0');
});

test('a match highlight is operable from the keyboard, not only the mouse', async () => {
    const { page, demo } = await mountPage();
    demo.answer = { matches: [match(0, 3), match(4, 3)], truncated: false };
    demo.answeredSubject = 'abc abc';
    await nextTick();

    // A click handler on a <mark> is a control only a pointer can reach: no role to announce,
    // nothing to press. WCAG 2.1.1 Keyboard, and the whole group table below is only reachable
    // through it.
    const marks = [...page.querySelectorAll<HTMLElement>('mark.hit')];
    expect(marks).toHaveLength(2);

    const enter = keydown('Enter');
    found(marks[1], 'second match highlight').dispatchEvent(enter);
    await nextTick();
    expect(demo.selected).toBe(1);

    const space = keydown(' ');
    found(marks[0], 'first match highlight').dispatchEvent(space);
    await nextTick();
    expect(demo.selected).toBe(0);
    expect(space.defaultPrevented).toBe(true); // or pressing it scrolls the page as well

    const tab = keydown('Tab');
    found(marks[1], 'second match highlight').dispatchEvent(tab);
    await nextTick();
    expect(demo.selected).toBe(0); // every other key is left alone
    expect(tab.defaultPrevented).toBe(false);
});

test('a row of the matches table is operable from the keyboard, not only the mouse', async () => {
    const { page, demo } = await mountPage();
    demo.answer = { matches: [match(0, 3), match(4, 3)], truncated: false };
    demo.answeredSubject = 'abc abc';
    await nextTick();

    // A real button in the row rather than a role on the <tr>: a row that claims to be a button
    // stops being a row, and the table's own semantics are what the rest of the answer is read by.
    const buttons = () => [...page.querySelectorAll<HTMLElement>('button.row-select')];
    expect(buttons()).toHaveLength(2);

    // The same roving tabindex the highlights use, for the same reason.
    expect(buttons().filter((button) => button.getAttribute('tabindex') === '0')).toHaveLength(1);
    expect(buttons()[0]?.getAttribute('aria-current')).toBe('true');

    found(buttons()[1], 'row control for the second match').dispatchEvent(
        new MouseEvent('click', { bubbles: true }),
    );
    await settle();
    expect(demo.selected).toBe(1);
    expect(buttons()[1]?.getAttribute('aria-current')).toBe('true');
    expect(buttons()[0]?.getAttribute('aria-current')).toBe('false');
    expect(buttons()[1]?.getAttribute('tabindex')).toBe('0');
    expect(buttons()[0]?.getAttribute('tabindex')).toBe('-1');

    found(buttons()[1], 'row control for the second match').dispatchEvent(keydown('ArrowUp'));
    await settle();
    expect(demo.selected).toBe(0);
    expect(document.activeElement).toBe(buttons()[0]);

    found(buttons()[0], 'row control for the first match').dispatchEvent(keydown('ArrowDown'));
    await settle();
    expect(demo.selected).toBe(1);

    const first = found(buttons()[0], 'row control for the first match');
    first.dispatchEvent(keydown('Enter')); // a button's own keyboard contract, no handler needed
    first.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    await nextTick();
    expect(demo.selected).toBe(0);
});

test('a table a narrow window clips can be scrolled by keyboard, is announced, and says so', async () => {
    const { page, demo } = await mountPage();
    demo.answer = {
        matches: [match(0, 3, [{ number: 1, name: '1', success: true, index: 0, length: 3, captures: [{ index: 0, length: 3 }] }])],
        truncated: false,
    };
    demo.answeredSubject = 'abc abc';
    await nextTick();

    // At 390 px the six-column matches table runs off the card and the last column is simply gone
    // (docs/demo/page-390.png). The scroll container is the only way to it, and a container with
    // no tab stop cannot be scrolled without a pointer.
    const panes = [...page.querySelectorAll('div.table-scroll')];
    expect(panes).toHaveLength(2);
    for (const pane of panes) {
        expect(pane.getAttribute('tabindex')).toBe('0');
        expect(pane.getAttribute('role')).toBe('region');
        expect(pane.getAttribute('aria-label')).toMatch(/\S/);

        // The visible half of the fix is the hint under the table, which said nothing to anyone
        // who could not see it. Tied to the region, it is read out when the region takes focus.
        const described = found(pane.getAttribute('aria-describedby'), 'aria-describedby on a scroll region');
        const hint = found(page.querySelector('#' + described), 'hint element #' + described);
        expect(hint.textContent).toMatch(/scroll/i);
    }
});

test('the subject pane says it is busy while it still shows the previous answer', async () => {
    const { page, demo } = await mountPage();
    demo.answer = { matches: [match(0, 3)], truncated: false };
    demo.answeredSubject = 'abc';
    await nextTick();

    const pane = () => found(page.querySelector('p.subject-pane'), 'subject pane');
    expect(pane().getAttribute('aria-busy')).toBe('false');

    // A keystroke: for the debounce plus the round trip the pane draws text the box no longer
    // says, which is right - the offsets belong to the answered subject - but it has to admit it.
    demo.subject = 'ZZ';
    await settle();
    expect(pane().getAttribute('aria-busy')).toBe('true');
    expect(pane().className).toMatch(/opacity-/);

    await until(() => !demo.busy, 'stopped being busy');
    expect(pane().getAttribute('aria-busy')).toBe('false');
    expect(pane().className).not.toMatch(/opacity-/);
});

test('a group with exactly one capture lists it, rather than showing a dash', async () => {
    const { page, demo } = await mountPage();
    const one: Group = { number: 1, name: '1', success: true, index: 0, length: 3, captures: [{ index: 0, length: 3 }] };
    const none: Group = { number: 2, name: 'two', success: false, index: -1, length: 0, captures: [] };
    demo.answer = { matches: [match(0, 3, [one, none])], truncated: false };
    demo.answeredSubject = 'abc abc';
    await nextTick();

    const rows = [...page.querySelectorAll('table')]
        .slice(1)
        .flatMap((table) => [...table.querySelectorAll('tbody tr')]);
    expect(rows).toHaveLength(2);

    // One capture is the ordinary case - every group that took part has at least one - and "-"
    // against it reads as "this group captured nothing", which is the answer the row above it
    // already gives for a group that really did not participate.
    const listed = found(rows[0]?.querySelector('td:last-child'), 'captures cell of the first group');
    expect(listed.textContent?.trim()).toBe('abc');

    const dash = found(rows[1]?.querySelector('td:last-child'), 'captures cell of the second group');
    expect(dash.textContent?.trim()).toBe('-');
});

test('the three v2 inputs are labelled controls, and the template appears with the mode that needs it', async () => {
    const { page, demo } = await mountPage();

    const labelled = (id: string): HTMLElement => {
        const field = found(page.querySelector<HTMLElement>('#' + id), 'field #' + id);
        expect(found(page.querySelector(`label[for="${id}"]`), 'label for #' + id).textContent).toMatch(/\S/);
        return field;
    };

    // Three radios rather than a select: three choices, all three on screen, and the one in force
    // readable without opening anything. Each carries its own label, which is what a radio is named by.
    const modes = () => [...page.querySelectorAll<HTMLInputElement>('input[name="mode"]')];
    expect(modes().map((input) => input.value)).toEqual(['', 'partial', 'replace']);
    for (const input of modes()) {
        expect(found(page.querySelector(`label[for="${input.id}"]`), 'label for #' + input.id).textContent).toMatch(
            /\S/,
        );
    }

    labelled('named-lists');

    // The template box belongs to replace mode and appears with it. Shown in every mode it is a
    // field that does nothing, and one somebody can fill in and then wonder why it was ignored.
    expect(page.querySelector('#replacement')).toBeNull();
    demo.mode = 'replace';
    await nextTick();
    labelled('replacement');

    // And the radios drive the state the engine is asked with, not just their own appearance.
    const partial = found(modes()[1], 'the partial-mode radio');
    partial.checked = true;
    partial.dispatchEvent(new Event('change', { bubbles: true }));
    await nextTick();
    expect(demo.mode).toBe('partial');
    expect(page.querySelector('#replacement')).toBeNull();
});

test('replace mode shows the rewritten subject, and an empty result is still a result', async () => {
    const { page, demo } = await mountPage();
    demo.mode = 'replace';
    demo.answeredSubject = 'abc';
    demo.answer = { matches: [match(0, 1)], truncated: false, replaced: 'Xbc' };
    await nextTick();

    expect(found(page.querySelector('.replaced-pane'), 'replaced pane').textContent).toBe('Xbc');

    // Replacing everything with nothing is a real answer and the commonest way to test a pattern -
    // `''` is not "no answer", and a pane that vanished for it would look like a broken engine.
    demo.answer = { matches: [match(0, 3)], truncated: false, replaced: '' };
    await nextTick();
    expect(found(page.querySelector('.replaced-pane'), 'replaced pane').textContent).toBe('');

    demo.answer = { matches: [match(0, 1)], truncated: false };
    await nextTick();
    expect(page.querySelector('.replaced-pane')).toBeNull();
});

test('a partial match says so, rather than looking like an ordinary one', async () => {
    const { page, demo } = await mountPage();
    demo.mode = 'partial';
    demo.answeredSubject = 'abc';
    demo.answer = { matches: [{ ...match(0, 3), partialMatch: true }], truncated: false };
    await nextTick();

    // The whole point of the mode: "the subject ran out before the pattern did" is a different
    // answer from "this matched", and the two rendered identically would demonstrate nothing.
    const mark = found(page.querySelector('mark.hit'), 'match highlight');
    expect(mark.className).toMatch(/hit-partial/);
    expect(mark.getAttribute('aria-label')).toBe('match 1, abc, partial');
    expect(found(page.querySelector('.status'), 'status row').textContent).toMatch(/ran out/i);

    demo.answer = { matches: [match(0, 3)], truncated: false };
    await nextTick();
    expect(found(page.querySelector('mark.hit'), 'match highlight').className).not.toMatch(/hit-partial/);
});

test('a fuzzy match shows where each error was spent, inside the highlight', async () => {
    const { page, demo } = await mountPage();
    demo.answeredSubject = 'xfoobat';
    // Upstream's answer for (?:foobar){i<=1,d<=1,s<=1} against "xfoobat"
    // (tools/probes/demo-json-contract-expectations.py, run 2026-09-20 against regex 2026.9.10):
    // span (0,6), one error of each kind, at subject positions 0, 1 and 6.
    demo.answer = {
        matches: [
            {
                ...match(0, 6),
                counts: { substitutions: 1, insertions: 1, deletions: 1 },
                edits: { substitutions: [0], insertions: [1], deletions: [6] },
            },
        ],
        truncated: false,
    };
    await nextTick();

    const mark = found(page.querySelector('mark.hit'), 'match highlight');
    // The subject is unchanged by the marking: a deletion has no character of its own, so the text
    // inside the highlight is still exactly the six characters the engine matched.
    expect(mark.textContent).toBe('xfooba');
    expect([...mark.querySelectorAll('span.edit')].map((edit) => `${edit.className} ${edit.textContent}`)).toEqual([
        'edit edit-sub x',
        'edit edit-ins f',
        'edit edit-del ',
    ]);

    // Colour is never the only signal: each mark names its kind in a title, the stylesheet puts
    // the letter under it, and the highlight's own label carries all three for a screen reader,
    // which never sees any of the marks (the <mark> has an aria-label of its own).
    expect([...mark.querySelectorAll('span.edit')].map((edit) => edit.getAttribute('title'))).toEqual([
        'substitution',
        'insertion',
        'deletion',
    ]);
    expect(mark.getAttribute('aria-label')).toBe('match 1, xfooba, 1 substitution, 1 insertion, 1 deletion');

    // An exact match keeps the plain highlight, and its label keeps the plain wording.
    demo.answer = { matches: [match(0, 6)], truncated: false };
    await nextTick();
    const exact = found(page.querySelector('mark.hit'), 'match highlight');
    expect(exact.querySelectorAll('span.edit')).toHaveLength(0);
    expect(exact.getAttribute('aria-label')).toBe('match 1, xfooba');
});

test('the help panel is the documentation, rendered as text and opened from the keyboard', async () => {
    const { page, demo } = await mountPage();
    demo.help = {
        source: 'docs/COMPARISON.md',
        note: 'generated',
        entries: {
            fuzzy: [
                {
                    heading: [
                        { code: true, text: '{e<=n}' },
                        { code: false, text: ': up to n errors' },
                    ],
                    blocks: [
                        {
                            kind: 'paragraph',
                            runs: [
                                { code: false, text: 'Allow ' },
                                { code: true, text: '<b>n</b>' },
                            ],
                        },
                        { kind: 'code', language: 'csharp', text: 'new FuzzyRegex("x");' },
                    ],
                },
            ],
        },
    };
    demo.helpKey = 'fuzzy';
    await nextTick();

    // Since S73 the prose lives behind the Help tab, so the tab is where a visitor asks for it.
    // Everything below is what it was: the tab decides whether the panel is on screen, not what
    // the panel is.
    const help = found(page.querySelector<HTMLElement>('#tab-help'), 'the help tab');
    help.click();
    await nextTick();

    // A <details>, so it is operable from the keyboard by construction rather than by a handler -
    // and never a hover-only panel, which is a control a keyboard cannot reach at all.
    const panel = found(page.querySelector<HTMLElement>('details.help-panel'), 'help panel');
    const summary = found(panel.querySelector('summary'), 'the panel summary');
    expect(summary.textContent).toContain('{e<=n}');

    const code = [...panel.querySelectorAll('code')].map((element) => element.textContent);
    expect(code).toContain('<b>n</b>');
    // Interpolated, never `v-html`: help.json is generated from a markdown file, and a file that
    // could put markup into the page is a file that could put a script there.
    expect(panel.innerHTML).toContain('&lt;b&gt;n&lt;/b&gt;');
    const fenced = found(panel.querySelector('pre'), 'a fenced code block');
    expect(fenced.textContent).toBe('new FuzzyRegex("x");');

    // A fenced sample is a line of code that does not wrap, so on a narrow window it is one more
    // region only a pointer could scroll. Same treatment as the tables and the caret line.
    expect(fenced.getAttribute('tabindex')).toBe('0');
    expect(fenced.getAttribute('role')).toBe('region');
    expect(fenced.getAttribute('aria-label')).toMatch(/scroll/i);

    // A feature with no documented section shows no panel, rather than an empty disclosure.
    demo.helpKey = 'undocumented';
    await nextTick();
    expect(page.querySelector('details.help-panel')).toBeNull();
});

test('hovering a match links the highlight to its row, and the row back to the highlight', async () => {
    const { page, demo } = await mountPage();
    demo.answeredSubject = 'abc abc';
    demo.answer = { matches: [match(0, 3), match(4, 3)], truncated: false };
    await nextTick();

    const marks = () => [...page.querySelectorAll<HTMLElement>('mark.hit')];
    const rows = () => [...page.querySelectorAll<HTMLElement>('tbody.match-rows tr')];
    const hover = (element: HTMLElement, kind: 'mouseenter' | 'mouseleave') =>
        element.dispatchEvent(new MouseEvent(kind));

    // Two matches and six numbers in a table: which row belongs to which highlight is the question
    // the table raises, and pointing at either is the cheapest possible answer to it.
    hover(found(marks()[1], 'second highlight'), 'mouseenter');
    await nextTick();
    expect(marks()[1]?.className).toMatch(/hit-linked/);
    expect(rows()[1]?.className).toMatch(/row-linked/);
    expect(rows()[0]?.className).not.toMatch(/row-linked/);

    hover(found(marks()[1], 'second highlight'), 'mouseleave');
    await nextTick();
    expect(rows()[1]?.className).not.toMatch(/row-linked/);

    hover(found(rows()[0], 'first row'), 'mouseenter');
    await nextTick();
    expect(marks()[0]?.className).toMatch(/hit-linked/);

    // Hovering selects nothing: the selection drives the group table below, and a pointer crossing
    // the pane on its way somewhere else must not rewrite what is being examined.
    expect(demo.selected).toBe(0);
    hover(found(rows()[1], 'second row'), 'mouseenter');
    await nextTick();
    expect(demo.selected).toBe(0);
});

/** The two ways the page offers each match: the highlight in the subject, the row in the table. */
function bothViews(page: HTMLElement) {
    return {
        marks: () => [...page.querySelectorAll<HTMLElement>('mark.hit')],
        rows: () => [...page.querySelectorAll<HTMLElement>('tbody.match-rows tr')],
        buttons: () => [...page.querySelectorAll<HTMLElement>('button.row-select')],
    };
}

/** An answer of three matches over a subject long enough for them to be apart. */
async function threeMatches() {
    const mounted = await mountPage();
    mounted.demo.answeredSubject = 'abc abc abc';
    mounted.demo.answer = { matches: [match(0, 3), match(4, 3), match(8, 3)], truncated: false };
    await nextTick();
    return mounted;
}

test('choosing a match in one place brings it into view in the other', async () => {
    const { page, demo } = await threeMatches();
    const { marks, rows, buttons } = bothViews(page);

    // The row is the thing being pointed at, so what has to move is the SUBJECT: a number in a
    // table says nothing about where in the text it is, and the answer to "which one is that" is
    // the highlight, which on a long subject is off the region's screen.
    found(buttons()[2], 'row control for the third match').click();
    await settle();
    expect(demo.selected).toBe(2);

    // Exactly one reveal, which is why this asserts the whole list and not that it contains the
    // highlight: the press on the number bubbles to the row, so a second handler on the button
    // would ask for the same scroll twice and the list would hold it twice.
    expect(reveals.map((reveal) => reveal.target)).toEqual([marks()[2]]);

    // `block: 'nearest'` in both directions: a match already on screen must not jump, because the
    // commonest click of all is on something the visitor can already see.
    expect(reveals[0]?.options).toEqual({ block: 'nearest', inline: 'nearest', behavior: 'smooth' });

    // And the other way round, where the row is what a visitor cannot see.
    reveals = [];
    found(marks()[0], 'first highlight').click();
    await settle();
    expect(demo.selected).toBe(0);
    expect(reveals.map((reveal) => reveal.target)).toEqual([rows()[0]]);

    // The whole row and not the little number inside it: the row is what carries the answer, and
    // `nearest` on a 24 px control is satisfied by a sliver of the row at the edge of the pane.
    expect(reveals[0]?.target.tagName).toBe('TR');
});

test('the keyboard reaches both views of a match, and an arrow keeps the focus in sight', async () => {
    const { page, demo } = await threeMatches();
    const { marks, rows, buttons } = bothViews(page);

    // Enter on a highlight does what a click does. A highlight is `role="button"` on a <mark>, so
    // the browser gives it nothing: without this the keyboard reached the selection and never the
    // row it belongs to.
    found(marks()[1], 'second highlight').focus();
    found(marks()[1], 'second highlight').dispatchEvent(keydown('Enter'));
    await settle();
    expect(demo.selected).toBe(1);
    expect(reveals.map((reveal) => reveal.target)).toEqual([rows()[1]]);

    // An arrow moves the selection and the focus, and asks for no reveal at all. Both halves are
    // in one scroll container, so a counterpart a screen away can only be shown by scrolling the
    // focused control off screen - and a real browser will not have it either way: measured in
    // Chrome on 2026-09-19 with 80 matches at 1366x768, the reveal scrolled the pane to the
    // counterpart and the focus call put it straight back. That is WCAG 2.4.3 Focus Order,
    // and Enter above is how the keyboard asks for the other half.
    reveals = [];
    found(marks()[1], 'second highlight').dispatchEvent(keydown('ArrowRight'));
    await settle();
    expect(demo.selected).toBe(2);
    expect(document.activeElement).toBe(marks()[2]);
    expect(reveals).toEqual([]);

    reveals = [];
    found(buttons()[2], 'row control for the third match').dispatchEvent(keydown('ArrowUp'));
    await settle();
    expect(demo.selected).toBe(1);
    expect(document.activeElement).toBe(buttons()[1]);
    expect(reveals).toEqual([]);
});

test('a visitor who asked for less motion is moved there instantly, not animated', async () => {
    // WCAG 2.3.3 Animation from Interactions: a scroll the visitor did not ask to watch is motion
    // triggered by interaction, and `prefers-reduced-motion` is how a browser passes that setting
    // on. `matchMedia` is what the page asks, so answering it is the whole simulation - the shell's
    // own query is answered false here, which is a narrow window and changes nothing about linking.
    vi.stubGlobal('matchMedia', (query: string) => ({
        matches: query.includes('prefers-reduced-motion'),
        addEventListener() {},
        removeEventListener() {},
    }));

    const { page } = await threeMatches();
    const { buttons } = bothViews(page);

    found(buttons()[2], 'row control for the third match').click();
    await settle();
    expect(reveals[0]?.options).toEqual({ block: 'nearest', inline: 'nearest', behavior: 'auto' });
});

test('the repository is one click from the header, and the footer still names it too', async () => {
    const { page } = await mountPage();

    // The footer link was the only one, at the bottom of a page the fixed shell now stops anyone
    // scrolling to at all - so on the live page it was unreachable, not merely far away.
    const header = found(page.querySelector<HTMLElement>('header.shell-header'), 'the header');
    const link = found(header.querySelector<HTMLAnchorElement>('a'), 'a link in the header');
    expect(link.getAttribute('href')).toBe('https://github.com/zejji/fuzzy-regex-cs');

    // Named by the repository it goes to, in the text or in the label: "GitHub" alone is a
    // destination a visitor cannot tell from any other GitHub link.
    const name = (link.getAttribute('aria-label') ?? '') + (link.textContent ?? '');
    expect(name).toContain('zejji/fuzzy-regex-cs');

    const footer = found(page.querySelector<HTMLElement>('footer.shell-footer'), 'the footer');
    expect(footer.querySelector('a[href="https://github.com/zejji/fuzzy-regex-cs"]')).not.toBeNull();
});

test('the focus ring covers every kind of control the page has, the help disclosure included', () => {
    // WCAG 2.4.13 is what the rule is for, and a <summary> it forgets is a disclosure a keyboard
    // user cannot see the focus on - the help panels are <details>, so that is every one of them.
    const rule = found(/:where\(([^)]+)\):focus-visible/.exec(styles), 'focus-visible rule in styles.css');
    const targets = found(rule[1], 'the selector list of the focus-visible rule')
        .split(',')
        .map((selector) => selector.trim());

    expect(targets).toContain('summary');
    // Every other kind the page puts on screen, so the list cannot be trimmed back either.
    expect(targets).toEqual(expect.arrayContaining(['a', 'button', 'input', 'textarea', '[tabindex]']));
});

test('a parse error is shown under the pattern, with a caret under the character it names', async () => {
    const { page, demo } = await mountPage();
    // The pattern that was ANSWERED, not the box: the offset indexes the string the engine was
    // given, and the box has moved on by the time the answer lands.
    demo.answeredPattern = '(?:colour){e<=x}';
    demo.pattern = '(?:colour){e<=x} and more typing';
    demo.failure = 'bad fuzzy constraint at position 13';
    demo.failureOffset = 13;
    await nextTick();

    const inline = found(page.querySelector<HTMLElement>('.parse-error'), 'the inline parse error');
    expect(inline.textContent).toContain('bad fuzzy constraint');

    // The caret is a monospace copy of the pattern with a hat under the character the engine named.
    const caret = found(inline.querySelector('pre'), 'the caret line');
    expect(caret.textContent).toBe('(?:colour){e<=x}\n' + ' '.repeat(13) + '^');

    // It scrolls sideways rather than wrapping - a wrapped pattern puts the hat under a character
    // on a different line - so it is a scroll region with a tab stop, a role and a name, as the
    // tables are (S71). NOT `aria-hidden`: an element that can take focus and is hidden from
    // assistive technology is a stop a screen reader lands on and is told nothing about.
    expect(caret.getAttribute('aria-hidden')).toBeNull();
    expect(caret.getAttribute('tabindex')).toBe('0');
    expect(caret.getAttribute('role')).toBe('region');
    expect(caret.getAttribute('aria-label')).toMatch(/\S/);
    const caretHint = found(
        page.querySelector('#' + found(caret.getAttribute('aria-describedby'), 'aria-describedby on the caret line')),
        'the sentence describing the caret line',
    );
    expect(caretHint.textContent).toMatch(/scroll/i);

    // Only the row of spaces and the hat is hidden: read out it is nothing at all, and the
    // position is in the sentence underneath in words.
    const hidden = found(caret.querySelector('[aria-hidden="true"]'), 'the hidden caret row');
    expect(hidden.textContent).toBe('\n' + ' '.repeat(13) + '^');
    expect(inline.textContent).toMatch(/character 14/); // the offset is 0-based; people count from 1

    // Said once. The message is under the field it is about, so the block below the inputs - which
    // is where every other failure appears - must not repeat it.
    expect(page.textContent?.match(/bad fuzzy constraint/g)).toHaveLength(1);

    // An error no character of the pattern is to blame for has no caret to draw, and goes back to
    // the block: a caret under character 1 of a pattern that parsed is a precise-looking lie.
    demo.failureOffset = null;
    await nextTick();
    expect(page.querySelector('.parse-error')).toBeNull();
    expect(page.textContent).toContain('bad fuzzy constraint');
});

test('the page draws the subject that was answered, not the one being typed', async () => {
    const { page, demo } = await mountPage();
    const group: Group = { number: 1, name: '1', success: true, index: 0, length: 6, captures: [{ index: 0, length: 6 }] };
    demo.answer = { matches: [match(0, 6, [group])], truncated: false };
    demo.answeredSubject = 'abcdef';
    await nextTick();

    demo.subject = 'ZZ'; // a keystroke: for the next 250 ms the answer belongs to the old text
    await nextTick();

    expect(found(page.querySelector('.subject-pane'), 'subject pane').textContent).toBe('abcdef');
    const cells = [...page.querySelectorAll('table')]
        .slice(1)
        .flatMap((table) => [...table.querySelectorAll('tbody tr td')]);
    expect(cells.map((cell) => cell.textContent?.trim())).toContain('abcdef'); // the group's Text
});

/** The snippet panel's three parts: the button that reveals it, the panel, and the code inside. */
function snippetParts(page: HTMLElement) {
    const toggle = found(page.querySelector<HTMLButtonElement>('button.snippet-toggle'), 'the C# button');
    const id = found(toggle.getAttribute('aria-controls'), 'aria-controls on the C# button');
    const panel = found(page.querySelector<HTMLElement>('#' + id), `the panel #${id}`);
    return { toggle, panel, code: found(panel.querySelector<HTMLElement>('pre'), 'the snippet') };
}

/** The six boxes the snippet is generated from, as the generator's own type. */
const inputsOf = (demo: NonNullable<Window['__demo']>): Inputs => ({
    pattern: demo.pattern,
    flags: demo.flags,
    subject: demo.subject,
    mode: demo.mode,
    replacement: demo.replacement,
    namedLists: demo.namedLists,
});

test('the C# panel opens onto the case as code, and hands the focus back on Escape', async () => {
    const { page, demo } = await mountPage();
    const { toggle, panel, code } = snippetParts(page);

    expect(toggle.textContent).toMatch(/C#/);
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    // In the page and hidden, never absent: `aria-controls` pointing at an id that resolves to
    // nothing names nothing, which is the fault the tab set had before chunk 2 fixed it.
    expect(panel.hidden).toBe(true);

    toggle.click();
    await settle();
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(panel.hidden).toBe(false);

    // The snippet for the case on screen, character for character - the page owns no language
    // rules of its own, so what it shows is what `toCSharp` returns for the boxes as they stand.
    expect(code.textContent).toBe(toCSharp(inputsOf(demo)));

    // Coloured by spans from the tokenizer, and every character still there.
    const spans = [...panel.querySelectorAll<HTMLElement>('pre span')];
    expect(spans.map((span) => span.textContent).join('')).toBe(code.textContent);
    expect(spans.some((span) => span.className.includes('tok-keyword'))).toBe(true);
    expect(spans.some((span) => span.className.includes('tok-string'))).toBe(true);

    // Opening moves the focus into the panel: a revealed region nobody is standing in is a region
    // a keyboard has to tab back through the whole answer to reach.
    expect(panel.contains(document.activeElement)).toBe(true);

    // Escape closes it and puts the focus back where it came from (WCAG 2.4.3 Focus Order).
    found(document.activeElement, 'the focused element').dispatchEvent(keydown('Escape'));
    await settle();
    expect(panel.hidden).toBe(true);
    expect(document.activeElement).toBe(toggle);

    // The button closes it too, and the focus never leaves the button it was already on.
    toggle.click();
    await settle();
    toggle.click();
    await settle();
    expect(panel.hidden).toBe(true);
    expect(document.activeElement).toBe(toggle);
});

test('the snippet is text and never markup, and copying says out loud what happened', async () => {
    const { page, demo } = await mountPage();
    demo.subject = '<script>alert(1)</script>';
    await nextTick();

    const { toggle, panel, code } = snippetParts(page);
    toggle.click();
    await settle();

    // Spans with text in them, never `v-html`: the subject is whatever a visitor typed, and a page
    // that rendered it as markup would run it.
    expect(code.textContent).toContain('<script>alert(1)</script>');
    expect(panel.innerHTML).toContain('&lt;script&gt;');
    expect(panel.querySelector('script')).toBeNull();

    const status = found(page.querySelector<HTMLElement>('.status'), 'the live region');
    expect(status.getAttribute('aria-live')).toBe('polite');

    const writeText = vi.fn<(text: string) => Promise<void>>().mockResolvedValue();
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    const copy = found(panel.querySelector<HTMLButtonElement>('button.snippet-copy'), 'the copy button');
    copy.click();
    await until(() => /copied/i.test(status.textContent ?? ''), 'said the snippet was copied');
    expect(writeText).toHaveBeenCalledWith(code.textContent);

    // A refused write is ordinary - no permission, or no secure context - so the answer is the
    // text selected and a sentence saying so, in the region that announces everything else.
    writeText.mockRejectedValue(new DOMException('Write permission denied.', 'NotAllowedError'));
    copy.click();
    await until(() => /selected/i.test(status.textContent ?? ''), 'said the snippet was selected');
    expect(window.getSelection()?.toString()).toBe(code.textContent);
});

/**
 * The answer can be removed while a visitor is standing inside it.
 *
 * Everything below the status line is one `v-if` on the answer, so a pattern that stops parsing
 * does not hide the focused control, it removes it - and a browser given no instructions puts the
 * focus on `<body>`, at the top of the document. The C# panel is where that actually happens: it is
 * opened from the keyboard, the focus is placed inside it, and the visitor is still there when the
 * pattern they pasted turns out not to parse.
 *
 * The destination is the pattern field, because nothing in the answer survives to take the focus -
 * the panel's toggle is inside the same `v-if` - and the pattern field is where the fix is typed.
 */
test('losing the answer while the C# panel is open keeps the focus on the page', async () => {
    const { page, demo } = await mountPage();
    const { toggle, panel } = snippetParts(page);

    toggle.click();
    await settle();
    expect(panel.contains(document.activeElement)).toBe(true);

    // The pattern stops parsing. No offset, so the message goes to the block below the inputs and
    // the whole answer - highlights, tables, the panel - leaves the page.
    demo.failure = 'missing ) at position 1';
    demo.failureOffset = null;
    await settle();

    expect(page.querySelector('button.snippet-toggle'), 'the panel is still on the page').toBeNull();
    expect(document.activeElement, 'the focus was dropped to the top of the document').not.toBe(document.body);
    expect(document.activeElement).toBe(page.querySelector('#pattern'));

    // Shut, not merely unmounted: the next answer must not bring a screenful of code back open
    // around a focus that has since moved somewhere else.
    demo.failure = '';
    await settle();
    expect(snippetParts(page).panel.hidden).toBe(true);
});

/**
 * The keyboard's way past the input pane.
 *
 * Measured at 1920x1080, 1440x900, 1366x768 and 1024x768 (`tools/probes/s73-widths.mjs`,
 * 2026-09-20): twenty-six tabs from the top of the page to the first control in the answer,
 * eighteen of them example buttons, because above the gate the examples region is always open. The
 * phone is six, since the same region is behind a closed disclosure there. One link at the front of
 * the document makes it one at every width.
 */
test('the first thing the keyboard reaches is the way to the answer', async () => {
    const { page } = await mountPage();

    const focusable = page.querySelectorAll<HTMLElement>(
        'a[href], button, input, textarea, select, [tabindex]:not([tabindex="-1"])',
    );
    const first = found(focusable[0], 'a focusable control');
    expect(first.tagName).toBe('A');
    expect(first.className).toContain('skip-link');
    expect(first.getAttribute('href')).toBe('#results');
    // Named for where it goes, not "skip to content": the answer is what this page is for.
    expect(first.textContent?.trim()).toMatch(/answer/i);

    // The target has to be able to take the focus, or the link moves the viewport and leaves the
    // focus at the top of the document - the tab that follows starts from the header again.
    const answer = found(page.querySelector<HTMLElement>('#results'), 'a region with id="results"');
    expect(answer.className).toContain('results-pane');
    expect(answer.getAttribute('tabindex')).toBe('-1');

    // Off the screen until it is focused, and never off the screen while it is: the rule is in the
    // stylesheet, which jsdom does not apply, so it is read as source the way the rest of this file
    // reads it.
    expect(styles).toMatch(/\.skip-link\s*\{[^}]*absolute/);
    expect(styles).toMatch(/\.skip-link:focus\s*\{/);

    // Following it moves the focus and NOT the address bar. The case lives in the fragment, so a
    // link that navigated to `#results` would replace a shareable URL with an anchor - measured in
    // Chrome before this handler existed (`tools/probes/s73-widths.mjs`, 2026-09-20: the page ended
    // the run at `?v=...#results`). The page survives it either way, because `applyFragment` leaves
    // somebody else's anchor alone, but the URL the visitor would copy no longer holds their case.
    location.hash = '#p=kitten&s=sitting';
    await settle();
    // Dispatched rather than `.click()` so the assertion can be on the event. jsdom performs no
    // fragment navigation on an anchor click, so `location.hash` below reads the same with or
    // without `@click.prevent` and cannot tell the two apart (measured 2026-09-20). What a real
    // browser acts on is `defaultPrevented`, and that jsdom does report.
    const click = new MouseEvent('click', { bubbles: true, cancelable: true });
    first.dispatchEvent(click);
    await settle();
    expect(document.activeElement).toBe(answer);
    expect(click.defaultPrevented, 'the browser would navigate and drop the case').toBe(true);
    // A weaker guard than it looks in jsdom, and kept for the other half of the claim: the handler
    // focuses the region and writes nothing to the address bar itself.
    expect(location.hash).toBe('#p=kitten&s=sitting');
});

// --- the flags control (S74) -------------------------------------------------------------------

/**
 * The flags panel's parts: the `<details>`, the row it shows shut, and the boxes inside it.
 *
 * `flags.test.ts` owns what a selection MEANS - the two exclusive pairs, the two defaults, the
 * string that comes out. What is left for here is that the boxes are wired to it, which is the half
 * a pure function cannot check.
 */
function flagsPanel(page: HTMLElement) {
    const panel = found(page.querySelector<HTMLDetailsElement>('details#flags-panel'), 'the flags panel');
    return {
        panel,
        summary: found(panel.querySelector<HTMLElement>('summary'), 'the flags summary row'),
        chosen: found(panel.querySelector<HTMLElement>('.flags-chosen'), 'the row that says what is on'),
        box: (name: string) => found(panel.querySelector<HTMLInputElement>(`#flag-${name}`), `the ${name} box`),
        help: (name: string) => ({
            button: found(
                panel.querySelector<HTMLButtonElement>(`#flag-help-button-${name}`),
                `the ${name} help button`,
            ),
            text: found(panel.querySelector<HTMLElement>(`#flag-help-${name}`), `the ${name} help`),
        }),
    };
}

test('the flags panel starts shut, saying what it is hiding, and offers every flag', async () => {
    const { page } = await mountPage();
    const { panel, chosen, box } = flagsPanel(page);

    expect(panel.open).toBe(false);
    expect(chosen.textContent?.trim()).toBe('none');

    // A chevron, like the two disclosures above it, and for a harder reason than matching them: a
    // `<summary>` laid out as a flex row stops being a `list-item`, so the browser draws no marker
    // and the row has nothing left to say it opens. Seen in Chrome at 1366 px before it was added.
    const chevron = found(
        page.querySelector<HTMLElement>('#flags-panel > summary [aria-hidden="true"]'),
        'a chevron on the flags row',
    );
    expect(chevron.textContent?.trim()).toMatch(/\S/);

    for (const name of CHECKBOXES) expect(box(name).type).toBe('checkbox');
    for (const group of RADIO_GROUPS) {
        for (const option of group.options) {
            expect(box(option).type).toBe('radio');
            expect(box(option).name).toBe(group.name);
        }
    }

    // Every member but `None`, which is what an empty panel already means. A member added to the
    // library reaches this count through `FLAG_NAMES`, which DemoSnippetTests pins to the enum, so
    // a flag nobody gave a control to fails here rather than being quietly unreachable.
    expect(panel.querySelectorAll('input')).toHaveLength(FLAG_NAMES.length - 1);
});

/** A worker that keeps every question it was asked, so a test can watch the page ask again. */
class RecordingWorker extends FakeWorker {
    static made: RecordingWorker[] = [];
    constructor() {
        super();
        RecordingWorker.made.push(this);
    }
}

test('ticking a flag asks the engine again, and the shut row says so', async () => {
    RecordingWorker.made = [];
    vi.stubGlobal('Worker', RecordingWorker);
    const { page, demo } = await mountPage();
    const { chosen, box } = flagsPanel(page);

    box('BestMatch').click();
    await settle();

    expect(demo.flags).toBe('BestMatch');
    expect(chosen.textContent?.trim()).toBe('BestMatch');
    await until(
        () =>
            RecordingWorker.made.some((worker) =>
                worker.posted.some((question) => question.flags === 'BestMatch'),
            ),
        'asked the engine again with BestMatch',
    );

    // And the C# the panel hands out is the C# for the case as it now stands.
    const { toggle, code } = snippetParts(page);
    toggle.click();
    await settle();
    expect(code.textContent).toContain('FuzzyRegexOptions.BestMatch');

    box('BestMatch').click();
    await settle();
    expect(demo.flags).toBe('');
    expect(chosen.textContent?.trim()).toBe('none');
});

test('a shared link that names flags opens with them ticked', async () => {
    // The string is still the state, so every link written before this panel existed still loads -
    // including one that names a default, which the panel shows chosen and writes back as nothing.
    location.hash = '#p=a&f=IgnoreCase,BestMatch,Version1&s=abc';
    const { page } = await mountPage();
    const { box, chosen } = flagsPanel(page);

    expect(box('IgnoreCase').checked).toBe(true);
    expect(box('BestMatch').checked).toBe(true);
    expect(box('Version1').checked).toBe(true);
    expect(box('Posix').checked).toBe(false);
    expect(chosen.textContent?.trim()).toBe('IgnoreCase, BestMatch');
});

test('a link naming both sides of a pair shows both, and one press repairs it', async () => {
    // `Unicode,Ascii` is a string the engine refuses ("ASCII, LOCALE and UNICODE flags are mutually
    // incompatible" - `tools/probes/demo-flag-pair-exclusivity.ps1`, 2026-09-20). The page does not
    // rewrite what it was handed, so the panel must not claim one side of it: a row reading `Ascii`
    // beside an error about the pair is the page contradicting itself, and the press the visitor
    // then makes on `Ascii` fires no `change` at all, because that radio already says it is chosen.
    location.hash = '#p=a&f=Unicode,Ascii&s=abc';
    const { page, demo } = await mountPage();
    const { box, chosen } = flagsPanel(page);

    expect(demo.flags).toBe('Unicode,Ascii');
    expect(box('Unicode').checked).toBe(false);
    expect(box('Ascii').checked).toBe(false);
    expect(chosen.textContent?.trim()).toBe('Unicode, Ascii');

    box('Ascii').click();
    await settle();
    expect(demo.flags).toBe('Ascii');
    expect(box('Ascii').checked).toBe(true);
    expect(box('Unicode').checked).toBe(false);
});

test('the character set is one choice of two, and picking one unpicks the other', async () => {
    const { page, demo } = await mountPage();
    const { box, chosen } = flagsPanel(page);

    // Nothing named, and the library's own default shown as chosen: measured, not assumed, by
    // `tools/probes/demo-flag-pair-exclusivity.ps1` (2026-09-20) - a pattern compiled with nothing
    // and one compiled with `Unicode` both come back `Options = Unicode, Version1, FullCase`.
    expect(demo.flags).toBe('');
    expect(box('Unicode').checked).toBe(true);
    expect(box('Ascii').checked).toBe(false);

    box('Ascii').click();
    await settle();
    expect(demo.flags).toBe('Ascii');
    expect(box('Unicode').checked).toBe(false);

    // Back to the default, which is written as nothing at all rather than as `Unicode`.
    box('Unicode').click();
    await settle();
    expect(demo.flags).toBe('');
    expect(box('Ascii').checked).toBe(false);
    expect(chosen.textContent?.trim()).toBe('none');
});

test('each flag explains itself in the library own words, and Escape puts it away', async () => {
    const { page } = await mountPage();
    const { help } = flagsPanel(page);
    const { button, text } = help('BestMatch');

    expect(text.hidden).toBe(true);
    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(text.textContent?.trim()).toBe(FLAG_HELP.BestMatch);

    // A pointer resting on it: shown while it is there, gone when it leaves.
    button.dispatchEvent(new MouseEvent('mouseenter', { bubbles: true }));
    await settle();
    expect(text.hidden).toBe(false);
    expect(button.getAttribute('aria-expanded')).toBe('true');
    button.dispatchEvent(new MouseEvent('mouseleave', { bubbles: true }));
    await settle();
    expect(text.hidden).toBe(true);

    // A press pins it, because a tap leaves no pointer behind to hold it open, and content that
    // cannot be dismissed without moving a pointer is what WCAG 1.4.13 is about.
    button.click();
    await settle();
    expect(text.hidden).toBe(false);
    button.dispatchEvent(new MouseEvent('mouseleave', { bubbles: true }));
    await settle();
    expect(text.hidden).toBe(false);

    button.dispatchEvent(keydown('Escape'));
    await settle();
    expect(text.hidden).toBe(true);
    expect(button.getAttribute('aria-expanded')).toBe('false');

    // One sentence shows at a time: the help sits in the flow under its own row, so two open at
    // once move the grid twice and nobody is reading both.
    button.click();
    await settle();
    help('Posix').button.dispatchEvent(new MouseEvent('mouseenter', { bubbles: true }));
    await settle();
    expect(text.hidden, 'a pointer passing another flag does not take a pinned sentence away').toBe(false);

    // A second press is the other way to dismiss it.
    button.click();
    await settle();
    expect(text.hidden).toBe(true);
});

test('the keyboard opens a flag help and closes it again', async () => {
    const { page } = await mountPage();
    const { help } = flagsPanel(page);
    const { button, text } = help('Posix');

    button.focus();
    await settle();
    expect(text.hidden).toBe(false);

    button.blur();
    await settle();
    expect(text.hidden).toBe(true);
});

test('Escape puts the help away when the pointer opened it and the focus is elsewhere', async () => {
    const { page } = await mountPage();
    const { help } = flagsPanel(page);
    const { button, text } = help('BestMatch');

    // The hover case: the pointer rests on the `(?)` while the hands are still in the pattern box,
    // so no keystroke ever reaches the flags panel. WCAG 1.4.13 asks for a dismissal that does not
    // need the pointer moved, which means the key is listened for on the document.
    const patternBox = found(page.querySelector<HTMLInputElement>('#pattern'), 'the pattern box');
    patternBox.focus();
    button.dispatchEvent(new MouseEvent('mouseenter', { bubbles: true }));
    await settle();
    expect(text.hidden).toBe(false);

    patternBox.dispatchEvent(keydown('Escape'));
    await settle();
    expect(text.hidden).toBe(true);
    expect(button.getAttribute('aria-expanded')).toBe('false');

    // A pinned sentence goes the same way once the focus has left the panel, which is what a visitor
    // who tapped the `(?)` and then clicked into the pattern box is holding.
    button.click();
    await settle();
    patternBox.focus();
    expect(text.hidden).toBe(false);

    patternBox.dispatchEvent(keydown('Escape'));
    await settle();
    expect(text.hidden).toBe(true);
});
