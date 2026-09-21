// S75. Can a real pointer get from a heading's `(?)` to the note it opened?
//
// WCAG 1.4.13 "Hoverable" asks that it can. The note is a line under the heading row, not a layer
// against the button, so the pointer crosses a gap of a few pixels where it is over neither, and
// the note used to close on the button's `mouseleave` before the pointer arrived. `PEEK_GRACE_MS`
// in `demo/web/src/lib/help-notes.ts` is the wait that fixes it; this measures the gap and walks
// the pointer across it at a human speed.
//
// Run: `pwsh -File tools/run-wasm-smoke.ps1 -SkipWebBuild` for the publish, then
// `node tools/probes/serve-demo-publish.mjs --root demo/FuzzyRegex.Demo.Wasm/bin/Release/net10.0/publish/wwwroot`,
// then browser_run_code_unsafe with filename = tools/probes/s75-hover-travel.mjs.
//
// Chrome, 1366x768, 2026-09-21. Before the grace, against a publish of the same page built without
// it: `{"gap":4,"travelMs":332,"onButton":true,"onArrival":false,...}` - the note was gone before
// the pointer had crossed four pixels. After: `{"gap":4,"travelMs":337,"onButton":true,
// "onArrival":true,"whileReading":true,"afterLeaving":false}`, and the same again on the publish
// that adds the note-under-the-pointer rule: `{"gap":4,"travelMs":330,...,"onArrival":true,
// "whileReading":true,"afterLeaving":false}`.

async (page) => {
    await page.setViewportSize({ width: 1366, height: 768 });
    await page.goto(`http://localhost:8213/?v=${Date.now()}`);
    await page.waitForSelector('mark.hit', { timeout: 60000 });

    const button = await page.locator('#heading-help-button-pattern').boundingBox();
    const open = () => page.evaluate(() => document.getElementById('heading-help-pattern')?.hidden === false);

    // On the `(?)`, which opens the note.
    await page.mouse.move(button.x + button.width / 2, button.y + button.height / 2);
    await page.waitForTimeout(100);
    const onButton = await open();

    const note = await page.locator('#heading-help-pattern').boundingBox();
    const gap = Math.round(note.y - (button.y + button.height));

    // Across the gap, in twenty steps with a pause between: about 200 ms for the whole journey,
    // which is the slow end of a hand moving a few pixels.
    const started = Date.now();
    for (let step = 1; step <= 20; step += 1) {
        const y = button.y + button.height / 2 + ((note.y + note.height / 2 - button.y - button.height / 2) * step) / 20;
        await page.mouse.move(button.x + button.width / 2, y);
        await page.waitForTimeout(10);
    }
    const travelMs = Date.now() - started;
    const onArrival = await open();

    // Resting on the note, for longer than the grace.
    await page.waitForTimeout(1000);
    const whileReading = await open();

    // Away from both, which is the close the grace was delaying.
    await page.mouse.move(10, 10);
    await page.waitForTimeout(1000);
    const afterLeaving = await open();

    return { gap, travelMs, onButton, onArrival, whileReading, afterLeaving };
}
