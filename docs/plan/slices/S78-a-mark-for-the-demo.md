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
