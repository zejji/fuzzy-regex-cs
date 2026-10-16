using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// Turning one item of a compiled replacement template into text, and expanding a
/// <c>str.format</c>-style template against a match.
/// </summary>
/// <remarks>
/// <para>
/// Port of the replacement helpers of <c>upstream/src/_regex.c</c>: <c>get_sub_replacement</c>
/// (<c>:21667</c>), which reads a match still living in the state, and
/// <c>get_match_replacement</c> (<c>:19635</c>), which reads a finished <see cref="Match"/>. The
/// loop that drives them is <c>pattern_subx</c> (<c>:21726</c>), ported onto
/// <c>FuzzyRegex.Subx</c> because it needs the pattern and the state.
/// </para>
/// <para>
/// <see cref="ExpandFormat"/> has no counterpart in <c>_regex.c</c>: <c>match_expandf</c>
/// (<c>:20045</c>) hands the template to CPython's <c>str.format</c> with one <c>Capture</c> object
/// per group, so what has to be ported is CPython's own format grammar, narrowed to what a
/// <c>Capture</c> can answer. See <c>docs/PORTMAP.md</c>.
/// </para>
/// </remarks>
internal static class Substitution
{
    /// <summary>
    /// Replaces every match, up to <paramref name="count"/> of them. Port of <c>pattern_subx</c>
    /// (<c>upstream/src/_regex.c</c> line 21726) less its argument parsing, covering upstream's
    /// <c>sub</c>, <c>subn</c>, <c>subf</c> and <c>subfn</c> and all four of their replacement
    /// kinds: a literal, a compiled template, a format string and a callable.
    /// </summary>
    /// <param name="regex">Upstream's <c>self</c>: the pattern being substituted with.</param>
    /// <param name="input">The subject.</param>
    /// <param name="start">Upstream's <c>pos</c>, before clamping.</param>
    /// <param name="end">Upstream's <c>endpos</c>, before clamping.</param>
    /// <param name="template">The replacement or format template, or <see langword="null"/> when
    /// <paramref name="evaluator"/> is given.</param>
    /// <param name="evaluator">Computes each replacement, or <see langword="null"/> for a template.</param>
    /// <param name="isFormat">Whether the template is a <c>str.format</c> one (upstream's <c>RE_SUBF</c>).</param>
    /// <param name="count">The most replacements to make, or a negative number for no limit.</param>
    /// <param name="replacements">Receives how many replacements were made.</param>
    /// <param name="limits">The time budget and cancellation token bounding the whole operation.</param>
    /// <returns>The subject with the matches replaced.</returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// The operation ran out of time.
    /// </exception>
    /// <exception cref="OperationCanceledException">The caller's token was cancelled.</exception>
    internal static string Subx(
        FuzzyRegex regex,
        string input,
        int start,
        int end,
        string? template,
        MatchEvaluator? evaluator,
        bool isFormat,
        int count,
        out int replacements,
        MatchLimits limits
    )
    {
        PatternObject pattern = regex.PatternObject;

        // get_limits (:21791) runs BEFORE the shortcut below, so the shortcut compares the
        // pattern's width against the CLAMPED slice rather than against whatever the caller
        // passed. Measured against regex 2026.9.10 on 2026-09-16:
        // `regex.compile('xx').sub(r'\g<bad', 'xxxxx', pos=4)` returns 'xxxxx' - the slice is one
        // character wide, so the shortcut fires and the malformed template is never compiled.
        // MatchState.Create clamps the same pair again to the same values, which is what upstream
        // does too (state_init_2 re-derives nothing).
        start = MatchState.ClampIndex(start, input.Length);
        end = MatchState.ClampIndex(end, input.Length);
        if (end < start)
        {
            end = start;
        }

        // "If the pattern is too long for the string, then take a shortcut, unless it's a fuzzy
        // pattern" (:21762). This runs before the template is compiled, so a malformed template is
        // not rejected on a subject the pattern cannot fit - measured, see
        // FuzzyRegex.CompileReplacement.
        //
        // Against the CODEPOINT count, not the code-unit count: `min_width` is a codepoint count
        // and this is a UTF-16 string, so the two differ on an astral subject and the shortcut
        // would be skipped where upstream takes it. Measured 2026-09-01:
        // `regex.subn('..', r'\g<bad>', '\U0001F600')` is `('\U0001f600', 0)` because 2 > 1, where
        // 2 > 2 is false and the template would be compiled and rejected. Found by S24's blind
        // review.
        //
        // `do_exact_match`'s width check (`Matcher.cs`) is the same comparison and now counts
        // characters too. It used code units until S40c, under the reading that a check which only
        // ever fails early may safely over-count; that reading was wrong, because the check gates
        // the NON-PARTIAL pass of a partial request and failing to fire suppresses the partial
        // retry. See the comment there.
        if (!pattern.IsFuzzy && HasFewerCodepointsThan(input, start, end, pattern.MinWidth))
        {
            replacements = 0;
            return input;
        }

        // Upstream spells "no limit" as count=0 and reads a negative count as "no replacements at
        // all" (regex.sub('a', 'b', 'aaaaa', count=-1) is ('aaaaa', 0), measured 2026-09-01). This
        // surface spells no limit as -1, as S01 decided, so the two are swapped here and nowhere else.
        int maxSub = count < 0 ? int.MaxValue : count;

        // A literal is used as it is; anything else is compiled or formatted per match.
        bool isLiteral = evaluator is null && IsLiteralTemplate(template!, isFormat ? '{' : '\\');
        IReadOnlyList<object>? compiled =
            evaluator is null && !isLiteral && !isFormat ? regex.CompileReplacement(template!) : null;

        using var state = regex.StateCache.Rent(
            pattern,
            input.AsMemory(),
            start,
            end,
            overlapped: false,
            partial: false,
            // "The MatchObject, and therefore repeated captures, will be visible only if the
            // replacement is callable or subf is used."
            visibleCaptures: evaluator is not null || isFormat,
            matchAll: false,
            limits
        );

        // A literal, or a template whose only group item is the whole match (item 0, read from
        // MatchPos and TextPos in GetSubReplacement), reads no group.
        if (isLiteral || (compiled is not null && compiled.All(static item => item is string or 0)))
        {
            state.SkipCapturesIfUnread();
        }

        // Upstream's join list (init_join_list, :19706): the pieces of the result, joined once the
        // last match is in. A reverse match adds them back to front and the join reads them the
        // other way round, which is upstream's list reversal (join_list_info, :19852).
        JoinList joined = default;
        try
        {
            int subCount = 0;
            int lastPos = state.Reverse ? state.TextLength : 0;

            while (subCount < maxSub)
            {
                int status = Matcher.DoMatch(state, search: true);
                if (status == MatchStatus.Cancelled)
                {
                    throw limits.Cancelled(input, regex.Pattern);
                }

                if (status != MatchStatus.Success)
                {
                    break;
                }

                // The segment before this match. An empty one adds nothing (JoinList.Add).
                joined.Add(
                    state.Reverse ? Slice(input, state.MatchPos, lastPos) : Slice(input, lastPos, state.MatchPos)
                );

                AddReplacement(
                    ref joined,
                    regex,
                    input,
                    state,
                    status,
                    template,
                    evaluator,
                    isLiteral,
                    isFormat,
                    compiled
                );

                subCount++;
                lastPos = state.TextPos;

                // Upstream's own line here is `state->must_advance = state->text_pos ==
                // state->match_pos` (:22047), which is what AdvancePastMatch does for a state whose
                // `overlapped` is false - and subx never sets it. Shared rather than repeated because
                // the scanner and the splitter run the same rule, and a slip in one of three copies
                // would be invisible.
                state.AdvancePastMatch();
            }

            // The segment following the last match.
            joined.Add(state.Reverse ? input.AsMemory(0, lastPos) : input.AsMemory(lastPos));

            replacements = subCount;
            return joined.Join(reverse: state.Reverse);
        }
        finally
        {
            joined.Dispose();
        }
    }

    /// <summary>
    /// Upstream's join list (<c>RE_JoinInfo</c>, <c>upstream/src/_regex.c</c> line 670): the
    /// pieces of a substitution's result, held as stretches of the strings they come from and
    /// copied once, into the result, when the last match is in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No piece is copied out of the subject before the join.</b> Until C2 this was a
    /// <c>List&lt;string&gt;</c> of substrings joined by <c>string.Concat</c>, so every piece was
    /// allocated and copied twice, and a pass over a megabyte with two group references in its
    /// template allocated 19.9 MB for a 2 MB answer (the <c>ReplaceTemplate</c> row of the
    /// 2026-10-13 gap table). The pieces live in an array rented from
    /// <see cref="ArrayPool{T}.Shared"/>, cleared and handed back by <see cref="Dispose"/>, which
    /// <c>Subx</c> calls in a <c>finally</c> so a timeout, a cancellation or a throwing evaluator
    /// returns it too.
    /// </para>
    /// <para>
    /// A <c>ref struct</c>, so it cannot outlive the call that owns the rented array, and
    /// <c>default</c> is an empty list: nothing is rented until the first non-empty piece.
    /// </para>
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    private ref struct JoinList
    {
        private ReadOnlyMemory<char>[]? _items;
        private int _count;
        private long _length;

        /// <summary>Adds a piece. An empty piece adds nothing, exactly as upstream skips one.</summary>
        /// <param name="item">The piece, a stretch of the subject, the template or a returned string.</param>
        internal void Add(ReadOnlyMemory<char> item)
        {
            if (item.IsEmpty)
            {
                return;
            }

            if (_items is null || _count == _items.Length)
            {
                Grow();
            }

            _items![_count++] = item;
            _length += item.Length;
        }

        /// <summary>The pieces joined, in order, or back to front for a reverse match.</summary>
        /// <param name="reverse">Whether the pieces were added back to front.</param>
        /// <returns>The joined text.</returns>
        /// <remarks>
        /// One piece that is a whole string is that string, so a call that replaced nothing hands
        /// back the subject itself, as <c>string.Concat</c> did over a one-item list. A total past
        /// <see cref="int.MaxValue"/> asks for the longest string instead, which the runtime refuses
        /// with the <see cref="OutOfMemoryException"/> <c>string.Concat</c> threw for it.
        /// </remarks>
        internal readonly string Join(bool reverse)
        {
            if (_count == 0)
            {
                return "";
            }

            if (
                _count == 1
                && MemoryMarshal.TryGetString(_items![0], out string? whole, out int start, out int length)
                && start == 0
                && length == whole.Length
            )
            {
                return whole;
            }

            return string.Create(
                _length > int.MaxValue ? int.MaxValue : (int)_length,
                (Items: _items!, Count: _count, Reverse: reverse),
                static (destination, join) =>
                {
                    int written = 0;
                    for (int i = 0; i < join.Count; i++)
                    {
                        ReadOnlySpan<char> item = join.Items[join.Reverse ? join.Count - 1 - i : i].Span;
                        item.CopyTo(destination[written..]);
                        written += item.Length;
                    }
                }
            );
        }

        /// <summary>Clears the pieces, so the pool holds no reference to a subject, and returns the array.</summary>
        internal void Dispose()
        {
            if (_items is null)
            {
                return;
            }

            _items.AsSpan(0, _count).Clear();
            ArrayPool<ReadOnlyMemory<char>>.Shared.Return(_items);
            _items = null;
            _count = 0;
            _length = 0;
        }

        /// <summary>Doubles the array, starting at sixteen pieces.</summary>
        private void Grow()
        {
            ReadOnlyMemory<char>[] grown = ArrayPool<ReadOnlyMemory<char>>.Shared.Rent(
                _items is null ? 16 : _items.Length * 2
            );

            if (_items is not null)
            {
                _items.AsSpan(0, _count).CopyTo(grown);
                _items.AsSpan(0, _count).Clear();
                ArrayPool<ReadOnlyMemory<char>>.Shared.Return(_items);
            }

            _items = grown;
        }
    }

    /// <summary>
    /// Whether a stretch of the subject holds fewer than <paramref name="width"/> codepoints, which
    /// is what upstream's <c>min_width</c> counts.
    /// </summary>
    /// <remarks>
    /// A codepoint is one or two UTF-16 code units, so the stretch's length settles the question
    /// unless it lies between <paramref name="width"/> and <c>2 * width - 1</c> units, and then only
    /// that many units are counted. Before D83 this counted every codepoint of the input on every
    /// call of a non-fuzzy pattern, whatever its width.
    /// </remarks>
    /// <param name="text">The subject.</param>
    /// <param name="start">Where the stretch starts.</param>
    /// <param name="end">One past where it ends.</param>
    /// <param name="width">The pattern's minimum width, in codepoints.</param>
    /// <returns>Whether the pattern cannot fit.</returns>
    private static bool HasFewerCodepointsThan(string text, int start, int end, long width)
    {
        int units = end - start;
        if (units < width)
        {
            return true;
        }

        if (width <= units / 2)
        {
            return false;
        }

        WorkCounter.ScannedSubject(units);
        long count = 0;
        foreach (Rune _ in text.AsSpan(start, units).EnumerateRunes())
        {
            if (++count >= width)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Adds what this match is replaced by: the four branches of <c>pattern_subx</c>'s loop body
    /// (<c>:21886-22045</c>), in upstream's order.
    /// </summary>
    /// <param name="joined">The join list.</param>
    /// <param name="regex">The pattern, for building a match object.</param>
    /// <param name="input">The subject.</param>
    /// <param name="state">The state holding the match.</param>
    /// <param name="status">What <c>do_match</c> returned.</param>
    /// <param name="template">The template, if there is one.</param>
    /// <param name="evaluator">The callable, if there is one.</param>
    /// <param name="isLiteral">Whether the template is used verbatim.</param>
    /// <param name="isFormat">Whether the template is a format string.</param>
    /// <param name="compiled">The compiled template, if there is one.</param>
    private static void AddReplacement(
        ref JoinList joined,
        FuzzyRegex regex,
        string input,
        MatchState state,
        int status,
        string? template,
        MatchEvaluator? evaluator,
        bool isLiteral,
        bool isFormat,
        IReadOnlyList<object>? compiled
    )
    {
        if (isLiteral)
        {
            joined.Add(template.AsMemory());
            return;
        }

        if (isFormat)
        {
            joined.Add(regex.NewMatch(state, input, status).ResultFormat(template!).AsMemory());
            return;
        }

        if (compiled is null)
        {
            // Upstream adds nothing when the callable returns None; a null string is the same.
            joined.Add(evaluator!(regex.NewMatch(state, input, status)).AsMemory());
            return;
        }

        // Searching backwards means the whole list is read back to front at the end, so the
        // template's own items go on in reverse order to come out in the right one.
        for (int i = 0; i < compiled.Count; i++)
        {
            object item = compiled[state.Reverse ? compiled.Count - 1 - i : i];
            joined.Add(GetSubReplacement(item, input, state, regex.GroupCount));
        }
    }

    /// <summary>
    /// Upstream <c>check_replacement_string</c> (<c>upstream/src/_regex.c</c> line 19865): a
    /// template with no special character in it is used exactly as it is, never compiled.
    /// </summary>
    /// <param name="replacement">The template.</param>
    /// <param name="specialChar"><c>\</c> for a <c>sub</c> template, <c>{</c> for a <c>subf</c> one.</param>
    /// <returns><see langword="true"/> if the template is a literal.</returns>
    /// <remarks>
    /// Upstream returns the template's length so that its callers can tell an empty template
    /// (length 0) from a literal one (length above 0) and skip it entirely. Both add nothing to
    /// the join list, so the distinction collapses here: an empty literal appends an empty string.
    /// <para>
    /// This is not merely an optimisation, and porting it as one would be a divergence: because a
    /// literal is never handed to the template compiler, a template that is malformed in a way
    /// only the compiler would notice is not rejected unless it holds the special character.
    /// </para>
    /// </remarks>
    internal static bool IsLiteralTemplate(string replacement, char specialChar) =>
        !replacement.Contains(specialChar, StringComparison.Ordinal);

    /// <summary>
    /// Upstream <c>get_sub_replacement</c> (<c>upstream/src/_regex.c</c> line 21667): one item of a
    /// compiled template, resolved against the match currently in the state.
    /// </summary>
    /// <param name="item">A literal run (<see cref="string"/>) or a group number (<see cref="int"/>).</param>
    /// <param name="subject">The subject being searched.</param>
    /// <param name="state">The state holding the match.</param>
    /// <param name="groupCount">The pattern's <c>public_group_count</c>.</param>
    /// <returns>The text the item stands for, as a stretch of the subject or of the template.</returns>
    /// <exception cref="FuzzyRegexParseException">The template references a group the pattern has not got.</exception>
    /// <remarks>
    /// Upstream returns <c>None</c> for a zero-width whole match and for a group that took no part,
    /// and its caller then adds nothing to the join list. Both cases are an empty stretch here,
    /// which the join list skips, so this returns the text in every case.
    /// </remarks>
    private static ReadOnlyMemory<char> GetSubReplacement(object item, string subject, MatchState state, int groupCount)
    {
        if (item is string literal)
        {
            return literal.AsMemory();
        }

        int index = (int)item;
        if (index == 0)
        {
            // The entire matched portion of the string. A reverse match reports its two ends the
            // other way round, exactly as pattern_new_match does.
            return state.Reverse
                ? Slice(subject, state.TextPos, state.MatchPos)
                : Slice(subject, state.MatchPos, state.TextPos);
        }

        if (index >= 1 && index <= groupCount)
        {
            System.Diagnostics.Debug.Assert(
                !state.SkipsCaptures,
                "C4: a template that reads a group must not skip the captures (Subx opts in only for a literal or item 0)"
            );
            GroupData group = state.Groups[index - 1];
            if (group.Current < 0)
            {
                return ReadOnlyMemory<char>.Empty;
            }

            GroupSpan span = group.Captures[group.Current];
            return Slice(subject, span.Start, span.End);
        }

        // RE_ERROR_INVALID_GROUP_REF (:21721), which set_error renders as regex.error with the
        // message below and no position (:2122). Verified 2026-09-01: regex.sub('x', r'\1', 'x')
        // raises `error: invalid group reference`, where match.expand raises IndexError instead -
        // see GetMatchReplacement, which is the same check with upstream's other answer.
        throw new FuzzyRegexParseException("invalid group reference");
    }

    /// <summary>
    /// Upstream <c>get_slice</c> (<c>upstream/src/_regex.c</c> line 18842): the subject from
    /// <paramref name="start"/> to <paramref name="end"/>, empty when the end lies before the start.
    /// A <c>\K</c> inside a lookaround leaves such a match, as <c>(?=y\K)</c> over <c>yy</c> does with
    /// (1, 0), and <c>PyUnicode_Substring</c> answers '' for it (D89).
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <param name="start">Where the stretch starts.</param>
    /// <param name="end">One past where it ends.</param>
    /// <returns>The stretch.</returns>
    private static ReadOnlyMemory<char> Slice(string subject, int start, int end) =>
        end <= start ? ReadOnlyMemory<char>.Empty : subject.AsMemory(start, end - start);

    /// <summary>
    /// Upstream <c>get_match_replacement</c> (<c>upstream/src/_regex.c</c> line 19635): one item of
    /// a compiled template, resolved against a finished match.
    /// </summary>
    /// <param name="item">A literal run (<see cref="string"/>) or a group number (<see cref="int"/>).</param>
    /// <param name="match">The match to read the groups from.</param>
    /// <param name="groupCount">The pattern's group count.</param>
    /// <returns>The text the item stands for.</returns>
    /// <exception cref="ArgumentException">The template references a group the pattern has not got.</exception>
    internal static string GetMatchReplacement(object item, Match match, int groupCount)
    {
        if (item is string literal)
        {
            return literal;
        }

        int index = (int)item;
        if (index == 0)
        {
            return match.Value;
        }

        if (index >= 1 && index <= groupCount)
        {
            // Upstream returns None for a group that took no part, which joins as "".
            Group group = match.GroupAt(index);
            return group.Success ? group.Value : "";
        }

        // RE_ERROR_NO_SUCH_GROUP (:19681), which is upstream's IndexError("no such group") - not
        // its own error type, and a different answer from the one `sub` gives for the same
        // template (measured 2026-09-01). ArgumentException for the reason DECISIONS 2026-08-31
        // records for `\g<name>`, and the three paramName rules are disapplied for the same
        // reason: the argument at fault is the public `replacement`, several frames above.
#pragma warning disable CA2208, S3928, MA0015
        throw new ArgumentException("no such group", "replacement");
#pragma warning restore CA2208, S3928, MA0015
    }

    /// <summary>
    /// Expands a <c>str.format</c>-style template, which is what <c>match_expandf</c>
    /// (<c>upstream/src/_regex.c</c> line 20045) delegates to CPython by calling
    /// <c>template.format(*captures, **named_captures)</c>.
    /// </summary>
    /// <param name="template">The format template.</param>
    /// <param name="resolve">
    /// Looks a field name up as a group: a run of digits is a positional argument and anything else
    /// is a named one, and <see langword="null"/> means the match has no such group.
    /// </param>
    /// <returns>The expanded text.</returns>
    /// <exception cref="FormatException">The template is malformed.</exception>
    /// <exception cref="ArgumentException">It references a group or a capture that is not there.</exception>
    /// <exception cref="NotSupportedException">
    /// It uses a conversion or a format spec, both of which upstream rejects too - see
    /// <see cref="ExpandField"/>.
    /// </exception>
    /// <remarks>
    /// A port of CPython's <c>str.format</c> grammar, not of a function in <c>_regex.c</c>, and
    /// narrowed to the fields a <c>_regex.Capture</c> can answer: the argument name, one optional
    /// <c>[index]</c> subscript, and nothing else.
    /// </remarks>
    internal static string ExpandFormat(string template, Func<string, Group?> resolve)
    {
        var result = new StringBuilder(template.Length);

        // CPython's automatic field numbering: unset until the first field decides it, after which
        // the two styles cannot be mixed.
        int automatic = -1;
        bool manual = false;

        int pos = 0;
        while (pos < template.Length)
        {
            char c = template[pos++];
            if (c == '}')
            {
                if (pos < template.Length && template[pos] == '}')
                {
                    result.Append('}');
                    pos++;
                    continue;
                }

                throw new FormatException("single '}' encountered in a format template");
            }

            if (c != '{')
            {
                result.Append(c);
                continue;
            }

            if (pos < template.Length && template[pos] == '{')
            {
                result.Append('{');
                pos++;
                continue;
            }

            int close = template.IndexOf('}', pos);
            if (close < 0)
            {
                throw new FormatException("single '{' encountered in a format template");
            }

            result.Append(ExpandField(template[pos..close], resolve, ref automatic, ref manual));
            pos = close + 1;
        }

        return result.ToString();
    }

    /// <summary>
    /// One replacement field of a format template - CPython's <c>field_name[!conversion][:spec]</c>
    /// - resolved to the text it stands for.
    /// </summary>
    /// <param name="field">The field, without its braces.</param>
    /// <param name="resolve">Looks a field name up as a group.</param>
    /// <param name="automatic">The next automatic field number, or <c>-1</c> if unset.</param>
    /// <param name="manual">Whether a field has already been numbered by hand.</param>
    /// <returns>The field's text.</returns>
    /// <remarks>
    /// Three shapes of field are rejected rather than supported, because <b>upstream rejects them
    /// too</b> - all three measured against <c>regex</c> 2026.7.19 on 2026-09-01:
    /// <list type="bullet">
    /// <item>A format spec: <c>regex.subf(r'(\w+)', '{1:&gt;10}', 'ab')</c> raises
    /// <c>TypeError: unsupported format string passed to _regex.Capture.__format__</c>, because
    /// the object being formatted is a <c>Capture</c> and not a <c>str</c>.</item>
    /// <item>The <c>!r</c> and <c>!a</c> conversions: they give
    /// <c>'&lt;_regex.Capture object at 0x...&gt;'</c>, a CPython object address that no port can
    /// reproduce and that nothing could want. <c>!s</c> is the identity and is supported.</item>
    /// <item>Attribute access (<c>{0.x}</c>) and a chained subscript (<c>{1[0][0]}</c>). The second
    /// does work upstream - it indexes into the <c>str</c> the first subscript produced - and is a
    /// deliberate corner cut: <c>ponytail:</c> one subscript level, because a second one indexes a
    /// string, which is codepoints upstream and UTF-16 code units here. Lift it by giving the
    /// chain its own codepoint-aware indexer if a caller ever wants it.</item>
    /// </list>
    /// </remarks>
    private static string ExpandField(string field, Func<string, Group?> resolve, ref int automatic, ref bool manual)
    {
        // CPython's parse_field: '!' and ':' end the field name, but only outside a subscript.
        int split = -1;
        bool inBrackets = false;
        for (int i = 0; i < field.Length && split < 0; i++)
        {
            if (field[i] == '[')
            {
                inBrackets = true;
            }
            else if (field[i] == ']')
            {
                inBrackets = false;
            }
            else if (!inBrackets && field[i] is '!' or ':')
            {
                split = i;
            }
        }

        string name = split < 0 ? field : field[..split];
        string rest = split < 0 ? "" : field[split..];

        if (rest.StartsWith('!'))
        {
            if (rest.Length < 2)
            {
                throw new FormatException("end of a format template while looking for a conversion");
            }

            char conversion = rest[1];
            if (conversion != 's')
            {
                throw new NotSupportedException(
                    $"the '!{conversion}' conversion is not supported in a format template; upstream "
                        + "applies it to a capture object, which has no portable representation"
                );
            }

            rest = rest[2..];
            if (rest.Length > 0 && rest[0] != ':')
            {
                throw new FormatException("expected ':' after a format template's conversion");
            }
        }

        if (rest.StartsWith(':') && rest.Length > 1)
        {
            throw new NotSupportedException(
                "a format spec is not supported in a format template; upstream raises TypeError "
                    + "because the value being formatted is a capture object, not a string"
            );
        }

        string argument = name;
        string? subscript = null;
        int bracket = name.IndexOf('[', StringComparison.Ordinal);
        if (bracket >= 0)
        {
            if (!name.EndsWith(']'))
            {
                throw new FormatException("unterminated '[' in a format template's field name");
            }

            argument = name[..bracket];
            subscript = name[(bracket + 1)..^1];
            if (subscript.Contains('[', StringComparison.Ordinal))
            {
                throw new NotSupportedException("only one '[...]' subscript is supported in a format template");
            }
        }

        if (argument.Contains('.', StringComparison.Ordinal))
        {
            throw new NotSupportedException("attribute access is not supported in a format template");
        }

        if (argument.Length == 0)
        {
            if (manual)
            {
                throw new FormatException("cannot switch from manual field specification to automatic field numbering");
            }

            automatic = Math.Max(automatic, 0);
            argument = automatic.ToString(CultureInfo.InvariantCulture);
            automatic++;
        }
        else if (IsAllDigits(argument))
        {
            if (automatic >= 0)
            {
                throw new FormatException("cannot switch from automatic field numbering to manual field specification");
            }

            manual = true;
        }

        // Upstream's IndexError (a positional argument past the end of the tuple) or KeyError
        // (a name the pattern has not got).
        Group group = resolve(argument) ?? throw BadField($"the pattern has no group '{argument}'");

        if (subscript is null)
        {
            // capture_str (:21437): the group's text, or "" if it took no part.
            return group.Value;
        }

        // capture_getitem (:21370). A negative index counts back from the end of the capture list,
        // and group 0's list is the whole match alone, so -1 and 0 both name it.
        CaptureCollection captures = group.Captures;
        if (!TryParseSubscript(subscript, out int index))
        {
            throw BadField($"'{subscript}' is not a capture index");
        }

        if (index < 0)
        {
            index += captures.Count;
        }

        return index >= 0 && index < captures.Count
            ? captures[index].Value
            : throw BadField("capture index out of range");
    }

    /// <summary>
    /// A format template that names something the match has not got. Upstream raises
    /// <c>IndexError</c> or <c>KeyError</c>; this is <see cref="ArgumentException"/> for the reason
    /// DECISIONS 2026-08-31 records for <c>\g&lt;name&gt;</c> - the template is a caller's argument.
    /// </summary>
    /// <param name="message">What is wrong with it.</param>
    /// <returns>The exception to throw.</returns>
    /// <remarks>
    /// CA2208, S3928 and MA0015 all want the <c>paramName</c> to name a parameter of the throwing
    /// method. They are right in general and wrong here, exactly as at
    /// <c>ParseFunctions.CompileReplGroup</c>: the argument at fault is the public <c>format</c>
    /// parameter of <c>FuzzyRegex.ReplaceFormat</c> and <c>Match.ResultFormat</c>, and this helper
    /// sits several frames below it. Disapplied here and nowhere else.
    /// </remarks>
#pragma warning disable CA2208, S3928, MA0015
    private static ArgumentException BadField(string message) => new(message, "format");
#pragma warning restore CA2208, S3928, MA0015

    /// <summary>
    /// Whether a field name is a positional argument. CPython's rule, which is a run of ASCII
    /// digits and nothing else - notably not <see cref="char.IsDigit(char)"/>, whose Unicode digits
    /// <c>int()</c> would accept but <c>str.format</c>'s field parser would not.
    /// </summary>
    /// <param name="text">The field name.</param>
    /// <returns><see langword="true"/> if it is all ASCII digits.</returns>
    private static bool IsAllDigits(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);

    /// <summary>
    /// A <c>{n[i]}</c> subscript as an index into a capture list. Upstream reaches the same value
    /// by two routes, and this is the intersection of them.
    /// </summary>
    /// <param name="subscript">The text between the brackets.</param>
    /// <param name="index">The index it denotes.</param>
    /// <returns><see langword="false"/> if it is not one this port accepts.</returns>
    /// <remarks>
    /// CPython's field parser turns a key that is all decimal digits into an <c>int</c> itself, so
    /// <c>{1[01]}</c> is index 1; anything else arrives at <c>capture_getitem</c> as a string and
    /// goes through <c>index_to_integer</c> (<c>upstream/src/_regex.c</c> line 21311), which is
    /// <c>int(text, 0)</c> - a full Python integer literal. Hence the leading-zero rule below:
    /// measured 2026-09-01, <c>regex.subf(r'(\w)+', '{1[01]}', 'abc')</c> is <c>'b'</c> while
    /// <c>'{1[-01]}'</c> raises <c>TypeError</c>, because a *signed* key is a base-0 literal and
    /// base 0 forbids a redundant leading zero.
    /// <para>
    /// ponytail: an optionally-signed ASCII decimal and nothing else. <c>int(text, 0)</c> also
    /// accepts surrounding whitespace, <c>0x</c>/<c>0o</c>/<c>0b</c> prefixes, digit underscores
    /// and non-ASCII decimal digits, so <c>{1[ 0 ]}</c>, <c>{1[0x1]}</c> and <c>{1[٢]}</c>
    /// are rejected here and accepted upstream. That is the ceiling, and it fails in the safe
    /// direction - a refusal, never a different capture. Lift it by porting <c>int(text, 0)</c>
    /// if a caller ever writes a capture index as a hex literal. Raised by S24's blind review,
    /// which also found the <c>-01</c> case, the one that answered where upstream refused.
    /// </para>
    /// </remarks>
    private static bool TryParseSubscript(string subscript, out int index)
    {
        index = 0;
        if (subscript.Length == 0)
        {
            return false;
        }

        bool signed = subscript[0] is '-' or '+';
        string digits = signed ? subscript[1..] : subscript;
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        // base 0's rule, which only a signed key is read under.
        if (signed && digits.Length > 1 && digits[0] == '0')
        {
            return false;
        }

        return int.TryParse(subscript, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out index);
    }
}
