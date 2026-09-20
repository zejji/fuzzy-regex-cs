// S73 chunk 5e. The two reference layouts in `docs/demo/`, taken the same way every time.
//
// `demo/README.md` links `page-1280.png`, and `page-390.png` is the phone reference the layout tests
// cite. They were taken by hand in S71 and the page has been rebuilt around them since, so they are
// re-taken from a script: 1280x900 (above the layout gate, where the shell is exactly the window)
// and 390x844 (below it, where the page scrolls and the regions become disclosures).
//
// The case is the one the page loads with, because that is what a visitor sees first and what the
// README is captioning. `fullPage` for the phone, which genuinely scrolls; the wide shot is the
// window, which above the gate is the whole page.
//
// Run: browser_run_code_unsafe, filename = tools/probes/s73-reference-screenshots.mjs, against a
// served build (`.scratch/serve` on port 8213). The `?v=` defeats the static server's cache after a
// rebuild, the same way `s73-widths.mjs` does.

async (page) => {
    const shots = [
        { path: 'docs/demo/page-1280.png', width: 1280, height: 900, fullPage: false },
        { path: 'docs/demo/page-390.png', width: 390, height: 844, fullPage: true },
    ];

    const taken = [];
    for (const { path, width, height, fullPage } of shots) {
        await page.setViewportSize({ width, height });
        await page.goto(`http://localhost:8213/?v=${Date.now()}`);
        await page.waitForSelector('mark.hit', { timeout: 60000 });
        // Nothing focused: a ring on whichever control the load happened to leave the focus on is
        // not part of the layout, and it moved between the S71 pair.
        await page.evaluate(() => {
            window.scrollTo(0, 0);
            document.activeElement?.blur();
        });
        await page.screenshot({ path, fullPage, scale: 'css' });
        taken.push({
            path,
            viewport: `${width}x${height}`,
            pattern: await page.inputValue('#pattern'),
            subject: await page.inputValue('#subject'),
            matches: await page.evaluate(() => document.querySelectorAll('mark.hit').length),
        });
    }
    return taken;
}
