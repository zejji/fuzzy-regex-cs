// S73 chunk 5. What the per-edit underlay actually paints, in a real browser.
//
// Two things a unit test cannot answer: what Chrome makes of the OKLCH tokens (the figures in
// `contrast.test.ts` are converted by our own arithmetic, and the BROWSER record there is what
// checks it), and whether the marks land on the characters the engine named without moving the
// subject's own characters.
//
// Run: browser_run_code_unsafe, filename = tools/probes/s73-edit-underlay.mjs, against a served
// build (`.scratch/serve` on port 8213, built from a `dotnet publish` of the demo - the engine has
// to be the one carrying `edits`). One bare function expression, because that tool evaluates the
// file rather than importing it.
//
// The `?v=` is load-bearing: the case lives in the URL hash, so `goto` does not reload the page,
// and a plain static server answers from cache after a rebuild.

async (page) => {
    await page.setViewportSize({ width: 1366, height: 768 });
    await page.goto(`http://localhost:8213/?v=${Date.now()}`);
    await page.waitForSelector('#pattern', { timeout: 60000 });

    // Upstream's answer for this pair (tools/probes/demo-json-contract-expectations.py, regex
    // 2026.9.10): span (0,6), one error of each kind, at subject positions 0, 1 and 6.
    await page.fill('#pattern', '(?:foobar){i<=1,d<=1,s<=1}');
    await page.fill('#subject', 'xfoobat');
    await page.waitForSelector('span.edit', { timeout: 60000 });

    const tokens = await page.evaluate(() => {
        const root = getComputedStyle(document.documentElement);
        const context = document.createElement('canvas').getContext('2d', { willReadFrequently: true });
        const paint = (name) => {
            context.fillStyle = root.getPropertyValue(name).trim();
            context.fillRect(0, 0, 1, 1);
            return [...context.getImageData(0, 0, 1, 1).data].slice(0, 3);
        };
        return {
            '--color-edit-sub': paint('--color-edit-sub'),
            '--color-edit-ins': paint('--color-edit-ins'),
            '--color-edit-del': paint('--color-edit-del'),
        };
    });

    const marks = await page.evaluate(() => {
        const mark = document.querySelector('mark.hit');
        const box = (element) => {
            const rect = element.getBoundingClientRect();
            return [Math.round(rect.x), Math.round(rect.y), Math.round(rect.width), Math.round(rect.height)];
        };
        return {
            text: mark?.textContent,
            label: mark?.getAttribute('aria-label'),
            mark: mark === null ? null : box(mark),
            edits: [...(mark?.querySelectorAll('span.edit') ?? [])].map((edit) => ({
                className: edit.className,
                text: edit.textContent,
                title: edit.getAttribute('title'),
                box: box(edit),
                colour: getComputedStyle(edit).color,
                decoration: getComputedStyle(edit).textDecoration,
                letter: getComputedStyle(edit, '::after').content,
            })),
        };
    });

    return { tokens, marks };
}
