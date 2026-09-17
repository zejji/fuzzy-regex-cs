using System.Globalization;
using System.Numerics;
using System.Text;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Parsing;

/// <summary>
/// The module-level parse and compile helpers of <c>upstream/regex/_regex_core.py</c>: everything
/// that is a plain function there rather than a method on a node.
/// </summary>
/// <remarks>
/// <para>
/// This slice (S07) reads patterns made of literals, dots and groups. Every other construct's
/// branch is present and throws <see cref="NotImplementedException"/> with a <c>needs:</c> tag
/// naming what would deliver it, which is what turns the corresponding compile-parity corpus rows
/// into skips rather than failures, and what the status board's "waiting on a capability" table is
/// built from.
/// </para>
/// <para>
/// The branches are in upstream's order even where a later slice will fill them in, so that an
/// upstream diff still lands on the right line.
/// </para>
/// </remarks>
internal static class ParseFunctions
{
    /// <summary>Upstream <c>_POSIX_CLASSES</c> (line 1727).</summary>
    private static readonly System.Collections.Frozen.FrozenSet<string> _posixClasses =
        System.Collections.Frozen.FrozenSet.ToFrozenSet(["ALNUM", "DIGIT", "PUNCT", "XDIGIT"], StringComparer.Ordinal);

    /// <summary>Upstream <c>_BINARY_VALUES</c> (line 1729).</summary>
    private static readonly System.Collections.Frozen.FrozenSet<string> _binaryValues =
        System.Collections.Frozen.FrozenSet.ToFrozenSet(
            ["YES", "Y", "NO", "N", "TRUE", "T", "FALSE", "F"],
            StringComparer.Ordinal
        );

    /// <summary>Upstream <c>SET_OPS</c> (line 183).</summary>
    private static readonly string[] _setOps = ["||", "~~", "&&", "--"];

    /// <summary>
    /// Upstream <c>VERBS</c> (lines 4671-4676): the backtracking control verbs <c>(*FAIL)</c>,
    /// <c>(*F)</c>, <c>(*PRUNE)</c> and <c>(*SKIP)</c>.
    /// </summary>
    /// <remarks>
    /// Upstream's table holds four shared singleton nodes; these are factories, because a node
    /// built here is handed straight into the parse tree and this port builds a fresh one each
    /// time, as <see cref="PositionEscape"/> does.
    /// </remarks>
    internal static readonly IReadOnlyDictionary<string, Func<RegexBase>> Verbs = new Dictionary<
        string,
        Func<RegexBase>
    >(StringComparer.Ordinal)
    {
        ["FAIL"] = static () => new Failure(),
        ["F"] = static () => new Failure(),
        ["PRUNE"] = static () => new Prune(),
        ["SKIP"] = static () => new Skip(),
    };

    /// <summary>
    /// Whether a character has more than one case. Upstream <c>is_cased_i</c>
    /// (<c>upstream/regex/_regex_core.py</c> lines 362-364), which asks the engine's
    /// <c>get_all_cases</c> - a four-level lookup into the generated Unicode tables
    /// (<c>upstream/src/_regex_unicode.c</c> line 30627).
    /// </summary>
    /// <param name="info">The parse state, whose flags select the encoding.</param>
    /// <param name="ch">The codepoint to test.</param>
    /// <returns><see langword="true"/> if the character is cased.</returns>
    /// <remarks>
    /// The <c>LOCALE</c> encoding throws <see cref="NotSupportedException"/> with a public-facing
    /// message: its casing comes from the C locale, not from a table.
    /// </remarks>
    internal static bool IsCasedI(Info info, int ch)
    {
        ArgumentNullException.ThrowIfNull(info);

        return RegexModule.GetAllCases(info.Flags, (uint)ch).Length > 1;
    }

    /// <summary>Upstream <c>make_case_flags</c> (lines 419-427).</summary>
    /// <param name="info">The parse state.</param>
    /// <returns>The case flags a node built here should carry.</returns>
    internal static int MakeCaseFlags(Info info)
    {
        int flags = info.Flags & RegexFlags.CaseFlags;

        // Turn off FULLCASE if ASCII is turned on.
        if ((info.Flags & RegexFlags.Ascii) != 0)
        {
            flags &= ~RegexFlags.FullCase;
        }

        return flags;
    }

    /// <summary>Upstream <c>make_character</c> (lines 429-435).</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="value">The codepoint.</param>
    /// <param name="inSet">Whether the character is inside a character set.</param>
    /// <returns>The character node.</returns>
    internal static Character MakeCharacter(Info info, int value, bool inSet = false) =>
        // A character set is built case-sensitively.
        inSet ? new Character(value) : new Character(value, caseFlags: MakeCaseFlags(info));

    /// <summary>Upstream <c>make_ref_group</c> (lines 437-439).</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="name">The group name or number, as the pattern wrote it.</param>
    /// <param name="position">Where the reference started, for the error messages.</param>
    /// <returns>The backreference.</returns>
    internal static RefGroup MakeRefGroup(Info info, string name, int position) =>
        new(info, name, position, caseFlags: MakeCaseFlags(info));

    /// <summary>Upstream <c>make_string_set</c> (lines 441-443).</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="name">The named list's name.</param>
    /// <returns>The named-list reference.</returns>
    internal static StringSet MakeStringSet(Info info, string name) => new(info, name, caseFlags: MakeCaseFlags(info));

    /// <summary>
    /// Python's <c>int(text)</c> as the three group-resolving nodes call it: the value if the text
    /// is a number, otherwise a signal to look the text up as a name.
    /// </summary>
    /// <param name="text">The group name or number, as the pattern wrote it.</param>
    /// <param name="group">The number, saturated to <see cref="int"/>'s range.</param>
    /// <returns><see langword="false"/> where upstream's <c>int()</c> raises <c>ValueError</c>.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="BigInteger"/> and not <see cref="int"/>, because Python's <c>int</c> has no width:
    /// <c>\g&lt;99999999999999999999&gt;</c> parses there and is then rejected by the range check as
    /// "invalid group reference". Narrowing with <see cref="int.TryParse(string, out int)"/> would
    /// fail the parse instead and reach the name lookup, giving "unknown group". Saturating is safe
    /// because a group count can never approach <see cref="int.MaxValue"/>, so a saturated value is
    /// on the same side of every range check as the true one.
    /// </para>
    /// <para>
    /// <see cref="PythonStr.TryParseInt"/> and not <c>BigInteger.Parse</c>, because Python's
    /// <c>int()</c> accepts <b>any</b> Unicode decimal digit and the invariant culture accepts only
    /// ASCII ones: <c>(?P=١)</c> is a reference to group 1 upstream, and reaches this method with
    /// the name still spelled in Arabic-Indic digits. Pinned by <c>UnicodeDigitGroupNameTests</c>.
    /// </para>
    /// </remarks>
    internal static bool TryParseGroupNumber(string text, out int group)
    {
        if (!PythonStr.TryParseInt(text, out BigInteger value))
        {
            group = 0;
            return false;
        }

        group = value > int.MaxValue ? int.MaxValue : (int)value;
        return true;
    }

    /// <summary>
    /// Python's <c>int(name)</c> where upstream lets its <c>ValueError</c> escape: the one
    /// <c>parse_name</c> call whose result is compared against <c>min_group</c>.
    /// </summary>
    /// <param name="name">The name, already known to satisfy <c>str.isdigit</c>.</param>
    /// <returns>The value.</returns>
    /// <exception cref="NotSupportedException">
    /// The name is a <c>str.isdigit</c> digit with no <i>decimal</i> value - <c>²</c> and its kin.
    /// </exception>
    /// <remarks>
    /// <c>str.isdigit</c> is <c>Numeric_Type</c> of <c>Decimal</c> <b>or</b> <c>Digit</c> while
    /// <c>int()</c> takes only <c>Decimal</c>, so <c>(?P=²)</c> reaches <c>int("²")</c> and upstream
    /// raises an uncaught <c>ValueError: invalid literal for int() with base 10: '²'</c> - measured
    /// 2026-08-30. As with the three patterns in <c>UpstreamInternalErrorTests</c>, there is no
    /// specified behaviour to port; what matters is that the pattern is rejected rather than
    /// quietly compiled.
    /// </remarks>
    private static BigInteger ParsePythonInt(string name) =>
        PythonStr.TryParseInt(name, out BigInteger value)
            ? value
            : throw new NotSupportedException(
                $"int('{name}') would raise ValueError: the name is a digit with no decimal value"
            );

    /// <summary>Upstream <c>make_property</c> (lines 445-450).</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="prop">The property node.</param>
    /// <param name="inSet">Whether the property is inside a character set.</param>
    /// <returns>The property, with the case flags a node built here should carry.</returns>
    internal static RegexBase MakeProperty(Info info, Property prop, bool inSet)
    {
        ArgumentNullException.ThrowIfNull(prop);

        // A character set is built case-sensitively.
        return inSet ? prop : prop.WithFlags(caseFlags: MakeCaseFlags(info));
    }

    /// <summary>Upstream <c>_parse_pattern</c> (lines 452-460).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The parsed pattern.</returns>
    internal static RegexBase ParsePattern(Source source, Info info)
    {
        List<RegexBase> branches = [ParseSequence(source, info)];
        while (source.MatchText("|"))
        {
            branches.Add(ParseSequence(source, info));
        }

        if (branches.Count == 1)
        {
            return branches[0];
        }

        return new Branch(branches);
    }

    /// <summary>Upstream <c>parse_sequence</c> (lines 462-546).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The parsed sequence.</returns>
    internal static RegexBase ParseSequence(Source source, Info info)
    {
        // Upstream seeds the list with None as the "no element yet" marker a quantifier looks for,
        // and strips the Nones at the end.
        List<RegexBase?> sequence = [null];
        int caseFlags = MakeCaseFlags(info);

        while (true)
        {
            int savedPos = source.Pos;
            int ch = source.Get();
            if (RegexFlags.IsSpecial(ch))
            {
                if (ch is ')' or '|' or Source.EndOfSource)
                {
                    // The end of a sequence. At the end of the pattern ch is "".
                    source.Pos = savedPos;
                    break;
                }

                switch (ch)
                {
                    case '\\':
                        // An escape sequence outside a set.
                        sequence.Add(ParseEscape(source, info, inSet: false));
                        break;

                    case '(':
                        // A parenthesised subpattern or a flag.
                        RegexBase? element = ParseParen(source, info);
                        if (element is null)
                        {
                            caseFlags = MakeCaseFlags(info);
                        }
                        else
                        {
                            sequence.Add(element);
                        }

                        break;

                    case '.':
                        // Any character.
                        if ((info.Flags & RegexFlags.DotAll) != 0)
                        {
                            sequence.Add(new AnyAll());
                        }
                        else if ((info.Flags & RegexFlags.Word) != 0)
                        {
                            sequence.Add(new AnyU());
                        }
                        else
                        {
                            sequence.Add(new Any());
                        }

                        break;

                    case '[':
                        // A character set.
                        sequence.Add(ParseSet(source, info));
                        break;

                    case '^':
                        // The start of a line or the string.
                        if ((info.Flags & RegexFlags.Multiline) != 0)
                        {
                            sequence.Add((info.Flags & RegexFlags.Word) != 0 ? new StartOfLineU() : new StartOfLine());
                        }
                        else
                        {
                            sequence.Add(new StartOfString());
                        }

                        break;

                    case '$':
                        // The end of a line or the string.
                        if ((info.Flags & RegexFlags.Multiline) != 0)
                        {
                            sequence.Add((info.Flags & RegexFlags.Word) != 0 ? new EndOfLineU() : new EndOfLine());
                        }
                        else
                        {
                            sequence.Add(
                                (info.Flags & RegexFlags.Word) != 0 ? new EndOfStringLineU() : new EndOfStringLine()
                            );
                        }

                        break;

                    case '?':
                    case '*':
                    case '+':
                    case '{':
                        // Looks like a quantifier.
                        (long MinCount, long? MaxCount)? counts = ParseQuantifier(source, info, ch);
                        if (counts is not null)
                        {
                            // It _is_ a quantifier.
                            ApplyQuantifier(source, info, counts.Value, caseFlags, ch, savedPos, sequence);
                            sequence.Add(null);
                        }
                        else
                        {
                            // It's not a quantifier. Maybe it's a fuzzy constraint.
                            FuzzyConstraints? constraints = ParseFuzzy(source, info, ch, caseFlags);

                            if (constraints is not null)
                            {
                                // It _is_ a fuzzy constraint.
                                if (constraints.IsActuallyFuzzy())
                                {
                                    ApplyConstraint(source, info, constraints, caseFlags, savedPos, sequence);
                                    sequence.Add(null);
                                }
                            }
                            else
                            {
                                // The element was just a literal. This is what makes `a{`, `{}` and
                                // `{x}` ordinary literal braces.
                                sequence.Add(new Character(ch, caseFlags: caseFlags));
                            }
                        }

                        break;

                    default:
                        // A literal.
                        sequence.Add(new Character(ch, caseFlags: caseFlags));
                        break;
                }
            }
            else
            {
                // A literal.
                sequence.Add(new Character(ch, caseFlags: caseFlags));
            }
        }

        return new Sequence([.. sequence.Where(static item => item is not null).Select(static item => item!)]);
    }

    /// <summary>Upstream <c>apply_quantifier</c> (lines 558-588).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="counts">The minimum and maximum repeat counts.</param>
    /// <param name="caseFlags">The case flags in force; upstream takes it and does not use it.</param>
    /// <param name="ch">The quantifier character; upstream takes it and overwrites it immediately.</param>
    /// <param name="savedPos">Where the quantifier started, for the error messages.</param>
    /// <param name="sequence">The sequence so far, whose last element the quantifier applies to.</param>
    internal static void ApplyQuantifier(
        Source source,
        Info info,
        (long MinCount, long? MaxCount) counts,
        int caseFlags,
        int ch,
        int savedPos,
        List<RegexBase?> sequence
    )
    {
        _ = (info, caseFlags, ch);

        RegexBase? element = sequence[^1];
        sequence.RemoveAt(sequence.Count - 1);
        if (element is null)
        {
            throw sequence.Count > 0
                ? new FuzzyRegexParseException("multiple repeat", source.String, savedPos)
                : new FuzzyRegexParseException("nothing to repeat", source.String, savedPos);
        }

        if (element is GreedyRepeat)
        {
            // GreedyRepeat covers LazyRepeat and PossessiveRepeat, which derive from it, exactly as
            // upstream's isinstance tuple does.
            throw new FuzzyRegexParseException("multiple repeat", source.String, savedPos);
        }

        (long minCount, long? maxCount) = counts;
        int savedPos2 = source.Pos;
        int suffix = source.Get();

        Func<RegexBase, long, long?, GreedyRepeat> repeated;
        if (suffix == '?')
        {
            // The "?" suffix that means it's a lazy repeat.
            repeated = static (s, min, max) => new LazyRepeat(s, min, max);
        }
        else if (suffix == '+')
        {
            // The "+" suffix that means it's a possessive repeat.
            repeated = static (s, min, max) => new PossessiveRepeat(s, min, max);
        }
        else
        {
            // No suffix means that it's a greedy repeat.
            source.Pos = savedPos2;
            repeated = static (s, min, max) => new GreedyRepeat(s, min, max);
        }

        // Ignore the quantifier if it applies to a zero-width item or the number of repeats is
        // fixed at 1.
        if (!element.IsEmpty() && (minCount != 1 || maxCount != 1))
        {
            element = repeated(element, minCount, maxCount);
        }

        sequence.Add(element);
    }

    /// <summary>Upstream <c>parse_quantifier</c> (lines 606-619).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state; upstream takes it and does not use it.</param>
    /// <param name="ch">The character that looked like a quantifier.</param>
    /// <returns>The repeat counts, or <see langword="null"/> if this is not a quantifier.</returns>
    internal static (long MinCount, long? MaxCount)? ParseQuantifier(Source source, Info info, int ch)
    {
        _ = info;

        // Upstream _QUANTIFIERS (line 604).
        switch (ch)
        {
            case '?':
                return (0, 1);
            case '*':
                return (0, null);
            case '+':
                return (1, null);
            case '{':
                // Looks like a limited repeated element, eg. 'a{2,3}'.
                return ParseLimitedQuantifier(source);
            default:
                return null;
        }
    }

    /// <summary>Upstream <c>is_above_limit</c> (lines 621-623).</summary>
    /// <param name="count">The count, or <see langword="null"/> for unlimited.</param>
    /// <returns><see langword="true"/> if the count is at or above <see cref="RegexFlags.Unlimited"/>.</returns>
    internal static bool IsAboveLimit(BigInteger? count) => count is not null && count.Value >= RegexFlags.Unlimited;

    /// <summary>Upstream <c>parse_limited_quantifier</c> (lines 625-653).</summary>
    /// <param name="source">The scanner.</param>
    /// <returns>The repeat counts, or <see langword="null"/> if this is not a quantifier after all.</returns>
    /// <remarks>
    /// The counts are <see cref="BigInteger"/> while they are being read, because upstream's
    /// <c>int()</c> has no width and <c>a{99999999999999999999</c> - no closing brace - must reach
    /// the "not a quantifier" return rather than overflow on the way there. Only counts that have
    /// passed <see cref="IsAboveLimit"/> are narrowed, and those fit in a <see cref="uint"/>.
    /// </remarks>
    internal static (long MinCount, long? MaxCount)? ParseLimitedQuantifier(Source source)
    {
        int savedPos = source.Pos;
        string minDigits = ParseCount(source);
        BigInteger minCount;
        BigInteger? maxCount;
        if (source.MatchText(","))
        {
            string maxDigits = ParseCount(source);

            // No minimum means 0 and no maximum means unlimited.
            minCount = minDigits.Length == 0 ? 0 : BigInteger.Parse(minDigits, CultureInfo.InvariantCulture);
            maxCount = maxDigits.Length == 0 ? null : BigInteger.Parse(maxDigits, CultureInfo.InvariantCulture);
        }
        else
        {
            if (minDigits.Length == 0)
            {
                source.Pos = savedPos;
                return null;
            }

            minCount = BigInteger.Parse(minDigits, CultureInfo.InvariantCulture);
            maxCount = minCount;
        }

        if (!source.MatchText("}"))
        {
            source.Pos = savedPos;
            return null;
        }

        if (IsAboveLimit(minCount) || IsAboveLimit(maxCount))
        {
            throw new FuzzyRegexParseException("repeat count too big", source.String, savedPos);
        }

        if (maxCount is not null && minCount > maxCount.Value)
        {
            throw new FuzzyRegexParseException("min repeat greater than max repeat", source.String, savedPos);
        }

        return ((long)minCount, maxCount is null ? null : (long)maxCount.Value);
    }

    /// <summary>Upstream <c>apply_constraint</c> (lines 590-602).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state; upstream takes it and does not use it.</param>
    /// <param name="constraints">The constraints the fuzzy section carries.</param>
    /// <param name="caseFlags">The case flags in force; upstream takes it and does not use it.</param>
    /// <param name="savedPos">Where the constraint started, for the error message.</param>
    /// <param name="sequence">The sequence so far, whose last element the constraint applies to.</param>
    internal static void ApplyConstraint(
        Source source,
        Info info,
        FuzzyConstraints constraints,
        int caseFlags,
        int savedPos,
        List<RegexBase?> sequence
    )
    {
        _ = (info, caseFlags);

        RegexBase? element = sequence[^1];
        sequence.RemoveAt(sequence.Count - 1);
        if (element is null)
        {
            throw new FuzzyRegexParseException("nothing for fuzzy constraint", source.String, savedPos);
        }

        // If a group is marked as fuzzy then put all of the fuzzy part in the group.
        if (element is Group group)
        {
            group.Subpattern = new Fuzzy(group.Subpattern, constraints);
            sequence.Add(group);
        }
        else
        {
            sequence.Add(new Fuzzy(element, constraints));
        }
    }

    /// <summary>Upstream <c>parse_fuzzy</c> (lines 655-677).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="ch">The character that started the constraint.</param>
    /// <param name="caseFlags">The case flags in force.</param>
    /// <returns>The constraints, or <see langword="null"/> if this is not a fuzzy constraint.</returns>
    internal static FuzzyConstraints? ParseFuzzy(Source source, Info info, int ch, int caseFlags)
    {
        int savedPos = source.Pos;

        if (ch != '{')
        {
            return null;
        }

        var constraints = new FuzzyConstraints();
        try
        {
            ParseFuzzyItem(source, constraints);
            while (source.MatchText(","))
            {
                ParseFuzzyItem(source, constraints);
            }
        }
        catch (ParseErrorException)
        {
            source.Pos = savedPos;
            return null;
        }

        if (source.MatchText(":"))
        {
            constraints.Test = ParseFuzzyTest(source, info, caseFlags);
        }

        if (!source.MatchText("}"))
        {
            throw new FuzzyRegexParseException("expected }", source.String, source.Pos);
        }

        return constraints;
    }

    /// <summary>Upstream <c>parse_fuzzy_item</c> (lines 679-687).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="constraints">The constraints built so far, which this adds to.</param>
    internal static void ParseFuzzyItem(Source source, FuzzyConstraints constraints)
    {
        int savedPos = source.Pos;
        try
        {
            ParseCostConstraint(source, constraints);
        }
        catch (ParseErrorException)
        {
            source.Pos = savedPos;

            ParseCostEquation(source, constraints);
        }
    }

    /// <summary>Upstream <c>parse_cost_constraint</c> (lines 689-748).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="constraints">The constraints built so far, which this adds to.</param>
    internal static void ParseCostConstraint(Source source, FuzzyConstraints constraints)
    {
        int savedPos = source.Pos;
        int ch = source.Get();
        if (RegexFlags.IsAlpha(ch))
        {
            // Syntax: constraint [("<=" | "<") cost]
            char constraint = ParseConstraint(constraints, ch);

            bool? maxInc = ParseFuzzyCompare(source);

            if (maxInc is null)
            {
                // No maximum cost.
                constraints.Limits[constraint] = (0, null);
            }
            else
            {
                // There's a maximum cost.
                int costPos = source.Pos;
                BigInteger maxCost = ParseCostLimit(source);

                // Inclusive or exclusive limit?
                if (!maxInc.Value)
                {
                    maxCost -= 1;
                }

                if (maxCost < 0)
                {
                    throw new FuzzyRegexParseException("bad fuzzy cost limit", source.String, costPos);
                }

                constraints.Limits[constraint] = (0, maxCost);
            }
        }
        else if (RegexFlags.IsDigit(ch))
        {
            // Syntax: cost ("<=" | "<") constraint ("<=" | "<") cost
            source.Pos = savedPos;

            // Minimum cost. Upstream assigns cost_pos twice; only the second assignment is read.
            BigInteger minCost = ParseCostLimit(source);

            bool minInc = ParseFuzzyCompare(source) ?? throw new ParseErrorException();

            char constraint = ParseConstraint(constraints, source.Get());

            bool maxInc = ParseFuzzyCompare(source) ?? throw new ParseErrorException();

            // Maximum cost.
            int costPos = source.Pos;
            BigInteger maxCost = ParseCostLimit(source);

            // Inclusive or exclusive limits?
            if (!minInc)
            {
                minCost += 1;
            }

            if (!maxInc)
            {
                maxCost -= 1;
            }

            if (!(minCost >= 0 && minCost <= maxCost))
            {
                throw new FuzzyRegexParseException("bad fuzzy cost limit", source.String, costPos);
            }

            constraints.Limits[constraint] = (minCost, maxCost);
        }
        else
        {
            throw new ParseErrorException();
        }
    }

    /// <summary>Upstream <c>parse_cost_limit</c> (lines 750-760).</summary>
    /// <param name="source">The scanner.</param>
    /// <returns>The cost.</returns>
    /// <remarks>
    /// <para>
    /// A <see cref="BigInteger"/>, and not saturated anywhere, because upstream's <c>int(digits)</c>
    /// is unbounded and its callers then do arithmetic on it: <c>max_cost -= 1</c>,
    /// <c>min_cost += 1</c> and <c>not 0 &lt;= min_cost &lt;= max_cost</c>. A fuzzy cost is the one
    /// number upstream range-checks nowhere - <c>is_above_limit</c> guards repeat counts and
    /// nothing guards these - so any ceiling put on the value here is observable as a pattern this
    /// port rejects and upstream compiles.
    /// </para>
    /// <para>
    /// Two drafts of this got that wrong, both caught by an S13 blind review pass. Saturating at
    /// <see cref="RegexFlags.Unlimited"/> made <c>{i&lt;4294967296}</c> subtract from the ceiling
    /// and cap at 4294967294, and made <c>{4294967296&lt;=i&lt;=4294967295}</c> compile.
    /// Saturating at <see cref="long.MaxValue"/> only moved the same fault upwards: with the
    /// ceiling equal to the arithmetic type's maximum, <c>{9223372036854775807&lt;i&lt;=…}</c>
    /// overflows to a negative minimum, and any two distinct values above the ceiling compare
    /// equal, so <c>{9223372036854775807&lt;=i&lt;9223372036854775808}</c> is rejected too - all
    /// three measured against regex 2026.7.19 on 2026-08-31 and all three accepted there. The
    /// clamp to <see cref="RegexFlags.Unlimited"/> belongs at the emitted code word, in
    /// <c>Fuzzy._compile</c>, and only there.
    /// </para>
    /// </remarks>
    internal static BigInteger ParseCostLimit(Source source)
    {
        int costPos = source.Pos;
        string digits = ParseCount(source);

        if (digits.Length > 0)
        {
            return BigInteger.Parse(digits, CultureInfo.InvariantCulture);
        }

        throw new FuzzyRegexParseException("bad fuzzy cost limit", source.String, costPos);
    }

    /// <summary>Upstream <c>parse_constraint</c> (lines 762-770).</summary>
    /// <param name="constraints">The constraints built so far, checked for a duplicate.</param>
    /// <param name="ch">The constraint letter.</param>
    /// <returns>The constraint letter.</returns>
    /// <remarks>Upstream takes <c>source</c> as well and does not use it.</remarks>
    internal static char ParseConstraint(FuzzyConstraints constraints, int ch)
    {
        if (ch is not ('d' or 'e' or 'i' or 's'))
        {
            throw new ParseErrorException();
        }

        if (constraints.Limits.ContainsKey((char)ch))
        {
            throw new ParseErrorException();
        }

        return (char)ch;
    }

    /// <summary>Upstream <c>parse_fuzzy_compare</c> (lines 772-779).</summary>
    /// <param name="source">The scanner.</param>
    /// <returns>
    /// <see langword="true"/> for <c>&lt;=</c>, <see langword="false"/> for <c>&lt;</c>, and
    /// <see langword="null"/> for neither.
    /// </returns>
    internal static bool? ParseFuzzyCompare(Source source)
    {
        if (source.MatchText("<="))
        {
            return true;
        }

        return source.MatchText("<") ? false : null;
    }

    /// <summary>Upstream <c>parse_cost_equation</c> (lines 781-806).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="constraints">The constraints built so far, which this adds the equation to.</param>
    internal static void ParseCostEquation(Source source, FuzzyConstraints constraints)
    {
        if (constraints.Cost is not null)
        {
            throw new FuzzyRegexParseException("more than one cost equation", source.String, source.Pos);
        }

        var cost = new FuzzyConstraints.CostEquation();

        ParseCostTerm(source, cost);
        while (source.MatchText("+"))
        {
            ParseCostTerm(source, cost);
        }

        bool maxInc = ParseFuzzyCompare(source) ?? throw new ParseErrorException();

        // Upstream's int(parse_count(source)) raises ValueError on no digits at all, which is not
        // its own error type; ParseCostLimit raises the "bad fuzzy cost limit" upstream would have
        // raised one line later anyway, at the same position.
        BigInteger maxCost = ParseCostLimit(source);

        if (!maxInc)
        {
            maxCost -= 1;
        }

        if (maxCost < 0)
        {
            throw new FuzzyRegexParseException("bad fuzzy cost limit", source.String, source.Pos);
        }

        cost.Max = maxCost;

        constraints.Cost = cost;
    }

    /// <summary>Upstream <c>parse_cost_term</c> (lines 808-818).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="cost">The cost equation built so far, which this adds a term to.</param>
    internal static void ParseCostTerm(Source source, FuzzyConstraints.CostEquation cost)
    {
        string coeff = ParseCount(source);
        int ch = source.Get();
        if (ch is not ('d' or 'i' or 's'))
        {
            throw new ParseErrorException();
        }

        if (cost.Coefficients.ContainsKey((char)ch))
        {
            throw new FuzzyRegexParseException("repeated fuzzy cost", source.String, source.Pos);
        }

        // Upstream's `int(coeff or 1)`: no digits means a coefficient of 1. Saturated for the same
        // reason ParseCostLimit is, and at the same place.
        cost.Coefficients[(char)ch] =
            coeff.Length == 0 ? BigInteger.One : BigInteger.Parse(coeff, CultureInfo.InvariantCulture);
    }

    /// <summary>Upstream <c>parse_fuzzy_test</c> (lines 820-844).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="caseFlags">The case flags in force.</param>
    /// <returns>What a substituted character has to match.</returns>
    internal static RegexBase ParseFuzzyTest(Source source, Info info, int caseFlags)
    {
        int savedPos = source.Pos;
        int ch = source.Get();
        if (RegexFlags.IsSpecial(ch))
        {
            switch (ch)
            {
                case '\\':
                    // An escape sequence outside a set.
                    return ParseEscape(source, info, false);

                case '.':
                    // Any character.
                    if ((info.Flags & RegexFlags.DotAll) != 0)
                    {
                        return new AnyAll();
                    }

                    return (info.Flags & RegexFlags.Word) != 0 ? new AnyU() : new Any();

                case '[':
                    // A character set.
                    return ParseSet(source, info);

                default:
                    throw new FuzzyRegexParseException("expected character set", source.String, savedPos);
            }
        }

        if (ch != Source.EndOfSource)
        {
            // A literal.
            return new Character(ch, caseFlags: caseFlags);
        }

        throw new FuzzyRegexParseException("expected character set", source.String, savedPos);
    }

    /// <summary>Upstream <c>parse_count</c> (lines 846-848).</summary>
    /// <param name="source">The scanner.</param>
    /// <returns>The digits, which may be empty.</returns>
    internal static string ParseCount(Source source) => source.GetWhile(RegexFlags.IsDigit);

    /// <summary>Upstream <c>parse_paren</c> (lines 850-940).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The subpattern, or <see langword="null"/> when the parenthesis held only inline flags.</returns>
    internal static RegexBase? ParseParen(Source source, Info info)
    {
        int savedPos = source.Pos;
        int ch = source.Get(overrideIgnore: true);
        if (ch == '?')
        {
            // (?...
            int savedPos2 = source.Pos;
            ch = source.Get(overrideIgnore: true);
            if (ch == '<')
            {
                // (?<...
                int savedPos3 = source.Pos;
                ch = source.Get();
                if (ch is '=' or '!')
                {
                    // (?<=... or (?<!...: lookbehind.
                    return ParseLookaround(source, info, behind: true, positive: ch == '=');
                }

                // (?<...: a named capture group.
                source.Pos = savedPos3;
                return ParseNamedGroup(source, info);
            }

            if (ch is '=' or '!')
            {
                // (?=... or (?!...: lookahead.
                return ParseLookaround(source, info, behind: false, positive: ch == '=');
            }

            if (ch == 'P')
            {
                // (?P...: a Python extension.
                return ParseExtension(source, info);
            }

            if (ch == '#')
            {
                // (?#...: a comment.
                ParseComment(source);
                return null;
            }

            if (ch == '(')
            {
                // (?(...: a conditional subpattern.
                return ParseConditional(source, info);
            }

            if (ch == '>')
            {
                // (?>...: an atomic subpattern.
                return ParseAtomic(source, info);
            }

            if (ch == '|')
            {
                // (?|...: a common/reset groups branch.
                return ParseCommon(source, info);
            }

            if (ch is 'R' or (>= '0' and <= '9'))
            {
                // (?R...: probably a call to a group.
                return ParseCallGroup(source, info, ch, savedPos2);
            }

            if (ch == '&')
            {
                // (?&...: a call to a named group.
                return ParseCallNamedGroup(source, info, savedPos2);
            }

            if (ch is '+' or '-' && RegexFlags.IsDigit(source.Peek()))
            {
                return ParseRelCallGroup(source, info, ch, savedPos2);
            }

            // (?...: probably a flags subpattern.
            source.Pos = savedPos2;
            return ParseFlagsSubpattern(source, info);
        }

        if (ch == '*')
        {
            // (*...
            int savedPos2 = source.Pos;
            string word = source.GetWhile(static c => c is ')' or '>', include: false);

            // Upstream's test is word[:1].isalpha(), which is Unicode-aware: measured against the
            // local oracle 2026-08-30, '(*e)' and '(*Ab)' both fail with "unknown verb" at
            // position 2 while '(*\U0001F600)' falls through to "nothing to repeat" at 1, so the
            // branch really does turn on Unicode letterhood and not on being ASCII. word[:1] is
            // one codepoint to Python, so a supplementary-plane first character is one character
            // there and a surrogate pair here; TryGetRuneAt reunites them, and answers false for
            // an unpaired surrogate, which is not alphabetic either. The length check is not
            // redundant: TryGetRuneAt throws rather than answering false for index 0 of "".
            if (word.Length > 0 && Rune.TryGetRuneAt(word, 0, out Rune firstRune) && PythonStr.IsAlpha(firstRune.Value))
            {
                if (!Verbs.TryGetValue(word, out Func<RegexBase>? verb))
                {
                    throw new FuzzyRegexParseException("unknown verb", source.String, savedPos2);
                }

                source.Expect(")");

                return verb();
            }

            // Upstream falls through to an unnamed capture group when the word is not alphabetic,
            // without rewinding to savedPos2 - the rewind below goes all the way back to savedPos.
        }

        // (...: an unnamed capture group.
        source.Pos = savedPos;
        int group = info.OpenGroup();
        int savedFlags = info.Flags;
        RegexBase subpattern;
        try
        {
            subpattern = ParsePattern(source, info);
            source.Expect(")");
        }
        finally
        {
            info.Flags = savedFlags;
            source.IgnoreSpace = (info.Flags & RegexFlags.Verbose) != 0;
        }

        info.CloseGroup();

        return new Group(info, group, subpattern);
    }

    /// <summary>Upstream <c>parse_comment</c> (lines 978-993).</summary>
    /// <param name="source">The scanner, positioned just after the <c>(?#</c>.</param>
    /// <remarks>
    /// The loop leaves the position on the closing parenthesis and <c>expect</c> then consumes it,
    /// so an unterminated comment is "missing )" rather than a silently swallowed rest of pattern.
    /// A backslash escapes the next character, which is how <c>(?#c\)x)</c> comments out a
    /// parenthesis.
    /// </remarks>
    internal static void ParseComment(Source source)
    {
        int savedPos;
        while (true)
        {
            savedPos = source.Pos;
            int c = source.Get(overrideIgnore: true);

            if (c is Source.EndOfSource or ')')
            {
                break;
            }

            if (c == '\\')
            {
                _ = source.Get(overrideIgnore: true);
            }
        }

        source.Pos = savedPos;
        source.Expect(")");
    }

    /// <summary>Upstream <c>parse_lookaround</c> (lines 995-1005).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="behind">Whether this looks behind rather than ahead.</param>
    /// <param name="positive">Whether the subpattern must match or must not.</param>
    /// <returns>The lookaround.</returns>
    internal static RegexBase ParseLookaround(Source source, Info info, bool behind, bool positive)
    {
        int savedFlags = info.Flags;
        RegexBase subpattern;
        try
        {
            subpattern = ParsePattern(source, info);
            source.Expect(")");
        }
        finally
        {
            info.Flags = savedFlags;
            source.IgnoreSpace = (info.Flags & RegexFlags.Verbose) != 0;
        }

        return new LookAround(behind, positive, subpattern);
    }

    /// <summary>Upstream <c>parse_conditional</c> (lines 1007-1048).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The conditional.</returns>
    internal static RegexBase ParseConditional(Source source, Info info)
    {
        int savedFlags = info.Flags;
        int savedPos = source.Pos;
        int ch = source.Get();
        if (ch == '?')
        {
            // (?(?...
            ch = source.Get();
            if (ch is '=' or '!')
            {
                // (?(?=... or (?(?!...: lookahead conditional.
                return ParseLookaroundConditional(source, info, behind: false, positive: ch == '=');
            }

            if (ch == '<')
            {
                // (?(?<...
                ch = source.Get();
                if (ch is '=' or '!')
                {
                    // (?(?<=... or (?(?<!...: lookbehind conditional.
                    return ParseLookaroundConditional(source, info, behind: true, positive: ch == '=');
                }
            }

            source.Pos = savedPos;
            throw new FuzzyRegexParseException("expected lookaround conditional", source.String, source.Pos);
        }

        source.Pos = savedPos;
        RegexBase yesBranch;
        RegexBase noBranch;
        string group;
        try
        {
            group = ParseName(source, allowNumeric: true);
            source.Expect(")");
            yesBranch = ParseSequence(source, info);
            noBranch = source.MatchText("|") ? ParseSequence(source, info) : new Sequence();

            source.Expect(")");
        }
        finally
        {
            info.Flags = savedFlags;
            source.IgnoreSpace = (info.Flags & RegexFlags.Verbose) != 0;
        }

        if (yesBranch.IsEmpty() && noBranch.IsEmpty())
        {
            return new Sequence();
        }

        return new Conditional(info, group, yesBranch, noBranch, savedPos);
    }

    /// <summary>Upstream <c>parse_lookaround_conditional</c> (lines 1050-1068).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="behind">Whether the test looks behind rather than ahead.</param>
    /// <param name="positive">Whether the test must match or must not.</param>
    /// <returns>The conditional.</returns>
    internal static RegexBase ParseLookaroundConditional(Source source, Info info, bool behind, bool positive)
    {
        int savedFlags = info.Flags;
        RegexBase subpattern;
        try
        {
            subpattern = ParsePattern(source, info);
            source.Expect(")");
        }
        finally
        {
            info.Flags = savedFlags;
            source.IgnoreSpace = (info.Flags & RegexFlags.Verbose) != 0;
        }

        RegexBase yesBranch = ParseSequence(source, info);
        RegexBase noBranch = source.MatchText("|") ? ParseSequence(source, info) : new Sequence();

        source.Expect(")");

        return new LookAroundConditional(behind, positive, subpattern, yesBranch, noBranch);
    }

    /// <summary>Upstream <c>parse_atomic</c> (lines 1070-1080).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The atomic subpattern.</returns>
    internal static RegexBase ParseAtomic(Source source, Info info)
    {
        int savedFlags = info.Flags;
        RegexBase subpattern;
        try
        {
            subpattern = ParsePattern(source, info);
            source.Expect(")");
        }
        finally
        {
            info.Flags = savedFlags;
            source.IgnoreSpace = (info.Flags & RegexFlags.Verbose) != 0;
        }

        return new Atomic(subpattern);
    }

    /// <summary>Upstream <c>parse_common</c> (lines 1082-1098).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The branch-reset group's branches.</returns>
    internal static RegexBase ParseCommon(Source source, Info info)
    {
        // Capture group numbers in different branches can reuse the group numbers.
        int initialGroupCount = info.GroupCount;

        // NOT UPSTREAM'S (S50, upstream issue 425): each branch gets its own view of which numbers
        // a reused name has already claimed, and the branch reset as a whole leaves none behind.
        // Saved and restored rather than just cleared, because branch resets nest.
        int[] outerBranchGroupNumbers = [.. info.BranchGroupNumbers];
        info.BranchGroupNumbers.Clear();

        List<RegexBase> branches = [ParseSequence(source, info)];
        int finalGroupCount = info.GroupCount;
        while (source.MatchText("|"))
        {
            info.GroupCount = initialGroupCount;
            info.BranchGroupNumbers.Clear();
            branches.Add(ParseSequence(source, info));
            finalGroupCount = Math.Max(finalGroupCount, info.GroupCount);
        }

        info.BranchGroupNumbers.Clear();
        info.BranchGroupNumbers.UnionWith(outerBranchGroupNumbers);

        info.GroupCount = finalGroupCount;
        source.Expect(")");

        return branches.Count == 1 ? branches[0] : new Branch(branches);
    }

    /// <summary>Upstream <c>parse_call_group</c> (lines 1100-1109).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="ch">The character that started the call: <c>R</c>, or the first digit.</param>
    /// <param name="pos">Where the call started, for the error messages.</param>
    /// <returns>The group call.</returns>
    internal static RegexBase ParseCallGroup(Source source, Info info, int ch, int pos)
    {
        string group = ch == 'R' ? "0" : ((char)ch).ToString() + source.GetWhile(RegexFlags.IsDigit);

        source.Expect(")");

        return new CallGroup(info, group, pos);
    }

    /// <summary>Upstream <c>parse_rel_call_group</c> (lines 1111-1124).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="ch">The sign, <c>+</c> or <c>-</c>.</param>
    /// <param name="pos">Where the call started, for the error messages.</param>
    /// <returns>The group call.</returns>
    /// <remarks>
    /// The offset is a <see cref="BigInteger"/> for the same reason as
    /// <see cref="ParseLimitedQuantifier"/>: Python's <c>int()</c> has no width, so
    /// <c>(?+99999999999999999999)</c> is arithmetic upstream performs and reports as an unknown
    /// group, not an overflow. The sum is only narrowed once it is known to be in range.
    /// </remarks>
    internal static RegexBase ParseRelCallGroup(Source source, Info info, int ch, int pos)
    {
        string digits = source.GetWhile(RegexFlags.IsDigit);
        if (digits.Length == 0)
        {
            throw new FuzzyRegexParseException("missing relative group number", source.String, source.Pos);
        }

        BigInteger offset = BigInteger.Parse(digits, CultureInfo.InvariantCulture);
        BigInteger group = ch == '+' ? info.GroupCount + offset : info.GroupCount - offset + 1;
        if (group <= 0)
        {
            throw new FuzzyRegexParseException("invalid relative group number", source.String, source.Pos);
        }

        source.Expect(")");

        return new CallGroup(info, group.ToString(CultureInfo.InvariantCulture), pos);
    }

    /// <summary>Upstream <c>parse_call_named_group</c> (lines 1126-1131).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="pos">Where the call started, for the error messages.</param>
    /// <returns>The group call.</returns>
    internal static RegexBase ParseCallNamedGroup(Source source, Info info, int pos)
    {
        string group = ParseName(source);
        source.Expect(")");

        return new CallGroup(info, group, pos);
    }

    /// <summary>Upstream <c>parse_extension</c> (lines 942-976).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The parsed node.</returns>
    internal static RegexBase ParseExtension(Source source, Info info)
    {
        int savedPos = source.Pos;
        int ch = source.Get();
        if (ch == '<')
        {
            // (?P<...: a named capture group.
            return ParseNamedGroup(source, info);
        }

        if (ch == '=')
        {
            // (?P=...: a named group reference.
            string name = ParseName(source, allowNumeric: true);
            source.Expect(")");
            if (info.IsOpenGroup(name))
            {
                throw new FuzzyRegexParseException("cannot refer to an open group", source.String, savedPos);
            }

            return MakeRefGroup(info, name, savedPos);
        }

        if (ch is '>' or '&')
        {
            // (?P>...: a call to a group.
            return ParseCallNamedGroup(source, info, savedPos);
        }

        source.Pos = savedPos;
        throw new FuzzyRegexParseException("unknown extension", source.String, savedPos);
    }

    /// <summary>Upstream <c>parse_flag_set</c> (lines 1133-1147).</summary>
    /// <param name="source">The scanner.</param>
    /// <returns>The flags the letters named.</returns>
    internal static int ParseFlagSet(Source source)
    {
        int flags = 0;

        while (true)
        {
            int savedPos = source.Pos;
            int ch = source.Get();
            string key = ch == Source.EndOfSource ? string.Empty : char.ConvertFromUtf32(ch);
            if (string.Equals(key, "V", StringComparison.Ordinal))
            {
                int next = source.Get();
                key += next == Source.EndOfSource ? string.Empty : char.ConvertFromUtf32(next);
            }

            // Upstream's loop ends on the KeyError from REGEX_FLAGS[ch].
            if (!RegexFlags.InlineFlags.TryGetValue(key, out int flag))
            {
                source.Pos = savedPos;
                return flags;
            }

            flags |= flag;
        }
    }

    /// <summary>Upstream <c>parse_flags</c> (lines 1149-1164).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The flags turned on and the flags turned off.</returns>
    internal static (int FlagsOn, int FlagsOff) ParseFlags(Source source, Info info)
    {
        int flagsOn = ParseFlagSet(source);
        int flagsOff;
        if (source.MatchText("-"))
        {
            flagsOff = ParseFlagSet(source);
            if (flagsOff == 0)
            {
                throw new FuzzyRegexParseException("bad inline flags: no flags after '-'", source.String, source.Pos);
            }
        }
        else
        {
            flagsOff = 0;
        }

        if ((flagsOn & RegexFlags.Locale) != 0)
        {
            // Remember that this pattern has an inline locale flag.
            info.InlineLocale = true;
        }

        return (flagsOn, flagsOff);
    }

    /// <summary>Upstream <c>parse_subpattern</c> (lines 1166-1183).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="flagsOn">The flags this subpattern turns on.</param>
    /// <param name="flagsOff">The flags this subpattern turns off.</param>
    /// <returns>The subpattern.</returns>
    internal static RegexBase ParseSubpattern(Source source, Info info, int flagsOn, int flagsOff)
    {
        int savedFlags = info.Flags;
        info.Flags = (info.Flags | flagsOn) & ~flagsOff;

        // Ensure that there aren't multiple encoding flags set.
        if ((info.Flags & (RegexFlags.Ascii | RegexFlags.Locale | RegexFlags.Unicode)) != 0)
        {
            info.Flags = (info.Flags & ~RegexFlags.AllEncodings) | flagsOn;
        }

        source.IgnoreSpace = (info.Flags & RegexFlags.Verbose) != 0;
        try
        {
            RegexBase subpattern = ParsePattern(source, info);
            source.Expect(")");
            return subpattern;
        }
        finally
        {
            info.Flags = savedFlags;
            source.IgnoreSpace = (info.Flags & RegexFlags.Verbose) != 0;
        }
    }

    /// <summary>Upstream <c>parse_flags_subpattern</c> (lines 1185-1218).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The subpattern, or <see langword="null"/> when the parenthesis held only inline flags.</returns>
    internal static RegexBase? ParseFlagsSubpattern(Source source, Info info)
    {
        (int flagsOn, int flagsOff) = ParseFlags(source, info);

        if ((flagsOff & RegexFlags.GlobalFlags) != 0)
        {
            throw new FuzzyRegexParseException(
                "bad inline flags: cannot turn off global flag",
                source.String,
                source.Pos
            );
        }

        if ((flagsOn & flagsOff) != 0)
        {
            throw new FuzzyRegexParseException("bad inline flags: flag turned on and off", source.String, source.Pos);
        }

        // Handle flags which are global in all regex behaviours.
        int newGlobalFlags = flagsOn & ~info.GlobalFlags & RegexFlags.GlobalFlags;
        if (newGlobalFlags != 0)
        {
            info.GlobalFlags |= newGlobalFlags;

            // A global has been turned on, so reparse the pattern.
            throw new UnscopedFlagSetException(info.GlobalFlags);
        }

        // Ensure that from now on we have only scoped flags.
        flagsOn &= ~RegexFlags.GlobalFlags;

        if (source.MatchText(":"))
        {
            return ParseSubpattern(source, info, flagsOn, flagsOff);
        }

        if (source.MatchText(")"))
        {
            ParsePositionalFlags(source, info, flagsOn, flagsOff);
            return null;
        }

        throw new FuzzyRegexParseException("unknown extension", source.String, source.Pos);
    }

    /// <summary>Upstream <c>parse_positional_flags</c> (lines 1220-1223).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="flagsOn">The flags to turn on.</param>
    /// <param name="flagsOff">The flags to turn off.</param>
    internal static void ParsePositionalFlags(Source source, Info info, int flagsOn, int flagsOff)
    {
        info.Flags = (info.Flags | flagsOn) & ~flagsOff;
        source.IgnoreSpace = (info.Flags & RegexFlags.Verbose) != 0;
    }

    /// <summary>Upstream <c>parse_name</c> (lines 1225-1242).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="allowNumeric">Whether a group number is a legal name here.</param>
    /// <param name="allowGroup0">Whether group 0 is a legal number here.</param>
    /// <returns>The name.</returns>
    internal static string ParseName(Source source, bool allowNumeric = false, bool allowGroup0 = false)
    {
        string name = source.GetWhile(static c => c is ')' or '>', include: false);

        if (name.Length == 0)
        {
            throw new FuzzyRegexParseException("missing group name", source.String, source.Pos);
        }

        if (IsDigitName(name))
        {
            int minGroup = allowGroup0 ? 0 : 1;

            // PythonStr.TryParseInt, not BigInteger.Parse: Python's int() has no fixed width, so
            // upstream compares the value against min_group, finds a huge number is not below it,
            // and carries on to fail on the missing ">" instead - which parse_escape catches and
            // degrades to literals. int.Parse would throw OverflowException straight through that
            // catch and turn `\g<99999999999` into an error upstream does not raise. It also
            // accepts any Unicode decimal digit, which BigInteger.Parse does not: `\g<١>` is a
            // reference to group 1 upstream. Info.IsOpenGroup and CallGroup/RefGroup/Conditional's
            // fix_groups parse the same name and go through the same function. Pinned by
            // GroupReferenceFallbackTests and UnicodeDigitGroupNameTests.
            //
            // The && short-circuits exactly as upstream's `not allow_numeric or int(name) < ...`
            // does, so a numeric name is never converted where numbers are not allowed - which is
            // what keeps `(?<²>a)` a plain "bad character in group name".
            //
            // Not identical above 4300 digits: CPython 3.11+ caps int(str) at
            // sys.get_int_max_str_digits() and raises ValueError, so upstream rejects
            // `\g<` + 4301 nines where we compile it. Deliberately not ported - that limit is a
            // CPython interpreter setting, changeable at runtime and with no .NET equivalent,
            // not part of the regex grammar. Measured 2026-08-30, see DECISIONS.
            if (!allowNumeric || ParsePythonInt(name) < minGroup)
            {
                throw new FuzzyRegexParseException("bad character in group name", source.String, source.Pos);
            }
        }
        else if (!IsIdentifierName(name))
        {
            throw new FuzzyRegexParseException("bad character in group name", source.String, source.Pos);
        }

        return name;
    }

    /// <summary>
    /// Python's <c>str.isdigit</c>, which upstream uses to tell a group number from a group name.
    /// </summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> if every character is a digit.</returns>
    /// <remarks>
    /// Python's version is Unicode-aware - superscript two is a digit to it - so this goes through
    /// the Unicode tables rather than testing for <c>0</c> to <c>9</c>.
    /// </remarks>
    internal static bool IsDigitName(string name) => PythonStr.IsDigitString(name);

    /// <summary>Python's <c>str.isidentifier</c>, which upstream uses to validate a group name.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> if it is a legal Python identifier.</returns>
    /// <remarks>
    /// Python's version accepts any character with the Unicode <c>XID_Start</c> or
    /// <c>XID_Continue</c> property, so this goes through the Unicode tables.
    /// </remarks>
    internal static bool IsIdentifierName(string name) => PythonStr.IsIdentifier(name);

    /// <summary>Upstream <c>parse_escape</c> (lines 1256-1337).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="inSet">Whether the escape is inside a character set.</param>
    /// <returns>The node the escape stands for.</returns>
    internal static RegexBase ParseEscape(Source source, Info info, bool inSet)
    {
        bool savedIgnore = source.IgnoreSpace;
        source.IgnoreSpace = false;
        int ch = source.Get();
        source.IgnoreSpace = savedIgnore;

        if (ch == Source.EndOfSource)
        {
            // A backslash at the end of the pattern.
            throw new FuzzyRegexParseException("bad escape (end of pattern)", source.String, source.Pos);
        }

        if (ch <= char.MaxValue && RegexFlags.HexEscapes.TryGetValue((char)ch, out int expectedLength))
        {
            // A hexadecimal escape sequence.
            return ParseHexEscape(source, info, (char)ch, expectedLength, inSet, (char)ch);
        }

        if (ch == 'g' && !inSet)
        {
            // A group reference.
            int savedPos = source.Pos;
            try
            {
                return ParseGroupRef(source, info);
            }
            catch (FuzzyRegexParseException)
            {
                // Invalid as a group reference, so assume it's a literal.
                source.Pos = savedPos;
            }

            return MakeCharacter(info, ch, inSet);
        }

        if (ch == 'G' && !inSet)
        {
            // A search anchor.
            return new SearchAnchor();
        }

        if (ch == 'L' && !inSet)
        {
            // A named list.
            return ParseStringSet(source, info);
        }

        if (ch == 'N')
        {
            // A named codepoint.
            return ParseNamedChar(source, info, inSet);
        }

        if (ch is 'p' or 'P')
        {
            // A Unicode property, positive or negative.
            return ParseProperty(source, info, ch == 'p', inSet);
        }

        if (ch == 'R' && !inSet)
        {
            // A line ending.
            List<int> charset = [0x0A, 0x0B, 0x0C, 0x0D];
            if (info.GuessEncoding == RegexFlags.Unicode)
            {
                charset.AddRange([0x85, 0x2028, 0x2029]);
            }

            return new Atomic(
                new Branch([
                    new String([0x0D, 0x0A]),
                    new SetUnion(info, [.. charset.Select(static c => new Character(c))]),
                ])
            );
        }

        if (ch == 'X' && !inSet)
        {
            // A grapheme cluster.
            return new Grapheme();
        }

        if (RegexFlags.IsAlpha(ch))
        {
            // An alphabetic escape sequence.
            // Positional escapes aren't allowed inside a character set.
            if (!inSet)
            {
                RegexBase? position = PositionEscape(info, ch);
                if (position is not null)
                {
                    return position;
                }
            }

            Property? charset = CharsetEscape(info, ch);
            if (charset is not null)
            {
                return charset;
            }

            if (RegexFlags.CharacterEscapes.TryGetValue((char)ch, out char value))
            {
                return new Character(value);
            }

            throw new FuzzyRegexParseException($"bad escape \\{(char)ch}", source.String, source.Pos);
        }

        if (RegexFlags.IsDigit(ch))
        {
            // A numeric escape sequence.
            return ParseNumericEscape(source, info, (char)ch, inSet);
        }

        // A literal.
        return MakeCharacter(info, ch, inSet);
    }

    /// <summary>
    /// The four positional-escape tables and the choice between them: upstream
    /// <c>POSITION_ESCAPES</c>, <c>ASCII_POSITION_ESCAPES</c>, <c>UNICODE_POSITION_ESCAPES</c> and
    /// <c>WORD_POSITION_ESCAPES</c> (lines 4635-4668), selected as <c>parse_escape</c> selects them
    /// (lines 1304-1315).
    /// </summary>
    /// <param name="info">The parse state, whose flags choose the table.</param>
    /// <param name="ch">The escape letter.</param>
    /// <returns>The node, or <see langword="null"/> if this letter is not a positional escape.</returns>
    /// <remarks>
    /// A function rather than four dictionaries because upstream's tables differ only in the four
    /// word-related entries, and its entries are shared singleton nodes - which these are not, and
    /// need not be, since every node this port builds is immutable.
    /// </remarks>
    internal static RegexBase? PositionEscape(Info info, int ch)
    {
        bool word = (info.Flags & RegexFlags.Word) != 0;

        int encoding = 0;
        if (!word)
        {
            if ((info.Flags & RegexFlags.Ascii) != 0)
            {
                encoding = RegexFlags.AsciiEncoding;
            }
            else if ((info.Flags & RegexFlags.Unicode) != 0)
            {
                encoding = RegexFlags.UnicodeEncoding;
            }
        }

        return ch switch
        {
            'A' => new StartOfString(),
            'b' => word ? new DefaultBoundary() : new Boundary(true, encoding),
            'B' => word ? new DefaultBoundary(false) : new Boundary(false, encoding),
            'K' => new Keep(),
            'm' => word ? new DefaultStartOfWord() : new StartOfWord(encoding),
            'M' => word ? new DefaultEndOfWord() : new EndOfWord(encoding),
            'Z' or 'z' => new EndOfString(),
            _ => (RegexBase?)null,
        };
    }

    /// <summary>Upstream <c>parse_numeric_escape</c> (lines 1339-1370).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="ch">The first digit.</param>
    /// <param name="inSet">Whether the escape is inside a character set.</param>
    /// <returns>The node the escape stands for.</returns>
    internal static RegexBase ParseNumericEscape(Source source, Info info, char ch, bool inSet)
    {
        if (inSet || ch == '0')
        {
            // Octal escape sequence, max 3 digits.
            return ParseOctalEscape(source, info, [ch], inSet);
        }

        // At least 1 digit, so either octal escape or group.
        string digits = ch.ToString();
        int savedPos = source.Pos;
        int next = source.Get();
        if (RegexFlags.IsDigit(next))
        {
            // At least 2 digits, so either octal escape or group.
            digits += (char)next;
            savedPos = source.Pos;
            next = source.Get();
            if (digits.All(static c => RegexFlags.IsOctDigit(c)) && RegexFlags.IsOctDigit(next))
            {
                // 3 octal digits, so octal escape sequence.
                int encoding = info.Flags & RegexFlags.AllEncodings;
                int octalMask = encoding is RegexFlags.Ascii or RegexFlags.Locale ? 0xFF : 0x1FF;

                int value = Convert.ToInt32(digits + (char)next, 8) & octalMask;
                return MakeCharacter(info, value);
            }
        }

        // Group reference.
        source.Pos = savedPos;
        if (info.IsOpenGroup(digits))
        {
            throw new FuzzyRegexParseException("cannot refer to an open group", source.String, source.Pos);
        }

        return MakeRefGroup(info, digits, source.Pos);
    }

    /// <summary>Upstream <c>parse_octal_escape</c> (lines 1372-1391).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="digits">The digits read so far.</param>
    /// <param name="inSet">Whether the escape is inside a character set.</param>
    /// <returns>The character the escape stands for.</returns>
    internal static RegexBase ParseOctalEscape(Source source, Info info, List<char> digits, bool inSet)
    {
        int savedPos = source.Pos;
        int ch = source.Get();
        while (digits.Count < 3 && RegexFlags.IsOctDigit(ch))
        {
            digits.Add((char)ch);
            savedPos = source.Pos;
            ch = source.Get();
        }

        source.Pos = savedPos;

        string text = new([.. digits]);
        if (text.Length > 0 && text.All(static c => RegexFlags.IsOctDigit(c)))
        {
            return MakeCharacter(info, Convert.ToInt32(text, 8), inSet);
        }

        // Upstream's ValueError branch: int(..., 8) could not read the digits.
        throw digits[0] is >= '0' and <= '7'
            ? new FuzzyRegexParseException($"incomplete escape \\{text}", source.String, source.Pos)
            : new FuzzyRegexParseException($"bad escape \\{digits[0]}", source.String, source.Pos);
    }

    /// <summary>Upstream <c>parse_hex_escape</c> (lines 1393-1414).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="esc">The escape letter, for the error message.</param>
    /// <param name="expectedLength">How many hex digits the escape takes.</param>
    /// <param name="inSet">Whether the escape is inside a character set.</param>
    /// <param name="type">The escape letter again; upstream passes it twice.</param>
    /// <returns>The character the escape stands for.</returns>
    internal static RegexBase ParseHexEscape(
        Source source,
        Info info,
        char esc,
        int expectedLength,
        bool inSet,
        char type
    )
    {
        int savedPos = source.Pos;
        var digits = new List<char>();
        for (int i = 0; i < expectedLength; i++)
        {
            int ch = source.Get();
            if (!RegexFlags.IsHexDigit(ch))
            {
                throw new FuzzyRegexParseException(
                    $"incomplete escape \\{type}{new string([.. digits])}",
                    source.String,
                    savedPos
                );
            }

            digits.Add((char)ch);
        }

        string text = new([.. digits]);
        long value = long.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (value < 0x110000)
        {
            return MakeCharacter(info, (int)value, inSet);
        }

        // Bad hex escape.
        throw new FuzzyRegexParseException($"bad hex escape \\{esc}{text}", source.String, savedPos);
    }

    /// <summary>Upstream <c>parse_group_ref</c> (lines 1416-1425).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The group reference.</returns>
    /// <remarks>
    /// The error paths are real, not deferred: <c>parse_escape</c> catches them and falls back to
    /// a literal <c>g</c>, so a pattern as ordinary as <c>\g</c> depends on them.
    /// </remarks>
    internal static RegexBase ParseGroupRef(Source source, Info info)
    {
        source.Expect("<");
        int savedPos = source.Pos;
        string name = ParseName(source, allowNumeric: true);
        source.Expect(">");
        if (info.IsOpenGroup(name))
        {
            throw new FuzzyRegexParseException("cannot refer to an open group", source.String, source.Pos);
        }

        return MakeRefGroup(info, name, savedPos);
    }

    /// <summary>Upstream <c>parse_string_set</c> (lines 1427-1435).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The named-list reference.</returns>
    /// <remarks>
    /// Upstream's <c>name is None</c> guard is dead: <c>parse_name</c> raises "missing group name"
    /// rather than returning <c>None</c>, so only the <c>info.kwargs</c> membership test can fire.
    /// </remarks>
    internal static RegexBase ParseStringSet(Source source, Info info)
    {
        source.Expect("<");
        string name = ParseName(source, allowNumeric: true);
        source.Expect(">");
        if (!info.Kwargs.ContainsKey(name))
        {
            throw new FuzzyRegexParseException("undefined named list", source.String, source.Pos);
        }

        return MakeStringSet(info, name);
    }

    /// <summary>Upstream <c>parse_named_char</c> (lines 1437-1451).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="inSet">Whether the escape is inside a character set.</param>
    /// <returns>The character the escape stands for.</returns>
    /// <remarks>
    /// Only the <c>unicodedata.lookup</c> call needs the character-name table. Everything around
    /// it is ordinary scanning, and it is what makes a bare <c>\N</c> the literal <c>N</c>.
    /// </remarks>
    internal static RegexBase ParseNamedChar(Source source, Info info, bool inSet)
    {
        int savedPos = source.Pos;
        if (source.MatchText("{"))
        {
            string name = source.GetWhile(RegexFlags.IsNamedCharPart, keepSpaces: true);
            if (source.MatchText("}"))
            {
                // Upstream's unicodedata.lookup, whose KeyError becomes this error.
                if (!UnicodeCharacterNames.TryLookup(name, out int value))
                {
                    throw new FuzzyRegexParseException("undefined character name", source.String, source.Pos);
                }

                return MakeCharacter(info, value, inSet);
            }
        }

        source.Pos = savedPos;
        return MakeCharacter(info, 'N', inSet);
    }

    /// <summary>Upstream <c>parse_property</c> (lines 1453-1487).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="positive">Whether this is <c>\p</c> or <c>\P</c>.</param>
    /// <param name="inSet">Whether the escape is inside a character set.</param>
    /// <returns>The property, or the literal <c>p</c> or <c>P</c> when there is no property here.</returns>
    internal static RegexBase ParseProperty(Source source, Info info, bool positive, bool inSet)
    {
        int savedPos = source.Pos;
        int ch = source.Get();
        if (ch == '{')
        {
            bool negate = source.MatchText("^");
            (string? propName, string name) = ParsePropertyName(source);
            if (source.MatchText("}"))
            {
                // It's correctly delimited.
                Property prop = LookupProperty(
                    propName,
                    name,
                    positive != negate,
                    source,
                    encoding: PropertyEncoding(info)
                );

                return MakeProperty(info, prop, inSet);
            }
        }
        else if (ch is 'C' or 'L' or 'M' or 'N' or 'P' or 'S' or 'Z')
        {
            // An abbreviated property, eg \pL.
            Property prop = LookupProperty(
                null,
                char.ConvertFromUtf32(ch),
                positive,
                source,
                encoding: PropertyEncoding(info)
            );

            return MakeProperty(info, prop, inSet);
        }

        // Not a property, so treat as a literal "p" or "P".
        source.Pos = savedPos;
        return MakeCharacter(info, positive ? 'p' : 'P', inSet);
    }

    /// <summary>Upstream <c>parse_property_name</c> (lines 1489-1509).</summary>
    /// <param name="source">The scanner.</param>
    /// <returns>The qualifier, if the name was qualified, and the name.</returns>
    internal static (string? PropName, string Name) ParsePropertyName(Source source)
    {
        string name = source.GetWhile(RegexFlags.IsPropertyNamePart);
        int savedPos = source.Pos;

        string? propName = null;
        int ch = source.Get();
        if (ch is ':' or '=')
        {
            string qualifier = name;
            name = TrimPythonWhitespace(
                source.GetWhile(static c => RegexFlags.IsAlnum(c) || c is ' ' or '&' or '_' or '-' or '.' or '/')
            );

            if (name.Length > 0)
            {
                // Name after the ":" or "=", so it's a qualified name.
                propName = qualifier;
                savedPos = source.Pos;
            }
            else
            {
                // No name after the ":" or "=", so assume it's an unqualified name.
                name = qualifier;
            }
        }

        source.Pos = savedPos;
        return (propName, name);
    }

    /// <summary>Upstream <c>parse_set</c> (lines 1511-1535).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The set node.</returns>
    internal static RegexBase ParseSet(Source source, Info info)
    {
        int version = Version(info);

        bool savedIgnore = source.IgnoreSpace;
        source.IgnoreSpace = false;

        // Negative set?
        bool negate = source.MatchText("^");
        RegexBase item;
        try
        {
            item = version == RegexFlags.Version0 ? ParseSetImpUnion(source, info) : ParseSetUnion(source, info);

            if (!source.MatchText("]"))
            {
                throw new FuzzyRegexParseException("missing ]", source.String, source.Pos);
            }
        }
        finally
        {
            source.IgnoreSpace = savedIgnore;
        }

        if (negate)
        {
            item = item.WithFlags(positive: !item.Positive);
        }

        return item.WithFlags(caseFlags: MakeCaseFlags(info));
    }

    /// <summary>Upstream <c>parse_set_union</c> (lines 1537-1545).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The union, or its single member.</returns>
    internal static RegexBase ParseSetUnion(Source source, Info info)
    {
        List<RegexBase> items = [ParseSetSymmDiff(source, info)];
        while (source.MatchText("||"))
        {
            items.Add(ParseSetSymmDiff(source, info));
        }

        return items.Count == 1 ? items[0] : new SetUnion(info, items);
    }

    /// <summary>Upstream <c>parse_set_symm_diff</c> (lines 1547-1555).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The symmetric difference, or its single member.</returns>
    internal static RegexBase ParseSetSymmDiff(Source source, Info info)
    {
        List<RegexBase> items = [ParseSetInter(source, info)];
        while (source.MatchText("~~"))
        {
            items.Add(ParseSetInter(source, info));
        }

        return items.Count == 1 ? items[0] : new SetSymDiff(info, items);
    }

    /// <summary>Upstream <c>parse_set_inter</c> (lines 1557-1565).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The intersection, or its single member.</returns>
    internal static RegexBase ParseSetInter(Source source, Info info)
    {
        List<RegexBase> items = [ParseSetDiff(source, info)];
        while (source.MatchText("&&"))
        {
            items.Add(ParseSetDiff(source, info));
        }

        return items.Count == 1 ? items[0] : new SetInter(info, items);
    }

    /// <summary>Upstream <c>parse_set_diff</c> (lines 1567-1575).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The difference, or its single member.</returns>
    internal static RegexBase ParseSetDiff(Source source, Info info)
    {
        List<RegexBase> items = [ParseSetImpUnion(source, info)];
        while (source.MatchText("--"))
        {
            items.Add(ParseSetImpUnion(source, info));
        }

        return items.Count == 1 ? items[0] : new SetDiff(info, items);
    }

    /// <summary>Upstream <c>parse_set_imp_union</c> (lines 1577-1598).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The implicit union, or its single member.</returns>
    internal static RegexBase ParseSetImpUnion(Source source, Info info)
    {
        int version = Version(info);

        List<RegexBase> items = [ParseSetMember(source, info)];
        while (true)
        {
            int savedPos = source.Pos;
            if (source.MatchText("]"))
            {
                // End of the set.
                source.Pos = savedPos;
                break;
            }

            // Upstream's `any(source.match(op) for op in SET_OPS)` consumes the operator it finds,
            // which is why the position is restored either way.
            if (version == RegexFlags.Version1 && Array.Exists(_setOps, source.MatchText))
            {
                // The new behaviour has set operators.
                source.Pos = savedPos;
                break;
            }

            items.Add(ParseSetMember(source, info));
        }

        return items.Count == 1 ? items[0] : new SetUnion(info, items);
    }

    /// <summary>Upstream <c>parse_set_member</c> (lines 1600-1639).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The member: a character, a range, a property or a nested set.</returns>
    internal static RegexBase ParseSetMember(Source source, Info info)
    {
        // Parse a set item.
        RegexBase start = ParseSetItem(source, info);
        int savedPos1 = source.Pos;
        if (start is not Character { Positive: true } startCharacter || !source.MatchText("-"))
        {
            // It's not the start of a range.
            return start;
        }

        int version = Version(info);

        // It looks like the start of a range of characters.
        int savedPos2 = source.Pos;
        if (version == RegexFlags.Version1 && source.MatchText("-"))
        {
            // It's actually the set difference operator '--', so return the character.
            source.Pos = savedPos1;
            return start;
        }

        if (source.MatchText("]"))
        {
            // We've reached the end of the set, so return both the character and hyphen.
            source.Pos = savedPos2;
            return new SetUnion(info, [start, new Character('-')]);
        }

        // Parse a set item.
        RegexBase end = ParseSetItem(source, info);
        if (end is not Character { Positive: true } endCharacter)
        {
            // It's not a range, so return the character, hyphen and property.
            return new SetUnion(info, [start, new Character('-'), end]);
        }

        // It _is_ a range.
        if (startCharacter.Value > endCharacter.Value)
        {
            throw new FuzzyRegexParseException("bad character range", source.String, source.Pos);
        }

        if (startCharacter.Value == endCharacter.Value)
        {
            return start;
        }

        return new Range(startCharacter.Value, endCharacter.Value);
    }

    /// <summary>Upstream <c>parse_set_item</c> (lines 1641-1677).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <returns>The item.</returns>
    internal static RegexBase ParseSetItem(Source source, Info info)
    {
        int version = Version(info);

        if (source.MatchText("\\"))
        {
            // An escape sequence in a set.
            return ParseEscape(source, info, inSet: true);
        }

        int savedPos = source.Pos;
        if (source.MatchText("[:"))
        {
            // Looks like a POSIX character class.
            try
            {
                return ParsePosixClass(source, info);
            }
            catch (ParseErrorException)
            {
                // Not a POSIX character class.
                source.Pos = savedPos;
            }
        }

        if (version == RegexFlags.Version1 && source.MatchText("["))
        {
            // It's the start of a nested set.

            // Negative set?
            bool negate = source.MatchText("^");
            RegexBase item = ParseSetUnion(source, info);

            if (!source.MatchText("]"))
            {
                throw new FuzzyRegexParseException("missing ]", source.String, source.Pos);
            }

            return negate ? item.WithFlags(positive: !item.Positive) : item;
        }

        int ch = source.Get();
        if (ch == Source.EndOfSource)
        {
            // Upstream's exact text. PatternCompiler.Compile re-words it - and only it - when the
            // pattern turns out to compile under version 0, which is the one case the version this
            // port defaults to is what broke it.
            throw new FuzzyRegexParseException("unterminated character set", source.String, source.Pos);
        }

        return new Character(ch);
    }

    /// <summary>Upstream <c>parse_posix_class</c> (lines 1679-1686).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state; upstream takes it and does not use it.</param>
    /// <returns>The property the class stands for.</returns>
    /// <exception cref="ParseErrorException">
    /// This is not a POSIX class after all, which <see cref="ParseSetItem"/> catches. An unknown
    /// class name raises <see cref="FuzzyRegexParseException"/> instead, which it does not.
    /// </exception>
    internal static Property ParsePosixClass(Source source, Info info)
    {
        _ = info;

        bool negate = source.MatchText("^");
        (string? propName, string name) = ParsePropertyName(source);
        if (!source.MatchText(":]"))
        {
            throw new ParseErrorException();
        }

        return LookupProperty(propName, name, !negate, source, posix: true);
    }

    /// <summary>Upstream <c>float_to_rational</c> (lines 1688-1697).</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>
    /// The numerator and denominator, or <see langword="null"/> where upstream's <c>int()</c>
    /// would raise <c>ValueError</c> - which <see cref="StandardiseName"/> catches.
    /// </returns>
    /// <exception cref="OverflowException">
    /// The value is infinite. Upstream's <c>int(inf)</c> raises <c>OverflowError</c>, which
    /// <c>standardise_name</c> does <b>not</b> catch, so <c>\p{Infinity}</c> propagates it out of
    /// <c>compile</c> (measured against the local oracle, 2026-08-30).
    /// </exception>
    /// <remarks>
    /// <see cref="BigInteger"/> rather than <c>long</c> because Python's <c>int</c> has no width:
    /// <c>\p{1e300}</c> standardises to a 301-digit string upstream.
    /// </remarks>
    internal static (BigInteger Numerator, BigInteger Denominator)? FloatToRational(double value)
    {
        if (double.IsNaN(value))
        {
            // Python's int(nan) raises ValueError.
            return null;
        }

        if (double.IsInfinity(value))
        {
            throw new OverflowException("cannot convert float infinity to integer");
        }

        double truncated = Math.Truncate(value);
        var intPart = new BigInteger(truncated);
        double error = value - truncated;
        if (Math.Abs(error) < 0.0001)
        {
            return (intPart, BigInteger.One);
        }

        // 1.0 / error is finite and not NaN, because error is finite and at least 0.0001 in
        // magnitude, so the recursive call cannot return null.
        (BigInteger den, BigInteger num) = FloatToRational(1.0 / error)!.Value;

        return ((intPart * den) + num, den);
    }

    /// <summary>Upstream <c>numeric_to_rational</c> (lines 1699-1718).</summary>
    /// <param name="numeric">The candidate numeric name.</param>
    /// <param name="result">The rational form, when the name is numeric.</param>
    /// <returns>
    /// <see langword="false"/> where upstream raises <c>ValueError</c> or
    /// <c>ZeroDivisionError</c>, which <c>standardise_name</c> catches.
    /// </returns>
    internal static bool TryNumericToRational(string numeric, out string result)
    {
        ArgumentNullException.ThrowIfNull(numeric);

        result = string.Empty;

        string sign = string.Empty;
        if (numeric.StartsWith('-'))
        {
            sign = "-";
            numeric = numeric[1..];
        }

        string[] parts = numeric.Split('/');
        double value;
        if (parts.Length == 2)
        {
            if (
                !TryParsePythonFloat(parts[0], out double numerator)
                || !TryParsePythonFloat(parts[1], out double denominator)
            )
            {
                return false;
            }

            if (denominator == 0)
            {
                // Python's float division by zero raises ZeroDivisionError.
                return false;
            }

            value = numerator / denominator;
        }
        else if (parts.Length == 1)
        {
            if (!TryParsePythonFloat(parts[0], out value))
            {
                return false;
            }
        }
        else
        {
            // Upstream's bare `raise ValueError()`.
            return false;
        }

        if (FloatToRational(value) is not (BigInteger num, BigInteger den))
        {
            return false;
        }

        result = string.Create(CultureInfo.InvariantCulture, $"{sign}{num}/{den}");
        if (result.EndsWith("/1", StringComparison.Ordinal))
        {
            result = result[..^2];
        }

        return true;
    }

    /// <summary>Upstream <c>standardise_name</c> (lines 1720-1725).</summary>
    /// <param name="name">The property or value name as written.</param>
    /// <returns>The standardised name.</returns>
    /// <remarks>
    /// The fallback is Python's <c>str.upper()</c>. Every name that reaches here is ASCII -
    /// <c>PROPERTY_NAME_PART</c> is alphanumerics plus <c>" &amp;_-."</c>, and a qualified value
    /// adds <c>"/"</c> - so the invariant upper-casing is the same mapping, and unlike the current
    /// culture it cannot turn <c>i</c> into <c>İ</c>.
    /// </remarks>
    internal static string StandardiseName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (TryNumericToRational(name, out string rational))
        {
            return rational;
        }

        return new string([.. name.Where(static ch => ch is not ('_' or '-' or ' '))]).ToUpperInvariant();
    }

    /// <summary>Upstream <c>lookup_property</c> (lines 1731-1799).</summary>
    /// <param name="property">The qualifier, or <see langword="null"/> for an unqualified name.</param>
    /// <param name="value">The value name.</param>
    /// <param name="positive">Whether the property is asserted or denied.</param>
    /// <param name="source">The scanner, for the error position. Null only for a compile-time lookup.</param>
    /// <param name="posix">Whether the name came from a POSIX bracket class.</param>
    /// <param name="encoding">The encoding tag the node should carry.</param>
    /// <returns>The property node.</returns>
    internal static Property LookupProperty(
        string? property,
        string value,
        bool positive,
        Source? source = null,
        bool posix = false,
        int encoding = 0
    )
    {
        // Normalise the names. Upstream's `property = standardise_name(property) if property else
        // None` standardises first, and every later `if property` / `not property` then tests the
        // *standardised* value - which can be the empty string, because standardise_name strips
        // "_", "-" and spaces. So `\p{_:Lu}` has no qualifier by the time it is looked up, and
        // falls through to the general-category, script, block and POSIX branches. Collapsing the
        // empty string to null here is what makes the rest of this function read as upstream does.
        string? propertyName = string.IsNullOrEmpty(property) ? null : StandardiseName(property);
        if (propertyName?.Length == 0)
        {
            propertyName = null;
        }

        value = StandardiseName(value);

        if (
            string.Equals(propertyName, "GENERALCATEGORY", StringComparison.Ordinal)
            && string.Equals(value, "ASSIGNED", StringComparison.Ordinal)
        )
        {
            value = "UNASSIGNED";
            positive = !positive;
        }

        if (posix && propertyName is null && _posixClasses.Contains(value.ToUpperInvariant()))
        {
            value = "POSIX" + value;
        }

        IReadOnlyDictionary<string, PropertyEntry> properties = RegexModule.GetProperties();

        if (propertyName is not null)
        {
            // Both the property and the value are provided.
            if (!properties.TryGetValue(propertyName, out PropertyEntry? qualified))
            {
                throw UnknownProperty(source, "unknown property");
            }

            if (!qualified.Values.TryGetValue(value, out int qualifiedValue))
            {
                throw UnknownProperty(source, "unknown property value");
            }

            return new Property(PackProperty(qualified.Id, qualifiedValue), positive, encoding: encoding);
        }

        // Only the value is provided. It might be the name of a GC, script or block value.
        foreach (string candidate in (string[])["GC", "SCRIPT", "BLOCK"])
        {
            PropertyEntry entry = properties[candidate];
            if (entry.Values.TryGetValue(value, out int valueId))
            {
                return new Property(PackProperty(entry.Id, valueId), positive, encoding: encoding);
            }
        }

        // It might be the name of a binary property.
        if (properties.TryGetValue(value, out PropertyEntry? binary))
        {
            if (_binaryValues.SetEquals(binary.Values.Keys))
            {
                return new Property(PackProperty(binary.Id, 1), positive, encoding: encoding);
            }

            return new Property(PackProperty(binary.Id, 0), !positive, encoding: encoding);
        }

        // It might be the name of a binary property starting with a prefix.
        if (
            value.StartsWith("IS", StringComparison.Ordinal)
            && properties.TryGetValue(value[2..], out PropertyEntry? prefixed)
            && prefixed.Values.ContainsKey("YES")
        )
        {
            return new Property(PackProperty(prefixed.Id, 1), positive, encoding: encoding);
        }

        // It might be the name of a script or block starting with a prefix.
        foreach ((string prefix, string prefixedProperty) in ((string, string)[])[("IS", "SCRIPT"), ("IN", "BLOCK")])
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            PropertyEntry entry = properties[prefixedProperty];
            if (entry.Values.TryGetValue(value[2..], out int valueId))
            {
                return new Property(PackProperty(entry.Id, valueId), positive, encoding: encoding);
            }
        }

        // Unknown property.
        throw UnknownProperty(source, "unknown property");
    }

    /// <summary>Upstream <c>_compile_replacement</c> (lines 1801-1871).</summary>
    /// <param name="source">The scanner, positioned just after the backslash.</param>
    /// <param name="groupCount">The pattern's capture group count, for <c>\g&lt;n&gt;</c>.</param>
    /// <param name="groupIndex">The pattern's group names, for <c>\g&lt;name&gt;</c>.</param>
    /// <returns>
    /// Whether the escape is a group reference, and the group number if it is or the character
    /// codes it stands for if it is not. Upstream returns a list rather than a single item so that
    /// an invalid escape can give back both the backslash and the character after it.
    /// <see cref="long"/> rather than <see cref="int"/> because <c>\UFFFFFFFF</c> is a legal
    /// escape here - upstream leaves it to fail in <c>chr()</c> - and does not fit an
    /// <see cref="int"/>.
    /// </returns>
    /// <remarks>
    /// Upstream's <c>is_unicode</c> is always true here and its <c>source.sep</c> is always a
    /// <c>str</c>, because this port has no bytes templates - so <c>\u</c>, <c>\U</c> and
    /// <c>\N{...}</c> are always available and the octal mask is always <c>0x1FF</c>.
    /// </remarks>
    /// <exception cref="FuzzyRegexParseException">The escape is not valid.</exception>
    internal static (bool IsGroup, long[] Items) CompileReplacement(
        Source source,
        int groupCount,
        IReadOnlyDictionary<string, int> groupIndex
    )
    {
        ArgumentNullException.ThrowIfNull(source);

        // Upstream: octal_mask = 0xFF for a bytes template, 0x1FF for a str one.
        const int octalMask = 0x1FF;

        int ch = source.Get();
        if (RegexFlags.IsAlpha(ch))
        {
            // An alphabetic escape sequence.
            if (RegexFlags.CharacterEscapes.TryGetValue((char)ch, out char value))
            {
                return (false, [value]);
            }

            if (RegexFlags.HexEscapes.TryGetValue((char)ch, out int expectedLength))
            {
                // A hexadecimal escape sequence.
                return (false, [ParseReplHexEscape(source, expectedLength, (char)ch)]);
            }

            if (ch == 'g')
            {
                // A group preference.
                return (true, [CompileReplGroup(source, groupCount, groupIndex)]);
            }

            if (ch == 'N')
            {
                // A named character.
                int? named = ParseReplNamedChar(source);
                if (named is not null)
                {
                    return (false, [named.Value]);
                }
            }

            throw new FuzzyRegexParseException($"bad escape \\{(char)ch}", source.String, source.Pos);
        }

        if (ch == '0')
        {
            // An octal escape sequence.
            var octal = new StringBuilder().Append('0');
            while (octal.Length < 3)
            {
                int savedPos = source.Pos;
                ch = source.Get();
                if (!RegexFlags.IsOctDigit(ch))
                {
                    source.Pos = savedPos;
                    break;
                }

                octal.Append((char)ch);
            }

            return (false, [Convert.ToInt32(octal.ToString(), 8) & octalMask]);
        }

        if (RegexFlags.IsDigit(ch))
        {
            // Either an octal escape sequence (3 digits) or a group reference (max 2 digits).
            string digits = ((char)ch).ToString();
            int savedPos = source.Pos;
            ch = source.Get();
            if (RegexFlags.IsDigit(ch))
            {
                digits += (char)ch;
                savedPos = source.Pos;
                ch = source.Get();

                // Upstream: `if ch and is_octal(digits + ch)`. The end of the template is falsy
                // there and EndOfSource is not an octal digit here, so the two agree.
                if (RegexFlags.IsOctDigit(ch) && digits.All(static c => RegexFlags.IsOctDigit(c)))
                {
                    // An octal escape sequence.
                    return (false, [Convert.ToInt32(digits + (char)ch, 8) & octalMask]);
                }
            }

            // A group reference. At most two ASCII digits, so int.Parse cannot overflow -
            // unlike the \g<...> path, which takes an unbounded Python int.
            source.Pos = savedPos;
            return (true, [int.Parse(digits, CultureInfo.InvariantCulture)]);
        }

        if (ch == '\\')
        {
            // An escaped backslash is a backslash.
            return (false, ['\\']);
        }

        if (ch == Source.EndOfSource)
        {
            // A trailing backslash.
            throw new FuzzyRegexParseException("bad escape (end of pattern)", source.String, source.Pos);
        }

        // An escaped non-backslash is a backslash followed by the literal.
        return (false, ['\\', ch]);
    }

    /// <summary>Upstream <c>parse_repl_hex_escape</c> (lines 1873-1883).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="expectedLength">How many hex digits the escape takes.</param>
    /// <param name="type">The escape letter, for the error message.</param>
    /// <returns>The value the escape spells.</returns>
    /// <remarks>
    /// No range check, unlike <see cref="ParseHexEscape"/>: upstream leaves <c>\UFFFFFFFF</c> to
    /// fail later in <c>chr()</c>. See <c>PatternCompiler.MakeString</c>.
    /// </remarks>
    private static long ParseReplHexEscape(Source source, int expectedLength, char type)
    {
        var digits = new List<char>();
        for (int i = 0; i < expectedLength; i++)
        {
            int ch = source.Get();
            if (!RegexFlags.IsHexDigit(ch))
            {
                throw new FuzzyRegexParseException(
                    $"incomplete escape \\{type}{new string([.. digits])}",
                    source.String,
                    source.Pos
                );
            }

            digits.Add((char)ch);
        }

        return long.Parse(new string([.. digits]), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>Upstream <c>parse_repl_named_char</c> (lines 1885-1900).</summary>
    /// <param name="source">The scanner.</param>
    /// <returns>The codepoint, or <see langword="null"/> when there is no <c>{...}</c> here.</returns>
    /// <remarks>
    /// The name characters are <c>ALPHA | {" "}</c>, which is <b>narrower</b> than the pattern-side
    /// <c>parse_named_char</c>'s <c>NAMED_CHAR_PART</c> (<c>ALNUM | {" ", "-"}</c>): a digit or a
    /// hyphen ends the name here, so the <c>}</c> is not found and the whole thing rewinds to a
    /// literal <c>N</c>.
    /// </remarks>
    private static int? ParseReplNamedChar(Source source)
    {
        int savedPos = source.Pos;
        if (source.MatchText("{"))
        {
            string name = source.GetWhile(static c => RegexFlags.IsAlpha(c) || c == ' ');

            if (source.MatchText("}"))
            {
                // Upstream's unicodedata.lookup, whose KeyError becomes this error.
                if (!UnicodeCharacterNames.TryLookup(name, out int value))
                {
                    throw new FuzzyRegexParseException("undefined character name", source.String, source.Pos);
                }

                return value;
            }
        }

        source.Pos = savedPos;
        return null;
    }

    /// <summary>Upstream <c>compile_repl_group</c> (lines 1902-1918).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="groupCount">The pattern's capture group count.</param>
    /// <param name="groupIndex">The pattern's group names.</param>
    /// <returns>The group number the reference resolves to.</returns>
    /// <remarks>
    /// Upstream raises <c>IndexError("unknown group")</c> for a name the pattern does not have,
    /// which is not its own <c>error</c> type and carries no offset. The port raises
    /// <see cref="ArgumentException"/>: the template is a caller's argument, it is what
    /// <c>Regex</c> raises for the same mistake, and it is what the ported
    /// <c>SymbolicRefsTests</c> asserts. Recorded in <c>docs/PORTMAP.md</c>.
    /// </remarks>
    private static int CompileReplGroup(Source source, int groupCount, IReadOnlyDictionary<string, int> groupIndex)
    {
        source.Expect("<");
        string name = ParseName(source, allowNumeric: true, allowGroup0: true);

        source.Expect(">");
        if (IsDigitName(name))
        {
            // Python's unbounded int(), as everywhere a group name is read as a number.
            BigInteger index = ParsePythonInt(name);
            if (index < 0 || index > groupCount)
            {
                throw new FuzzyRegexParseException("invalid group reference", source.String, source.Pos);
            }

            return (int)index;
        }

        // CA2208, S3928 and MA0015 all want the paramName to name a parameter of *this* method.
        // They are right in general and wrong here: the argument at fault is the public
        // `replacement` parameter of FuzzyRegex.Replace and Match.Result, which is what a caller
        // can act on, and this private helper sits three frames below it. The alternatives are a
        // paramName naming one of this method's own arguments, which would be false, or an extra
        // parameter carrying a string the Source already holds, which is worse code for nothing.
        // Disapplied here and nowhere else; see DECISIONS 2026-08-31.
#pragma warning disable CA2208, S3928, MA0015
        return groupIndex.TryGetValue(name, out int number)
            ? number
            : throw new ArgumentException("unknown group", "replacement");
#pragma warning restore CA2208, S3928, MA0015
    }

    /// <summary>
    /// The three <c>CHARSET_ESCAPES</c> tables and the choice between them: upstream
    /// <c>CHARSET_ESCAPES</c>, <c>ASCII_CHARSET_ESCAPES</c> and <c>UNICODE_CHARSET_ESCAPES</c>
    /// (lines 4605-4633), selected as <c>parse_escape</c> selects them (lines 1317-1322).
    /// </summary>
    /// <param name="info">The parse state, whose flags choose the table.</param>
    /// <param name="ch">The escape letter.</param>
    /// <returns>The property, or <see langword="null"/> if this letter is not a set escape.</returns>
    /// <remarks>
    /// A function rather than three dictionaries because the tables differ only in the encoding tag
    /// on six of their seven entries - <c>\h</c> keeps the base entry in all three - and because
    /// upstream's entries are shared singletons, which these need not be: every node this port
    /// builds is immutable.
    /// </remarks>
    internal static Property? CharsetEscape(Info info, int ch)
    {
        ArgumentNullException.ThrowIfNull(info);

        int encoding = PropertyEncoding(info);

        return ch switch
        {
            'd' => LookupProperty(null, "Digit", true, encoding: encoding),
            'D' => LookupProperty(null, "Digit", false, encoding: encoding),
            'h' => LookupProperty(null, "Blank", true),
            's' => LookupProperty(null, "Space", true, encoding: encoding),
            'S' => LookupProperty(null, "Space", false, encoding: encoding),
            'w' => LookupProperty(null, "Word", true, encoding: encoding),
            'W' => LookupProperty(null, "Word", false, encoding: encoding),
            _ => null,
        };
    }

    /// <summary>Upstream <c>_compile_firstset</c> (lines 370-378).</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="fs">The pattern's first set.</param>
    /// <returns>The bytecode that scans for it, empty when there is none.</returns>
    internal static List<uint[]> CompileFirstset(Info info, HashSet<RegexBase?> fs)
    {
        bool reverse = (info.Flags & RegexFlags.Reverse) != 0;
        RegexBase? set = CheckFirstset(info, reverse, fs);
        if (set is null or AnyAll)
        {
            return [];
        }

        // Compile the firstset.
        return set.Compile(reverse);
    }

    /// <summary>Upstream <c>_check_firstset</c> (lines 380-409).</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="reverse">Whether the pattern is matched right to left.</param>
    /// <param name="fs">The pattern's first set.</param>
    /// <returns>The set node to scan for, or <see langword="null"/> when there is no useful one.</returns>
    internal static RegexBase? CheckFirstset(Info info, bool reverse, HashSet<RegexBase?> fs)
    {
        if (fs.Count == 0 || fs.Contains(null))
        {
            return null;
        }

        // If we ignore the case, for simplicity we won't build a firstset.
        HashSet<RegexBase> members = [];
        int caseFlags = RegexFlags.NoCase;
        foreach (RegexBase? item in fs)
        {
            if (item is Character { Positive: false })
            {
                return null;
            }

            caseFlags |= item!.CaseFlags;
            members.Add(item.WithFlags(caseFlags: RegexFlags.NoCase));
        }

        if (caseFlags == RegexFlags.FullIgnoreCase)
        {
            return null;
        }

        // Build the firstset. One of the two points PORTMAP's "Where we diverge" requires the
        // members to be sorted at, because a Python set of nodes has no stable order; the corpus
        // recorder sorts by the same rendered key.
        int setCaseFlags = caseFlags & ~RegexFlags.FullCase;
        List<RegexBase> ordered = [.. members.OrderBy(static m => m.RenderKey(), StringComparer.Ordinal)];

        var set = new SetUnion(info, ordered, caseFlags: setCaseFlags, zerowidth: true);

        return set.Optimise(info, reverse, inSet: true);
    }

    /// <summary>Upstream <c>_flatten_code</c> (lines 411-417).</summary>
    /// <param name="code">The bytecode, still in tuples.</param>
    /// <returns>The flat bytecode.</returns>
    internal static List<uint> FlattenCode(IEnumerable<uint[]> code)
    {
        List<uint> flatCode = [];
        foreach (uint[] c in code)
        {
            flatCode.AddRange(c);
        }

        return flatCode;
    }

    /// <summary>Upstream <c>_check_group_features</c> (lines 4421-4458).</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="parsed">The parsed pattern.</param>
    internal static void CheckGroupFeatures(Info info, RegexBase parsed)
    {
        Dictionary<(int Group, bool Reverse, bool Fuzzy), int> callRefs = [];
        List<(RegexBase Group, bool Reverse, bool Fuzzy)> additionalGroups = [];

        foreach ((RegexBase call, bool reverse, bool fuzzy) in info.GroupCalls)
        {
            var callGroup = (CallGroup)call;

            // Look up the reference of this group call.
            (int, bool, bool) key = (callGroup.GroupNumber, reverse, fuzzy);
            if (!callRefs.TryGetValue(key, out int reference))
            {
                // This group doesn't have a reference yet, so look up its features.
                if (callGroup.GroupNumber == 0)
                {
                    // Calling the pattern as a whole.
                    bool rev = (info.Flags & RegexFlags.Reverse) != 0;

                    bool fuz = parsed is Fuzzy;
                    if ((rev, fuz) != (reverse, fuzzy))
                    {
                        // The pattern as a whole doesn't have the features we want, so we'll need
                        // to make a copy of it with the desired features.
                        additionalGroups.Add((new CallRef(callRefs.Count, parsed), reverse, fuzzy));
                    }
                }
                else
                {
                    // Calling a capture group.
                    (Group group, bool defReverse, bool defFuzzy) = info.DefinedGroups[callGroup.GroupNumber];
                    if ((defReverse, defFuzzy) != (reverse, fuzzy))
                    {
                        // The group doesn't have the features we want, so we'll need to make a copy
                        // of it with the desired features.
                        additionalGroups.Add((group, reverse, fuzzy));
                    }
                }

                reference = callRefs.Count;
                callRefs[key] = reference;
            }

            callGroup.CallRefIndex = reference;
        }

        info.CallRefs = callRefs;
        info.AdditionalGroups = additionalGroups;
    }

    /// <summary>Upstream <c>_get_required_string</c> (lines 4460-4479).</summary>
    /// <param name="parsed">The parsed pattern.</param>
    /// <param name="flags">The resolved flags.</param>
    /// <returns>The required string's offset, characters and case flags.</returns>
    internal static (long ReqOffset, int[] ReqChars, int ReqFlags) GetRequiredString(RegexBase parsed, int flags)
    {
        (long reqOffset, RegexBase? required) = parsed.GetRequiredString((flags & RegexFlags.Reverse) != 0);

        if (required is null)
        {
            return (0, [], 0);
        }

        // Upstream sets `required.required = True` on whatever node came back. Only String reads
        // it; on a Character the assignment just creates an attribute nothing looks at.
        if (required is String requiredString)
        {
            requiredString.Required = true;
        }

        if (reqOffset >= RegexFlags.Unlimited)
        {
            reqOffset = -1;
        }

        int reqFlags = required.CaseFlags;
        if ((flags & RegexFlags.Unicode) == 0)
        {
            reqFlags &= ~RegexFlags.Unicode;
        }

        int[] reqChars = required switch
        {
            String s => s.FoldedCharacters,
            Character c => c.FoldedCharacters,
            _ => throw new NotSupportedException($"{required.GetType().Name} has no folded_characters"),
        };

        // No narrowing cast: the offset can be as large as UNLIMITED - 1 = 4294967294, which an
        // int cannot hold. Truncating it turned '(?:a{65535}){0,65535}b's offset of 4294836225
        // into -131071 (found writing Gaps/Parsing/RepeatWidthOverflowTests.cs).
        return (reqOffset, reqChars, reqFlags);
    }

    /// <summary>
    /// The body shared by <c>(?P&lt;name&gt;...)</c> and <c>(?&lt;name&gt;...)</c>, which upstream
    /// writes out twice (lines 868-882 and 946-961).
    /// </summary>
    private static Group ParseNamedGroup(Source source, Info info)
    {
        string name = ParseName(source);
        int group = info.OpenGroup(name);
        source.Expect(">");
        int savedFlags = info.Flags;
        RegexBase subpattern;
        try
        {
            subpattern = ParsePattern(source, info);
            source.Expect(")");
        }
        finally
        {
            info.Flags = savedFlags;
            source.IgnoreSpace = (info.Flags & RegexFlags.Verbose) != 0;
        }

        info.CloseGroup();

        return new Group(info, group, subpattern);
    }

    /// <summary>
    /// Upstream's <c>(info.flags &amp; _ALL_VERSIONS) or DEFAULT_VERSION</c>, written out in six of
    /// the set parsers.
    /// </summary>
    private static int Version(Info info) =>
        (info.Flags & RegexFlags.AllVersions) != 0 ? info.Flags & RegexFlags.AllVersions : info.DefaultVersion;

    /// <summary>
    /// The encoding tag a property gets from the flags in force, written out identically in
    /// <c>parse_property</c> (lines 1462-1467, 1474-1479) and in <c>parse_escape</c>'s choice of
    /// charset-escape table (lines 1317-1322).
    /// </summary>
    private static int PropertyEncoding(Info info)
    {
        if ((info.Flags & RegexFlags.Ascii) != 0)
        {
            return RegexFlags.AsciiEncoding;
        }

        return (info.Flags & RegexFlags.Unicode) != 0 ? RegexFlags.UnicodeEncoding : 0;
    }

    /// <summary>Upstream's <c>(prop_id &lt;&lt; 16) | val_id</c>.</summary>
    private static uint PackProperty(int propertyId, int valueId) => ((uint)propertyId << 16) | (uint)valueId;

    /// <summary>
    /// Upstream's two-armed <c>raise error(...)</c>, which drops the pattern and the position when
    /// there is no <c>Source</c> - which happens only for the module-level
    /// <c>CHARSET_ESCAPES</c> lookups, all of which succeed.
    /// </summary>
    private static FuzzyRegexParseException UnknownProperty(Source? source, string message) =>
        source is null
            ? new FuzzyRegexParseException(message)
            : new FuzzyRegexParseException(message, source.String, source.Pos);

    /// <summary>
    /// Python's <c>float()</c>, which is not <see cref="double.TryParse(string, out double)"/>:
    /// it accepts <c>inf</c>, <c>infinity</c> and <c>nan</c> in any case, and underscores between
    /// digits.
    /// </summary>
    /// <param name="text">The candidate number.</param>
    /// <param name="value">The parsed value.</param>
    /// <returns><see langword="false"/> where Python raises <c>ValueError</c>.</returns>
    /// <remarks>
    /// Only names made of alphanumerics and <c>" &amp;_-./"</c> reach here, so Python's acceptance
    /// of non-ASCII digits - <c>float("١٢")</c> is 12.0 - is unreachable and not ported.
    /// </remarks>
    private static bool TryParsePythonFloat(string text, out double value)
    {
        value = 0;

        string trimmed = TrimPythonWhitespace(text);
        if (trimmed.Length == 0)
        {
            return false;
        }

        // An underscore is legal only between two digits: float("1_0") is 10.0, float("1_") and
        // float("1._5") are both ValueError.
        for (int i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] != '_')
            {
                continue;
            }

            if (
                i == 0
                || i == trimmed.Length - 1
                || !char.IsAsciiDigit(trimmed[i - 1])
                || !char.IsAsciiDigit(trimmed[i + 1])
            )
            {
                return false;
            }
        }

        string digits = trimmed.Replace("_", string.Empty, StringComparison.Ordinal);

        string body = digits;
        double sign = 1;
        if (body.Length > 0 && body[0] is '+' or '-')
        {
            sign = body[0] == '-' ? -1 : 1;
            body = body[1..];
        }

        if (
            string.Equals(body, "inf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(body, "infinity", StringComparison.OrdinalIgnoreCase)
        )
        {
            value = sign * double.PositiveInfinity;
            return true;
        }

        if (string.Equals(body, "nan", StringComparison.OrdinalIgnoreCase))
        {
            value = double.NaN;
            return true;
        }

        return double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Python's <c>str.strip()</c> with no argument, which strips exactly the characters
    /// <c>str.isspace</c> accepts - not the same set as <see cref="string.Trim()"/>.
    /// </summary>
    private static string TrimPythonWhitespace(string text)
    {
        int start = 0;
        int end = text.Length;
        while (start < end && Source.IsSpace(text[start]))
        {
            start++;
        }

        while (end > start && Source.IsSpace(text[end - 1]))
        {
            end--;
        }

        return text[start..end];
    }
}
