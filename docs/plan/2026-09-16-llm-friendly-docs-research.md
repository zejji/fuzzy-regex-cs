# Making FuzzyRegex's documentation LLM-friendly

Research date: 2026-09-16. All URLs read on that date unless stated.

## The problem, stated precisely

An assistant answering "how do I do a fuzzy match in .NET with FuzzyRegex?" has a strong prior
from training data: the Python `regex` module's README, which is a single 1,036-line `README.rst`
(`upstream/README.rst`, measured in this repo). That prior is *wrong for this port* in exactly the
places `docs/DIVERGENCES.md` lists: `Match` means `search`, `-1` means "no limit" in `Split`/
`Replace` where upstream says `0`, templates use `\1` not `$1`, version 1 is the default, `findall`
does not exist. The failure mode is not "the assistant knows nothing"; it is "the assistant is
confidently upstream-shaped". Documentation work has to *overwrite* a prior, not fill a vacuum.

## 1. llms.txt: the evidence says skip it (as an SEO play)

- Spec: Jeremy Howard, published 2024-09-03, revised through 2026-08-10, now v2
  (https://llmstxt.org/, read 2026-09-16). It proposes `/llms.txt` (a curated Markdown index of
  links) plus clean `.md` alternates of each page. **`llms-full.txt` is not in the spec** - it is a
  community convention (whole site concatenated) popularised by Mintlify.
- Ahrefs, 137,210 domains, May 2026 data (https://ahrefs.com/blog/llmstxt-study/): 28% publish one;
  **97% of those files received zero traffic**; 96% of the requests that did arrive were bots; AI
  bots were 19.5% of that residue. The killer line: *"Zero requests came from AI bots for llms.txt
  files that don't exist. They never go looking."*
- No major provider commits to reading it in production (OpenAI, Google, Anthropic, Meta, Mistral);
  Google's Gary Illyes said at Search Central Bangkok, July 2025, that Google does not support it
  and has no plans to; John Mueller compared it to the keywords meta tag. Summaries:
  https://www.1clickreport.com/blog/llms-txt-evidence-2026 and
  https://www.digitalapplied.com/blog/llms-txt-in-practice-adoption-evidence-2026.
- **The one real use is the one that matters here**: IDE agents (Claude Code, Cursor, Copilot,
  Windsurf) *do* fetch a URL when a human or a CLAUDE.md hands it to them. So the value of
  `llms.txt` is not discovery, it is being a **single fetchable URL you can paste**. That value is
  delivered just as well by one flat Markdown file with a stable URL.

Verdict: do not chase llms.txt for visibility. Do produce **one flat, complete, fetchable Markdown
file** - which is also the thing an `llms-full.txt` would have been.

## 2. NuGet: XML docs are the highest-leverage artefact, and they already work

- Measured on this machine, 2026-09-16: `~/.nuget/packages` holds **12,976 `.xml` doc files against
  14,243 `lib/**.dll`** - roughly 91% of packaged assemblies ship IntelliSense XML. It is the
  ecosystem norm, it sits next to the DLL in the restored package folder, and it is plain text.
- Who reads it: Roslyn/C# Dev Kit turns it into hover and completion, so **Copilot in VS/VS Code
  sees it as editor context**. Claude Code has no .NET symbol service - it reads *files*. The XML is
  a file at a predictable path, so an agent that is told where to look can `grep` it; an agent that
  is not told will guess from training data instead. Evidence that this is a live failure: someone
  built an MCP server specifically because "Claude kept hallucinating my NuGet APIs"
  (https://dev.to/prashant_patil_9e62d3fa8a/i-just-wanted-claude-to-stop-hallucinating-my-nuget-apis-somehow-i-ended-up-building-a-full-c-dev-12om).
- Repo state: `Directory.Build.props:25` already sets `GenerateDocumentationFile=true`, and
  `src/FuzzyRegex/FuzzyRegex.csproj` already sets `PackageReadmeFile=README.md` **with** the matching
  `<None Include=... Pack="true">` - the mistake called out in
  https://www.devleader.ca/2026/07/04/nuget-package-metadata-best-practices-readme-icon-tags-and-license
  is already avoided. Not set: `IncludeSymbols`/`SymbolPackageFormat=snupkg`. Source Link ships in
  the .NET 8+ SDK by default, so no package reference is needed.
- The README in the `.nupkg` is fetchable by URL (NuGet README URI template resource,
  https://learn.microsoft.com/en-us/nuget/api/readme-template-resource) and is the first thing a
  human sees on nuget.org (https://devblogs.microsoft.com/dotnet/write-a-high-quality-readme-for-nuget-packages/).

**The cheapest large win in this whole document: put the divergence warning in the `<remarks>` of
the member it affects.** `<remarks>` on `Split`, `Replace`, `Match`, `MatchAtStart`, `Result` -
"Unlike `Regex.Replace`, the template uses `\1`, not `$1`" - lands in IntelliSense, in the `.xml`
in every consumer's package cache, and in Copilot's editor context. No website, no hosting, no
crawler.

## 3. Context7 / MCP / DeepWiki

- **Context7** (https://context7.com/docs/adding-libraries): anyone can add any public repo, no star
  threshold. It indexes `.md`, `.mdx`, `.markdown`, `.rst`, `.txt`, `.ipynb` and ignores source when
  docs exist; a `context7.json` in the repo controls folders and inclusion, and authors can claim the
  library. Cost: one form submission plus a ~10-line JSON. It is the only registry here with a
  self-serve author action.
- **Microsoft Learn MCP** (https://learn.microsoft.com/en-us/training/support/mcp): serves
  learn.microsoft.com only. A third-party package cannot publish into it. Skip.
- **DeepWiki** (https://cognition.com/blog/deepwiki, https://docs.devin.ai/work-with-devin/deepwiki):
  free, indexes public repos on demand, has an MCP server. It generates *from the repo*, so the
  action is "have a good repo", not "publish something extra". Submit the URL once, no maintenance.

## 4. Practices that actually change the answer

From https://buildwithfern.com/post/how-to-write-llm-friendly-documentation (Mar 2026) and
https://docs.kapa.ai/improving/writing-best-practices, both of which agree on retrieval-chunk logic:

1. **Each heading is a self-contained answer.** A retrieved chunk must make sense alone. One
   canonical page per concept; never split one concept across pages.
2. **State the contrast where it bites, not in a separate chapter.** "Unlike Python `regex`… /
   Unlike `System.Text.RegularExpressions`…" goes in the section about the member, because that is
   the chunk that gets retrieved.
3. **Complete runnable examples with the expected output inline** - an assistant will copy the
   example; if the output is only implied it will invent one.
4. **One migration table** (Python name -> .NET name -> difference), because a table survives
   chunking and is trivially quotable.
5. **Stable anchors, versioned docs, nothing load-bearing in an image.**
6. **Test it**: paste the doc URL into Claude Code/Cursor and ask the five questions you expect;
   correct answers without follow-up fetches is the pass condition (Fern's own test).
7. **AGENTS.md / CLAUDE.md**: these are *project*-scoped context for people working in a repo, not
   something a library ships to consumers (https://medium.com/data-science-collective/claude-md-vs-agents-md-vs-skill-md-which-file-owns-what-in-2026-13859378f56a).
   The consumer-facing equivalent is an **Agent Skill** (`SKILL.md`, Anthropic spec published
   2025-12-18, now an open standard at agentskills.io adopted by Codex, Copilot, Cursor, Gemini CLI;
   https://code.claude.com/docs/en/skills). Shipping a skill is real but speculative for a v1.0
   library with no users.

## 5. Specific to this port

The port's whole risk surface is a *name and semantics collision with something the models already
know well*. That argues for one artefact above all others: a **single-page, complete "FuzzyRegex vs
Python `regex` vs `System.Text.RegularExpressions`" reference**, written from DIVERGENCES.md's
SHIPPED rows only, mirroring upstream's own single-file README shape (which is the form the models
already learned). Two tables, one per comparand, plus fuzzy syntax with worked examples and outputs.

## Recommendation

### Do

| Item | Where | Why | Cost |
|---|---|---|---|
| `<remarks>` divergence notes on every affected public member | `src/FuzzyRegex/*.cs` | Reaches IntelliSense, Copilot and the `.xml` in every consumer's package cache; needs no site and no crawler | ~1 day, done alongside the XML docs Phase 8 owes anyway |
| `docs/COMPARISON.md` - one page, two tables (Python `regex` -> here, `Regex` -> here) + fuzzy syntax, runnable examples **with expected output**, generated-from/checked-against DIVERGENCES.md SHIPPED rows | repo root `docs/` | The single canonical chunk that overwrites the training prior; the one URL to paste at an agent | 1-2 days |
| Expand `README.md` to a complete getting-started (it currently says "not yet usable"); keep it the `PackageReadmeFile` | repo root | It is the nupkg README, the nuget.org page and the GitHub landing page, all from one file | 0.5 day |
| A convention test asserting every public member has XML docs, and that each DIVERGENCES SHIPPED row is named in COMPARISON.md | `tests/FuzzyRegex.Tests/Conventions/` | The repo already tests conventions this way; stops the docs rotting | 0.5 day |
| `IncludeSymbols` + `SymbolPackageFormat=snupkg` | `src/FuzzyRegex/FuzzyRegex.csproj` | Two lines; Source Link is already on by default in the .NET 10 SDK | 5 min |

### Consider

| Item | Why | Cost |
|---|---|---|
| `context7.json` at repo root + submit at context7.com/add-library | Only registry with a self-serve author action; indexes `.md` directly | 15 min, one-off |
| Submit the repo to DeepWiki once | Free, MCP-queryable, generated from the repo | 5 min |
| `llms.txt` at the docs root (if a docs site happens) as a plain index | Worthless for discovery; useful only as a paste-able URL, which COMPARISON.md already is | 30 min - defer until there is a site |

### Skip

- **llms.txt as an AI-visibility strategy** - 97% of published files get zero traffic; crawlers never
  look for one (Ahrefs, May 2026).
- **`llms-full.txt`** - not in the spec, and COMPARISON.md is already the flat complete file.
- **A `SKILL.md` shipped for consumers** - real standard, but speculative before the library has users.
- **`AGENTS.md`/`CLAUDE.md` aimed at consumers** - wrong scope; those are for people editing *this* repo.
- **Microsoft Learn MCP** - closed to third parties.
- **A DocFX/Statiq API site for 1.0** - the XML docs plus two Markdown files cover the same ground at
  a fraction of the cost; add it when the surface outgrows one page.

The through-line: every "do" item is a file the project already had a reason to write. Nothing here
is an LLM-specific artefact except the phrasing discipline (self-contained sections, contrast stated
where it bites, outputs shown), which costs nothing extra and helps humans identically.
