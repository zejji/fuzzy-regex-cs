// S73 chunk 5. What the sticky-header fix costs at each width.
//
// The fix under test: drop `.table-scroll`'s overflow inside the fixed-shell gate, so the nearest
// scrollport for the sticky `th` becomes `.results-pane`, which is the region that actually
// scrolls. Outside the gate the document scrolls and the table keeps its own horizontal scroller.
//
// The cost to measure is horizontal: with no scroller of its own, a table wider than the pane has
// to be scrolled by something else. This asks who, at each of the five widths the slice checks.
//
// Run: browser_run_code_unsafe, filename = tools/probes/s73-sticky-header-fix-at-five-widths.mjs,
// against a served build (`.scratch/serve` on port 8213). One function expression, because that
// tool evaluates the file rather than importing it.

async (page) => {
    const base = 'http://localhost:8213';
    const subject = Array(20).fill('the color of the collar in colur and collor').join(' ');
    // The cache buster is load-bearing: the case is carried in the hash, so a second `goto` does not
    // reload, and `python -m http.server` serves the old bundle to a browser that already has it.
    const url = `${base}/?v=${Date.now()}#p=${encodeURIComponent('(?:colour){e<=2}')}&f=&s=${encodeURIComponent(subject)}&m=&r=&l=`;
    const gated =
        '@media (min-width: 64rem) and (min-height: 600px) {' +
        '.table-scroll { overflow-x: visible; overflow-y: visible; } }';
    const widths = [
        [1920, 1080],
        [1440, 900],
        [1366, 768],
        [1024, 768],
        [390, 844],
    ];

    const out = [];
    for (const [width, height] of widths) {
        for (const variant of ['baseline', 'gated fix']) {
            await page.setViewportSize({ width, height });
            await page.goto(url);
            await page.waitForSelector('tbody.match-rows tr:nth-child(40)', { timeout: 60000 });
            const handle = variant === 'gated fix' ? await page.addStyleTag({ content: gated }) : null;
            const measured = await page.evaluate(() => {
                const pane = document.querySelector('.results-pane');
                const scroll = document.querySelector('.table-scroll');
                const th = document.querySelector('.data-table th');
                const doc = document.documentElement;
                const scroller = pane.scrollHeight > pane.clientHeight ? pane : doc;
                scroller.scrollTop = 0;
                const anchorTop = Math.round(
                    (scroller === pane ? pane.getBoundingClientRect().top : 0),
                );
                scroller.scrollTop = 1500;
                const after = Math.round(th.getBoundingClientRect().top);
                const style = getComputedStyle(scroll);
                const answer = {
                    verticalScroller: scroller === pane ? '.results-pane' : 'document',
                    tableScrollOverflow: [style.overflowX, style.overflowY],
                    thTopAfterScroll: after,
                    sticks: after >= anchorTop - 1 && after < anchorTop + 40,
                    // The cost: who, if anyone, has to scroll sideways for the six columns.
                    docScrollsX: doc.scrollWidth > doc.clientWidth,
                    paneScrollsX: pane.scrollWidth > pane.clientWidth,
                    tableScrollScrollsX: scroll.scrollWidth > scroll.clientWidth,
                    tableWidth: Math.round(scroll.querySelector('table').getBoundingClientRect().width),
                    paneWidth: Math.round(pane.clientWidth),
                };
                scroller.scrollTop = 0;
                return answer;
            });
            if (handle) await handle.evaluate((el) => el.remove());
            out.push({ window: `${width}x${height}`, variant, ...measured });
        }
    }
    return out;
}
