/**
 * Every colour pair the page paints, measured rather than asserted in a comment.
 *
 * The tokens are read out of `styles.css` and out of Tailwind's own theme, so this fails when a
 * token moves - which is the thing a recorded figure cannot do. WCAG 2.2 wants 4.5:1 for body
 * text (1.4.3), 3:1 for text at 18.66 px bold or 24 px (1.4.3 again), and 3:1 for the borders and
 * focus rings that carry meaning (1.4.11 Non-text Contrast, 2.4.13 Focus Appearance).
 *
 * The ink pane is why the list is long: half the page sits on a dark surface as of S73, so every
 * pair on it is new. The conversion behind the numbers is `colour.ts`, checked against a real
 * browser the day it was written.
 */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { expect, test } from 'vitest';

import { bytes, contrast, parseColour } from './colour';

const read = (relative: string): string => readFileSync(join(import.meta.dirname, relative), 'utf8');

/** `--name: value;` out of a stylesheet, for both our tokens and Tailwind's. */
function tokens(source: string): Record<string, string> {
    return Object.fromEntries(
        [...source.matchAll(/^\s*(--color-[\w-]+):\s*([^;]+);/gm)].map((match) => [match[1] ?? '', (match[2] ?? '').trim()]),
    );
}

const PALETTE: Record<string, string> = {
    ...tokens(read('../node_modules/tailwindcss/theme.css')),
    ...tokens(read('../src/styles.css')),
};

const of = (name: string): string => {
    const value = PALETTE[name];
    if (value === undefined) throw new Error(`no token named ${name}`);
    return value;
};

/**
 * The pairs, each with the floor its use asks for and a word on where it is painted.
 *
 * A pair missing from this table is a pair nobody has measured, which is why the test below also
 * counts the tokens: a new `--color-` in `styles.css` that never appears here fails.
 */
const PAIRS: readonly (readonly [string, string, number, string])[] = [
    // The ink surface: the header, the input pane and the footer.
    ['--color-shell-text', '--color-shell', 4.5, 'text on the ink pane'],
    ['--color-shell-muted', '--color-shell', 4.5, 'labels and hints on the ink pane'],
    ['--color-shell-text', '--color-shell-raised', 4.5, 'text in a field on the ink pane'],
    ['--color-shell-muted', '--color-shell-raised', 4.5, 'a hint inside a raised panel'],
    ['--color-accent-bright', '--color-shell', 4.5, 'a link in the footer, and the focus ring'],
    ['--color-accent-bright', '--color-shell-raised', 4.5, 'the selected tab, and a focus ring on a field'],
    ['--color-shell-edge', '--color-shell', 3, 'the border of a field: 1.4.11'],
    ['--color-shell-edge', '--color-shell-raised', 3, 'the border of a raised panel: 1.4.11'],

    // The results region, which is light.
    ['--color-slate-900', '--color-white', 4.5, 'the answer'],
    ['--color-accent', '--color-white', 4.5, 'a link, and the focus ring, on light'],
    ['--color-accent', '--color-accent-soft', 4.5, 'the row the pointer is over, and the tab panel'],
    ['--color-slate-600', '--color-white', 4.5, 'a column heading'],

    // The three kinds of edit, each also carrying its letter, because a red-green reader separates
    // amber from red by the letter and not by the hue.
    ['--color-edit-sub', '--color-edit-sub-soft', 4.5, 'the substitutions chip'],
    ['--color-edit-ins', '--color-edit-ins-soft', 4.5, 'the insertions chip'],
    ['--color-edit-del', '--color-edit-del-soft', 4.5, 'the deletions chip'],
    ['--color-edit-sub', '--color-white', 4.5, 'a substitution count in the table'],
    ['--color-edit-ins', '--color-white', 4.5, 'an insertion count in the table'],
    ['--color-edit-del', '--color-white', 4.5, 'a deletion count in the table'],

    // The match highlights, measured in S72 and held here so the figures cannot drift silently.
    ['--color-slate-900', '--color-hit-a', 4.5, 'the subject inside a match'],
    ['--color-slate-900', '--color-hit-b', 4.5, 'the subject inside the next match along'],
    ['--color-hit-edge', '--color-hit-a', 3, "a match's own border: 1.4.11"],
];

test.each(PAIRS)('%s on %s clears %s:1 - %s', (foreground, background, floor) => {
    expect(contrast(of(foreground), of(background))).toBeGreaterThanOrEqual(floor);
});

// A token that is out of the sRGB gamut is painted by the browser as something other than the
// colour written down, and every figure above would then be measuring a colour nobody sees.
test('every token the page defines is inside sRGB', () => {
    for (const [name, value] of Object.entries(tokens(read('../src/styles.css')))) {
        expect(parseColour(value).clipped, `${name} is outside sRGB`).toBe(false);
    }
});

/**
 * Every token the page defines is in the table above.
 *
 * The gap this closes: a colour added without a measured partner. The pairs are the record of what
 * was measured, so a token missing from them is a colour nobody has held to a floor.
 */
test('no colour is defined without a measured pair', () => {
    const measured = new Set(PAIRS.flatMap(([foreground, background]) => [foreground, background]));
    expect([...Object.keys(tokens(read('../src/styles.css')))].filter((name) => !measured.has(name))).toEqual([]);
});

/** The `bg-` and `text-` a rule applies, as token names. `undefined` where the rule sets neither. */
function surface(selector: string): { background: string | undefined; text: string | undefined } {
    const source = read('../src/styles.css');
    const rule = new RegExp(`(?:^|\\n)\\s*${selector.replace('.', '\\.')}\\s*\\{([^}]*)\\}`).exec(source)?.[1];
    if (rule === undefined) throw new Error(`no rule for ${selector}`);
    const named = (kind: string): string | undefined => {
        const utility = new RegExp(`@apply[^;]*\\b${kind}-([\\w-]+)`).exec(rule)?.[1];
        return utility === undefined ? undefined : `--color-${utility}`;
    };
    return { background: named('bg'), text: named('text') };
}

/**
 * The colour a word inherits is measured against the surface it lands on.
 *
 * Found by looking at the built page and not by a test: the three mode radios had labels nobody
 * could read, because `body` painted the ink shell but set the white side's `text-slate-900`. Every
 * word that does not name its own colour takes the one from the region above it, so those two rules
 * are the ones the rest of the page falls back to, and both belong in the measured table.
 */
test.each([
    ['body', 'everything on the ink that names no colour of its own'],
    ['.results-pane', 'everything on the white island that names no colour of its own'],
])('%s inherits a measured pair down to %s', (selector) => {
    const { background, text } = surface(selector);
    expect(background, `${selector} paints no surface`).toBeDefined();
    expect(text, `${selector} names no ink`).toBeDefined();
    expect(
        PAIRS.filter(([foreground, behind]) => foreground === text && behind === background),
        `${text} on ${background} is not in the measured table`,
    ).not.toEqual([]);
});

/**
 * What Chrome actually paints each token, recorded 2026-09-19 from Chrome 153 (Playwright), serving
 * the published demo. Read back through a canvas, which is the browser's own OKLCH to sRGB with its
 * own gamut mapping:
 *
 *   const root = getComputedStyle(document.documentElement);
 *   const ctx = document.createElement('canvas').getContext('2d', { willReadFrequently: true });
 *   ctx.fillStyle = root.getPropertyValue('--color-shell').trim();
 *   ctx.fillRect(0, 0, 1, 1);
 *   ctx.getImageData(0, 0, 1, 1).data;   // [19, 25, 34]
 *
 * Without this the ratios above are this file's own arithmetic checked against itself. Re-record it
 * by running that snippet over the token names when a token changes.
 */
const BROWSER: Readonly<Record<string, readonly [number, number, number]>> = {
    '--color-accent': [4, 98, 211],
    '--color-accent-bright': [116, 166, 239],
    '--color-accent-soft': [234, 242, 254],
    '--color-shell': [19, 25, 34],
    '--color-shell-raised': [32, 39, 48],
    '--color-shell-edge': [109, 117, 128],
    '--color-shell-text': [243, 245, 249],
    '--color-shell-muted': [179, 187, 200],
    '--color-hit-a': [254, 221, 151],
    '--color-hit-b': [241, 196, 94],
    '--color-hit-edge': [143, 107, 9],
    '--color-edit-sub': [141, 93, 28],
    '--color-edit-ins': [25, 112, 55],
    '--color-edit-del': [179, 34, 40],
    '--color-edit-sub-soft': [251, 232, 211],
    '--color-edit-ins-soft': [208, 247, 214],
    '--color-edit-del-soft': [254, 228, 226],
};

test.each(Object.entries(BROWSER))('%s is converted to the colour a browser paints', (name, painted) => {
    // One byte of slack, and no more: the rounding at the end of the conversion is the only thing
    // that may differ, and a whole channel out is a different colour.
    for (const [channel, value] of bytes(of(name)).entries()) {
        expect(Math.abs(value - painted[channel]!), `${name} channel ${channel}`).toBeLessThanOrEqual(1);
    }
});

// The record above names every token the page defines, so a new one cannot arrive unmeasured.
test('every token the page defines has been painted by a browser', () => {
    expect(Object.keys(tokens(read('../src/styles.css'))).filter((name) => !(name in BROWSER))).toEqual([]);
});

// The converter itself, against the two colours whose sRGB nobody has to look up.
test('the conversion agrees with the colours everyone knows', () => {
    expect(bytes('oklch(1 0 0)')).toEqual([255, 255, 255]);
    expect(bytes('oklch(0 0 0)')).toEqual([0, 0, 0]);
    expect(bytes('#fff')).toEqual([255, 255, 255]);
    expect(contrast('#fff', '#000')).toBe(21);
});
