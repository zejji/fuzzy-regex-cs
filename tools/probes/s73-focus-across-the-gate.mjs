// S73 chunk 5. Where the focus goes when the window crosses the shell's gate, in a real browser.
//
// jsdom answers half of this and not the other half: it does move the focus to `<body>` when the
// focused element is REMOVED (the widening case, where the disclosure buttons are `v-if="!wide"`),
// and it does nothing at all when the focused element is merely `hidden` (the narrowing case). So
// the unit tests assert the condition in one direction and the effect in the other, and the browser
// is what settles both at once.
//
// Run: browser_run_code_unsafe, filename = tools/probes/s73-focus-across-the-gate.mjs, against a
// served build (`.scratch/serve` on port 8213). One function expression, because that tool
// evaluates the file rather than importing it.
//
// Reading it: `afterWidening` should name a control inside the region the disclosure opened - the
// selected tab, or the flags field - and never BODY. With the `keepFocusInsideTheRegion()` call
// removed from App.vue it reads BODY, which is the control that makes the rest mean something.

async (page) => {
    const base = 'http://localhost:8213';
    const url = `${base}/?v=${Date.now()}`;
    const name = () =>
        page.evaluate(() => {
            const el = document.activeElement;
            if (el === null) return 'null';
            const id = el.id === '' ? '' : `#${el.id}`;
            const controls = el.getAttribute?.('aria-controls');
            return `${el.tagName}${id}${controls ? `[aria-controls=${controls}]` : ''}`;
        });

    const out = [];
    for (const region of ['advanced-inputs', 'examples-and-help']) {
        // Start narrow, where the disclosures exist at all.
        await page.setViewportSize({ width: 390, height: 844 });
        await page.goto(url);
        await page.waitForSelector(`button[aria-controls="${region}"]`, { timeout: 60000 });
        await page.focus(`button[aria-controls="${region}"]`);
        const before = await name();

        // Widen across the gate: the button the visitor is standing on stops existing.
        await page.setViewportSize({ width: 1366, height: 768 });
        await page.waitForTimeout(150);
        const afterWidening = await name();

        // And the other direction, from a control inside the region that folds away.
        await page.focus(region === 'advanced-inputs' ? '#flags' : '[role="tab"][aria-selected="true"]');
        const beforeNarrowing = await name();
        await page.setViewportSize({ width: 390, height: 844 });
        await page.waitForTimeout(150);
        const afterNarrowing = await name();
        const regionOpen = await page.evaluate(
            (id) => document.getElementById(id)?.hidden === false,
            region,
        );

        out.push({ region, before, afterWidening, beforeNarrowing, afterNarrowing, regionOpen });
    }
    return out;
}
