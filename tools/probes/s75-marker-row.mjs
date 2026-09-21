// S75. Where the `s`, `i` and `d` letters are actually drawn, which jsdom cannot answer: it has no
// layout, so a test there can say the letter exists and never that it collides with the border
// above it. The owner's finding was entirely about those few pixels.
//
// Three distances per marker, all in CSS pixels and all measured off real boxes:
//
//   underlineToLetter  the run's box bottom (where the wavy line sits) to the letter's top
//   letterToBorder     the highlight's border bottom to the letter's top; NEGATIVE means the
//                      letter is drawn through the border, which is what -8px did
//   letterToNextLine   the letter's bottom to the top of the line box below it; NEGATIVE means it
//                      is drawn into the next line of the subject
//
// Each viewport is measured twice. "before" injects the geometry S73 shipped - a 28 px line and the
// letter 8 px under the run - so the owner's finding is a number here and not a memory, and
// "after" is whatever the stylesheet currently says. Two subjects: one with substitutions and
// insertions, and the owner's `(foobar){e}` case for the counted gaps.
//
// Run: browser_run_code_unsafe, filename = tools/probes/s75-marker-row.mjs, against a served
// publish on 8213:
//
//   pwsh -File tools/run-wasm-smoke.ps1 -OutDir .scratch/s75-publish
//   node tools/probes/serve-demo-publish.mjs --root .scratch/s75-publish/wwwroot
//
// The widths are S73's two: 1366x768, the commonest desktop viewport, and 390x844, the phone the
// layout tests cite.

async (page) => {
    const cases = [
        {
            name: 'sub+ins',
            pattern: '(?:colour){e<=3}',
            // Long enough to wrap at BOTH widths, so "letter to next line" is measured against a
            // real line below. The pane is 90ch at 1366, so this must pass 90 characters.
            subject:
                'the colour of the clour and the c0lour and the colouur and the colour again, and one more clour at the end of it all',
        },
        {
            name: 'del',
            // The owner's case. Its last two matches spend 5 and 6 deletions, all in one place.
            pattern: '(foobar){e}',
            subject: 'xirefoabralfobarxie',
        },
    ];

    // The geometry S73 shipped, re-applied over the built stylesheet.
    const before = `
        .subject-pane.has-markers { line-height: 1.75rem; }
        .edit::after { bottom: -8px; left: 0; transform: none; }
    `;

    const measure = () =>
        page.evaluate(() => {
            const letterBox = (element) => {
                // getComputedStyle's second argument reads the pseudo-element. `content` proves the
                // letter is really there; the box is derived from the run's own rect, because a
                // pseudo-element has no node to call getBoundingClientRect on.
                //
                // The letter is absolutely positioned in the run's own box (`.edit` is relative),
                // so a `bottom` of -13px puts its BOTTOM edge 13px BELOW the run's bottom - the
                // offset is subtracted from the containing block's bottom, not added to it.
                const after = getComputedStyle(element, '::after');
                const run = element.getBoundingClientRect();
                const height = parseFloat(after.height);
                const bottom = run.bottom - parseFloat(after.bottom);
                return { content: after.content, top: bottom - height, bottom };
            };

            const pane = document.querySelector('.subject-pane');

            // The pane's LINE boxes. `pane.getClientRects()` is the block's own single box; a range
            // over its contents is what reports one rect per rendered line.
            const range = document.createRange();
            range.selectNodeContents(pane);
            const lines = [...range.getClientRects()];

            const rows = [...document.querySelectorAll('mark.hit .edit')].map((edit) => {
                const run = edit.getBoundingClientRect();
                const letter = letterBox(edit);

                // The border on the run's OWN line. A highlight that wraps has one client rect per
                // line, and its bounding box runs from the first line's top to the last line's
                // bottom - so measuring against that box says a marker on line one is 38 px above
                // "the border", which is true of a box nobody draws. The rect the run sits in is
                // the border the reader sees under it.
                const rects = [...edit.closest('mark.hit').getClientRects()];
                const border =
                    rects.find((rect) => rect.top <= run.top + 1 && rect.bottom >= run.bottom - 1) ??
                    rects[0];
                const below = lines
                    .map((rect) => rect.top)
                    .filter((top) => top > letter.top + 1)
                    .sort((a, b) => a - b)[0];

                return {
                    letter: letter.content.replaceAll('"', ''),
                    underlineToLetter: +(letter.top - run.bottom).toFixed(1),
                    letterToBorder: +(letter.top - border.bottom).toFixed(1),
                    letterToNextLine: below === undefined ? null : +(below - letter.bottom).toFixed(1),
                };
            });

            // One line per distinct geometry rather than one per marker: every `s` on the page is
            // drawn by the same rule, and six identical rows are six chances to misread the one
            // that matters.
            const shapes = new Map();
            for (const row of rows) {
                const { letter, ...geometry } = row;
                const key = JSON.stringify(geometry);
                const seen = shapes.get(key);
                if (seen === undefined) shapes.set(key, { letters: [letter], markers: 1, ...geometry });
                else {
                    seen.markers++;
                    if (!seen.letters.includes(letter)) seen.letters.push(letter);
                }
            }

            return {
                lineHeight: getComputedStyle(pane).lineHeight,
                markers: rows.length,
                shapes: [...shapes.values()],
            };
        });

    const measured = [];

    for (const size of [
        { width: 1366, height: 768 },
        { width: 390, height: 844 },
    ]) {
        await page.setViewportSize(size);

        for (const { name, pattern, subject } of cases) {
            await page.goto(
                `http://localhost:8213/?v=${Date.now()}#p=${encodeURIComponent(pattern)}&s=${encodeURIComponent(subject)}`,
            );
            await page.waitForSelector('mark.hit .edit', { timeout: 60_000 });

            const after = await measure();

            const id = await page.evaluate((css) => {
                const style = document.createElement('style');
                style.id = 's75-before';
                style.textContent = css;
                document.head.append(style);
                return style.id;
            }, before);
            const was = await measure();
            await page.evaluate((styleId) => document.getElementById(styleId).remove(), id);

            measured.push({ width: size.width, case: name, before: was, after });
        }
    }

    return measured;
}
