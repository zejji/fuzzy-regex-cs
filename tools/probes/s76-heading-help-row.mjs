// S76 follow-up. Two things about the `(?)` beside each input heading that jsdom cannot answer:
// where its centre sits against the middle of the label's letters, and how much space is left
// between the button and the box below it.
//
// Run: browser_run_code_unsafe, filename = tools/probes/s76-heading-help-row.mjs, against a served
// build on 8213 (`tools/probes/serve-demo-publish.mjs`, or a vite dev server on the same port).
// The `?v=` defeats the cache after a rebuild, as the S73 and S74 probes do.
//
// Measured 2026-09-21 in Chrome at 1366x768 and 390x844.
//   Before, published build 10d1ae3: offset 2.01px, gap 0px, at both widths.
//   After:                           offset 0.01px, gap 4px, at both widths.
// The offset came from `vertical-align: middle` centring the label's MARGIN box rather than its
// letters, and the gap from that same margin sitting inside a line box the 24px button overflowed.

async (page) => {
    const widths = [
        { width: 1366, height: 768 },
        { width: 390, height: 844 },
    ];

    const measured = [];
    for (const { width, height } of widths) {
        await page.setViewportSize({ width, height });
        await page.goto(`http://localhost:8213/?v=${Date.now()}`);
        await page.waitForSelector('.field-heading', { timeout: 60000 });

        const rows = await page.evaluate(() => {
            const read = (id) => {
                const input = document.getElementById(id);
                const heading = input.parentElement.querySelector('.field-heading');
                const label = heading.querySelector('.field-label');
                const button = heading.querySelector('.flag-help-button');
                const [l, b, i] = [label, button, input].map((e) => e.getBoundingClientRect());
                return {
                    id,
                    // Positive means the button rides below the middle of the label's letters.
                    offset: +(b.top + b.height / 2 - (l.top + l.height / 2)).toFixed(2),
                    gap: +(i.top - b.bottom).toFixed(2),
                    lineHeight: getComputedStyle(heading).lineHeight,
                    labelMarginBottom: getComputedStyle(label).marginBottom,
                };
            };
            return ['pattern', 'subject'].map(read);
        });

        measured.push({ width, rows });
    }

    return measured;
}
