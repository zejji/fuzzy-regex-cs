// S73 chunk 5. Does the match table's column header stick?
//
// Chunk 4 found that `.table-scroll` is `overflow-x: auto`, and CSS makes the other axis `auto`
// with it, so `.table-scroll` - not `.results-pane` - is the nearest scrollport for the sticky
// `th`, and `.table-scroll` never scrolls vertically. This probe measures that, then measures the
// candidate fixes by injecting a stylesheet, so the answer comes from the browser rather than from
// a reading of the spec.
//
// Run: the Playwright MCP tool's browser_run_code_unsafe with
//   filename = tools/probes/s73-sticky-column-header.mjs
// against a served build (`.scratch/serve` on port 8213; see the sittings notes). The file is one
// function expression because that tool evaluates it, not imports it.
//
// It restores the page between variants, so the answers are comparable.

async (page) => {
    const base = 'http://localhost:8213';
    const subject = Array(20).fill('the color of the collar in colur and collor').join(' ');
    // The cache buster is load-bearing: the case is carried in the hash, so a second `goto` does not
    // reload, and `python -m http.server` serves the old bundle to a browser that already has it.
    // Without it this probe measures the build before the one you just made.
    const url = `${base}/?v=${Date.now()}#p=${encodeURIComponent('(?:colour){e<=2}')}&f=&s=${encodeURIComponent(subject)}&m=&r=&l=`;
    const variants = [
        ['baseline', ''],
        ['overflow-y: clip', '.table-scroll { overflow-y: clip; }'],
        ['no overflow on .table-scroll', '.table-scroll { overflow-x: visible; overflow-y: visible; }'],
    ];

    await page.setViewportSize({ width: 1366, height: 768 });
    await page.goto(url);
    await page.waitForSelector('tbody.match-rows tr:nth-child(40)', { timeout: 60000 });

    const results = [];
    for (const [name, css] of variants) {
        const handle = css ? await page.addStyleTag({ content: css }) : null;
        // Scroll the region that actually scrolls, then ask where the header ended up.
        const measured = await page.evaluate(() => {
            const pane = document.querySelector('.results-pane');
            const scroll = document.querySelector('.table-scroll');
            const th = document.querySelector('.data-table th');
            pane.scrollTop = 0;
            const paneTop = Math.round(pane.getBoundingClientRect().top);
            const before = Math.round(th.getBoundingClientRect().top);
            pane.scrollTop = 1500;
            const after = Math.round(th.getBoundingClientRect().top);
            const style = getComputedStyle(scroll);
            return {
                paneScrolls: pane.scrollHeight > pane.clientHeight,
                paneTop,
                tableScrollOverflow: [style.overflowX, style.overflowY],
                tableScrollScrolls: scroll.scrollHeight > scroll.clientHeight,
                thTopAt0: before,
                thTopAt1500: after,
                // The test of sticking: after the scroll the header is still at the top of the pane.
                sticks: after >= paneTop - 1 && after < paneTop + 40,
                onScreen: after >= 0 && after <= window.innerHeight,
            };
        });
        if (handle) await handle.evaluate((el) => el.remove());
        results.push({ variant: name, ...measured });
    }
    await page.evaluate(() => {
        document.querySelector('.results-pane').scrollTop = 0;
    });
    return results;
}
