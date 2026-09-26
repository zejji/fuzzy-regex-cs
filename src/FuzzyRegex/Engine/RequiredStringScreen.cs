using System.Buffers;
using Fuzzy.Text.RegularExpressions.Parsing;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// Refuses a subject that cannot hold the pattern's required string, for the five required-string
/// opcodes <c>Matcher.LocateRequiredString</c> has no ported arm for: <c>STRING_REV</c>,
/// <c>STRING_IGN</c>, <c>STRING_IGN_REV</c>, <c>STRING_FLD</c> and <c>STRING_FLD_REV</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> Upstream's <c>locate_required_string</c> (<c>upstream/src/_regex.c</c>
/// lines 11143-11365) searches for these too, so <c>(?i)(?:ss|\xdf)+x</c> over 25 U+00DF is refused
/// in microseconds upstream, where this port ran the exponential search and timed out. A timeout
/// where upstream answers is a wrong answer in practice.
/// </para>
/// <para>
/// <b>Why it is not upstream's arms.</b> Those arms are not transparent (DECISIONS 2026-08-31):
/// <c>string_search_fld</c> compares with <c>same_char_ign_turkic</c>, which the matcher never
/// uses, and the arms hand the matcher a start position and a <c>req_pos</c> it then skips over
/// without comparing. This screen does neither. It only answers "could the required string occur
/// in what is left of the slice?", and it answers yes whenever ANY rule the matcher could apply
/// would let it: a subject character matches a required character if one of its cases is the same
/// character ignoring case, or if the full case folding of one of its cases spells the next few
/// required characters, and a folding may be cut short at either end of the string. That is a
/// superset of what <c>CHARACTER_IGN</c>, <c>STRING_IGN</c> and <c>STRING_FLD</c> accept, so a
/// refusal here is a subject every attempt would have failed on, and an acceptance changes nothing:
/// the matcher then runs every attempt it would have run without the screen. The required characters are the
/// literal's folded characters (<c>ParseFunctions.GetRequiredString</c>), and the case-set rule is
/// what keeps a node holding the unfolded literal inside the superset.
/// </para>
/// <para>
/// <b>Cost.</b> A vectorised search for the code units that could hold the string's first
/// character picks the windows to check. The first scan stops at the place nearest the attempt
/// position, which costs next to nothing when there is one close by, as <c>string_search</c> does
/// upstream. The answer is remembered on the <see cref="MatchState"/> (keyed on the slice bounds),
/// so attempts short of it cost one comparison. Past it, a caller that takes the start-position
/// jump scans on for the next nearest place; any other scans once from the slice edge for the
/// farthest place, and every later attempt is then settled without scanning again.
/// </para>
/// <para>
/// <b>The start-position jump is taken only at offset 0</b>, where the required string is the
/// pattern's first consuming item and so starts exactly where the attempt does (ends, reversed): no
/// attempt short of the nearest possible occurrence can match, so the search starts there. At any
/// other offset a folding can change how many subject characters the items before it take, so the
/// attempts run from where they would have.
/// </para>
/// <para>
/// <b>Partial matching is not screened.</b> A partial match can end before its required string
/// begins, so no subject can be refused for lacking it.
/// </para>
/// </remarks>
internal static class RequiredStringScreen
{
    /// <summary>The cached answer when the pass found no possible occurrence.</summary>
    private const int _notFound = -1;

    /// <summary>
    /// Whether <paramref name="op"/> is one of the opcodes this screen covers.
    /// </summary>
    /// <param name="op">The required-string node's opcode.</param>
    /// <returns><see langword="true"/> for the five opcodes the locator's own arms do not cover.</returns>
    internal static bool Covers(Opcode op) =>
        op is Opcode.StringRev or Opcode.StringIgn or Opcode.StringIgnRev or Opcode.StringFld or Opcode.StringFldRev;

    /// <summary>
    /// The nearest place, from <see cref="MatchState.TextPos"/> in the pattern's direction, where the
    /// required string could occur.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="reqString">The required-string node.</param>
    /// <param name="nearest">
    /// Whether the caller will start its attempt at the place returned, and so needs the nearest
    /// one; otherwise any place will do and only a refusal matters.
    /// </param>
    /// <param name="cancelled">Set when the timeout or token ended the pass.</param>
    /// <returns>
    /// Where the string could start (forward) or end (reverse); <see cref="MatchState.TextPos"/>
    /// itself when the screen stands aside; or -1 when it cannot occur at all, so the whole
    /// operation can be refused.
    /// </returns>
    internal static int Locate(MatchState state, Node reqString, bool nearest, out bool cancelled)
    {
        cancelled = false;

        // LOCALE is not ported and its case tables refuse to answer; screen nothing. Nor a partial
        // match, which can stop before the required string starts.
        if (reqString.Encoding == CaseEncoding.Locale || state.PartialSide != MatchState.PartialNone)
        {
            return state.TextPos;
        }

        bool reverse = reqString.Op is Opcode.StringRev or Opcode.StringIgnRev or Opcode.StringFldRev;
        int textPos = state.TextPos;
        bool covered =
            state.ScreenSliceStart == state.SliceStart
            && state.ScreenSliceEnd == state.SliceEnd
            && (reverse ? textPos <= state.ScreenFrom : textPos >= state.ScreenFrom);

        if (covered)
        {
            // The last scan found nothing between its start and the slice edge; or this position
            // has not passed what it found; or what it found was the farthest there is.
            if (
                state.ScreenFound == _notFound
                || (reverse ? textPos >= state.ScreenFound : textPos <= state.ScreenFound)
            )
            {
                return state.ScreenFound;
            }

            if (state.ScreenFoundFarthest)
            {
                return _notFound;
            }
        }

        // A caller that only needs a yes or no scans for the nearest occurrence the first time,
        // which is cheap when there is one close by, and for the farthest once it has passed that,
        // which settles every later attempt in one scan.
        bool farthest = !nearest && covered;
        int found = Scan(state, reqString, reverse, farthest, out cancelled);

        if (cancelled)
        {
            return _notFound;
        }

        state.ScreenSliceStart = state.SliceStart;
        state.ScreenSliceEnd = state.SliceEnd;
        state.ScreenFrom = textPos;
        state.ScreenFound = found;
        state.ScreenFoundFarthest = farthest;

        // Every attempt between here and what was found is cleared for a caller that does not jump.
        if (!nearest && found != _notFound)
        {
            state.ScreenClearedLow = reverse ? found : textPos;
            state.ScreenClearedHigh = reverse ? textPos : found;
        }
        else
        {
            state.ScreenClearedLow = 1;
            state.ScreenClearedHigh = 0;
        }

        return found;
    }

    /// <summary>
    /// Where, between <see cref="MatchState.TextPos"/> and the slice edge in the pattern's direction,
    /// the required string could start (forward) or end (reverse): the place nearest the position,
    /// or the one farthest from it; or <see cref="_notFound"/>.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="reqString">The required-string node.</param>
    /// <param name="reverse">Whether the pattern reads right to left.</param>
    /// <param name="farthest">Whether to find the farthest place rather than the nearest.</param>
    /// <param name="cancelled">Set when the timeout or token ended the pass.</param>
    /// <returns>The position, or <see cref="_notFound"/>.</returns>
    private static int Scan(MatchState state, Node reqString, bool reverse, bool farthest, out bool cancelled)
    {
        cancelled = false;

        SearchValues<char> units = state.Pattern.ReqScreenUnits!;
        ReadOnlySpan<char> text = state.Text.Span;

        // Candidates are window starts in [TextPos, SliceEnd) forward, and window ends in
        // (SliceStart, TextPos] reversed. The code units that could hold the window's first
        // character (reading order) are searched for in [low, high), from whichever end is nearer
        // the place wanted.
        int low = reverse ? state.SliceStart : state.TextPos;
        int high = reverse ? state.TextPos : state.SliceEnd;
        bool descending = reverse != farthest;

        while (low < high)
        {
            if (Poll(state))
            {
                cancelled = true;
                return _notFound;
            }

            int hit = descending ? text[low..high].LastIndexOfAny(units) : text[low..high].IndexOfAny(units);
            if (hit < 0)
            {
                return _notFound;
            }

            hit += low;

            // The whole character the unit belongs to, within the range.
            int edge;
            if (reverse)
            {
                bool pairStart =
                    hit + 1 < high && char.IsHighSurrogate(text[hit]) && char.IsLowSurrogate(text[hit + 1]);
                edge = pairStart ? hit + 2 : hit + 1;
            }
            else
            {
                bool pairEnd = hit > low && char.IsLowSurrogate(text[hit]) && char.IsHighSurrogate(text[hit - 1]);
                edge = pairEnd ? hit - 1 : hit;
            }

            if (WindowMayMatch(state, reqString, edge, reverse))
            {
                return edge;
            }

            // Past that character, in the direction of the search.
            if (descending)
            {
                high = reverse ? state.PrevPos(edge) : edge;
            }
            else
            {
                low = reverse ? edge : state.NextPos(edge);
            }
        }

        return _notFound;
    }

    /// <summary>
    /// The code units <see cref="Scan"/> searches for: every one that could hold a subject
    /// character able to take the required string's first character in reading order, under any
    /// rule <see cref="WindowMayMatch"/> applies.
    /// </summary>
    /// <remarks>
    /// For a case-insensitive string that is the closure of the first character under
    /// <see cref="Encodings.AllCases"/>, plus <see cref="Specials"/>:
    /// every character with a case whose full folding is longer than one character, or whose case
    /// data is not a plain equivalence class. Outside those, a character's cases share one
    /// single-character folding and each is in the others' case sets, so it can take the first
    /// required character only by being in that character's closure. An astral character
    /// contributes both its surrogates.
    /// </remarks>
    /// <param name="reqString">The required-string node.</param>
    /// <returns>The code units, or <see langword="null"/> when the screen does not apply.</returns>
    internal static SearchValues<char>? FirstUnits(Node reqString)
    {
        if (!Covers(reqString.Op) || reqString.Encoding == CaseEncoding.Locale || reqString.Values.Count == 0)
        {
            return null;
        }

        bool reverse = reqString.Op is Opcode.StringRev or Opcode.StringIgnRev or Opcode.StringFldRev;
        uint first = reqString.Values[reverse ? reqString.Values.Count - 1 : 0];
        var codepoints = new HashSet<uint> { first };

        if (reqString.Op != Opcode.StringRev)
        {
            Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
            var pending = new Stack<uint>();
            pending.Push(first);

            while (pending.Count > 0)
            {
                int count = Encodings.AllCases(reqString.Encoding, pending.Pop(), cases);
                for (int c = 0; c < count; c++)
                {
                    if (codepoints.Add(cases[c]))
                    {
                        pending.Push(cases[c]);
                    }
                }
            }
        }

        var units = new HashSet<char>(reqString.Op == Opcode.StringRev ? [] : _specialUnits);
        foreach (uint codepoint in codepoints)
        {
            if (codepoint is >= 0xD800 and <= 0xDFFF or > 0x10FFFF)
            {
                // An unpaired surrogate reads as itself.
                if (codepoint <= 0xDFFF)
                {
                    units.Add((char)codepoint);
                }

                continue;
            }

            foreach (char unit in char.ConvertFromUtf32((int)codepoint))
            {
                units.Add(unit);
            }
        }

        return SearchValues.Create([.. units]);
    }

    /// <summary>
    /// The special characters: every codepoint with a case <c>y</c> whose full folding is longer than
    /// one character, whose case set does not hold the codepoint back, whose folding is outside its
    /// case set, or whose folding's case set does not hold <c>y</c>. Derived from the case tables;
    /// <c>RequiredStringScreenTests</c> recomputes it over every codepoint and must be updated with
    /// the tables.
    /// </summary>
    internal static readonly uint[] Specials =
    [
        0x00DF,
        0x0130,
        0x0149,
        0x01F0,
        0x0390,
        0x03B0,
        0x0587,
        0x1E96,
        0x1E97,
        0x1E98,
        0x1E99,
        0x1E9A,
        0x1E9E,
        0x1F50,
        0x1F52,
        0x1F54,
        0x1F56,
        0x1F80,
        0x1F81,
        0x1F82,
        0x1F83,
        0x1F84,
        0x1F85,
        0x1F86,
        0x1F87,
        0x1F88,
        0x1F89,
        0x1F8A,
        0x1F8B,
        0x1F8C,
        0x1F8D,
        0x1F8E,
        0x1F8F,
        0x1F90,
        0x1F91,
        0x1F92,
        0x1F93,
        0x1F94,
        0x1F95,
        0x1F96,
        0x1F97,
        0x1F98,
        0x1F99,
        0x1F9A,
        0x1F9B,
        0x1F9C,
        0x1F9D,
        0x1F9E,
        0x1F9F,
        0x1FA0,
        0x1FA1,
        0x1FA2,
        0x1FA3,
        0x1FA4,
        0x1FA5,
        0x1FA6,
        0x1FA7,
        0x1FA8,
        0x1FA9,
        0x1FAA,
        0x1FAB,
        0x1FAC,
        0x1FAD,
        0x1FAE,
        0x1FAF,
        0x1FB2,
        0x1FB3,
        0x1FB4,
        0x1FB6,
        0x1FB7,
        0x1FBC,
        0x1FC2,
        0x1FC3,
        0x1FC4,
        0x1FC6,
        0x1FC7,
        0x1FCC,
        0x1FD2,
        0x1FD3,
        0x1FD6,
        0x1FD7,
        0x1FE2,
        0x1FE3,
        0x1FE4,
        0x1FE6,
        0x1FE7,
        0x1FF2,
        0x1FF3,
        0x1FF4,
        0x1FF6,
        0x1FF7,
        0x1FFC,
        0xFB00,
        0xFB01,
        0xFB02,
        0xFB03,
        0xFB04,
        0xFB05,
        0xFB06,
        0xFB13,
        0xFB14,
        0xFB15,
        0xFB16,
        0xFB17,
    ];

    /// <summary>The code units of <see cref="Specials"/>, which every case-insensitive screen searches for.</summary>
    private static readonly char[] _specialUnits =
    [
        .. Specials.SelectMany(static c => char.ConvertFromUtf32((int)c)).Distinct(),
    ];

    /// <summary>
    /// The same cancellation gate <c>Matcher.SimpleStringSearch</c> uses: one check per 256 steps.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <returns><see langword="true"/> when the timeout has passed or the token is cancelled.</returns>
    private static bool Poll(MatchState state)
    {
        state.Iterations = (ushort)(state.Iterations + 0x100);
        return state.Iterations == 0 && (state.CheckTimedOut() || state.Cancellation.IsCancellationRequested);
    }

    /// <summary>
    /// Whether the required string could occupy the text starting at <paramref name="edge"/>
    /// (forward) or ending at it (reverse), under any rule the matcher could apply.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="reqString">The required-string node.</param>
    /// <param name="edge">Where the window starts (forward) or ends (reverse).</param>
    /// <param name="reverse">Whether to read right to left.</param>
    /// <returns><see langword="true"/> if an occurrence there cannot be ruled out.</returns>
    private static bool WindowMayMatch(MatchState state, Node reqString, int edge, bool reverse)
    {
        List<uint> values = reqString.Values;
        int length = values.Count;
        bool exact = reqString.Op == Opcode.StringRev;
        int pos = edge;

        // The common case first: while both sides are ASCII (or the string is case-sensitive) only
        // the one-for-one rule can apply, so the walk is a plain comparison. Anything else goes to
        // the general walk, from the start of the window.
        for (int i = 0; i < length; i++)
        {
            if (reverse ? pos <= state.SliceStart : pos >= state.SliceEnd)
            {
                return false;
            }

            int charPos = reverse ? state.PrevPos(pos) : pos;
            uint ch = state.CharAt(charPos);
            uint want = values[reverse ? length - 1 - i : i];

            if (exact)
            {
                if (want != ch)
                {
                    return false;
                }
            }
            else if (ch >= 0x80 || want >= 0x80)
            {
                return GeneralWindowMayMatch(state, reqString, edge, reverse);
            }
            else if (!Matcher.SameCharIgn(reqString.Encoding, want, ch))
            {
                return false;
            }

            pos = reverse ? charPos : state.NextPos(pos);
        }

        return true;
    }

    /// <summary>
    /// <see cref="WindowMayMatch"/> for a case-insensitive string once a character outside ASCII
    /// is involved: every rule, tracked as the set of required characters consumed so far.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="reqString">The required-string node.</param>
    /// <param name="edge">Where the window starts (forward) or ends (reverse).</param>
    /// <param name="reverse">Whether to read right to left.</param>
    /// <returns><see langword="true"/> if an occurrence there cannot be ruled out.</returns>
    private static bool GeneralWindowMayMatch(MatchState state, Node reqString, int edge, bool reverse)
    {
        List<uint> values = reqString.Values;
        int length = values.Count;
        CaseEncoding encoding = reqString.Encoding;

        // 'reached[i]': the first 'i' required characters, in reading order, can be consumed.
        Span<bool> reached = length < 128 ? stackalloc bool[length + 1] : new bool[length + 1];
        Span<bool> next = length < 128 ? stackalloc bool[length + 1] : new bool[length + 1];
        Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
        Span<uint> folded = stackalloc uint[UnicodeTables.MaxCases * UnicodeTables.MaxFolded];
        Span<int> foldedLengths = stackalloc int[UnicodeTables.MaxCases];

        reached.Clear();
        reached[0] = true;
        bool first = true;
        int pos = edge;

        while (true)
        {
            if (reverse ? pos <= state.SliceStart : pos >= state.SliceEnd)
            {
                // Ran out of slice with required characters left.
                return false;
            }

            int charPos = reverse ? state.PrevPos(pos) : pos;
            uint ch = state.CharAt(charPos);
            next.Clear();
            bool any = false;
            int caseCount = -1;

            for (int i = 0; i < length; i++)
            {
                if (!reached[i])
                {
                    continue;
                }

                uint want = values[reverse ? length - 1 - i : i];

                // One subject character for one required character, ignoring case. Between two ASCII
                // characters that is the only rule that can apply, because every case of an ASCII
                // character fully folds to one character, its lowercase (RequiredStringScreenTests
                // pins that).
                if (Matcher.SameCharIgn(encoding, want, ch))
                {
                    next[i + 1] = true;
                    any = true;
                }

                if (ch < 0x80 && want < 0x80)
                {
                    continue;
                }

                // One subject character for the full folding of any of its cases. The first
                // character may contribute only the tail of its folding (reading order), and any
                // character may run past the end of the required string.
                if (caseCount < 0)
                {
                    caseCount = Encodings.AllCases(encoding, ch, cases);
                    for (int c = 0; c < caseCount; c++)
                    {
                        foldedLengths[c] = Encodings.FullCaseFold(
                            encoding,
                            cases[c],
                            folded.Slice(c * UnicodeTables.MaxFolded, UnicodeTables.MaxFolded)
                        );
                    }
                }

                for (int c = 0; c < caseCount; c++)
                {
                    ReadOnlySpan<uint> folding = folded.Slice(c * UnicodeTables.MaxFolded, foldedLengths[c]);
                    int lastSkip = first ? folding.Length - 1 : 0;

                    for (int skip = 0; skip <= lastSkip; skip++)
                    {
                        int consumed = FoldedRun(values, i, folding, skip, reverse, encoding);
                        if (consumed > 0)
                        {
                            next[i + consumed] = true;
                            any = true;
                        }
                    }
                }
            }

            if (next[length])
            {
                return true;
            }

            if (!any)
            {
                return false;
            }

            next.CopyTo(reached);
            first = false;
            pos = reverse ? charPos : state.NextPos(pos);
        }
    }

    /// <summary>
    /// How many required characters, from reading index <paramref name="start"/>, a subject
    /// character's folding spells after skipping <paramref name="skip"/> of its own characters, or 0
    /// if it does not. The folding may run past the end of the required string.
    /// </summary>
    /// <param name="values">The required characters, in text order.</param>
    /// <param name="start">How many required characters are already consumed, in reading order.</param>
    /// <param name="folded">The folding, in text order.</param>
    /// <param name="skip">How many folded characters to skip at the reading-order start.</param>
    /// <param name="reverse">Whether reading order is right to left.</param>
    /// <param name="encoding">The encoding in force.</param>
    /// <returns>The number of required characters consumed, or 0.</returns>
    private static int FoldedRun(
        List<uint> values,
        int start,
        ReadOnlySpan<uint> folded,
        int skip,
        bool reverse,
        CaseEncoding encoding
    )
    {
        int length = values.Count;
        int consumed = 0;

        for (int f = skip; f < folded.Length; f++)
        {
            int index = start + consumed;
            if (index >= length)
            {
                break;
            }

            uint want = values[reverse ? length - 1 - index : index];
            uint have = folded[reverse ? folded.Length - 1 - f : f];

            if (!Matcher.SameCharIgn(encoding, want, have))
            {
                return 0;
            }

            ++consumed;
        }

        return consumed;
    }
}
