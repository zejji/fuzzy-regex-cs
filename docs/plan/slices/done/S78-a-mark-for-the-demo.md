---
slice: S78
phase: 9
title: A mark for the demo, so the tab has a face and the console has no 404
delivers: []
---

# S78 - a favicon for the browser demo

> **Owner request (2026-09-21).** The page has no favicon. A small "FR" mark would do, and it should
> look professional without being dull. The console 404 the orchestrator saw on 2026-09-21
> (`GET /favicon.ico 404`, the only error the published build produced in a full browser pass) is
> the same gap seen from the other side.

## 1. What is there now

`demo/web/index.html` declares no icon at all, so every browser asks for `/favicon.ico` at the site
root and gets a 404. On GitHub Pages that request goes to the account root rather than the
repository subpath, which is a second reason it can never succeed.

## 2. What to build

- **An SVG icon**, `favicon.svg`, as the primary. It scales, it is a few hundred bytes, and every
  browser this demo supports reads it.
- **A 180 px PNG**, `apple-touch-icon.png`, which iOS uses and which does not read SVG.
- **A legacy `favicon.ico`** holding 16 and 32 px, for the browsers and feed readers that still ask
  for it by name, and to silence the root request.
- **The declarations in `demo/web/index.html`**, with RELATIVE hrefs (`./favicon.svg`), because the
  page has no `<base href>` by deliberate decision and has to boot at a repository subpath, at a
  local server root and at a fork's preview path.

## 3. The design brief

The mark is "FR", or something that reads as FuzzyRegex at 16 px. It has to survive being 16 px on a
crowded tab strip, which is the constraint that kills most cleverness: two letters at that size is
already near the limit, so the shape carries the identity, not the detail.

- **Palette from the page**, not a new one: `--color-shell` `oklch(0.21 0.02 258)` is the dark
  surface, `--color-accent` `oklch(0.52 0.19 258)` and `--color-accent-bright`
  `oklch(0.72 0.12 258)` are the interactive blue. `demo/web/src/styles.css` holds them.
- **Legible on both** a light and a dark browser chrome. A mark that disappears into a dark tab strip
  is a mark that is only half drawn.
- **One idea, executed well.** The page's own character is an application shell with a dark input
  pane and a light answer pane; something that echoes that is better than a generic monogram.
- **No gradients that band at 16 px, no hairlines, no text below about 6 px of stroke.**
- Use the `frontend-design` skill for the design pass, and produce two or three candidates to choose
  between rather than one.

## 4. Done when

- [ ] `favicon.svg`, `apple-touch-icon.png` and `favicon.ico` are in the .NET project's web root,
      committed, and the publish carries them (the integrity check in `run-wasm-smoke.ps1` covers
      every published endpoint, so they are covered the moment they ship).
- [ ] `demo/web/index.html` declares all three with relative hrefs.
- [ ] A web test asserts the declarations exist and that none of them is an absolute path, which is
      the failure that only shows on Pages.
- [ ] Rendered at 16, 32 and 180 px and looked at, not assumed: the 16 px version is the one that
      decides whether the design works.
- [ ] The published build loads with no console error, which is the check that started this.

## Closing notes (2026-09-21)

**What shipped.** `favicon.svg`, `favicon.ico` (16 and 32) and `apple-touch-icon.png` (180) in the
.NET web root, declared in `demo/web/index.html` with relative hrefs. The published page now asks
for `favicon.svg` and gets 200, the root `/favicon.ico` answers 200, and a full load produces no
console error - the check that started this slice.

**The design took three rounds, and the first two were wrong in ways worth recording.**

- Round one delegated the design to a subagent with the `frontend-design` skill named in its brief.
  Its report showed no evidence the skill or any research shaped the work, and the owner's verdict on
  the result was "boring font, nasty colours, nasty spacing". A skill named in a brief is not a skill
  applied.
- Round two drew the letters as axis-aligned rectangles on a 32-unit grid, reasoning that curves
  blur when a 32 px artboard is downsampled to 16. That optimisation bought pixel-snapping and paid
  for it with a square-bowled R that reads as a wireframe. It also produced a variant with a cut
  corner, which the owner read as damage rather than design.
- Round three used real outlines: `F` and `R` from Cascadia Code Bold, pulled through `fontTools`
  (`.scratch/favicon-candidates/glyphs.py` extracts, `build.py` composes), spaced by their ink
  bounds rather than the font's monospace advance, which is what had been leaving a hole between the
  two capitals. A coding face is also the right register for a regex library.

**Licensing.** Cascadia Code is SIL OFL 1.1, so the derived artwork may ship; `NOTICE` records the
copyright, the licence and the fact that only two outlines are used and no font file is
redistributed. A system face such as Segoe UI would not have been usable this way.

**Two faults caught by looking rather than by trusting the toolchain.**

- The first PNG and .ico were mostly transparent. Opening an SVG file directly in a browser renders
  it at its intrinsic 32 px in the corner of a 180 px viewport, so the screenshot packaged empty
  space. Reading the centre pixel of each .ico frame is what found it; a wrapper page that sizes the
  icon to the viewport is what fixed it.
- `vue-tsc` rejected the new test's regex destructuring (`'href' is possibly undefined`) although
  Vitest passed, because the web build type-checks before it tests. Run `tools/build-demo-web.ps1`,
  not `npx vitest run`, before believing the front end is green.

**Tests.** `layout.test.ts` pins that the three icons are declared, that every href is relative -
the failure that only appears on Pages, where an absolute path resolves to the account root - and
that the three files exist in the web root the publish gathers. The publish integrity check covers
them from the moment they ship: 58 endpoints before, 64 after.

**Sources kept.** The candidates and renders are under `.scratch/favicon-candidates/`, which is
gitignored, and `w7.svg` is the file that became `favicon.svg`. The generator scripts are there too,
so the mark can be rebuilt at another weight or spacing without redrawing it.
