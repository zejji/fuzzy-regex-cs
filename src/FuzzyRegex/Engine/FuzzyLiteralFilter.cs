using System.Runtime.InteropServices;
using System.Text;
using Fuzzy.Text.RegularExpressions.Parsing;
using Fuzzy.Text.RegularExpressions.Unicode;

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
/// <b>One exception: a lone character no ASCII character equals.</b> A fuzzy full-folded run holding
/// <c>ß</c> or a ligature reads it one character at a time on a second path (ledger entry 49), where
/// the <c>ß</c> is a <c>CHARACTER_IGN</c> node of its own. No ASCII character equals it ignoring
/// case, so an ASCII subject never holds it untouched and the piece search never finds a piece
/// holding it - which is also true of the match, so the argument above still holds on the pieces
/// that are left. The node must be one character from the Basic Multilingual Plane, so that one
/// literal value is still one code unit, and <see cref="Matcher.SameCharIgn"/> must pair it with
/// none of the 128 ASCII characters: KELVIN SIGN, which the engine pairs with <c>k</c>, is refused.
/// A string node keeps the ASCII rule, because its values are compared a folded character at a time.
/// </para>
/// <para>
/// sync-divergence: upstream's <c>basic_match</c> (<c>upstream/src/_regex.c:11767-11814</c>) runs
/// no prefilter at all under a fuzzy section / this type screens the start positions of a fuzzy
/// literal before each attempt / a fuzzy phrase search over short records otherwise tries every
/// position of every record. Re-aligning: nothing to follow unless upstream adds a fuzzy prefilter
/// of its own, or changes what a fuzzy section can match - an error that damages two characters at
/// once would break the one-edit-one-piece argument above. Ledger entry 49's reading is not one:
/// substituting the whole <c>ß</c> damages one character on the path that reads it as one, and
/// each path is a literal of its own.
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

    /// <summary>A remembered piece position meaning the piece is not in the rest of the slice.</summary>
    internal const int Absent = int.MaxValue;

    private FuzzyLiteralFilter(string[] pieces, int[] offsets, int maxErrors, bool ignoreCase, bool reverse)
    {
        Pieces = pieces;
        Offsets = offsets;
        MaxErrors = maxErrors;
        _comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        Reverse = reverse;
        _asciiPiece = [.. pieces.Select(static piece => Ascii.IsValid(piece))];
    }

    /// <summary>
    /// Per piece, whether it is ASCII. One that is not holds a lone character no ASCII character
    /// equals (see the class remarks), so it is never in the ASCII stretch the search accepts, and
    /// is not searched for: it would be searched to the end of the slice for nothing.
    /// </summary>
    private readonly bool[] _asciiPiece;

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
        if (!walk.Collect(fuzzy.Next1.Node, new LiteralPath()) || walk.Reverse is not bool isReverse)
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
        foreach (LiteralPath literal in walk.Literals)
        {
            // Cut at unit boundaries only: every value is a unit of its own except inside an
            // expanding character's folding, which a piece must hold whole (see ReadRun). With every
            // value a unit this is the plain j * n / (k + 1) cut.
            List<int> units = [.. Enumerable.Range(0, literal.Values.Count).Where(i => literal.UnitStarts[i])];
            if (units.Count < pieceCount)
            {
                return null;
            }

            string text = string.Concat(literal.Values.Select(static c => (char)c));
            for (int j = 0; j < pieceCount; j++)
            {
                int offset = units[j * units.Count / pieceCount];
                int end = j + 1 < pieceCount ? units[(j + 1) * units.Count / pieceCount] : text.Length;
                if (end - offset < MinPieceLength)
                {
                    return null;
                }

                string piece = text[offset..end];

                // One search per distinct piece: two paths through a branch can share one. Keeping
                // the larger offset moves the start bound earlier, which can only make the engine
                // try more positions.
                int seen = pieces.IndexOf(piece);
                if (seen >= 0)
                {
                    offsets[seen] = Math.Max(offsets[seen], offset);
                    continue;
                }

                offsets.Add(offset);
                pieces.Add(piece);
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
        internal List<LiteralPath> Literals { get; } = [];

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
        internal bool Collect(Node? node, LiteralPath values)
        {
            // Full case folding splits one literal into several nodes: '(?i)strasse' is STRING_FLD
            // 'st', STRING_IGN 'ra', STRING_FLD 'ss', STRING_IGN 'e', so that 'ß' and U+FB06 can
            // match. Over ASCII text every one of them compares a character at a time, so the chain
            // is one literal. If any node ignores case every piece is searched ignoring case, which
            // can only find more.
            while (node is not null && node.Op != Opcode.EndFuzzy)
            {
                bool nodeIsReverse;
                switch (node.Op)
                {
                    case Opcode.Branch when node.Next2.Node is not null && ReadRun(node) is { } run:
                    {
                        // Ledger entry 49's run and its one-character reading are one literal: the
                        // packed folding, cut only between characters. Enumerating the reading's
                        // choices instead gave 2^n + 1 literals, and at four expanding characters
                        // more pieces than MaxPieces, which switched the filter off.
                        if ((Reverse is bool runDirection && runDirection != run.Reverse) || !run.Seen)
                        {
                            return false;
                        }

                        IgnoreCase = true;
                        Reverse = run.Reverse;
                        values.AddUnits(run.Units, run.Reverse);

                        // The walk goes on where the reading rejoins the packed node.
                        node = run.Packed.Next1.Node;
                        continue;
                    }

                    case Opcode.Branch when node.Next2.Node is not null:
                        // Each branch adds a path, so a walk that ends with MaxPieces literals or fewer
                        // passes fewer than MaxPieces of them. Refusing at that count bounds the
                        // recursion: 8000 in a row overflowed the stack.
                        if (++_branches >= MaxPieces)
                        {
                            return false;
                        }

                        // Both arms continue to the same END_FUZZY; each gets its own copy.
                        return Collect(node.Next1.Node, values.Copy()) && Collect(node.Next2.Node, values);
                    case Opcode.Branch:
                        node = node.Next1.Node;
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

                if ((Reverse is bool direction && direction != nodeIsReverse) || !TheSearchSeesAsTheEngineDoes(node))
                {
                    return false;
                }

                // A reverse chain is walked from the literal's end, though each node holds its own
                // characters in reading order, so a reverse node goes in front of the ones before it.
                Reverse = nodeIsReverse;
                values.AddUnits([.. node.Values.Select(static v => new[] { v })], nodeIsReverse);
                node = node.Next1.Node;
            }

            if (node?.Next1.Node is not { Op: Opcode.Success } || Literals.Count >= MaxPieces)
            {
                return false;
            }

            Literals.Add(values);
            return true;
        }

        /// <summary>
        /// Recognises ledger entry 49's compile of a fuzzy full-folded run - a branch whose first
        /// arm is the packed <c>STRING_FLD</c> and whose second reads the same run one character at
        /// a time - and returns the run as units, one per pattern character, each holding that
        /// character's folding. Anything else returns <see langword="null"/> and is walked as an
        /// ordinary branch.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Why one literal cut between characters is sound for both arms.</b> Take the packed
        /// folding, and cut it into <c>k + 1</c> pieces only where one pattern character ends and
        /// the next begins. On the packed arm an edit changes one folded letter, which lies in one
        /// piece; that arm is one literal and any cut serves it. On the reading arm each expanding
        /// character is either the character alone or its folding: an edit to the character alone
        /// changes one unit, an edit inside its folding changes one letter of one unit, and a unit
        /// lies in one piece because no cut splits it. An insertion damages at most one piece on
        /// either arm. So any match with at most <c>k</c> edits leaves one piece untouched.
        /// </para>
        /// <para>
        /// That piece is found by the search. The filter only answers over ASCII text, where no
        /// subject character's folding spans two units, and where the character alone cannot match
        /// untouched: <see cref="TheSearchSeesAsTheEngineDoes"/> requires that no ASCII character
        /// equals it. So every expanding character in the untouched piece was matched through its
        /// folding, and the text there is the piece's own folded text. The start bound holds too:
        /// a piece's folded offset is at least the number of pattern characters in front of it on
        /// any reading, each of which reads at most one subject character.
        /// </para>
        /// <para>
        /// The recognition is checked, not assumed: the units joined must equal the packed node's
        /// values exactly, and each character's own branch must be the character against its folding.
        /// </para>
        /// </remarks>
        /// <param name="branch">A branch node with two arms.</param>
        /// <returns>The run, or <see langword="null"/>.</returns>
        private static Run? ReadRun(Node branch)
        {
            if (branch.Next1.Node is not { Op: Opcode.StringFld or Opcode.StringFldRev } packed)
            {
                return null;
            }

            bool reverse = packed.Op == Opcode.StringFldRev;
            Node? after = packed.Next1.Node;
            var units = new List<uint[]>();
            bool seen = TheSearchSeesAsTheEngineDoes(packed);
            Node? cur = branch.Next2.Node;
            Span<uint> folded = stackalloc uint[UnicodeTables.MaxFolded];

            // Each step reads at least one unit, and there are no more units than packed values.
            int steps = 0;
            while (cur != after)
            {
                if (cur is null || ++steps > 2 * packed.Values.Count + 2)
                {
                    return null;
                }

                List<uint[]> read = [];
                switch (cur.Op)
                {
                    case Opcode.String or Opcode.StringIgn or Opcode.StringFld when !reverse:
                    case Opcode.StringRev or Opcode.StringIgnRev or Opcode.StringFldRev when reverse:
                        read.AddRange(cur.Values.Select(static v => new[] { v }));
                        seen &= TheSearchSeesAsTheEngineDoes(cur);
                        cur = cur.Next1.Node;
                        break;
                    case Opcode.Character or Opcode.CharacterIgn when !reverse && cur.Match && cur.Step != 0:
                    case Opcode.CharacterRev or Opcode.CharacterIgnRev when reverse && cur.Match && cur.Step != 0:
                        read.Add(folded[..Encodings.FullCaseFold(cur.Encoding, cur.Values[0], folded)].ToArray());
                        seen &= TheSearchSeesAsTheEngineDoes(cur);
                        cur = cur.Next1.Node;
                        break;
                    case Opcode.Branch when cur.Next2.Node is not null:
                    {
                        // One character's choice of itself or its folding (Character.CompileCore).
                        (Node? one, Node? fold) = cur.Next1.Node is { Op: Opcode.StringFld or Opcode.StringFldRev }
                            ? (cur.Next2.Node, cur.Next1.Node)
                            : (cur.Next1.Node, cur.Next2.Node);
                        if (
                            one is not { Op: Opcode.CharacterIgn or Opcode.CharacterIgnRev, Match: true }
                            || fold is not { Op: Opcode.StringFld or Opcode.StringFldRev }
                            || one.Next1.Node != fold.Next1.Node
                            || !folded[..Encodings.FullCaseFold(one.Encoding, one.Values[0], folded)]
                                .SequenceEqual(fold.Values.ToArray())
                        )
                        {
                            return null;
                        }

                        read.Add([.. fold.Values]);
                        seen &= TheSearchSeesAsTheEngineDoes(one) && TheSearchSeesAsTheEngineDoes(fold);
                        cur = one.Next1.Node;
                        break;
                    }

                    case Opcode.Branch:
                        cur = cur.Next1.Node;
                        break;
                    default:
                        return null;
                }

                // A reverse chain is walked from the run's end, so each node's units go in front.
                units.InsertRange(reverse ? 0 : units.Count, read);
            }

            if (!units.SelectMany(static u => u).SequenceEqual(packed.Values))
            {
                return null;
            }

            return new Run(packed, units, reverse, seen);
        }

        /// <summary>
        /// Whether an ordinal search over ASCII text answers for this node as the engine does: every
        /// value is ASCII, or the node is one character from the Basic Multilingual Plane that no
        /// ASCII character equals, so neither the engine nor the search finds it there. See the
        /// class remarks.
        /// </summary>
        /// <param name="node">A string or character node on the path.</param>
        /// <returns>Whether the node can be part of a literal.</returns>
        private static bool TheSearchSeesAsTheEngineDoes(Node node)
        {
            bool character =
                node.Op is Opcode.Character or Opcode.CharacterIgn or Opcode.CharacterRev or Opcode.CharacterIgnRev;
            bool ignoreCase = node.Op is Opcode.CharacterIgn or Opcode.CharacterIgnRev;

            foreach (uint c in node.Values)
            {
                if (c <= 0x7F)
                {
                    continue;
                }

                if (!character || c > 0xFFFF)
                {
                    return false;
                }

                for (uint ascii = 0; ignoreCase && ascii <= 0x7F; ascii++)
                {
                    if (Matcher.SameCharIgn(node.Encoding, c, ascii))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>A recognised ledger-49 run: see <c>LiteralWalk.ReadRun</c>.</summary>
    /// <param name="Packed">The packed <c>STRING_FLD</c> node, whose successor the walk goes on from.</param>
    /// <param name="Units">One folding per pattern character, in reading order.</param>
    /// <param name="Reverse">Whether the run is the reverse kind.</param>
    /// <param name="Seen">Whether every node of both arms passes <c>TheSearchSeesAsTheEngineDoes</c>.</param>
    private sealed record Run(Node Packed, List<uint[]> Units, bool Reverse, bool Seen);

    /// <summary>
    /// One literal: its values in reading order, and for each value whether a piece may start
    /// there. Only the first value of an expanding character's folding may start one.
    /// </summary>
    private sealed class LiteralPath
    {
        /// <summary>The values, in reading order.</summary>
        internal List<uint> Values { get; } = [];

        /// <summary>Per value, whether a cut may fall in front of it.</summary>
        internal List<bool> UnitStarts { get; } = [];

        /// <summary>A copy, for the other arm of a branch.</summary>
        /// <returns>The copy.</returns>
        internal LiteralPath Copy()
        {
            var copy = new LiteralPath();
            copy.Values.AddRange(Values);
            copy.UnitStarts.AddRange(UnitStarts);
            return copy;
        }

        /// <summary>Adds units at the end, or at the front for a reverse chain.</summary>
        /// <param name="units">The units, in reading order.</param>
        /// <param name="front">Whether they go in front of what is already here.</param>
        internal void AddUnits(List<uint[]> units, bool front)
        {
            List<uint> values = [.. units.SelectMany(static u => u)];
            List<bool> starts = [.. units.SelectMany(static u => u.Select(static (_, i) => i == 0))];
            Values.InsertRange(front ? 0 : Values.Count, values);
            UnitStarts.InsertRange(front ? 0 : UnitStarts.Count, starts);
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
    /// this call had to search is not all ASCII. The answer depends only on the subject and the two
    /// positions; <paramref name="memory"/> only saves searching again.
    /// </summary>
    /// <param name="text">The whole subject.</param>
    /// <param name="textPos">Where the next attempt would start.</param>
    /// <param name="sliceEnd">The end of the slice; no piece is searched for beyond it.</param>
    /// <param name="memory">What this scan has learned so far; see <see cref="ScanMemory"/>.</param>
    /// <returns>A position, <see cref="NoMatch"/> or <see cref="CannotTell"/>.</returns>
    internal int NextStart(ReadOnlySpan<char> text, int textPos, int sliceEnd, ScanMemory memory)
    {
        // A piece remembered as absent was searched for up to the old slice end only.
        if (memory.SliceEnd != sliceEnd)
        {
            memory.From.AsSpan().Fill(int.MaxValue);
            memory.SliceEnd = sliceEnd;
        }

        long start = long.MaxValue;
        for (int j = 0; j < Pieces.Length; j++)
        {
            string piece = Pieces[j];

            // A search from From[j] that found the piece at Found[j] answers for every position
            // between the two, since nothing in that stretch is an earlier occurrence. Outside it -
            // the scan has moved past the occurrence, or back before the search began - search again.
            if (textPos < memory.From[j] || textPos > memory.Found[j])
            {
                int at =
                    textPos < sliceEnd && _asciiPiece[j]
                        ? text[textPos..sliceEnd].IndexOf(piece.AsSpan(), _comparison)
                        : -1;
                memory.From[j] = textPos;
                memory.Found[j] = at < 0 ? Absent : textPos + at;
            }

            // Everything this piece's search looked at must be ASCII, or its answer is not the
            // engine's. The found piece's own characters are part of that.
            int searched = memory.Found[j] == Absent ? sliceEnd : memory.Found[j] + piece.Length;
            if (searched > textPos && !memory.IsAscii(text, textPos, searched))
            {
                return CannotTell;
            }

            if (memory.Found[j] != Absent)
            {
                start = Math.Min(start, (long)memory.Found[j] - Offsets[j] - MaxErrors);
            }
        }

        return start == long.MaxValue ? NoMatch : (int)Math.Max(start, textPos);
    }

    /// <summary>
    /// Whether a reverse match could lie anywhere in <c>[sliceStart, textPos)</c>: <see langword="false"/>
    /// only when that stretch is all ASCII and holds no piece. As with <see cref="NextStart"/>, the
    /// answer depends only on the subject and the two positions.
    /// </summary>
    /// <param name="text">The whole subject.</param>
    /// <param name="sliceStart">The start of the slice.</param>
    /// <param name="textPos">Where the reverse search starts.</param>
    /// <param name="memory">What this scan has learned so far; see <see cref="ScanMemory"/>.</param>
    /// <returns>Whether the engine must search at all.</returns>
    internal bool MayMatchBefore(ReadOnlySpan<char> text, int sliceStart, int textPos, ScanMemory memory)
    {
        if (textPos <= sliceStart)
        {
            return true;
        }

        // Both facts below are about [sliceStart, x), so they hold for one slice start only.
        if (memory.SliceStart != sliceStart)
        {
            memory.AbsentBelow.AsSpan().Fill(int.MinValue);
            memory.WitnessEnd = int.MaxValue;
            memory.SliceStart = sliceStart;
        }

        // A piece or a non-ASCII character already seen inside the stretch settles it.
        if (textPos >= memory.WitnessEnd)
        {
            return true;
        }

        ReadOnlySpan<char> stretch = text[sliceStart..textPos];
        for (int j = 0; j < Pieces.Length; j++)
        {
            // Not in [sliceStart, AbsentBelow[j]), so not in any shorter stretch either.
            if (textPos <= memory.AbsentBelow[j])
            {
                continue;
            }

            // The last occurrence, so that the search reads back only as far as it must.
            int at = stretch.LastIndexOf(Pieces[j].AsSpan(), _comparison);
            if (at >= 0)
            {
                memory.WitnessEnd = sliceStart + at + Pieces[j].Length;
                return true;
            }

            memory.AbsentBelow[j] = textPos;
        }

        if (!memory.IsAscii(text, sliceStart, textPos))
        {
            memory.WitnessEnd = memory.AsciiEnd + 1;
            return true;
        }

        return false;
    }

    /// <summary>A fresh <see cref="ScanMemory"/> sized for this filter's pieces.</summary>
    /// <returns>The memory, knowing nothing yet.</returns>
    internal ScanMemory NewScanMemory() => new(Pieces.Length);

    /// <summary>
    /// What the filter has learned about one scan's subject, kept across every step of the scan so
    /// that no step searches again what an earlier one searched. D14: the memory used to live for
    /// one step, so a <c>Matches</c> walk searched a piece that never occurs to the end of the
    /// subject on every step, and a reverse walk re-read the whole subject before it on every step.
    /// That was quadratic: 2.3 s over a million characters (measured 2026-09-28).
    /// </summary>
    /// <remarks>
    /// One per <see cref="MatchState"/>, which serves one scan on one thread at a time, and cleared
    /// by <see cref="Reset"/> whenever the state starts a scan of a new subject. Every fact is kept
    /// with the range it is true over and checked against each query, so it stays right when the
    /// scan moves forward, when <c>(?r)</c> moves it backward, and when <c>BESTMATCH</c> goes back to
    /// an earlier position or narrows the slice.
    /// </remarks>
    internal sealed class ScanMemory
    {
        /// <summary>Makes a memory for <paramref name="pieces"/> pieces.</summary>
        /// <param name="pieces">How many pieces the filter has.</param>
        internal ScanMemory(int pieces)
        {
            From = new int[pieces];
            Found = new int[pieces];
            AbsentBelow = new int[pieces];
            Reset();
        }

        /// <summary>Forward: where each piece's last search began; <c>int.MaxValue</c> for never.</summary>
        internal int[] From { get; }

        /// <summary>
        /// Forward: where each piece's last search found it, or <see cref="Absent"/> for nowhere
        /// before <see cref="SliceEnd"/>.
        /// </summary>
        internal int[] Found { get; }

        /// <summary>Forward: the slice end <see cref="Found"/> was searched up to.</summary>
        internal int SliceEnd { get; set; }

        /// <summary>
        /// Reverse: per piece, a position <c>x</c> such that the piece is not in
        /// <c>[SliceStart, x)</c>; <c>int.MinValue</c> for unknown.
        /// </summary>
        internal int[] AbsentBelow { get; }

        /// <summary>
        /// Reverse: a position <c>x</c> such that <c>[SliceStart, x)</c> holds a piece or a
        /// character that is not ASCII; <c>int.MaxValue</c> for none known.
        /// </summary>
        internal int WitnessEnd { get; set; }

        /// <summary>Reverse: the slice start <see cref="AbsentBelow"/> and <see cref="WitnessEnd"/> are about.</summary>
        internal int SliceStart { get; set; }

        /// <summary>Both directions: <c>[AsciiFrom, AsciiEnd)</c> is known to be all ASCII.</summary>
        internal int AsciiFrom { get; private set; }

        /// <summary>
        /// The end of the stretch known to be ASCII. When a check stopped at a character that is
        /// not ASCII, this is that character.
        /// </summary>
        internal int AsciiEnd { get; private set; }

        /// <summary>Forgets everything, for a scan of a new subject.</summary>
        internal void Reset()
        {
            From.AsSpan().Fill(int.MaxValue);
            Found.AsSpan().Fill(Absent);
            SliceEnd = -1;
            AbsentBelow.AsSpan().Fill(int.MinValue);
            WitnessEnd = int.MaxValue;
            SliceStart = -1;
            AsciiFrom = 0;
            AsciiEnd = 0;
        }

        /// <summary>
        /// Whether <c>[from, to)</c> is all ASCII, reading only what the known stretch does not
        /// already cover. On <see langword="false"/>, <see cref="AsciiEnd"/> is the first character
        /// at or after <paramref name="from"/> that is not ASCII.
        /// </summary>
        /// <param name="text">The whole subject.</param>
        /// <param name="from">The start of the stretch.</param>
        /// <param name="to">Its end, at least <paramref name="from"/>.</param>
        /// <returns>Whether the stretch is all ASCII.</returns>
        internal bool IsAscii(ReadOnlySpan<char> text, int from, int to)
        {
            // The known stretch must reach back to 'from' without a gap, or it says nothing here.
            if (from < AsciiFrom || from > AsciiEnd)
            {
                AsciiFrom = from;
                AsciiEnd = from;
            }

            if (to <= AsciiEnd)
            {
                return true;
            }

            // The ushort form: the char form of the except-in-range searches boxes its bounds, 96 B
            // a call on .NET 10 (probe, 2026-09-23), and AllocationTests catch it.
            int other = MemoryMarshal
                .Cast<char, ushort>(text[AsciiEnd..to])
                .IndexOfAnyExceptInRange((ushort)0, (ushort)0x7F);
            AsciiEnd = other < 0 ? to : AsciiEnd + other;
            return other < 0;
        }
    }
}
