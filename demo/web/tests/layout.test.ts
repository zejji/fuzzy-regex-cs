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
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { createApp, nextTick, type App as VueApp } from 'vue';

import App from '../src/App.vue';

import { builtCss } from './built-css';
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

/**
 * Every selector that declares a vertical scroll, one entry per selector in a list.
 *
 * Over compiled CSS, so `.input-pane{overflow-y:auto}.results-pane{overflow-y:auto}` and
 * `.input-pane,.results-pane{overflow-y:auto}` give the same answer: they are the same stylesheet,
 * and the old source-reading version failed on the difference. `overflow:auto` shorthand counts,
 * and so does a `.overflow-y-auto` utility - it would arrive here as a selector of its own and
 * break the equality, which is the point.
 */
function scrollOwners(css: string): string[] {
    return [...css.matchAll(/([^{}@]+)\{([^}]*)\}/g)]
        .filter(([, , body]) => /overflow(?:-y)?:\s*(?:auto|scroll)/.test(body ?? ''))
        .flatMap(([, selector]) => (selector ?? '').split(',').map((one) => one.trim()))
        .filter((selector) => selector !== '');
}

/** The media query that fixes the shell, read off the block that stops the document scrolling. */
function gateQuery(css: string): string {
    return found(
        /@media([^{]+)\{[^{}]*(?:html,\s*)?body\{[^}]*overflow:\s*hidden/.exec(css),
        'a media query that stops the document scrolling',
    )[1] as string;
}

/** That query's whole block, so a test can ask what is inside the gate and what is outside it. */
function gateBlock(css: string): string {
    const start = css.indexOf(`@media${gateQuery(css)}`);
    let depth = 0;
    for (let at = css.indexOf('{', start); at < css.length; at += 1) {
        if (css[at] === '{') depth += 1;
        if (css[at] === '}') {
            depth -= 1;
            if (depth === 0) return css.slice(start, at + 1);
        }
    }
    throw new Error('the gated block does not close');
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
test('exactly two regions own a vertical scroll', async () => {
    expect(scrollOwners(await builtCss()).sort()).toEqual(['.input-pane', '.results-pane']);
});

/**
 * All the CSS is in `styles.css`, because the tests above read what `styles.css` compiles to.
 *
 * A `<style>` block inside a single-file component ships - `.probe-third-scroller { overflow-y:
 * auto }` appended to `App.vue` came out in `assets/index-D2ryK5O7.css`, measured - and the
 * in-process build in `built-css.ts` cannot see it: its entry is the stylesheet, and an SFC's
 * styles reach the bundle through the JavaScript graph that starts at `index.html`. So a third
 * scroll region added that way would ship with every assertion above still green.
 *
 * Compiling the page's whole module graph in a test would close the gap and cost a JavaScript
 * build per test file. Keeping every rule in one file closes it at the other end, costs nothing,
 * and is what this project already does.
 */
test('no component brings a stylesheet of its own', () => {
    const components = readdirSync(join(import.meta.dirname, '../src'), { recursive: true, encoding: 'utf8' })
        .filter((name) => name.endsWith('.vue'))
        .map((name) => join(import.meta.dirname, '../src', name));

    expect(components.length, 'the glob found no components').toBeGreaterThan(0);
    for (const component of components) {
        expect(readFileSync(component, 'utf8'), `${component} declares styles`).not.toMatch(/<style[\s>]/);
    }
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
test('the page stops scrolling only when the window has the height for it', async () => {
    const query = gateQuery(await builtCss());

    expect(query).toMatch(/min-width:\s*64rem|width\s*>=\s*64rem/);
    expect(query).toMatch(/min-height:|height\s*>=/);
});

// `100vh` on mobile Safari is the window without its toolbar, so a shell sized with it is taller
// than the space it has and its last row sits under the chrome. `dvh` is the same number after the
// toolbar is accounted for, and it is what the shell is sized in.
// Over the BUILT stylesheet because the source cannot answer the question: `@apply min-h-screen`
// says nothing about `vh`, and it is what compiled to `min-height: 100vh` and beat the gate's
// `height: 100dvh` on the same selector. Chunk 2 shipped that, and this is the assertion that sees it.
test('the shell is sized in dvh, never vh', async () => {
    const css = await builtCss();

    expect(css).toMatch(/\.shell\{[^}]*min-height:\s*100dvh/);
    expect(css).not.toMatch(/100vh/);
});

/**
 * The panes scroll only while the shell is the thing holding them.
 *
 * Seen at 1366x500 in a real browser: the panes kept `lg:overflow-y-auto`, which asks about the
 * width alone, so below the height gate the document scrolled AND both panes scrolled inside it -
 * three stacked scroll regions, which is the failure the two-region rule exists to stop. Both
 * owners therefore live inside the same block as the fixed shell.
 */
test('the panes scroll only inside the gate that fixes the shell', async () => {
    const css = await builtCss();

    expect(scrollOwners(gateBlock(css)).sort()).toEqual(['.input-pane', '.results-pane']);
    // And nowhere else: a `lg:` variant would put one back on a width-only question.
    expect(scrollOwners(css.replace(gateBlock(css), ' '))).toEqual([]);
});

// The gate is written twice - as a media query here, as a string the script hands `matchMedia`
// there - because CSS cannot pass a query to a script. Two copies of one decision is a drift
// hazard, so this is the thing that notices.
test('the script and the stylesheet gate the shell on the same window', async () => {
    const inScript = found(/SHELL_QUERY = '([^']+)'/.exec(appSource), 'the shell query in App.vue')[1];

    // The script's query is what `matchMedia` is handed, so it is written the way a person writes
    // one; the build rewrites the stylesheet's into CSS Media Queries 4 range syntax
    // (`min-width: 64rem` becomes `width>=64rem`). Comparing them means putting one into the other's
    // form, and the script's is the one with a fixed spelling.
    const asRange = (query: string): string =>
        query
            .replace(/min-(width|height):\s*/g, '$1>=')
            .replace(/max-(width|height):\s*/g, '$1<=')
            .replace(/[\s()]/g, '');

    expect(asRange(gateQuery(await builtCss()))).toBe(asRange(inScript ?? ''));
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

/**
 * Underlining means "this navigates", and nothing else on the page may borrow it.
 *
 * The owner's first look at v2 said the match numbers looked like links and appeared to do nothing
 * when clicked: `.row-select` was accent-coloured and underlined, so it made a promise the page
 * could not keep. The rule is worth a test rather than a habit, because `underline` is one Tailwind
 * utility away in any template, and the utility would arrive in the compiled stylesheet as a
 * selector of its own - which is what this reads.
 *
 * Both spellings: Tailwind's `underline` utility compiles to the `text-decoration-line` longhand,
 * but hand-written CSS in this file may say `text-decoration: underline` and a blind review's
 * mutant proved the longhand-only read let that through (2026-09-19).
 *
 * The solid underline only. Tailwind's preflight gives `abbr:where([title])` an
 * `underline dotted`, which is the browsers' own abbreviation convention and reads as nothing
 * like a link; the style a visitor mistakes for one is the plain line this page's links carry.
 *
 * SHORTCUT: this reads the stylesheet with regular expressions rather than parsing it, so it sees
 * what someone writes by accident and not what someone writes to get past it - a keyword behind
 * `var(--deco)` is invisible to it, and a selector that merely contains a link compound
 * (`a:hover ~ .row-select`) is taken for a link. Both were proved past it on 2026-09-19. The lift
 * is a real CSS parser (`postcss` is already in the tree as a Tailwind dependency); the reason not
 * to take it yet is that this test guards a habit, and the habit is `class="underline"`.
 */

/**
 * The comma-separated selectors of one rule, cut at the top level only.
 *
 * A plain `split(',')` cuts inside `:where(.shell-header, .shell-footer) a` as well, and the
 * fragment `:where(.shell-header` is then read as a selector that is not a link - so the file's
 * own scoped link rule would have failed the test the moment it carried the underline itself.
 */
function selectorList(selector: string): string[] {
    const parts: string[] = [];
    let depth = 0;
    let current = '';
    for (const character of selector) {
        if (character === '(' || character === '[') depth += 1;
        else if (character === ')' || character === ']') depth -= 1;

        if (character === ',' && depth === 0) {
            parts.push(current);
            current = '';
        } else {
            current += character;
        }
    }
    return [...parts, current].map((one) => one.trim()).filter((one) => one.length > 0);
}
test('nothing but a link is underlined', async () => {
    // At-rule preludes dropped first, so a rule inside `@media (hover: hover)` is read as the rule
    // it is. Left in, the prelude becomes the "selector" and the rule inside it is never checked -
    // which is where `hover:underline` on a real link lands, so the common case was the broken one.
    const rules = (await builtCss()).replace(/@[a-z-]+[^{;]*\{/gi, ' ');

    const underlined = [...rules.matchAll(/([^{}@]+)\{([^}]*)\}/g)]
        .filter(
            ([, , body]) =>
                /text-decoration(?:-line)?:[^;]*\bunderline\b/.test(body ?? '') &&
                !/text-decoration[^;]*\b(?:dotted|dashed|wavy|double)\b/.test(body ?? ''),
        )
        .flatMap(([, selector]) => selectorList(selector ?? ''));

    expect(underlined.length, 'no rule underlines anything, so the link style is gone').toBeGreaterThan(0);
    for (const selector of underlined) {
        // A selector naming the <a> element, which a utility class never does: `class="underline"`
        // compiles to `.underline` and says nothing about what wears it, so an underline a link
        // needs is written in this file against `a`, not in the template.
        const why = `${selector} underlines something that is not a link - underline links in styles.css, against 'a'`;
        expect(selector, why).toMatch(/(^|[\s>+~])a(?:[:[.]|$)/);
    }
});

/**
 * How hard `selector` matches an element carrying exactly `classes`, or null if it does not match.
 *
 * The number is CSS specificity for the shapes this file writes the highlight states in: a compound
 * of classes, with `:not(.class)` allowed, where every token counts one. Any other shape - a tag, a
 * combinator, a pseudo-element, `*` - is reported as no match, which is what keeps the universal
 * rule that declares Tailwind's variables out of an answer about one `<mark>`.
 */
function specificity(selector: string, classes: readonly string[]): number | null {
    const CLASS_TOKEN = /:not\(\.([a-z0-9-]+)\)|\.([a-z0-9-]+)/gi;
    const tokens = [...selector.matchAll(CLASS_TOKEN)];
    if (tokens.length === 0) return null;
    if (selector.replace(CLASS_TOKEN, '').trim() !== '') return null;

    for (const [, excluded, required] of tokens) {
        if (excluded !== undefined && classes.includes(excluded)) return null;
        if (required !== undefined && !classes.includes(required)) return null;
    }
    return tokens.length;
}

/**
 * What the cascade leaves on such an element for one property, or null if nothing declares it.
 *
 * Source order decides between equal specificities, which is the whole question here, so a later
 * rule of the same strength replaces the winner. At-rule preludes are dropped first for the reason
 * the underline test gives: left in, `@layer components{` reads as a selector and swallows the
 * first rule inside the layer.
 */
function cascade(css: string, classes: readonly string[], property: string): string | null {
    const rules = css.replace(/@[a-z-]+[^{;]*\{/gi, ' ');
    const declaration = new RegExp(`(?:^|;)\\s*${property}\\s*:\\s*([^;]+)`, 'g');
    let winner: { strength: number; value: string } | null = null;

    for (const [, list, body] of rules.matchAll(/([^{}@]+)\{([^}]*)\}/g)) {
        // The last declaration in the rule, because `outline-dashed` writes `outline-style` twice.
        const value = [...(body ?? '').matchAll(declaration)].at(-1)?.[1]?.trim();
        if (value === undefined) continue;

        for (const selector of selectorList(list ?? '')) {
            const strength = specificity(selector, classes);
            if (strength !== null && (winner === null || strength >= winner.strength)) {
                winner = { strength, value };
            }
        }
    }

    return winner?.value ?? null;
}

/**
 * Selected outranks hovered on the mark that is both.
 *
 * `.hit-current` and `.hit-linked` paint the same property at equal specificity, so which one wins
 * on a mark carrying both was decided by source order alone - and the hover rule is written second.
 * Hovering the selected match therefore repainted it as a hover, which is the ordinary case and not
 * a corner: the pointer is usually what selected it in the first place.
 *
 * Over the COMPILED stylesheet, because only the compiled output shows the second half of that
 * fault: `outline-dashed` sets a `--tw-outline-style` variable as well as `outline-style`, and that
 * variable is what `.hit-current`'s own `outline-style: var(--tw-outline-style)` reads - so the
 * dashed line reached the selected mark even where the colour did not. The source is two `@apply`
 * lines with no conflict visible in them at all.
 */
test('the selected match keeps its own outline under the pointer', async () => {
    const css = await builtCss();
    const both = ['hit', 'hit-current', 'hit-linked'];

    expect(cascade(css, both, 'outline-color'), 'the hover colour wins on the selected mark').toBe(
        'var(--color-slate-900)',
    );
    expect(cascade(css, both, 'outline-style'), 'the selected mark is drawn dashed').not.toBe('dashed');
    expect(cascade(css, both, '--tw-outline-style'), 'the hover rule flips the style variable').not.toBe('dashed');

    // And a hovered match that is not selected still gets the dashed cue: the fix is a narrower
    // selector, not a deleted rule.
    expect(cascade(css, ['hit', 'hit-linked'], 'outline-color')).toBe('var(--color-slate-500)');
    expect(cascade(css, ['hit', 'hit-linked'], 'outline-style')).toBe('dashed');
});

/** Every rule whose selector list names exactly this selector, in source order. */
function rulesFor(css: string, selector: string): string[] {
    return [...css.matchAll(/([^{}@]+)\{([^}]*)\}/g)]
        .filter(([, list]) => selectorList(list ?? '').includes(selector))
        .map(([, , body]) => body ?? '');
}

/**
 * The sticky column header sticks to the region that scrolls, with nothing in between.
 *
 * `position: sticky` sticks to the nearest ancestor scrollport, and CSS Overflow 3 makes one axis
 * enough: with `overflow-x: auto` the other axis's used value becomes `auto` too. So `.table-scroll`
 * - which exists for the six columns at 390 px - was the `th`'s scrollport, and `.table-scroll`
 * never scrolls vertically, because the table is as tall as its rows. The header therefore scrolled
 * away with them: measured at 1366x768 with the pane scrolled to 1500, the `th` at y=-949, off the
 * top of a window whose pane starts at y=64.
 *
 * `overflow-y: clip` was the fix chunk 4 proposed, and the browser refused it: the same clause of
 * the spec turns a `clip` beside an `auto` into `hidden`, which is still a scroll container, and the
 * measurement came back -949 unchanged. What works is giving the wrapper no overflow at all inside
 * the gate, so the scrollport is `.results-pane`: the `th` then holds at y=84, the top of the pane.
 * Both numbers are from `tools/probes/s73-sticky-column-header.mjs`, and the wrapper costs nothing
 * to drop there - at 1024x768, the narrowest window inside the gate, the table is 479 px in a 503 px
 * pane, and nothing gains a sideways scroll (`tools/probes/s73-sticky-header-fix-at-five-widths.mjs`).
 */
test('the sticky column header has no scroll container between it and the pane', async () => {
    const css = await builtCss();
    const gate = gateBlock(css);

    expect(rulesFor(css, '.data-table th').join(' '), 'the column header is not sticky at all').toMatch(
        /position:\s*sticky/,
    );

    // Inside the gate: no overflow on the wrapper, so the pane is the nearest scrollport.
    const inside = rulesFor(gate, '.table-scroll').join(' ');
    expect(inside, 'the table wrapper does not give up its overflow inside the gate').toMatch(
        /overflow:\s*visible/,
    );
    for (const body of rulesFor(gate, '.table-scroll')) {
        expect(body, 'a scroll container inside the gate takes the sticky header with it').not.toMatch(
            /overflow(?:-x|-y)?:\s*(?:auto|scroll|hidden|clip)/,
        );
    }

    // Outside it the document scrolls, the pane owns no overflow, and the six columns need the
    // wrapper's own sideways scroller back - at 390 px the table is 479 px wide in a 342 px pane.
    expect(
        rulesFor(css.replace(gate, ' '), '.table-scroll').join(' '),
        'below the gate the table has no way to scroll sideways',
    ).toMatch(/overflow-x:\s*auto/);

    // And the header docks at the pane's border, not 20 px inside it. A sticky `top: 0` is the
    // scrollport's PADDING edge, so with the pane's `sm:p-5` the rows went on scrolling through the
    // band above the docked header, in the open: measured at 1366x768 with the pane at 1200, pane
    // top y=64, header top y=84, `elementFromPoint(900, 66)` a `TD`. The header is pulled up by the
    // pane's own padding, and these two numbers are why this assertion exists rather than a comment:
    // change `sm:p-5` alone and the band comes back with nothing on screen to say so.
    const spacing = (declaration: string, property: string): number => {
        const found = new RegExp(`${property}:\\s*calc\\(var\\(--spacing\\)\\s*\\*\\s*(-?\\d+)\\)`).exec(declaration);
        return found ? Number(found[1]) : Number.NaN;
    };
    // The pane's padding at its widest - `p-4 sm:p-5`, and the gate starts at 64rem, so the `sm:`
    // one is what applies wherever the header sticks. Taken as the largest rather than the last,
    // because the order the build emits two rules for one selector in is not the point being made.
    // At-rule preludes dropped first, or the `sm:` rule is read as a rule named `media (width>=40rem)`
    // and its padding is never seen - the same trap the underline test above fell into.
    const panePadding = rulesFor(css.replace(/@[a-z-]+[^{;]*\{/gi, ' '), '.results-pane')
        .map((body) => spacing(body, '(?<!-)padding'))
        .filter((step) => !Number.isNaN(step));
    expect(panePadding.length, 'the pane declares no padding').toBeGreaterThan(0);
    expect(spacing(rulesFor(gate, '.data-table th').join(' '), 'top')).toBe(-Math.max(...panePadding));
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
    //
    // The skip link is a child but not a row: it is `position: absolute`, so it is out of the flex
    // flow and occupies none of the height the three rows divide. It has to be the FIRST child,
    // because being the first tab stop is the whole of what it does; `page.test.ts` pins the rest
    // of it, and the rule that takes it off the screen until it is focused.
    expect(shell.firstElementChild?.classList.contains('skip-link')).toBe(true);
    const rows = [...shell.children].filter((child) => !child.classList.contains('skip-link'));
    expect(rows.map((child) => child.tagName.toLowerCase())).toEqual(['header', 'main', 'footer']);

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

    // Both panels are in the page, the unselected one hidden, and every tab's `aria-controls`
    // resolves. One panel rendered at a time left the unselected tab pointing at an id that was not
    // there, and this assertion is over BOTH tabs because the version that checked `tabs[0]` alone
    // could not see it (S73 chunk 2 review, finding 1).
    const panels = [...page.querySelectorAll<HTMLElement>('[role="tabpanel"]')];
    expect(panels.map((panel) => panel.hidden)).toEqual([false, true]);
    for (const tab of tabs) {
        const panel = found(
            page.querySelector<HTMLElement>(`#${tab.getAttribute('aria-controls')}`),
            `the panel ${tab.id} controls`,
        );
        expect(panel.getAttribute('role')).toBe('tabpanel');
        expect(panel.getAttribute('aria-labelledby')).toBe(tab.id);
        expect(panel.hidden).toBe(tab.getAttribute('aria-selected') !== 'true');
    }
});

/**
 * A panel a keyboard can reach, whether or not it holds anything to focus.
 *
 * APG's Tabs pattern, note 4: "When the tabpanel does not contain any focusable elements or the
 * first element with content is not focusable, the tabpanel should set tabindex=0 to include it in
 * the tab sequence of the page." With no sample loaded the Help panel is one paragraph, so without
 * this its words are on screen and out of reach.
 */
test('the help panel is in the tab sequence exactly while it has nothing to focus', async () => {
    served = SAMPLES;
    const { page } = await mountPage();
    const help = () => found(page.querySelector<HTMLElement>('#panel-help'), 'the help panel');

    expect(help().getAttribute('tabindex')).toBe('0');

    await until(() => page.querySelectorAll('button.example-button').length === 2, 'rendered its samples');
    found([...page.querySelectorAll<HTMLElement>('button.example-button')][0], 'the documented sample').click();
    await nextTick();

    // Loaded: the panel now holds <details> summaries and code boxes, so a stop on the panel itself
    // would be one press in the way of them.
    expect(help().getAttribute('tabindex')).toBeNull();
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

    // Hidden rather than absent: the button says `aria-controls`, so the thing it names has to be
    // in the page for the reference to mean anything. `hidden` is what takes it off the screen and
    // out of the accessibility tree, so nothing is reachable that is not visible.
    const closed = (selector: string) =>
        found(page.querySelector<HTMLElement>(selector), `the region ${selector}`).hidden;
    expect(closed('#advanced-inputs')).toBe(true);
    expect(closed('#examples-and-help')).toBe(true);
    // The two boxes that are the case at its shortest stay on screen.
    expect(page.querySelector('#pattern')).not.toBeNull();
    expect(page.querySelector('#subject')).not.toBeNull();

    // Both disclosures say what they do and what state they are in, which is what `aria-expanded`
    // on the button that controls them is for.
    const disclosures = [...page.querySelectorAll<HTMLElement>('button.disclosure')];
    expect(disclosures.map((button) => button.getAttribute('aria-expanded'))).toEqual(['false', 'false']);

    // Every control that reveals a region names one that exists - the snippet panel's button
    // since chunk 3, which is in the answer and not in this pane.
    for (const button of page.querySelectorAll<HTMLElement>('button[aria-expanded]')) {
        expect(page.querySelector(`#${button.getAttribute('aria-controls')}`)).not.toBeNull();
    }

    found(disclosures[0], 'the inputs disclosure').click();
    await nextTick();
    expect(closed('#advanced-inputs')).toBe(false);

    found(disclosures[1], 'the samples disclosure').click();
    await nextTick();
    expect(closed('#examples-and-help')).toBe(false);
});

/**
 * The two disclosures, so every rule below is asked of both.
 *
 * Each region has its own template ref, its own argument at its own `@click`, and its own line in
 * the watcher, and the asymmetry is not theoretical: with these rules written for the samples panel
 * alone, reverting one call site to `@click="advanced = !advanced"` left the suite green, and so did
 * deleting the `advanced` half of the widen-close. A rule that holds for one region and is never
 * asked of the other is half a rule.
 */
const disclosures = [
    {
        region: 'the samples panel',
        id: 'examples-and-help',
        other: 'advanced-inputs',
        focus: '[role="tab"][aria-selected="true"]',
    },
    { region: 'the secondary inputs', id: 'advanced-inputs', other: 'examples-and-help', focus: '#flags' },
] as const;

/** Drive the width gate the page listens to, starting at `wide`. Returns the handle to change it. */
function stubViewport(wide: boolean): (matches: boolean) => void {
    let change = (matches: boolean): void => void matches;
    vi.stubGlobal('matchMedia', () => ({
        matches: wide,
        addEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) => {
            change = (matches) => listener({ matches } as MediaQueryListEvent);
        },
        removeEventListener() {},
    }));
    return (matches) => change(matches);
}

/**
 * A window that narrows must not take the focus with it.
 *
 * Resize a wide window while a tab has focus and the two regions fold away, `hidden` taking the
 * focused button out of the page. The browser then moves focus to `<body>`, and a keyboard visitor
 * is back at the top of the document with nothing to say why - WCAG 3.2.2 On Input is the rule a
 * layout change that moves focus breaks. Opening whichever disclosure holds the focus keeps the
 * element in the page, so the focus stays where the visitor put it.
 *
 * Simulated through `matchMedia`, which is what the page asks and what a real resize would change.
 *
 * What is asserted is the CONDITION, not the effect, and the difference matters. The browser moves
 * focus to `<body>` because the focused element stopped being rendered; jsdom does not implement
 * that fixup at all, so `document.activeElement` here is whatever was last focused even inside a
 * `hidden` subtree - measured, with the fix disabled: `hidden=true activeElementIsTab=true`. An
 * assertion on `activeElement` would therefore pass with the fix removed and prove nothing. The
 * thing that does differ is whether the focused control is still rendered, which is exactly what
 * decides the browser's behaviour, so that is what the test asks.
 */
test.each(disclosures)(
    'narrowing the window keeps the focused control in the page: $region',
    async ({ id, other, focus }) => {
        const change = stubViewport(true);
        const { page } = await mountPage();

        const control = found(page.querySelector<HTMLElement>(focus), `the focus target in ${id}`);
        control.focus();
        expect(document.activeElement).toBe(control);

        change(false);
        await nextTick();

        expect(found(page.querySelector<HTMLElement>(`#${id}`), `the ${id} region`).hidden).toBe(false);
        expect(control.closest('[hidden]')).toBeNull();
        // And the disclosure that now controls it says it is open, rather than claiming a closed
        // region the reader can see.
        const disclosure = found(
            page.querySelector<HTMLElement>(`button[aria-controls="${id}"]`),
            `the ${id} disclosure`,
        );
        expect(disclosure.getAttribute('aria-expanded')).toBe('true');

        // The other one is untouched: only the region holding the focus opens.
        expect(found(page.querySelector<HTMLElement>(`#${other}`), `the ${other} region`).hidden).toBe(true);
    },
);

/**
 * What the gate opened, the gate closes again.
 *
 * The rule above opens a region without anybody clicking it, so widening the window has to put that
 * back - otherwise maximising and restoring a window leaves the samples panel expanded above the
 * answer, which is the screenful this slice exists to remove. A region the VISITOR opened is left
 * alone: that is their decision and it survives a resize, as it does today.
 */
test.each(disclosures)(
    'a region the gate opened closes again when the window widens: $region',
    async ({ id, focus }) => {
        const change = stubViewport(true);
        const { page } = await mountPage();
        const region = () => found(page.querySelector<HTMLElement>(`#${id}`), `the ${id} region`);

        found(page.querySelector<HTMLElement>(focus), `the focus target in ${id}`).focus();
        change(false);
        await nextTick();
        expect(region().hidden).toBe(false);

        // Wide again, and the focus has moved somewhere outside both regions.
        change(true);
        await nextTick();
        found(page.querySelector<HTMLElement>('#pattern'), 'the pattern field').focus();

        change(false);
        await nextTick();
        expect(region().hidden).toBe(true);
    },
);

/**
 * Touching a disclosure hands it back to the visitor, whoever opened it first.
 *
 * The case is a real one: the gate opens the samples region because the focus was in it, the
 * visitor collapses it, then expands it again because they want it - and it is theirs from that
 * point, so the next widening must not take it away. Ownership that is only ever written by the
 * gate would still be the gate's here, which is what this pins.
 */
test.each(disclosures)(
    'a region the gate opened belongs to the visitor once they touch it: $region',
    async ({ id, focus }) => {
        const change = stubViewport(true);
        const { page } = await mountPage();
        const region = () => found(page.querySelector<HTMLElement>(`#${id}`), `the ${id} region`);
        const disclosure = () =>
            found(page.querySelector<HTMLElement>(`button[aria-controls="${id}"]`), `the ${id} disclosure`);

        found(page.querySelector<HTMLElement>(focus), `the focus target in ${id}`).focus();
        change(false);
        await nextTick();
        expect(region().hidden).toBe(false);

        disclosure().click();
        await nextTick();
        expect(region().hidden).toBe(true);
        disclosure().click();
        await nextTick();
        expect(region().hidden).toBe(false);

        // Wide and narrow again, with the focus nowhere near it. The visitor asked for it open.
        change(true);
        await nextTick();
        found(page.querySelector<HTMLElement>('#pattern'), 'the pattern field').focus();
        change(false);
        await nextTick();
        expect(region().hidden).toBe(false);
    },
);

/**
 * A window that WIDENS must not take the focus with it either, and this is the mirror of the rule
 * above rather than the same rule twice.
 *
 * The disclosure buttons exist only on one column - they are `v-if="!wide"` - so a window crossing
 * the gate upwards removes the very control the visitor has focused, and the browser drops the
 * focus to `<body>`. Narrowing was fixed in chunk 2 by keeping the focused element rendered; that
 * answer is not available here, because the button is not hidden, it is gone: on two columns the
 * region it opened is open for good and a control that says "open this" has nothing left to say.
 *
 * So the focus needs a destination, and the destination is the first control INSIDE the region the
 * button named. It is the nearest thing to where the visitor was: the same region, the same reading
 * order, and a visible focus ring, which a `tabindex="-1"` container would not give them.
 *
 * Unlike the narrowing test, the assertion here is on `document.activeElement`, because jsdom does
 * implement this half: removing the focused element moves the focus to `<body>`. Measured with the
 * fix reverted, both regions: `activeElement` is `BODY`.
 */
test.each(disclosures)(
    'widening the window moves the focus into the region the disclosure opened: $region',
    async ({ id, focus }) => {
        const change = stubViewport(false);
        const { page } = await mountPage();

        const disclosure = found(
            page.querySelector<HTMLElement>(`button[aria-controls="${id}"]`),
            `the ${id} disclosure`,
        );
        disclosure.focus();
        expect(document.activeElement).toBe(disclosure);

        change(true);
        await nextTick();
        await nextTick();

        expect(page.querySelector(`button[aria-controls="${id}"]`), 'the disclosure survives a wide window').toBeNull();
        expect(document.activeElement).toBe(page.querySelector(focus));
    },
);

// The focus is moved only when the widening is what took the control away. A visitor typing in the
// pattern field while their window grows keeps their caret.
test('widening the window leaves a focus outside the disclosures alone', async () => {
    const change = stubViewport(false);
    const { page } = await mountPage();

    const pattern = found(page.querySelector<HTMLElement>('#pattern'), 'the pattern field');
    pattern.focus();

    change(true);
    await nextTick();
    await nextTick();

    expect(document.activeElement).toBe(pattern);
});

// A region the visitor opened is theirs, and a resize does not take it away.
test.each(disclosures)('a region the visitor opened survives the window widening: $region', async ({ id }) => {
    const change = stubViewport(false);
    const { page } = await mountPage();
    const region = () => found(page.querySelector<HTMLElement>(`#${id}`), `the ${id} region`);

    found(page.querySelector<HTMLElement>(`button[aria-controls="${id}"]`), `the ${id} disclosure`).click();
    await nextTick();
    expect(region().hidden).toBe(false);

    change(true);
    await nextTick();
    change(false);
    await nextTick();
    expect(region().hidden).toBe(false);
});

/**
 * Ownership is per region, not a single flag.
 *
 * The gate opens one region because the focus is in it; the visitor then opens the OTHER one for
 * their own reasons. Their press says nothing about the first, so widening still closes it. Written
 * as one "somebody has touched something" flag this passes every rule above and fails here, which is
 * why the test exists: the press clears ownership only of the region pressed.
 */
test.each(disclosures)(
    'pressing one disclosure leaves the other with the gate: $region',
    async ({ id, other, focus }) => {
        const change = stubViewport(true);
        const { page } = await mountPage();
        const hidden = (selector: string) => found(page.querySelector<HTMLElement>(selector), selector).hidden;

        found(page.querySelector<HTMLElement>(focus), `the focus target in ${id}`).focus();
        change(false);
        await nextTick();
        expect(hidden(`#${id}`)).toBe(false);

        found(page.querySelector<HTMLElement>(`button[aria-controls="${other}"]`), `the ${other} disclosure`).click();
        await nextTick();
        expect(hidden(`#${other}`)).toBe(false);

        change(true);
        await nextTick();
        found(page.querySelector<HTMLElement>('#pattern'), 'the pattern field').focus();
        change(false);
        await nextTick();

        expect(hidden(`#${id}`)).toBe(true);
        expect(hidden(`#${other}`)).toBe(false);
    },
);

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

    // Asked of every button that reveals a region, not only of the two that fold this pane: the
    // snippet panel's button opens one in the answer and has the same job of looking pressable.
    const revealers = [...page.querySelectorAll<HTMLElement>('button[aria-expanded]')];
    expect(revealers.filter((button) => button.classList.contains('disclosure'))).toHaveLength(2);
    for (const button of revealers) {
        const chevron = found(button.querySelector('[aria-hidden="true"]'), 'a chevron on a disclosure');
        expect(chevron.textContent?.trim()).toMatch(/\S/);

        // 44 px, from whichever rule dresses it: `.disclosure` on the ink pane, `.button` on white.
        const shape = button.classList.contains('disclosure') ? '.disclosure' : '.button';
        expect(
            new RegExp(`\\${shape}\\s*\\{[^}]*\\bmin-h-11\\b`).test(declarations),
            `${shape} is 44 px tall`,
        ).toBe(true);
    }
});
