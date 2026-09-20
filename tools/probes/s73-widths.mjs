// S73 chunk 5d. The demo at five widths, measured in one pass.
//
// What it answers, at 1920x1080, 1440x900, 1366x768, 1024x768 and 390x844: does the page scroll
// sideways, is the answer above the fold, does the shell fit the window above the gate, is the gate
// where the stylesheet says it is, and does the keyboard reach twenty controls with a visible focus
// ring at each one. It also records what Chrome paints for every colour token, which is what the
// `BROWSER` table in `contrast.test.ts` is checked against.
//
// Run: browser_run_code_unsafe, filename = tools/probes/s73-widths.mjs, against a served build
// (`.scratch/serve` on port 8213). One bare function expression, because that tool evaluates the
// file rather than importing it. Screenshots land in `.scratch/`.
//
// The `?v=` is load-bearing: the case lives in the URL hash, so `goto` does not reload the page, and
// a plain static server answers from cache after a rebuild.

async (page) => {
    const WIDTHS = [
        [1920, 1080],
        [1440, 900],
        [1366, 768],
        [1024, 768],
        [390, 844],
    ];

    // A case with something in every region: several matches, each fuzzy, so the underlay, the
    // table and the group rows are all on screen while the boxes are measured. The keys are the
    // fragment's own, from `src/lib/fragment.ts`; an empty box is written out because the page
    // reads "cleared" and "not mentioned" differently. Spelt out with `encodeURIComponent` rather
    // than `URLSearchParams`, which the runner that evaluates this file does not define.
    const CASE = Object.entries({
        p: '(?:kitten){e<=2}',
        f: '',
        s: 'sitting kitten mitten bitten kitty',
        m: '',
        r: '',
        l: '',
    })
        .map(([key, value]) => `${key}=${encodeURIComponent(value)}`)
        .join('&');

    const widths = {};
    for (const [width, height] of WIDTHS) {
        // A fresh load per width, and the viewport set before it. Resizing one page across the five
        // carries state between them that is not what a visitor arrives to: the first run of this
        // probe measured the phone with its examples region open and the answer 2,799 px down the
        // page, because the tab walk at 1024 had left the focus on an example button and chunk 2's
        // narrowing fix opens the region the focus is in rather than losing it.
        await page.setViewportSize({ width, height });
        // The case arrives in the fragment rather than being typed in, and `mark.hit-current` is
        // what the wait is on. Typing it in cannot be waited for: the page comes up with a case of
        // its own already answered, so `mark.hit` is on screen from the first paint and the wait
        // returns before the typed case has been through the worker. Measured 2026-09-20 against
        // this build - the typed version reported the phone as 1208 of 844 with the answer heading
        // at 693 and the tab after the skip link landing on `button button-primary`, all of which
        // are the DEFAULT case's layout, and a 2 s sleep after the wait changed every one of them.
        await page.goto(`http://localhost:8213/?v=${Date.now()}#${CASE}`);
        await page.waitForSelector('#pattern', { timeout: 60000 });
        await page.waitForSelector('mark.hit-current', { timeout: 60000 });
        // Blurring is not enough to walk the page from the top: Chrome resumes sequential focus
        // from where the last focused element was, so a walk after filling the subject starts at
        // the control AFTER it and never sees the two fields. Focusing the body moves the starting
        // point back to the document, and `tabindex="-1"` makes the body focusable without putting
        // it in the tab order.
        await page.evaluate(() => {
            window.scrollTo(0, 0);
            document.activeElement?.blur();
            document.body.setAttribute('tabindex', '-1');
            document.body.focus();
        });

        const measured = await page.evaluate(
            ([viewportWidth, viewportHeight]) => {
                const root = document.documentElement;
                const rect = (selector) => {
                    const element = document.querySelector(selector);
                    if (element === null) return null;
                    const r = element.getBoundingClientRect();
                    return [Math.round(r.x), Math.round(r.y), Math.round(r.width), Math.round(r.height)];
                };
                const answer = [...document.querySelectorAll('h2')].find((h) => /match/i.test(h.textContent ?? ''));
                const answerRect = answer?.getBoundingClientRect() ?? null;
                return {
                    sideways: { scrollWidth: root.scrollWidth, clientWidth: root.clientWidth },
                    page: { scrollHeight: root.scrollHeight, clientHeight: root.clientHeight },
                    answer: {
                        text: answer?.textContent?.trim() ?? null,
                        bottom: answerRect === null ? null : Math.round(answerRect.bottom),
                        aboveTheFold: answerRect !== null && answerRect.bottom <= viewportHeight,
                    },
                    disclosures: [...document.querySelectorAll('button[aria-expanded]')].map((b) =>
                        (b.textContent ?? '').trim(),
                    ),
                    boxes: {
                        header: rect('header'),
                        results: rect('.results-pane'),
                        subject: rect('.subject-pane'),
                        table: rect('.data-table'),
                        footer: rect('footer'),
                    },
                    clipped: [...document.querySelectorAll('.results-pane, .subject-pane, header, footer')]
                        .map((element) => element.getBoundingClientRect())
                        .filter((r) => r.left < -1 || r.right > viewportWidth + 1).length,
                };
            },
            [width, height],
        );

        // What one CSS pixel comes back as. Playwright compensates a 125% Windows display by
        // zooming the page to 0.8, so an outline the stylesheet sets to 2px computes to 1.6px and a
        // 3px border to 2.4px - measured, 2026-09-20. The ring is checked against this rather than
        // against 2, or the probe is measuring the harness.
        const scale = await page.evaluate(() => {
            const probe = document.createElement('div');
            probe.style.cssText = 'outline: 2px solid red';
            document.body.append(probe);
            const width = Number.parseFloat(getComputedStyle(probe).outlineWidth);
            probe.remove();
            return width / 2;
        });

        // The skip link, from the top: one tab to reach it, and Enter to land in the answer. Its
        // box is measured while it has the focus, because a skip link that stays off the screen
        // when focused is worse than none - the visitor is somewhere they cannot see.
        await page.keyboard.press('Tab');
        const skip = await page.evaluate(
            ([viewportWidth, viewportHeight]) => {
                const active = document.activeElement;
                const r = active.getBoundingClientRect();
                return {
                    stop: active.className || active.tagName,
                    text: active.textContent?.trim(),
                    href: active.getAttribute('href'),
                    onScreen: r.top >= 0 && r.left >= 0 && r.bottom <= viewportHeight && r.right <= viewportWidth,
                    box: [Math.round(r.x), Math.round(r.y), Math.round(r.width), Math.round(r.height)],
                };
            },
            [width, height],
        );
        await page.keyboard.press('Enter');
        skip.lands = await page.evaluate(() => document.activeElement?.id ?? document.activeElement?.tagName);
        await page.keyboard.press('Tab');
        skip.thenReaches = await page.evaluate(() => {
            const active = document.activeElement;
            return active === document.body ? 'BODY' : active.id || active.className || active.tagName;
        });

        // Back to the top for the full walk.
        await page.evaluate(() => {
            window.scrollTo(0, 0);
            document.activeElement?.blur();
            document.body.focus();
        });

        // Every control on the page, in tab order. Forty stops rather than twenty, because at a
        // wide width the examples list alone is sixteen of them and a walk that stops short cannot
        // say whether the answer is reachable at all. Each stop must be a real control, inside the
        // window, wearing a ring the visitor can see.
        const walk = [];
        for (let i = 0; i < 40; i++) {
            await page.keyboard.press('Tab');
            walk.push(
                await page.evaluate(
                    ([viewportWidth, viewportHeight]) => {
                        const active = document.activeElement;
                        if (active === null || active === document.body) return { stop: 'BODY' };
                        const r = active.getBoundingClientRect();
                        const style = getComputedStyle(active);
                        const ring = Math.max(
                            Number.parseFloat(style.outlineWidth) || 0,
                            Number.parseFloat(style.getPropertyValue('--tw-ring-offset-width')) || 0,
                        );
                        return {
                            stop: active.id || active.className || active.tagName,
                            tag: active.tagName,
                            visible:
                                r.bottom > 0 && r.top < viewportHeight && r.right > 0 && r.left < viewportWidth,
                            ring,
                        };
                    },
                    [width, height],
                ),
            );
        }

        await page.screenshot({ path: `.scratch/s73-width-${width}x${height}.png`, scale: 'css' });

        widths[`${width}x${height}`] = {
            ...measured,
            skip,
            walk: {
                scale,
                stops: walk.length,
                // How many tabs it takes to get from the top of the page to the answer.
                toTheAnswer: walk.findIndex((one) => /hit|row-select|table-scroll/.test(one.stop)) + 1,
                body: walk.filter((one) => one.stop === 'BODY').length,
                offscreen: walk.filter((one) => one.stop !== 'BODY' && one.visible !== true).map((one) => one.stop),
                ringless: walk
                    .filter((one) => one.stop !== 'BODY' && one.ring < 2 * scale - 0.01)
                    .map((one) => `${one.stop} (${one.ring})`),
                // Run-length encoded, because "three example buttons in a row" is the fact and
                // thirteen repeated strings is not.
                order: walk
                    .map((one) => one.stop)
                    .reduce((runs, stop) => {
                        const last = runs.at(-1);
                        if (last?.stop === stop) last.run += 1;
                        else runs.push({ stop, run: 1 });
                        return runs;
                    }, [])
                    .map(({ stop, run }) => (run === 1 ? stop : `${stop} x${run}`))
                    .join(' > '),
            },
        };
    }

    // Once, not per width: every colour token as the browser paints it, read back through a canvas,
    // which is Chrome's own OKLCH to sRGB with its own gamut mapping.
    const tokens = await page.evaluate(() => {
        const names = new Set();
        // Recursive, because Tailwind v4 declares its theme inside `@layer` and the first run of
        // this probe walked only the top level and found nothing at all.
        const collect = (rules) => {
            for (const rule of rules) {
                if (rule.style !== undefined) {
                    for (const property of rule.style) {
                        if (property.startsWith('--color-')) names.add(property);
                    }
                }
                if (rule.cssRules !== undefined) collect(rule.cssRules);
            }
        };
        for (const sheet of document.styleSheets) {
            try {
                collect(sheet.cssRules);
            } catch {
                continue;
            }
        }
        const root = getComputedStyle(document.documentElement);
        const context = document.createElement('canvas').getContext('2d', { willReadFrequently: true });
        const painted = {};
        for (const name of [...names].sort()) {
            const value = root.getPropertyValue(name).trim();
            if (value === '') continue;
            context.fillStyle = value;
            context.fillRect(0, 0, 1, 1);
            painted[name] = [...context.getImageData(0, 0, 1, 1).data].slice(0, 3);
        }
        return painted;
    });

    return { widths, tokens };
}
