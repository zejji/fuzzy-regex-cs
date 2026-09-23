using System.Text;
using Fuzzy.Text.RegularExpressions.Parsing;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// A reject-only search prefilter for a pattern that is one fuzzy literal, such as
/// <c>(?:amber lantern works){e&lt;=2}</c>, or one fuzzy alternation of literals, such as
/// <c>(?:amber lantern works|copper field studio){e&lt;=2}</c> or <c>(?:\L&lt;phrases&gt;){e&lt;=2}</c>.
/// <b>This port's own: upstream has no equivalent</b>, and under a fuzzy section it has no
/// prefilter at all, because an error can delete any character a required-string search would
/// look for. S60b item 10.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is sound.</b> The literal is cut into <c>k + 1</c> disjoint pieces, where <c>k</c> is
/// the most errors the section's constraints allow. One substitution, insertion or deletion damages
/// at most one piece, so any match leaves at least one piece standing, character for character, in
/// the text it matched (Navarro, <i>A Guided Tour to Approximate String Matching</i>, ACM Computing
/// Surveys 33(1), 2001, section 8.1: "a single edit operation cannot alter both halves of the
/// pattern"). A match starting at <c>p</c> reads at most <c>offset(j) + k</c> characters before its
/// untouched piece <c>j</c>, so <c>p</c> is at least that piece's first occurrence from the search
/// position minus both. A subject holding no piece at all holds no match.
/// </para>
/// <para>
/// <b>An alternation is several literals.</b> A match of <c>(?:a|b){e&lt;=k}</c> is <c>a</c> or
/// <c>b</c> with at most <c>k</c> errors, so it holds an untouched piece of the branch it matched.
/// Each literal is cut into its own <c>k + 1</c> pieces, the start bound is the least over every
/// piece of every literal, and a subject is refused only when no literal has a piece in it.
/// </para>
/// <para>
/// <b>It only skips.</b> The engine makes every attempt it would have made at every position the
/// bound leaves, and the attempts are independent because the pattern holds nothing but the
/// literal - no verb, no group, no anchor. Reverse, <c>BESTMATCH</c> and <c>ENHANCEMATCH</c> choose
/// among matches, and every candidate holds an untouched piece, so they keep the filter. A partial
/// match does not: it can be a prefix of the literal holding no whole piece, so
/// <c>Matcher.BasicMatch</c> withholds the filter from partial searches.
/// </para>
/// <para>
/// <b>ASCII only, on both sides.</b> Case-insensitive matching pairs some ASCII letters with
/// characters that are not ASCII - <c>k</c> with KELVIN SIGN U+212A, <c>s</c> with LONG S U+017F,
/// and under full case folding <c>ss</c> with U+00DF and <c>fi</c> with U+FB01 - and an ordinal
/// case-insensitive search sees none of those. Between two ASCII strings it is exact: every fold
/// the engine applies to an ASCII letter lands on the same letter or its other case, and a Turkic
/// dotless or dotted I only narrows what matches. So the literal must be ASCII, and every stretch of
/// subject the filter searches is checked for ASCII first; the first non-ASCII character switches
/// the filter off for the rest of that search. ASCII also makes code units and characters the same
/// count, so the offsets need no walk.
/// </para>
/// <para>
/// sync-divergence: upstream's <c>basic_match</c> (<c>upstream/src/_regex.c:11767-11814</c>) runs
/// no prefilter at all under a fuzzy section / this type screens the start positions of a fuzzy
/// literal before each attempt / a fuzzy phrase search over short records otherwise tries every
/// position of every record. Re-aligning: nothing to follow unless upstream adds a fuzzy prefilter
/// of its own, or changes what a fuzzy section can match - an error that damages two characters at
/// once would break the one-edit-one-piece argument above.
/// </para>
/// </remarks>
internal sealed class FuzzyLiteralFilter
{
    /// <summary>
    /// The most pieces the filter searches for, over all its literals, so the per-search cache is a
    /// small fixed <c>stackalloc</c>. One literal may have <c>k</c> up to 31; three may have 9.
    /// </summary>
    // SHORTCUT: each piece is its own IndexOf, cached between attempts. A long named list would want
    // one SearchValues<string> pass over every piece instead, with a bound that no longer knows
    // which piece it found; the ceiling is 32 pieces, the upgrade is that pass, behind a benchmark.
    internal const int MaxPieces = 32;

    /// <summary>
    /// The shortest piece worth searching for. Shorter pieces occur almost everywhere, so the filter
    /// would pay its searches and skip nothing.
    /// </summary>
    // SHORTCUT: the floor is judged, not measured - the only workload measured is ManyInputs'
    // FuzzyPhrase rows, whose pieces are 6-7 characters long. The upgrade is a sweep of piece length
    // against a no-match subject, keeping the shortest length that still wins.
    internal const int MinPieceLength = 3;

    /// <summary>Returned by <see cref="NextStart"/> when the subject holds no piece.</summary>
    internal const int NoMatch = -1;

    /// <summary>Returned by <see cref="NextStart"/> when the stretch it searched is not ASCII.</summary>
    internal const int CannotTell = -2;

    /// <summary>A cached piece position the next search must refresh.</summary>
    internal const int Unknown = -1;

    /// <summary>A cached piece position meaning the piece is not in the rest of the slice.</summary>
    internal const int Absent = int.MaxValue;

    private FuzzyLiteralFilter(string[] pieces, int[] offsets, int maxErrors, bool ignoreCase, bool reverse)
    {
        Pieces = pieces;
        Offsets = offsets;
        MaxErrors = maxErrors;
        _comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        Reverse = reverse;
    }

    /// <summary>The <c>k + 1</c> pieces, in the literal's order.</summary>
    internal string[] Pieces { get; }

    /// <summary>Where each piece starts in the literal.</summary>
    internal int[] Offsets { get; }

    /// <summary><c>k</c>: the most errors the section's constraints allow.</summary>
    internal int MaxErrors { get; }

    /// <summary>
    /// Whether the literal is matched backwards. The reverse search uses the filter only to refuse
    /// a subject holding no piece; it does not move the start.
    /// </summary>
    // SHORTCUT: a reverse search could jump as the forward one does, mirrored - the last occurrence
    // of each piece plus the literal's length after it plus k. Refusing is enough for short records;
    // the mirror is the upgrade when a long reverse fuzzy search is measured.
    internal bool Reverse { get; }

    private readonly StringComparison _comparison;

    /// <summary>
    /// Builds the filter for a compiled pattern, or returns <see langword="null"/> when the pattern
    /// is not exactly one fuzzy section holding one ASCII literal with a small enough error budget.
    /// </summary>
    /// <param name="pattern">The compiled pattern, after optimisation.</param>
    /// <returns>The filter, or <see langword="null"/>.</returns>
    internal static FuzzyLiteralFilter? TryCreate(PatternObject pattern)
    {
        // The shape: FUZZY -> string nodes and branches -> END_FUZZY -> SUCCESS, and nothing else.
        // A test node on FUZZY is an insertion class ('{e<=1:[a-z]}'); it only narrows, but the
        // shape is kept exact so the argument in the remarks is the whole argument.
        if (pattern.StartNode is not { Op: Opcode.Fuzzy } fuzzy || fuzzy.Next2.Node is not null)
        {
            return null;
        }

        var walk = new LiteralWalk();
        if (!walk.Collect(fuzzy.Next1.Node, []) || walk.Reverse is not bool isReverse)
        {
            return null;
        }

        long maxErrors = MostErrors(fuzzy.Values);
        int pieceCount = (int)Math.Min(maxErrors + 1, int.MaxValue);
        if (maxErrors < 0 || (long)pieceCount * walk.Literals.Count > MaxPieces)
        {
            return null;
        }

        var pieces = new List<string>();
        var offsets = new List<int>();
        foreach (List<uint> values in walk.Literals)
        {
            if (values.Count / pieceCount < MinPieceLength || values.Any(static c => c > 0x7F))
            {
                return null;
            }

            string text = string.Concat(values.Select(static c => (char)c));
            for (int j = 0; j < pieceCount; j++)
            {
                int offset = j * text.Length / pieceCount;
                offsets.Add(offset);
                pieces.Add(text[offset..((j + 1) * text.Length / pieceCount)]);
            }
        }

        return new FuzzyLiteralFilter([.. pieces], [.. offsets], (int)maxErrors, walk.IgnoreCase, isReverse);
    }

    /// <summary>
    /// Reads the body of a fuzzy section as the literals it can match: one for a string, one per
    /// path through a branch. <c>(?:amber (?:lantern|stone) works)</c> is two literals, and so is
    /// <c>(?:amber lantern works|amber stone archive)</c> after the optimiser has moved the common
    /// <c>amber </c> out in front of the branch.
    /// </summary>
    private sealed class LiteralWalk
    {
        private int _branches;

        /// <summary>The literals found so far, each as the characters it reads, in reading order.</summary>
        internal List<List<uint>> Literals { get; } = [];

        /// <summary>Whether any string node ignores case.</summary>
        internal bool IgnoreCase { get; private set; }

        /// <summary>Whether the string nodes are the reverse kind; unset until one is seen.</summary>
        internal bool? Reverse { get; private set; }

        /// <summary>
        /// Walks from <paramref name="node"/> to <c>END_FUZZY</c>, adding a literal per path. Returns
        /// <see langword="false"/> at anything but a string or a branch, at an end that is not
        /// followed by <c>SUCCESS</c>, or once there are more literals than the filter has pieces.
        /// </summary>
        /// <param name="node">Where this path continues.</param>
        /// <param name="values">The characters this path has read so far; the walk owns it.</param>
        /// <returns>Whether the body is literals and nothing else.</returns>
        internal bool Collect(Node? node, List<uint> values)
        {
            // Full case folding splits one literal into several nodes: '(?i)strasse' is STRING_FLD
            // 'st', STRING_IGN 'ra', STRING_FLD 'ss', STRING_IGN 'e', so that 'ß' and U+FB06 can
            // match. Over ASCII text every one of them compares a character at a time, so the chain
            // is one literal. If any node ignores case every piece is searched ignoring case, which
            // can only find more.
            for (; node is not null && node.Op != Opcode.EndFuzzy; node = node.Next1.Node)
            {
                bool nodeIsReverse;
                switch (node.Op)
                {
                    case Opcode.Branch when node.Next2.Node is not null:
                        // Each branch adds a path, so a walk that ends with MaxPieces literals or fewer
                        // passes fewer than MaxPieces of them. Refusing at that count bounds the
                        // recursion: 8000 in a row overflowed the stack.
                        if (++_branches >= MaxPieces)
                        {
                            return false;
                        }

                        // Both arms continue to the same END_FUZZY; each gets its own copy.
                        return Collect(node.Next1.Node, [.. values]) && Collect(node.Next2.Node, values);
                    case Opcode.Branch:
                        continue;
                    // One character is a CHARACTER node, not a STRING: the 'e' that '(?r)(?:stone
                    // fine|oak strasse)' moves out as a common suffix. A negated one is a class, and
                    // a zero-width one is a check that reads nothing.
                    case Opcode.Character when node.Match && node.Step != 0:
                    case Opcode.String:
                        nodeIsReverse = false;
                        break;
                    case Opcode.CharacterIgn when node.Match && node.Step != 0:
                    case Opcode.StringIgn:
                    case Opcode.StringFld:
                        IgnoreCase = true;
                        nodeIsReverse = false;
                        break;
                    case Opcode.CharacterRev when node.Match && node.Step != 0:
                    case Opcode.StringRev:
                        nodeIsReverse = true;
                        break;
                    case Opcode.CharacterIgnRev when node.Match && node.Step != 0:
                    case Opcode.StringIgnRev:
                    case Opcode.StringFldRev:
                        IgnoreCase = true;
                        nodeIsReverse = true;
                        break;
                    default:
                        return false;
                }

                if (Reverse is bool direction && direction != nodeIsReverse)
                {
                    return false;
                }

                // A reverse chain is walked from the literal's end, though each node holds its own
                // characters in reading order, so a reverse node goes in front of the ones before it.
                Reverse = nodeIsReverse;
                values.InsertRange(nodeIsReverse ? 0 : values.Count, node.Values);
            }

            if (node?.Next1.Node is not { Op: Opcode.Success } || Literals.Count >= MaxPieces)
            {
                return false;
            }

            Literals.Add(values);
            return true;
        }
    }

    /// <summary>
    /// The most errors a section's constraints allow, or -1 when nothing bounds them. The tightest
    /// of three bounds, each enforced on every error by <c>Matcher.ThisErrorPermitted</c> and
    /// <c>Matcher.InsertionPermitted</c>: the total limit, the sum of the per-kind limits, and the
    /// cost limit divided by the cheapest error a kind the section allows can cost.
    /// </summary>
    /// <param name="values">The <c>FUZZY</c> node's values.</param>
    /// <returns><c>k</c>, or -1.</returns>
    private static long MostErrors(List<uint> values)
    {
        long bound = values[FuzzyValue.MaxErr] >= RegexFlags.Unlimited ? long.MaxValue : values[FuzzyValue.MaxErr];

        long perKind = 0;
        long cheapest = long.MaxValue;
        for (int type = 0; type < FuzzyValue.Count; type++)
        {
            long max = values[FuzzyValue.MaxBase + type];
            if (max == 0)
            {
                continue;
            }

            perKind = max >= RegexFlags.Unlimited || perKind == long.MaxValue ? long.MaxValue : perKind + max;
            cheapest = Math.Min(cheapest, values[FuzzyValue.CostBase + type]);
        }

        bound = Math.Min(bound, perKind);
        if (cheapest > 0 && cheapest != long.MaxValue && values[FuzzyValue.MaxCost] < RegexFlags.Unlimited)
        {
            bound = Math.Min(bound, values[FuzzyValue.MaxCost] / cheapest);
        }

        return bound == long.MaxValue ? -1 : bound;
    }

    /// <summary>
    /// The earliest position at or after <paramref name="textPos"/> where a forward match could
    /// start, or <see cref="NoMatch"/> when none can, or <see cref="CannotTell"/> when the stretch
    /// this call had to search is not all ASCII.
    /// </summary>
    /// <param name="text">The whole subject.</param>
    /// <param name="textPos">Where the next attempt would start.</param>
    /// <param name="sliceEnd">The end of the slice; no piece is searched for beyond it.</param>
    /// <param name="found">
    /// Per piece, where it was last found, carried between the attempts of one search. Starts as
    /// <see cref="Unknown"/>; a position behind <paramref name="textPos"/> is searched again.
    /// </param>
    /// <param name="asciiEnd">
    /// How far past the search's first position the subject is known to be ASCII, carried with
    /// <paramref name="found"/>. Starts at the search's first position.
    /// </param>
    /// <returns>A position, <see cref="NoMatch"/> or <see cref="CannotTell"/>.</returns>
    internal int NextStart(ReadOnlySpan<char> text, int textPos, int sliceEnd, Span<int> found, ref int asciiEnd)
    {
        long start = long.MaxValue;
        for (int j = 0; j < Pieces.Length; j++)
        {
            string piece = Pieces[j];
            if (found[j] < textPos)
            {
                int at = textPos < sliceEnd ? text[textPos..sliceEnd].IndexOf(piece.AsSpan(), _comparison) : -1;
                found[j] = at < 0 ? Absent : textPos + at;
            }

            // Everything this piece's search looked at must be ASCII, or its answer is not the
            // engine's. The found piece's own characters are part of that.
            int searched = found[j] == Absent ? sliceEnd : found[j] + piece.Length;
            if (searched > asciiEnd)
            {
                int from = Math.Max(asciiEnd, textPos);
                // Ascii.IsValid, not ContainsAnyExceptInRange: that boxes its bounds, 96 B a
                // call on .NET 10 (probe, 2026-09-23), and AllocationTests catch it.
                if (!Ascii.IsValid(text[from..searched]))
                {
                    return CannotTell;
                }

                asciiEnd = searched;
            }

            if (found[j] != Absent)
            {
                start = Math.Min(start, (long)found[j] - Offsets[j] - MaxErrors);
            }
        }

        return start == long.MaxValue ? NoMatch : (int)Math.Max(start, textPos);
    }

    /// <summary>
    /// Whether a reverse match could lie anywhere in <c>[sliceStart, textPos)</c>: <see langword="false"/>
    /// only when that stretch is all ASCII and holds no piece.
    /// </summary>
    /// <param name="text">The whole subject.</param>
    /// <param name="sliceStart">The start of the slice.</param>
    /// <param name="textPos">Where the reverse search starts.</param>
    /// <returns>Whether the engine must search at all.</returns>
    internal bool MayMatchBefore(ReadOnlySpan<char> text, int sliceStart, int textPos)
    {
        if (textPos <= sliceStart)
        {
            return true;
        }

        ReadOnlySpan<char> stretch = text[sliceStart..textPos];
        if (!Ascii.IsValid(stretch))
        {
            return true;
        }

        foreach (string piece in Pieces)
        {
            if (stretch.Contains(piece.AsSpan(), _comparison))
            {
                return true;
            }
        }

        return false;
    }
}
