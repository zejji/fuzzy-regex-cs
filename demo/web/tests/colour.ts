/**
 * OKLCH to sRGB, and the WCAG contrast ratio between two colours.
 *
 * Here so that a contrast figure is a TEST rather than a sentence in a comment. Every measurement
 * this project has recorded so far was taken once, by hand, through a canvas in a real browser
 * (see the notes in `styles.css`), which proves the colour pair of that afternoon and nothing
 * about the one somebody edits next week.
 *
 * The conversion is CSS Color 4's, section 9 (OKLab) and section 10 (the sRGB transfer function):
 * <https://www.w3.org/TR/css-color-4/#color-conversion-code>. The ratio is WCAG 2.2's, in
 * <https://www.w3.org/TR/WCAG22/#dfn-contrast-ratio>, over the relative luminance defined in
 * <https://www.w3.org/TR/WCAG22/#dfn-relative-luminance>.
 *
 * Checked against a real browser, and the check is itself a test: `contrast.test.ts` holds what
 * Chrome 153 painted every token on 2026-09-19, read back off a canvas, and asserts this conversion
 * lands within one 8-bit step of it. The snippet that produced those bytes is in the comment above
 * the table, so it can be re-run when a token moves.
 */

/** A colour as the browser would paint it: three channels, 0 to 1, after gamut clipping. */
export interface Srgb {
    readonly r: number;
    readonly g: number;
    readonly b: number;
    /** True when a channel had to be clipped, so the ratio below is the clipped colour's. */
    readonly clipped: boolean;
}

const cube = (x: number): number => x * x * x;

/** `oklch(L C H)`, with L as either a fraction or a percentage, and `#rgb` / `#rrggbb`. */
export function parseColour(text: string): Srgb {
    const hex = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(text.trim());
    if (hex !== null) {
        const digits = hex[1] ?? '';
        const wide = digits.length === 6;
        const channel = (i: number): number =>
            parseInt(wide ? digits.slice(i * 2, i * 2 + 2) : (digits[i] ?? '0').repeat(2), 16) / 255;
        return { r: channel(0), g: channel(1), b: channel(2), clipped: false };
    }

    const oklch = /^oklch\(\s*([\d.]+)(%?)\s+([\d.]+)\s+([\d.]+)\s*\)$/.exec(text.trim());
    if (oklch === null) throw new Error(`not a colour this understands: ${text}`);

    const lightness = Number(oklch[1]) / (oklch[2] === '%' ? 100 : 1);
    const chroma = Number(oklch[3]);
    const hue = (Number(oklch[4]) * Math.PI) / 180;
    return fromOklab(lightness, chroma * Math.cos(hue), chroma * Math.sin(hue));
}

function fromOklab(lightness: number, a: number, b: number): Srgb {
    const l = cube(lightness + 0.3963377774 * a + 0.2158037573 * b);
    const m = cube(lightness - 0.1055613458 * a - 0.0638541728 * b);
    const s = cube(lightness - 0.0894841775 * a - 1.291485548 * b);

    const linear = [
        4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
        -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
        -0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s,
    ];

    // Clipped, not gamut-mapped: a colour this project uses is inside sRGB, and `inGamut` below is
    // what keeps it that way. Reporting the clip rather than hiding it means a token that drifts
    // out of the gamut fails a test instead of quietly measuring as something else.
    const clipped = linear.some((value) => value < -0.0001 || value > 1.0001);
    const [r, g, blue] = linear.map(encode) as [number, number, number];
    return { r, g, b: blue, clipped };
}

/** The sRGB transfer function, then the clip. */
const encode = (value: number): number => {
    const encoded = value <= 0.0031308 ? 12.92 * value : 1.055 * Math.pow(Math.abs(value), 1 / 2.4) - 0.055;
    return Math.min(1, Math.max(0, encoded));
};

/** WCAG relative luminance. */
const luminance = ({ r, g, b }: Srgb): number => {
    const channel = (value: number): number =>
        value <= 0.04045 ? value / 12.92 : Math.pow((value + 0.055) / 1.055, 2.4);
    return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b);
};

/** The WCAG contrast ratio, 1 to 21, rounded to two places so a test can state a number. */
export function contrast(foreground: string, background: string): number {
    const [light, dark] = [parseColour(foreground), parseColour(background)]
        .map(luminance)
        .sort((first, second) => second - first) as [number, number];
    return Math.round(((light + 0.05) / (dark + 0.05)) * 100) / 100;
}

/** 0 to 255, for comparing against what a browser reports. */
export const bytes = (text: string): [number, number, number] => {
    const { r, g, b } = parseColour(text);
    return [r, g, b].map((value) => Math.round(value * 255)) as [number, number, number];
};
