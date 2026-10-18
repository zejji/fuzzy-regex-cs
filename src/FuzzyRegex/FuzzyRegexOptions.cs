namespace Fuzzy.Text.RegularExpressions;

/// <summary>
/// Options that change how a pattern is compiled and matched.
/// </summary>
/// <remarks>
/// <para>
/// The bit values are upstream's, from <c>RegexFlag</c> in <c>upstream/regex/_regex_core.py</c>
/// lines 73-90, so that the parser port can compare against upstream's tables directly rather
/// than through a translation layer. Nothing casts between this and
/// <see cref="System.Text.RegularExpressions.RegexOptions"/>, so the numbers need not agree with
/// the built-in engine's.
/// </para>
/// <para>
/// Where a .NET name and an upstream name exist for the same behaviour, the .NET name is used
/// and the upstream spelling is given here, so a <c>System.Text.RegularExpressions</c> user needs
/// no new vocabulary. Upstream flags this port does not expose - <c>LOCALE</c>, <c>DEBUG</c> and
/// <c>TEMPLATE</c> - are absent rather than present and ignored. <c>LOCALE</c> can still be
/// set from inside a pattern, as <c>(?L)</c>; it is left out here because it asks for the C
/// library's current locale, which .NET has no equivalent of and which no oracle row could pin.
/// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#l-works-only-when-casing-is-not-requested">the comparison page</see>.
/// </para>
/// <para>
/// There is no <c>ExplicitCapture</c>. It has no upstream counterpart, so nothing in the
/// compile-parity corpus could verify it and it would have been the only unverified logic in the
/// parser; a caller who wants a non-capturing group writes <c>(?:...)</c>, as upstream's users do
/// (DECISIONS 2026-08-30, decision C).
/// </para>
/// </remarks>
[Flags]
public enum FuzzyRegexOptions
{
    /// <summary>No options: case-sensitive, single-line, left-to-right matching.</summary>
    None = 0x0,

    /// <summary>Case-insensitive matching. Upstream <c>IGNORECASE</c> / <c>I</c>.</summary>
    /// <remarks>
    /// <b>The Turkic <c>I</c> pairings are not applied by default</b>. <c>(?i)</c> and
    /// <c>(?fi)</c> do not pair <c>I</c> with <c>ı</c>, or <c>i</c> with <c>İ</c>, where
    /// upstream does. No Turkic mode is offered: PCRE2, Perl and .NET all agree with this port,
    /// and upstream exposes no locale a caller could ask for the pairing with.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#the-turkic-i-pairings-are-not-applied-by-default">the comparison page</see>.
    /// <para>
    /// <b>A case-insensitive cased property under ASCII means the 52 ASCII letters.</b> With
    /// <c>(?a)</c> and <c>(?i)</c>, <c>\p{Lu}</c>, <c>\p{Ll}</c>, <c>[[:upper:]]</c> and their kin
    /// match <c>a</c> to <c>z</c> and <c>A</c> to <c>Z</c> only; <c>(?u:...)</c> gives the Unicode
    /// answer. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-case-insensitive-cased-property-under-ascii-means-the-52-ascii-letters">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A case-insensitive cased property answers the same bare and in a set.</b> Under
    /// <c>(?i)</c>, <c>\p{Lu}</c>, <c>\p{Ll}</c>, <c>\p{Lt}</c>, <c>\p{Upper}</c> and
    /// <c>\p{Lower}</c> mean any cased letter wherever they appear, as in Perl and <c>Regex</c>.
    /// See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-case-insensitive-cased-property-answers-the-same-bare-and-in-a-set">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A case-insensitive set matches each member first, then combines them</b>, as Perl's
    /// extended sets and .NET's set subtraction do, so the answer stays the same however the set is
    /// written. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-case-insensitive-set-matches-each-member-first-then-combines-them">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A literal under a scoped <c>(?i:...)</c> is found in text that holds only its full case
    /// folding</b>, so <c>(?i:ss)</c> finds 'ß' whether <c>(?i)</c> covers the whole pattern or one
    /// group. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-literal-under-a-scoped-i-is-found-in-text-that-holds-only-its-full-case-folding">the comparison page</see>.
    /// </para>
    /// </remarks>
    IgnoreCase = 0x2,

    /// <summary>
    /// <c>^</c> and <c>$</c> match at the start and end of every line, as well as of the whole
    /// subject.
    /// Upstream <c>MULTILINE</c> / <c>M</c>.
    /// </summary>
    Multiline = 0x8,

    /// <summary>
    /// <c>.</c> matches any character including a newline. Upstream <c>DOTALL</c> / <c>S</c>;
    /// the .NET name is <c>Singleline</c>, which means the same thing.
    /// </summary>
    Singleline = 0x10,

    /// <summary>
    /// The character classes <c>\w</c>, <c>\W</c>, <c>\s</c>, <c>\S</c>, <c>\d</c>, <c>\D</c> and
    /// the word boundaries <c>\b</c>, <c>\B</c> cover the whole of Unicode. Upstream
    /// <c>UNICODE</c> / <c>U</c>, and its default for a text pattern; the inline form is
    /// <c>(?u)</c>.
    /// </summary>
    /// <remarks>
    /// <b>Already on.</b> Upstream's <c>_main._compile</c> ORs it into every text pattern that
    /// names no encoding (<c>upstream/regex/_main.py</c> lines 570-574), so this port does too and
    /// <see cref="FuzzyRegex.Options"/> reports it on every pattern. It is exposed so that a
    /// caller can say so explicitly and read it back; passing it changes nothing.
    /// Combining it with <see cref="Ascii"/> is rejected, as upstream rejects it.
    /// <para>
    /// <b>A scoped <c>(?a:...)</c> or <c>(?u:...)</c> answers exactly as the same encoding set for
    /// the whole pattern</b>, for every construct whose answer depends on the encoding; upstream
    /// applies a scoped encoding only to <c>\p{...}</c> and <c>\b</c>. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-scoped-a-or-u-answers-exactly-as-the-same-encoding-set-for-the-whole-pattern">the comparison page</see>.
    /// </para>
    /// </remarks>
    Unicode = 0x20,

    /// <summary>
    /// Unescaped whitespace in the pattern is ignored and <c>#</c> starts a comment. Upstream
    /// <c>VERBOSE</c> / <c>X</c>.
    /// </summary>
    IgnorePatternWhitespace = 0x40,

    /// <summary>
    /// The character classes <c>\w</c>, <c>\W</c>, <c>\s</c>, <c>\S</c>, <c>\d</c>, <c>\D</c> and
    /// the word boundaries <c>\b</c>, <c>\B</c> cover ASCII only, so everything above U+007F is
    /// answered as if it were unassigned. Upstream <c>ASCII</c> / <c>A</c>; the inline form is
    /// <c>(?a)</c>.
    /// </summary>
    /// <remarks>
    /// Mutually incompatible with <see cref="Unicode"/> - and with upstream's <c>LOCALE</c>, which
    /// this port does not expose - so passing both throws
    /// <see cref="FuzzyRegexParseException"/> with upstream's own message, "ASCII, LOCALE and
    /// UNICODE flags are mutually incompatible".
    /// <para>
    /// <b>A case-insensitive cased property under ASCII means the 52 ASCII letters.</b> With
    /// <c>(?a)</c> and <c>(?i)</c>, <c>\p{Lu}</c>, <c>\p{Ll}</c>, <c>[[:upper:]]</c> and their kin
    /// match <c>a</c> to <c>z</c> and <c>A</c> to <c>Z</c> only; <c>(?u:...)</c> gives the Unicode
    /// answer. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-case-insensitive-cased-property-under-ascii-means-the-52-ascii-letters">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A scope that names no encoding keeps the one around it</b>: <c>(?a:(?s:\w))</c> refuses
    /// 'é', as Python's <c>re</c> does, where upstream's inner scope drops the outer <c>(?a:</c>.
    /// See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-scope-that-names-no-encoding-keeps-the-one-around-it">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A POSIX class takes the scope's encoding</b>: <c>(?a:[[:alpha:]])</c> refuses 'é', as
    /// <c>(?a:\p{L})</c> does, where upstream reads it with the pattern's tables. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-posix-class-takes-the-scopes-encoding">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>An encoding named by positional flags inside a group replaces the one in force</b>:
    /// <c>(?a:(?u)\w)</c> matches 'é', as <c>(?a:(?u:\w))</c> does; upstream keeps both and ASCII
    /// wins. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#an-encoding-named-by-positional-flags-inside-a-group-replaces-the-one-in-force">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A scoped <c>(?a:...)</c> or <c>(?u:...)</c> answers exactly as the same encoding set for
    /// the whole pattern</b>, for every construct whose answer depends on the encoding; upstream
    /// applies a scoped encoding only to <c>\p{...}</c> and <c>\b</c>. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-scoped-a-or-u-answers-exactly-as-the-same-encoding-set-for-the-whole-pattern">the comparison page</see>.
    /// </para>
    /// </remarks>
    Ascii = 0x80,

    /// <summary>
    /// Search backwards, from the end of the subject towards the start. Upstream
    /// <c>REVERSE</c> / <c>R</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Reversed partial matches run out of text at the slice start.</b> Under <c>(?r)</c> with
    /// <c>partial: true</c>, a pattern that still needs characters at <c>beginning</c> is a partial
    /// there, as <c>Regex</c> and Boost treat the bound. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#reversed-partial-matches-run-out-of-text-at-the-slice-start">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b><c>\X</c> matches the same grapheme cluster backwards as forwards</b>, on every line of
    /// Unicode's conformance file; upstream's reversed <c>\X</c> stops after one codepoint. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#x-matches-the-same-grapheme-cluster-backwards-as-forwards">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A reversed partial search lets <c>$</c> hold before a final newline in the text to
    /// come</b>, where upstream answers no match for an empty subject. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-reversed-partial-search-lets--hold-before-a-final-newline-in-the-text-to-come">the comparison page</see>.
    /// </para>
    /// </remarks>
    RightToLeft = 0x400,

    /// <summary>
    /// <c>\b</c> and <c>\B</c> use the default Unicode word-boundary rules (UAX #29) instead of
    /// the <c>\w</c>-to-<c>\W</c> transition. Upstream <c>WORD</c> / <c>W</c>; the inline form is
    /// <c>(?w)</c>.
    /// </summary>
    /// <remarks>
    /// The rules join a word across an apostrophe and across other characters a simple
    /// <c>\w</c> test would break on, so the set of matches changes.
    /// Measured against <c>regex</c> 2026.9.10 on 2026-09-16:
    /// <c>regex.findall(r'\b\w+\b', "can't", regex.WORD)</c> is <c>[]</c> where the same call
    /// without the flag is <c>['can', 't']</c> - UAX #29 keeps <c>can't</c> whole, so no boundary
    /// sits where <c>\w+</c> has to stop.
    /// <para>
    /// <b>The WORD flag's word boundary is Unicode's default one.</b> It agrees with all 1,944
    /// lines of Unicode 17.0.0's <c>WordBreakTest.txt</c>; upstream disagrees with 268. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#the-word-flags-word-boundary-is-unicodes-default-one">the comparison page</see>.
    /// </para>
    /// </remarks>
    Word = 0x800,

    /// <summary>
    /// Find the best fuzzy match rather than the first one. Upstream <c>BESTMATCH</c> / <c>B</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>(?e)</c> and <c>(?b)</c> rank candidates by fuzzy COST</b> (cheapest, then fewer
    /// errors, then earliest); upstream ranks by error count only. The divergence applies only
    /// where the pattern has exactly one fuzzy section and a weighted cost equation - with unit
    /// costs, or a nested second section, this falls back to upstream's error-count budget and
    /// the two engines agree exactly.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#e-and-b-rank-candidates-by-fuzzy-cost">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A BESTMATCH or ENHANCEMATCH scan does not skip positions after an empty match.</b> The
    /// starts are the ones plain matching finds, and the flag only chooses the fit at each. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-bestmatch-or-enhancematch-scan-does-not-skip-positions-after-an-empty-match">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>Under BESTMATCH and ENHANCEMATCH, <c>\G</c> holds only where the search began</b>,
    /// whichever start the flag is trying; upstream moves it to the start of each re-run. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#under-bestmatch-and-enhancematch-g-holds-only-where-the-search-began">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>BESTMATCH counts only the errors a match contains.</b> Errors made in a thrown-away
    /// negative lookaround, cut atomic group or failed conditional test leave the total, where
    /// upstream keeps them and can prefer a match that is no better. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#bestmatch-counts-only-the-errors-a-match-contains">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>BESTMATCH and ENHANCEMATCH never let their budget decide a negative lookaround or a
    /// conditional's test.</b> Those bodies match exactly as they would without the flag. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#bestmatch-and-enhancematch-never-let-their-budget-decide-a-negative-lookaround-or-a-conditionals-test">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b><c>BestMatch</c> and <c>EnhanceMatch</c> keep a fit that ends in trailing insertions</b>,
    /// so a <c>FullMatch</c> or a pattern ending in <c>$</c> answers what it answers without the
    /// flag; upstream can lose that match. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#bestmatch-and-enhancematch-keep-a-fit-that-ends-in-trailing-insertions">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>From <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#fuzzyregexoptionsbestmatch--b-take-the-best-fuzzy-match-rather-than-the-first">the comparison page</see>:</para>
    /// <code language="csharp">
    /// using Fuzzy.Text.RegularExpressions;
    ///
    /// Match m = FuzzyRegex.Match("xirefoabralfobarxie", "(?b)(foobar){e}");
    /// Console.WriteLine((m.Index, m.Length));   // (11, 5)
    /// </code>
    /// </example>
    BestMatch = 0x1000,

    /// <summary>
    /// After finding a fuzzy match, try to improve its fit. Upstream <c>ENHANCEMATCH</c> /
    /// <c>E</c>.
    /// </summary>
    /// <remarks>
    /// Ranks candidates by fuzzy cost rather than upstream's error count under the same
    /// conditions as <see cref="BestMatch"/>; see its remarks.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#e-and-b-rank-candidates-by-fuzzy-cost">the comparison page</see>.
    /// <para>
    /// <b>A BESTMATCH or ENHANCEMATCH scan does not skip positions after an empty match.</b> The
    /// starts are the ones plain matching finds, and the flag only chooses the fit at each. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-bestmatch-or-enhancematch-scan-does-not-skip-positions-after-an-empty-match">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>Under BESTMATCH and ENHANCEMATCH, <c>\G</c> holds only where the search began</b>,
    /// whichever start the flag is trying; upstream moves it to the start of each re-run. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#under-bestmatch-and-enhancematch-g-holds-only-where-the-search-began">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>BESTMATCH counts only the errors a match contains.</b> Errors made in a thrown-away
    /// negative lookaround, cut atomic group or failed conditional test leave the total, where
    /// upstream keeps them and can prefer a match that is no better. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#bestmatch-counts-only-the-errors-a-match-contains">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>BESTMATCH and ENHANCEMATCH never let their budget decide a negative lookaround or a
    /// conditional's test.</b> Those bodies match exactly as they would without the flag. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#bestmatch-and-enhancematch-never-let-their-budget-decide-a-negative-lookaround-or-a-conditionals-test">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b><c>BestMatch</c> and <c>EnhanceMatch</c> keep a fit that ends in trailing insertions</b>,
    /// so a <c>FullMatch</c> or a pattern ending in <c>$</c> answers what it answers without the
    /// flag; upstream can lose that match. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#bestmatch-and-enhancematch-keep-a-fit-that-ends-in-trailing-insertions">the comparison page</see>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>From <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#fuzzyregexoptionsenhancematch--e-tighten-a-match-after-it-is-found">the comparison page</see>:</para>
    /// <code language="csharp">
    /// using Fuzzy.Text.RegularExpressions;
    ///
    /// Match m = FuzzyRegex.Match("xirefoabralfobarxie", "(?e)(foobar){e}");
    /// Console.WriteLine((m.Index, m.Length));   // (0, 3)
    /// </code>
    /// </example>
    EnhanceMatch = 0x8000,

    /// <summary>
    /// Leftmost-longest (POSIX) matching instead of leftmost-first. Upstream <c>POSIX</c> /
    /// <c>P</c>.
    /// </summary>
    /// <remarks>
    /// The match still starts as far left as it can; the flag only decides between matches that
    /// start in the same place, taking the longest rather than the first the pattern happens to
    /// produce. So <c>a|ab|abc</c> matches <c>abc</c> in <c>"abcd"</c> under this flag and <c>a</c>
    /// without it.
    /// <para>
    /// <b>It can be much slower.</b> Ordinary matching stops at the first match it reaches; this
    /// one keeps backtracking through every remaining path so that it can be sure nothing longer
    /// exists. A pattern that answers instantly without the flag can take seconds with it - so set
    /// a <c>matchTimeout</c> on any pattern you did not write yourself.
    /// </para>
    /// </remarks>
    Posix = 0x10000,

    /// <summary>
    /// Use Unicode full case-folding when matching case-insensitively, so that (for example)
    /// <c>ß</c> matches <c>SS</c> and <c>ﬁ</c> matches <c>fi</c>. Upstream <c>FULLCASE</c> /
    /// <c>F</c>.
    /// </summary>
    /// <remarks>
    /// <b>Already on</b> under <see cref="Version1"/>, which is the default, so passing it changes
    /// nothing unless you also pass <see cref="Version0"/>. To turn it off, pass
    /// <see cref="Version0"/> or write <c>(?-f)</c> in the pattern. It does not turn
    /// case-insensitive matching on by itself; it changes what <see cref="IgnoreCase"/> means.
    /// <para>
    /// <b>A fuzzy deletion that finishes a full-case-folded string or backreference costs one
    /// edit</b>, where upstream charges a second edit for a subject character nothing was compared
    /// with. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-fuzzy-deletion-that-finishes-a-full-case-folded-string-or-backreference-costs-one-edit">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A full-folded backreference that ends half-way through a folding charges the rest as an
    /// edit</b>, as upstream already does for a literal; upstream's backreference gives up instead.
    /// See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-full-folded-backreference-that-ends-half-way-through-a-folding-charges-the-rest-as-an-edit">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A retried fuzzy edit in a full-folded backreference steps past what it used up</b>, where
    /// upstream's second try compares a character the first edit already used. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-retried-fuzzy-edit-in-a-full-folded-backreference-steps-past-what-it-used-up">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A fuzzy full-folded match can stop part-way into a folding</b>, with one deletion for
    /// each folded character it had matched there; upstream cannot end the match at that point. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-fuzzy-full-folded-match-can-stop-part-way-into-a-folding">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>A lazy repeat finds a full-folded literal that starts at the repeat's last position</b>,
    /// where upstream's look-ahead for the literal stops reading there and loses the match. See
    /// <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#a-lazy-repeat-finds-a-full-folded-literal-that-starts-at-the-repeats-last-position">the comparison page</see>.
    /// </para>
    /// </remarks>
    FullCase = 0x4000,

    /// <summary>
    /// Legacy behaviour, compatible with <c>System.Text.RegularExpressions</c> and Python's
    /// <c>re</c>: simple case-folding, and an unescaped <c>[</c> inside a set is a literal.
    /// Upstream <c>VERSION0</c> / <c>V0</c>, and upstream's own default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pass this to compile a pattern written for <c>Regex</c> or for <c>re</c> unchanged. The two
    /// live differences from <see cref="Version1"/> are the ones named above; measured on
    /// <c>regex</c> 2026.9.10 and .NET 10 (<c>tools/probes/upstream-version-defaults.py</c> and its
    /// <c>.ps1</c> twin), the zero-width and inline-flag differences upstream's README also lists
    /// no longer exist. There is a third, undocumented one: a backreference to a group that is
    /// still open is a compile error here and is accepted under <see cref="Version1"/>.
    /// </para>
    /// <para>
    /// <b><c>(?V0)</c> really means version 0, where upstream's own algorithm would leave version
    /// 1's <c>FULLCASE</c> on</b>. A leading global flag makes upstream parse the pattern twice,
    /// seeding the second parse with the default version's flags already folded in; this port
    /// keeps the version-implied defaults out of that seed, so an inline <c>(?V0)</c> behaves
    /// exactly like passing this value from the start.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#v0-really-means-version-0-where-upstreams-own-algorithm-would-leave-version-1s-fullcase-on">the comparison page</see>.
    /// </para>
    /// <para>
    /// <b>The "unterminated character set" parse error names <c>FuzzyRegexOptions.Version0</c>
    /// and the <c>\[</c> escape</b>, but only for a pattern version 0 actually accepts: under
    /// <see cref="Version1"/> (the default) an unescaped <c>[</c> inside a set, such as
    /// <c>[[]</c>, stops compiling, and the message tells a caller arriving from <c>Regex</c> how
    /// to get back the literal.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#the-unterminated-character-set-parse-error-names-fuzzyregexoptionsversion0-and-the--escape">the comparison page</see>.
    /// </para>
    /// </remarks>
    Version0 = 0x2000,

    /// <summary>
    /// <b>The default.</b> Nested sets and set operations (<c>[[a-z]--[aeiou]]</c>), and full case
    /// folding when matching case-insensitively. Upstream <c>VERSION1</c> / <c>V1</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Version 1 is the default</b> (nested sets and set operations; full case-folding under
    /// <see cref="IgnoreCase"/>). Upstream defaults to <c>VERSION0</c> for <c>re</c>
    /// compatibility; this library has no such users to protect, and the two behaviours version
    /// 1 adds are the reason to use it over <c>System.Text.RegularExpressions</c>. See
    /// <c>docs/DIVERGENCES.md</c>.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#version-1-is-the-default">the comparison page</see>.
    /// </para>
    /// <para>
    /// The one pattern that changes meaning is an unescaped <c>[</c> inside a set: <c>[[]</c> is a
    /// set containing <c>[</c> under <see cref="Version0"/> and an unterminated nested set here.
    /// The parse error says so and names both ways out.
    /// See <see href="https://github.com/zejji/fuzzy-regex-cs/blob/main/docs/COMPARISON.md#the-unterminated-character-set-parse-error-names-fuzzyregexoptionsversion0-and-the--escape">the comparison page</see>.
    /// </para>
    /// </remarks>
    Version1 = 0x100,
}
