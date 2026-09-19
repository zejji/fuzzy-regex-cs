/**
 * The shell: three regions in one viewport, and exactly two of them scroll.
 *
 * Two halves, for the reason `page.test.ts` gives at its top: jsdom applies no stylesheet, so what
 * a browser DOES with the layout is asserted here against the stylesheet as source, and what the
 * page PUTS ON SCREEN is asserted against the mounted DOM. The real check - that the answer is in
 * the first viewport at 1366x768 - is Playwright, and its numbers are in the slice's closing notes.
 * What these tests hold is the structure those numbers depend on: if the scroll owners move, the
 * measurement stops meaning anything.
 */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { createApp, nextTick, type App as VueApp } from 'vue';

import App from '../src/App.vue';

import { FakeWorker } from './fake-worker';

const styles = readFileSync(join(import.meta.dirname, '../src/styles.css'), 'utf8');
const appSource = readFileSync(join(import.meta.dirname, '../src/App.vue'), 'utf8');
const template = /<template>([\s\S]*)<\/template>/.exec(appSource)?.[1] ?? '';

/** The stylesheet's declarations, without its prose: the comments below discuss `100vh` by name. */
const declarations = styles.replace(/\/\*[\s\S]*?\*\//g, ' ');

function found<T>(value: T | null | undefined, what: string): T {
    if (value === null || value === undefined) throw new Error(`the page has no ${what}`);
    return value;
}

// --- the stylesheet ---------------------------------------------------------------------------

/**
 * The two regions that own a scroll, and nothing else.
 *
 * Smashing Magazine's sticky-menu guidelines are where the cap comes from: stacked scroll panes
 * cause discoverability errors, because a wheel over the wrong pane moves the wrong thing. Two is
 * the number this page needs - the inputs and the answer - so a third arriving unnoticed is what
 * this test is for.
 */
test('exactly two regions own a vertical scroll', () => {
    const owners = [...declarations.matchAll(/([^{}]+)\{([^}]*)\}/g)]
        .filter(([, , body]) => /overflow-y-auto|overflow-y:\s*auto|@apply[^;]*\boverflow-auto/.test(body ?? ''))
        .map(([, selector]) => (selector ?? '').trim().split('\n').pop()?.trim());

    expect(owners.sort()).toEqual(['.input-pane', '.results-pane']);
});

// A utility class in the template would slip past the rule above, because it never appears in the
// stylesheet at all. Tailwind's vertical-scroll utilities are therefore banned from the markup:
// where a region scrolls is a layout decision and it is made in one file.
test('no vertical scroll is declared in the markup', () => {
    expect(template).not.toMatch(/class="[^"]*\b(?:overflow-y-auto|overflow-y-scroll|overflow-auto)\b/);
});

/**
 * The fixed shell is gated on height as well as width, and this is the Hunt item the slice names.
 *
 * A window that is 1366 wide and 380 tall - a phone in landscape, or a laptop at 200 % zoom, where
 * the CSS viewport halves in both directions - would otherwise get an application frame with no
 * room for its own content, and "the page does not scroll" becomes "the button cannot be reached".
 * Below the gate the shell releases and the document scrolls as a document, which is also what
 * WCAG 1.4.10 Reflow asks for at 320 CSS px.
 */
test('the page stops scrolling only when the window has the height for it', () => {
    const query = found(
        /@media([^{]*)\{\s*(?:html,\s*)?body\s*\{[^}]*overflow:\s*hidden/.exec(declarations),
        'a media query that stops the document scrolling',
    );

    expect(query[1]).toMatch(/min-width:/);
    expect(query[1]).toMatch(/min-height:/);
});

// `100vh` on mobile Safari is the window without its toolbar, so a shell sized with it is taller
// than the space it has and its last row sits under the chrome. `dvh` is the same number after the
// toolbar is accounted for, and it is what the shell is sized in.
test('the shell is sized in dvh, never vh', () => {
    expect(declarations).toMatch(/height:\s*100dvh/);
    expect(declarations).not.toMatch(/100vh/);
});

/**
 * The panes scroll only while the shell is the thing holding them.
 *
 * Seen at 1366x500 in a real browser: the panes kept `lg:overflow-y-auto`, which asks about the
 * width alone, so below the height gate the document scrolled AND both panes scrolled inside it -
 * three stacked scroll regions, which is the failure the two-region rule exists to stop. Both
 * owners therefore live inside the same block as the fixed shell.
 */
test('the panes scroll only inside the gate that fixes the shell', () => {
    const gated = found(
        /@media[^{]+\{([\s\S]*?)\n\s{0,4}\}\s*$/.exec(declarations.trimEnd()),
        'the gated block at the end of the stylesheet',
    )[1];

    expect(gated).toMatch(/\.input-pane\s*\{[^}]*overflow-y:\s*auto/);
    expect(gated).toMatch(/\.results-pane\s*\{[^}]*overflow-y:\s*auto/);
    // And nowhere else: a `lg:` variant would put one back on a width-only question.
    expect(declarations).not.toMatch(/overflow-y-auto/);
});

// The gate is written twice - as a media query here, as a string the script hands `matchMedia`
// there - because CSS cannot pass a query to a script. Two copies of one decision is a drift
// hazard, so this is the thing that notices.
test('the script and the stylesheet gate the shell on the same window', () => {
    const inScript = found(/SHELL_QUERY = '([^']+)'/.exec(appSource), 'the shell query in App.vue')[1];
    const inStyles = found(/@media\s*\(([^{]+)\)\s*\{\s*html,/.exec(declarations), 'the shell media query')[1];

    const tidy = (query: string): string => query.replace(/\s+/g, ' ').replace(/[()]/g, '').trim();
    expect(tidy(inScript ?? '')).toBe(tidy(inStyles ?? ''));
});

// One scheme, done well (owner decision, 2026-09-19). A half-maintained second scheme is worse
// than none: every colour pair below was measured in light, and a `dark:` variant that nobody
// measures is a contrast failure waiting for the first visitor whose system is set that way.
test('there is one colour scheme and it is light', () => {
    expect(declarations).not.toMatch(/prefers-color-scheme/);
    expect(declarations).not.toMatch(/\bdark:/);
    expect(appSource.replace(/<!--[\s\S]*?-->/g, ' ')).not.toMatch(/\bdark:/);
    expect(declarations).toMatch(/color-scheme:\s*light\s*;/);
});

// --- the page ---------------------------------------------------------------------------------

let app: VueApp<Element> | null = null;
let host: HTMLDivElement | null = null;

const EMPTY_HELP = JSON.stringify({
    source: 'docs/COMPARISON.md',
    note: 'generated',
    entries: {
        fuzzy: [
            {
                heading: [{ code: false, text: 'Fuzzy matching' }],
                blocks: [{ kind: 'paragraph', runs: [{ code: false, text: 'Up to n errors.' }] }],
            },
        ],
    },
});

/**
 * Two samples: one whose `key` names a documented section, one with no key at all.
 *
 * The pair is the whole point - a page that opened the explanation for both would pass a test that
 * only ever loaded the documented one.
 */
const SAMPLES = JSON.stringify([
    { title: 'Fuzzy', note: 'One error allowed.', pattern: 'a', flags: '', subject: 'a', key: 'fuzzy' },
    { title: 'Plain', note: 'No prose for this one.', pattern: 'b', flags: '', subject: 'b' },
]);

let served = '[]';

beforeEach(() => {
    location.hash = '';
    served = '[]';
    vi.stubGlobal('Worker', FakeWorker);
    vi.stubGlobal(
        'fetch',
        vi.fn((input: unknown) =>
            Promise.resolve(new Response(String(input).includes('help.json') ? EMPTY_HELP : served, { status: 200 })),
        ),
    );
});

/** Wait for a condition the page reaches through its own promises, rather than counting ticks. */
async function until(ready: () => boolean, what: string): Promise<void> {
    for (let attempt = 0; attempt < 50; attempt += 1) {
        if (ready()) return;
        await nextTick();
        await new Promise((resolve) => setTimeout(resolve, 0));
    }
    throw new Error(`the page never ${what}`);
}

afterEach(() => {
    app?.unmount();
    host?.remove();
    app = null;
    host = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
});

async function mountPage() {
    host = document.createElement('div');
    document.body.append(host);
    app = createApp(App);
    app.mount(host);
    await nextTick();
    await nextTick();
    return { page: host, demo: found(window.__demo, 'state on window.__demo') };
}

const keydown = (key: string) => new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });

test('the shell is a header, two panes and a footer, and the panes are the scrolling ones', async () => {
    const { page } = await mountPage();
    const shell = found(page.querySelector<HTMLElement>('.shell'), 'shell');

    // The rows of the grid, in order. A footer inside the scrolling input pane would be a footer
    // most visitors never reach, so it is a row of the shell.
    expect([...shell.children].map((child) => child.tagName.toLowerCase())).toEqual(['header', 'main', 'footer']);

    expect(found(page.querySelector('.input-pane'), 'input pane').closest('main')).not.toBeNull();
    expect(found(page.querySelector('.results-pane'), 'results pane').closest('main')).not.toBeNull();
});

/**
 * The tab set, to NN/g's rules: one row, one tab always selected, the panel below the list.
 *
 * ARIA's own pattern on top of that - `tablist`, `tab`, `tabpanel`, each tab naming the panel it
 * controls and each panel naming its tab - because the visual cue that a tab is selected is read
 * out only if `aria-selected` says so too.
 */
test('examples and help are a tab set with one tab always selected', async () => {
    const { page } = await mountPage();
    const list = found(page.querySelector<HTMLElement>('[role="tablist"]'), 'tab list');
    const tabs = [...list.querySelectorAll<HTMLElement>('[role="tab"]')];

    expect(tabs.map((tab) => tab.textContent?.trim())).toEqual(['Examples', 'Help']);
    expect(tabs.filter((tab) => tab.getAttribute('aria-selected') === 'true')).toHaveLength(1);

    // One tab stop for the set, not one per tab: the arrows move between them, which is the roving
    // tabindex the match highlights and the table rows already use.
    expect(tabs.map((tab) => tab.getAttribute('tabindex'))).toEqual(['0', '-1']);

    const panel = found(page.querySelector<HTMLElement>('[role="tabpanel"]'), 'tab panel');
    expect(page.querySelectorAll('[role="tabpanel"]')).toHaveLength(1);
    expect(panel.getAttribute('aria-labelledby')).toBe(tabs[0]?.id);
    expect(tabs[0]?.getAttribute('aria-controls')).toBe(panel.id);
});

test('the arrows move between the tabs and the ends hold', async () => {
    const { page } = await mountPage();
    const tabs = () => [...page.querySelectorAll<HTMLElement>('[role="tab"]')];
    const selected = () => tabs().findIndex((tab) => tab.getAttribute('aria-selected') === 'true');
    const list = found(page.querySelector<HTMLElement>('[role="tablist"]'), 'tab list');

    list.dispatchEvent(keydown('ArrowRight'));
    await nextTick();
    expect(selected()).toBe(1);
    expect(document.activeElement).toBe(tabs()[1]);

    // The end holds rather than wrapping, the same way the match highlights do.
    list.dispatchEvent(keydown('ArrowRight'));
    await nextTick();
    expect(selected()).toBe(1);

    list.dispatchEvent(keydown('Home'));
    await nextTick();
    expect(selected()).toBe(0);
});

// Loading a sample is a visitor asking what it does, and its explanation is behind the other tab.
// Answering that click by showing the explanation is the whole point of having the prose; the
// examples are one click back, and nothing is stolen from a visitor who did not click.
//
// Driven by clicking the buttons the page renders, not by calling the state's own `load`: the tab
// switch belongs to the page, so a test that reached past the page would prove nothing about it.
test('loading a sample that has an explanation shows it', async () => {
    served = SAMPLES;
    const { page } = await mountPage();
    await until(() => page.querySelectorAll('button.example-button').length === 2, 'rendered its samples');

    const tabs = [...page.querySelectorAll<HTMLElement>('[role="tab"]')];
    const samples = [...page.querySelectorAll<HTMLElement>('button.example-button')];

    found(samples[0], 'the documented sample').click();
    await nextTick();
    expect(tabs[1]?.getAttribute('aria-selected')).toBe('true');

    // Back to the examples, then a sample with no documented section: it leaves the visitor where
    // they were, rather than opening a panel onto nothing.
    found(tabs[0], 'the examples tab').click();
    await nextTick();
    await until(() => page.querySelectorAll('button.example-button').length === 2, 'came back to its samples');
    found(
        [...page.querySelectorAll<HTMLElement>('button.example-button')][1],
        'the undocumented sample',
    ).click();
    await nextTick();
    expect(tabs[0]?.getAttribute('aria-selected')).toBe('true');
});

/**
 * On one column, the boxes below the subject and the whole sample panel start closed.
 *
 * Everything open above the answer on a phone is a screenful between a visitor and the answer,
 * which is the fault this slice exists to fix. `matchMedia` is what the page asks, so this is
 * what a narrow window is simulated with - jsdom has no layout and would answer nothing.
 */
test('a narrow window folds the secondary inputs and the sample panel away', async () => {
    vi.stubGlobal('matchMedia', () => ({ matches: false, addEventListener() {}, removeEventListener() {} }));
    const { page } = await mountPage();

    expect(page.querySelector('#advanced-inputs')).toBeNull();
    expect(page.querySelector('[role="tablist"]')).toBeNull();
    expect(page.querySelector('#flags')).toBeNull();
    // The two boxes that are the case at its shortest stay on screen.
    expect(page.querySelector('#pattern')).not.toBeNull();
    expect(page.querySelector('#subject')).not.toBeNull();

    // Both disclosures say what they do and what state they are in, which is what `aria-expanded`
    // on the button that controls them is for.
    const disclosures = [...page.querySelectorAll<HTMLElement>('button[aria-expanded]')];
    expect(disclosures.map((button) => button.getAttribute('aria-expanded'))).toEqual(['false', 'false']);

    found(disclosures[0], 'the inputs disclosure').click();
    await nextTick();
    expect(page.querySelector('#flags')).not.toBeNull();

    found(disclosures[1], 'the samples disclosure').click();
    await nextTick();
    expect(page.querySelector('[role="tablist"]')).not.toBeNull();
});

/**
 * A disclosure looks like a control, and is the size of one.
 *
 * Found at 390 px in a real browser: written as a tab, the two of them read as two lines of prose
 * with nothing to say they could be pressed. A chevron and a row of its own are the cheapest way to
 * say "this opens", and `min-h-11` is 44 px, which clears WCAG 2.2's 24x24 (2.5.8) with room for a
 * thumb. The chevron is decoration - `aria-expanded` is what a screen reader is told.
 */
test('a disclosure is a control and not a line of text', async () => {
    vi.stubGlobal('matchMedia', () => ({ matches: false, addEventListener() {}, removeEventListener() {} }));
    const { page } = await mountPage();

    const disclosures = [...page.querySelectorAll<HTMLElement>('button[aria-expanded]')];
    expect(disclosures).toHaveLength(2);
    for (const button of disclosures) {
        expect(button.classList.contains('disclosure')).toBe(true);
        const chevron = found(button.querySelector('[aria-hidden="true"]'), 'a chevron on a disclosure');
        expect(chevron.textContent?.trim()).toMatch(/\S/);
    }

    expect(/\.disclosure\s*\{[^}]*\bmin-h-11\b/.test(declarations), 'the disclosure is 44 px tall').toBe(true);
});
