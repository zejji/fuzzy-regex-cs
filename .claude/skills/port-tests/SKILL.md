---
name: port-tests
description: Use when translating upstream/regex/tests/test_regex.py into TUnit tests for the FuzzyRegex port. Covers the file and namespace layout, the skip and provenance conventions the status board parses, how to split a fan-out Python test method, how to translate codepoint indices into UTF-16, and which upstream tests do not port.
---

# Porting upstream's test suite

`upstream/regex/tests/test_regex.py` is 4,540 lines and 102 test methods holding roughly 1,544
assertions. It is the specification. Every applicable assertion becomes a TUnit test, written
before the engine exists and skipped until its slice lands.

Faithfulness beats elegance. A ported test should be recognisable beside its Python original, so
that a future upstream change to that test maps onto ours by eye.

## Where a test goes

```
tests/FuzzyRegex.Tests/Ported/<Area>/<Thing>Tests.cs   namespace Fuzzy.Text.RegularExpressions.Tests.Ported.<Area>
tests/FuzzyRegex.Tests/Gaps/<Area>/<Thing>Tests.cs     namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.<Area>
```

`<Area>` is the feature area and it **is** the row in `docs/STATUS.md`: the status generator reads
it straight out of the namespace. Use the areas already present before inventing a new one.
Anything under `Ported` counts towards the parity percentage; anything under `Gaps` is ours and
does not. `tests/FuzzyRegex.Tests/Conventions/PortedTestConventions.cs` enforces this and will
fail the build if you break it.

## The shape of a ported test

```csharp
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Ported.Quantifiers;

public sealed class SearchStarPlusTests
{
    [Test]
    [Skip("needs:quantifiers - the VM has no repeat opcode yet")]
    [Property("Upstream", "RegexTests.test_search_star_plus#1")]
    public void Search_star_matches_empty_at_the_start()
        => FuzzyRegex.Search("a*", "xxx")!.Span.Should().Be((0, 0));
}
```

Four things every ported test carries:

- **`[Skip("needs:<capability> - <why>")]`** until the engine can do it. The reason **must**
  start with `needs:` followed by a lower-case kebab tag of at least three characters, then a
  space, colon or hyphen. That tag is what `docs/STATUS.md` groups by, and what a slice file
  declares it delivers.

  A skipped test names a **capability, not a slice**. Ported tests are written now; the slices
  that enable them are authored one phase ahead, so a slice id here would be an invention that
  goes stale. "Tests waiting on `lookbehind`: 47" is also the number a slice author actually
  wants. Reuse an existing tag before coining a new one - check the bottom of `docs/STATUS.md`.
- **`[Property("Upstream", "RegexTests.<python method>#<assertion index>")]`** - the provenance.
  It survives into the TRX report, so any test can be traced back to its origin. Number the
  assertions from 1 in source order.
- **A name that says the behaviour**, not the Python method name. `test_search_star_plus`
  becomes several tests; each gets its own descriptive name.
- **AwesomeAssertions** (`.Should()...`), not raw asserts.

## Splitting a fan-out method

Most upstream methods pack many independent assertions into one function:

```python
def test_search_star_plus(self):
    self.assertEqual(regex.search('a*', 'xxx').span(0), (0, 0))
    self.assertEqual(regex.search('x+', 'axx').span(0), (1, 3))
    self.assertEqual(regex.search('x', 'aaa'), None)
```

One TUnit test per **independent** assertion, so a failure names exactly what broke rather than
stopping the whole method at the first problem. Use `[Arguments(...)]` when several assertions
differ only in data:

```csharp
[Test]
[Arguments("a*", "xxx", 0, 0)]
[Arguments("x*", "axx", 0, 0)]
[Arguments("x+", "axx", 1, 3)]
[Skip("needs:quantifiers - the VM has no repeat opcode yet")]
[Property("Upstream", "RegexTests.test_search_star_plus#1-4")]
public void Search_spans(string pattern, string subject, int start, int end) { ... }
```

Keep assertions that build on each other (a match, then a group from that match) in one test.

## Translating indices: codepoints to UTF-16

The highest-risk part of the whole port. Python indexes by codepoint; .NET strings are UTF-16
code units, and our public API follows .NET (design spec section 4).

- **BMP-only test data:** indices are identical. Copy them.
- **Any non-BMP character in the pattern or subject (`\U0001...`, emoji, astral scripts):** every
  index and length shifts, because one codepoint is two chars. Recompute by hand, and add a
  comment saying so:

  ```csharp
  // Upstream expects (1, 2) in codepoints; U+1F600 is a surrogate pair, so UTF-16 gives (2, 4).
  ```

- **Never guess.** Verify against Python, and paste what you actually saw:

  ```powershell
  $env:PYTHONIOENCODING = 'utf-8'; $env:PYTHONDONTWRITEBYTECODE = '1'
  python -c "import regex; m = regex.search(r'...', '...'); print(m.span())"
  ```

  `PYTHONIOENCODING` because the Windows console codepage cannot print most of what you will be
  checking, and `PYTHONDONTWRITEBYTECODE` because running the oracle from inside `upstream/`
  otherwise leaves a `__pycache__` directory that makes the submodule show as dirty.

- Every such test also gets a sibling in `Gaps/Surrogates` pinning the UTF-16 behaviour directly:
  the ported suite proves parity, the gap tests prove we did the translation on purpose.

## Tests that do not port

Skip these, and say why in a comment where the port would have gone:

- **Python-only mechanics:** `test_weakref`, pickling, `__copy__`/`__deepcopy__`, `sys.getrefcount`,
  `test_constants` (checks Python-level flag integers).
- **`bytes` patterns:** upstream matches bytes as well as str. Our port is `char`-based. Record
  each such test in `docs/PORTMAP.md` under "not ported: bytes patterns" so the omission is
  visible and countable, not silent.
- **CPython implementation details:** recursion limits, deprecation warnings, `re` compatibility
  shims.

Anything else that looks unportable is a question for the owner, not a decision to make quietly.

## Before you finish

```powershell
dotnet build                       # the conventions test must pass
tools/check-ratchet.ps1            # must be GREEN - newly added tests are skipped, so it will be
```

Newly ported tests are skipped, so they add nothing to the baseline. That is correct: they count
when their slice enables them, and the jump in `docs/STATUS.md` is the slice's evidence.
