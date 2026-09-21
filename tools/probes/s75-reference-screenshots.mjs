// S75. The two reference layouts this slice adds to `docs/demo/`, taken the same way every time.
//
// `page-1280.png` and `page-390.png` show the page as it loads, with an exact match and nothing
// marked. They cannot show what this slice is about: the marker row under a fuzzy match, the legend
// that names the three colours, the alignment view under the groups, and a heading's `(?)` note.
// So these two are taken on a fuzzy case with one note open, at the laptop width the layout tests
// cite and on the phone.
//
// Run: browser_run_code_unsafe, filename = tools/probes/s75-reference-screenshots.mjs, against a
// served publish on 8213 (`tools/probes/serve-demo-publish.mjs`). The `?v=` defeats the static
// server's cache after a rebuild, the same way `s73-widths.mjs` does.

async (page) => {
    // Upstream's own example, and the one the marker row was fixed against: two errors allowed, so
    // every kind of mark is on screen at once. Spelt out with `encodeURIComponent` because the
    // runner that evaluates this file does not define `URLSearchParams`.
    const CASE = Object.entries({
        p: '(?:colour){e<=2}',
        f: '',
        s: 'the color of a colouur, and a calorie',
        m: '',
        r: '',
        l: '',
    })
        .map(([key, value]) => `${key}=${encodeURIComponent(value)}`)
        .join('&');

    const shots = [
        { path: 'docs/demo/alignment-1366.png', width: 1366, height: 768, fullPage: false },
        { path: 'docs/demo/alignment-390.png', width: 390, height: 844, fullPage: true },
    ];

    const taken = [];
    for (const { path, width, height, fullPage } of shots) {
        await page.setViewportSize({ width, height });
        await page.goto(`http://localhost:8213/?v=${Date.now()}#${CASE}`);
        await page.waitForSelector('mark.hit-current', { timeout: 60000 });

        // The pattern's note, opened by a click so that it stays open with nothing focused. A note
        // that was only peeked shuts again the moment the focus leaves the `(?)`.
        await page.click('#heading-help-button-pattern');
        await page.evaluate(() => document.activeElement?.blur());

        await page.screenshot({ path, fullPage, scale: 'css' });
        taken.push({
            path,
            viewport: `${width}x${height}`,
            matches: await page.evaluate(() => document.querySelectorAll('mark.hit').length),
            // What the shot has to show, so a blank region is a number here rather than something
            // to notice by eye later.
            marks: await page.evaluate(() => document.querySelectorAll('.edit').length),
            legend: await page.evaluate(() => document.querySelectorAll('.edit-chip').length),
            alignmentCells: await page.evaluate(() => document.querySelectorAll('.alignment-cell').length),
            noteOpen: await page.evaluate(
                () => document.getElementById('heading-help-pattern')?.hidden === false,
            ),
        });
    }
    return taken;
}
