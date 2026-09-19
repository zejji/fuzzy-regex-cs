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
import type { Group, Match } from '../src/types';

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

beforeEach(() => {
    location.hash = '';
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
    const panes = [...page.querySelectorAll('div.overflow-x-auto')];
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
