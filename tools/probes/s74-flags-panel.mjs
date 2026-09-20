// S74. What the flags panel actually does in a browser, which jsdom cannot answer: how tall the shut
// row is, how many columns the grid finds at each width, and whether a help sentence stays inside
// the panel on a phone.
//
// Run: browser_run_code_unsafe, filename = tools/probes/s74-flags-panel.mjs, against a served
// publish on 8213 (`tools/probes/serve-demo-publish.mjs`). The `?v=` defeats the cache after a
// rebuild, the same way `s73-widths.mjs` does.
//
// The three widths are S73's: 1366x768 and 1440x900 are the two commonest desktop viewports, and
// 390x844 is the phone the layout tests cite.

async (page) => {
    const widths = [
        { width: 1366, height: 768 },
        { width: 1440, height: 900 },
        { width: 1920, height: 1080 },
        { width: 390, height: 844 },
    ];

    const measured = [];
    for (const { width, height } of widths) {
        await page.setViewportSize({ width, height });
        await page.goto(`http://localhost:8213/?v=${Date.now()}`);
        await page.waitForSelector('mark.hit', { timeout: 60000 });
        await page.evaluate(() => {
            window.scrollTo(0, 0);
            document.activeElement?.blur();
        });

        // On one column the panel lives inside a disclosure that is shut, so open that first.
        await page.evaluate(() => {
            const gate = document.querySelector('button[aria-controls="advanced-inputs"]');
            if (gate instanceof HTMLElement && gate.getAttribute('aria-expanded') === 'false') gate.click();
        });

        const shut = await page.evaluate(() => {
            const summary = document.querySelector('#flags-panel > summary');
            const chosen = document.querySelector('.flags-chosen');
            const line = Number.parseFloat(getComputedStyle(chosen).lineHeight);
            return {
                rowHeight: summary.getBoundingClientRect().height,
                lineHeight: line,
                says: chosen.textContent.trim(),
                // One line high is the claim, so it is measured as a ratio and not eyeballed.
                lines: Math.round(chosen.getBoundingClientRect().height / line),
            };
        });

        await page.screenshot({
            path: `.scratch/s74-shots/flags-shut-${width}x${height}.png`,
            fullPage: width < 1000,
            scale: 'css',
        });

        // Open it, tick three flags so the shut row has something long to say, and open a help.
        await page.evaluate(() => {
            document.querySelector('#flags-panel').open = true;
            for (const id of ['flag-IgnorePatternWhitespace', 'flag-EnhanceMatch', 'flag-Ascii']) {
                document.getElementById(id).click();
            }
            document.getElementById('flag-help-button-Ascii').click();
        });
        // Vue renders on a microtask, so everything below would otherwise be measured against the
        // DOM as it was before the clicks - which is how the first run of this probe read `none` on
        // a row that had three flags on it.
        await page.waitForTimeout(100);

        const open = await page.evaluate(() => {
            const panel = document.querySelector('#flags-panel');
            const grid = document.querySelector('.flag-grid');
            const tops = [...grid.children].map((child) => Math.round(child.getBoundingClientRect().top));
            const help = document.getElementById('flag-help-Ascii');
            const panelBox = panel.getBoundingClientRect();
            const helpBox = help.getBoundingClientRect();

            // What a column has to be able to hold: the widest row laid out on one line, which is
            // what decides whether a second column can exist at all in a pane this narrow.
            //
            // The label's width is taken from a Range over its text and not from the element, which
            // is `flex-1` and therefore always exactly as wide as its column - the first run of this
            // probe reported the column width back as the content width and said nothing.
            const widestRow = Math.max(
                ...[...panel.querySelectorAll('.flag-row')].map((row) => {
                    const name = row.querySelector('.flag-name');
                    const text = document.createRange();
                    text.selectNodeContents(name);
                    const others = [...row.children]
                        .filter((child) => child !== name)
                        .reduce((all, child) => all + child.getBoundingClientRect().width, 0);
                    return Math.ceil(text.getBoundingClientRect().width + others + 8 * row.children.length);
                }),
            );

            return {
                // Children sharing a top are a row, so the count of them is the column count.
                columns: tops.filter((top) => top === tops[0]).length,
                gridWidth: Math.round(grid.getBoundingClientRect().width),
                widestRow,
                panelHeight: Math.round(panel.getBoundingClientRect().height),
                says: document.querySelector('.flags-chosen').textContent.trim(),
                helpInsidePanel:
                    helpBox.left >= panelBox.left - 0.5 && helpBox.right <= panelBox.right + 0.5,
                helpOnScreen: helpBox.left >= 0 && helpBox.right <= window.innerWidth,
                // Nothing in this panel is allowed to leave the flow.
                positioned: [...panel.querySelectorAll('*')]
                    .filter((node) => getComputedStyle(node).position !== 'static')
                    .map((node) => node.className || node.tagName),
                shownHelp: [...panel.querySelectorAll('.flag-help')].filter((node) => !node.hidden).length,
            };
        });

        await page.screenshot({
            path: `.scratch/s74-shots/flags-open-${width}x${height}.png`,
            fullPage: width < 1000,
            scale: 'css',
        });

        measured.push({ viewport: `${width}x${height}`, shut, open });
    }

    return measured;
}
