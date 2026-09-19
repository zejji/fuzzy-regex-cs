// The page as a browser assembles it: App.vue mounted into jsdom, over the same fake worker the
// state machine's tests use.
//
// demo.test.ts covers what the page BELIEVES; this file covers what it PUTS ON SCREEN, which is
// where a keyboard user and a screen reader meet it. There is no @vue/test-utils in this project
// and this needs none: `createApp().mount()` is the same call main.ts makes, and the assertions
// below are ordinary DOM queries. What still needs a real browser - that the runtime boots, that
// focus is visible, that the layout holds at 390 px - is checks.html and the screenshots.

import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { createApp, nextTick, type App as VueApp } from 'vue';

import App from '../src/App.vue';
import type { Group, Match } from '../src/types';

import { FakeWorker } from './fake-worker';

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

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
    vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(new Response('[]', { status: 200 }))));
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
    await sleep(400); // the debounce plus the fake's round trip

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

    await sleep(400);
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
