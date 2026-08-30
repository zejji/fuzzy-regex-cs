using System.Globalization;
using System.Numerics;

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
    /// <summary>Upstream <c>CHARSET_ESCAPES</c> keys (lines 4606-4613).</summary>
    private static readonly System.Collections.Frozen.FrozenSet<char> _charsetEscapes =
        System.Collections.Frozen.FrozenSet.ToFrozenSet(['d', 'D', 'h', 's', 'S', 'w', 'W']);

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
    /// Deliberate corner cut, S07. Below U+0080 the answer is exactly "is it an ASCII letter", and
    /// upstream's ASCII and Unicode encodings agree there; above it they do not, and the answer
    /// needs the tables. Measured against the built oracle on 2026-08-30: over codepoints 0-127
    /// both <c>get_all_cases(regex.UNICODE, cp)</c> and <c>get_all_cases(regex.ASCII, cp)</c>
    /// report exactly the 52 codepoints A-Z and a-z as cased, and they first disagree at U+00B5.
    /// <b>Ceiling:</b> any pattern with a non-ASCII character under <c>IGNORECASE</c>, or under the
    /// <c>LOCALE</c> encoding, throws instead of compiling. <b>Upgrade path:</b> S09 transliterates
    /// <c>re_get_all_cases</c> and this becomes a call to it.
    /// </remarks>
    internal static bool IsCasedI(Info info, int ch)
    {
        if ((info.Flags & RegexFlags.Locale) != 0)
        {
            throw new NotImplementedException(
                "needs:locale-flag - the LOCALE encoding's casing depends on the C locale (S09)"
            );
        }

        if (ch >= 0x80)
        {
            throw new NotImplementedException(
                "needs:unicode-tables - deciding whether a non-ASCII character is cased needs the Unicode tables (S09)"
            );
        }

        return RegexFlags.IsAlpha(ch);
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
                        throw new NotImplementedException(
                            "needs:character-classes - parse_set and the set node types are not ported yet (S10)"
                        );

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
                            // It's not a quantifier. Maybe it's a fuzzy constraint. Upstream parses
                            // one here and, when there is none, falls through to "the element was
                            // just a literal" - which is what makes `a{`, `{}` and `{x}` ordinary
                            // literal braces. Telling those two apart needs the whole fuzzy
                            // constraint grammar (parse_fuzzy_item and its eight helpers, S13), so
                            // there is no narrower seam to throw at than this one.
                            ParseFuzzy(source, info, ch, caseFlags);
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

        return new Sequence([.. sequence.Where(item => item is not null).Select(item => item!)]);
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

    /// <summary>Upstream <c>parse_fuzzy</c> (lines 655-677).</summary>
    /// <param name="source">The scanner.</param>
    /// <param name="info">The parse state.</param>
    /// <param name="ch">The character that started the constraint.</param>
    /// <param name="caseFlags">The case flags in force.</param>
    /// <returns>The constraints, never - this throws until S13.</returns>
    internal static object? ParseFuzzy(Source source, Info info, int ch, int caseFlags)
    {
        _ = (source, info, ch, caseFlags);

        throw new NotImplementedException(
            "needs:fuzzy-syntax - parse_fuzzy_item and the cost grammar are not ported yet (S13)"
        );
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
                    throw new NotImplementedException(
                        "needs:lookbehind - parse_lookaround and the LookAround node are not ported yet (S11)"
                    );
                }

                // (?<...: a named capture group.
                source.Pos = savedPos3;
                return ParseNamedGroup(source, info);
            }

            if (ch is '=' or '!')
            {
                throw new NotImplementedException(
                    "needs:lookaround - parse_lookaround and the LookAround node are not ported yet (S11)"
                );
            }

            if (ch == 'P')
            {
                // (?P...: a Python extension.
                return ParseExtension(source, info);
            }

            if (ch == '#')
            {
                throw new NotImplementedException("needs:comments - parse_comment is not ported yet (S10)");
            }

            if (ch == '(')
            {
                throw new NotImplementedException(
                    "needs:conditionals - parse_conditional and the Conditional node are not ported yet (S11)"
                );
            }

            if (ch == '>')
            {
                throw new NotImplementedException(
                    "needs:atomic - parse_atomic and the Atomic node are not ported yet (S11)"
                );
            }

            if (ch == '|')
            {
                throw new NotImplementedException("needs:branch-reset - parse_common is not ported yet (S11)");
            }

            if (ch is 'R' or (>= '0' and <= '9'))
            {
                throw new NotImplementedException(
                    "needs:recursion - parse_call_group and the CallGroup node are not ported yet (S11)"
                );
            }

            if (ch == '&')
            {
                throw new NotImplementedException("needs:recursion - parse_call_named_group is not ported yet (S11)");
            }

            if (ch is '+' or '-' && RegexFlags.IsDigit(source.Peek()))
            {
                throw new NotImplementedException("needs:recursion - parse_rel_call_group is not ported yet (S11)");
            }

            // (?...: probably a flags subpattern.
            source.Pos = savedPos2;
            return ParseFlagsSubpattern(source, info);
        }

        if (ch == '*')
        {
            // (*...
            int savedPos2 = source.Pos;
            string word = source.GetWhile(c => c is ')' or '>', include: false);
            if (word.Length > 0)
            {
                // Upstream's test is word[:1].isalpha(), which is Unicode-aware: measured against
                // the local oracle 2026-08-30, '(*e)' and '(*Ab)' both fail with "unknown verb"
                // at position 2 while '(*\U0001F600)' falls through to "nothing to repeat" at 1,
                // so the branch really does turn on Unicode letterhood and not on being ASCII.
                RequireAscii(word[..1]);
                if (RegexFlags.IsAlpha(word[0]))
                {
                    throw new NotImplementedException(
                        "needs:backtracking-verbs - the VERBS table and its nodes are not ported yet (S11)"
                    );
                }
            }

            // Upstream falls through to an unnamed capture group when the word is not alphabetic,
            // without rewinding to savedPos2 - the rewind below goes all the way back to savedPos.
            _ = savedPos2;
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
            throw new NotImplementedException("needs:backrefs - the RefGroup node is not ported yet (S11)");
        }

        if (ch is '>' or '&')
        {
            // (?P>...: a call to a group.
            throw new NotImplementedException("needs:recursion - parse_call_named_group is not ported yet (S11)");
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
        string name = source.GetWhile(c => c is ')' or '>', include: false);

        if (name.Length == 0)
        {
            throw new FuzzyRegexParseException("missing group name", source.String, source.Pos);
        }

        if (IsDigitName(name))
        {
            int minGroup = allowGroup0 ? 0 : 1;

            // BigInteger, not int: Python's int() has no fixed width, so upstream compares the
            // value against min_group, finds a huge number is not below it, and carries on to
            // fail on the missing ">" instead - which parse_escape catches and degrades to
            // literals. int.Parse would throw OverflowException straight through that catch and
            // turn `\g<99999999999` into an error upstream does not raise. Info.IsOpenGroup
            // parses the same name and needs the same treatment. Pinned by
            // GroupReferenceFallbackTests.
            //
            // Not identical above 4300 digits: CPython 3.11+ caps int(str) at
            // sys.get_int_max_str_digits() and raises ValueError, so upstream rejects
            // `\g<` + 4301 nines where we compile it. Deliberately not ported - that limit is a
            // CPython interpreter setting, changeable at runtime and with no .NET equivalent,
            // not part of the regex grammar. Measured 2026-08-30, see DECISIONS.
            if (!allowNumeric || BigInteger.Parse(name, CultureInfo.InvariantCulture) < minGroup)
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
    /// Python's version is Unicode-aware (superscript two is a digit to it), so anything outside
    /// ASCII throws rather than guessing. <b>Upgrade path:</b> S09's Unicode tables.
    /// </remarks>
    internal static bool IsDigitName(string name)
    {
        RequireAscii(name);
        return name.Length > 0 && name.All(c => RegexFlags.IsDigit(c));
    }

    /// <summary>Python's <c>str.isidentifier</c>, which upstream uses to validate a group name.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> if it is a legal Python identifier.</returns>
    /// <remarks>
    /// Python's version accepts any character with the Unicode <c>XID_Start</c> or
    /// <c>XID_Continue</c> property, so anything outside ASCII throws rather than guessing.
    /// <b>Upgrade path:</b> S09's Unicode tables.
    /// </remarks>
    internal static bool IsIdentifierName(string name)
    {
        RequireAscii(name);
        if (name.Length == 0 || (!RegexFlags.IsAlpha(name[0]) && name[0] != '_'))
        {
            return false;
        }

        return name.All(c => RegexFlags.IsAlnum(c) || c == '_');
    }

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
            throw new NotImplementedException(
                "needs:named-lists - parse_string_set and the StringSet node are not ported yet (S13)"
            );
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
            throw new NotImplementedException("needs:escapes - \\R needs the Atomic (S11) and SetUnion (S10) nodes");
        }

        if (ch == 'X' && !inSet)
        {
            throw new NotImplementedException("needs:grapheme - the Grapheme node is not ported yet (S10)");
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

            if (_charsetEscapes.Contains((char)ch))
            {
                throw new NotImplementedException(
                    "needs:character-classes - the predefined character-set escapes are not ported yet (S10)"
                );
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
            if (digits.All(c => RegexFlags.IsOctDigit(c)) && RegexFlags.IsOctDigit(next))
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

        throw new NotImplementedException("needs:backrefs - the RefGroup node is not ported yet (S11)");
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
        if (text.Length > 0 && text.All(c => RegexFlags.IsOctDigit(c)))
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
        string name = ParseName(source, allowNumeric: true);
        source.Expect(">");
        if (info.IsOpenGroup(name))
        {
            throw new FuzzyRegexParseException("cannot refer to an open group", source.String, source.Pos);
        }

        throw new NotImplementedException("needs:backrefs - the RefGroup node is not ported yet (S11)");
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
            _ = source.GetWhile(RegexFlags.IsNamedCharPart, keepSpaces: true);
            if (source.MatchText("}"))
            {
                // Upstream: value = unicodedata.lookup(name), or error("undefined character name").
                throw new NotImplementedException(
                    "needs:named-characters - \\N{...} needs the Unicode character-name table (S09)"
                );
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
    /// <remarks>
    /// Only <c>lookup_property</c> needs the Unicode property tables; the scanning around it is
    /// what makes a bare <c>\p</c> the literal <c>p</c>.
    /// </remarks>
    internal static RegexBase ParseProperty(Source source, Info info, bool positive, bool inSet)
    {
        int savedPos = source.Pos;
        int ch = source.Get();
        if (ch == '{')
        {
            _ = source.MatchText("^");
            _ = ParsePropertyName(source);
            if (source.MatchText("}"))
            {
                // It's correctly delimited.
                throw new NotImplementedException(
                    "needs:unicode-properties - lookup_property needs the Unicode property tables (S09)"
                );
            }
        }
        else if (ch is 'C' or 'L' or 'M' or 'N' or 'P' or 'S' or 'Z')
        {
            // An abbreviated property, eg \pL.
            throw new NotImplementedException(
                "needs:unicode-properties - lookup_property needs the Unicode property tables (S09)"
            );
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
                source.GetWhile(c => RegexFlags.IsAlnum(c) || c is ' ' or '&' or '_' or '-' or '.' or '/')
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

        // Build the firstset. Upstream:
        //     fs = SetUnion(info, list(members), case_flags=case_flags & ~FULLCASE, zerowidth=True)
        //     return fs.optimise(info, reverse, in_set=True)
        int setCaseFlags = RegexFlags.CaseFlagsCombination(caseFlags & ~RegexFlags.FullCase);

        if (members.Count == 1)
        {
            // SetUnion.optimise's one-member case (lines 3939-3943) hands the member back with the
            // set's flags rather than building a set at all, so the commonest firstset of all - a
            // pattern that starts with one known character - needs no SetUnion node. The member's
            // own optimise is then the identity for a Character (line 2608), which is the only node
            // a firstset can hold until the set types land.
            RegexBase only = members.First();
            return only.WithFlags(positive: only.Positive, caseFlags: setCaseFlags, zerowidth: true);
        }

        // More than one member needs the real SetUnion. That is also one of the two points
        // PORTMAP's "Where we diverge" requires the members to be sorted at, because a Python set
        // of nodes has no stable order; the sort lands with the set node.
        _ = (info, reverse);
        throw new NotImplementedException(
            "needs:character-classes - a firstset of more than one member needs the SetUnion node (S10)"
        );
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
        // Upstream reads `parsed` to decide whether the pattern as a whole is fuzzy, inside the
        // loop below.
        _ = parsed;

        if (info.GroupCalls.Count > 0)
        {
            // The body of upstream's loop needs CallRef, Fuzzy and the group-call nodes.
            throw new NotImplementedException(
                "needs:recursion - group calls need the CallRef and CallGroup nodes (S11)"
            );
        }

        info.CallRefs = [];
        info.AdditionalGroups = [];
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

    private static void RequireAscii(string name)
    {
        if (name.Any(c => c >= 0x80))
        {
            throw new NotImplementedException(
                "needs:unicode-tables - a non-ASCII group name needs Python's Unicode identifier rules (S09)"
            );
        }
    }
}
