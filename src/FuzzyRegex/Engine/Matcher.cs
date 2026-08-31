using Fuzzy.Text.RegularExpressions.Parsing;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// The status codes the matcher passes around. Port of the <c>RE_ERROR_*</c> defines
/// (<c>upstream/src/_regex.c</c> lines 103-122), reduced to the ones this port can produce: the rest
/// describe CPython argument errors that our own signatures make unrepresentable.
/// </summary>
internal static class MatchStatus
{
    /// <summary>Upstream <c>RE_ERROR_SUCCESS</c>.</summary>
    internal const int Success = 1;

    /// <summary>Upstream <c>RE_ERROR_FAILURE</c>.</summary>
    internal const int Failure = 0;

    /// <summary>Upstream <c>RE_ERROR_ILLEGAL</c>: an opcode the engine does not know.</summary>
    internal const int Illegal = -1;

    /// <summary>Upstream <c>RE_ERROR_CANCELLED</c>, which for this port only ever means a timeout.</summary>
    internal const int Cancelled = -5;

    /// <summary>Upstream <c>RE_ERROR_PARTIAL</c>.</summary>
    internal const int Partial = -13;

    /// <summary>Upstream <c>bool_as_status</c> (<c>upstream/src/_regex.c</c> line 2225).</summary>
    /// <param name="matched">Whether the thing matched.</param>
    /// <returns><see cref="Success"/> or <see cref="Failure"/>.</returns>
    internal static int From(bool matched) => matched ? Success : Failure;
}

/// <summary>
/// The exceptions the matcher throws where an opcode's implementation has not been ported yet.
/// </summary>
/// <remarks>
/// The S07 rule: port the control flow, throw at the leaf. A silent no-op here would turn a missing
/// construct into a wrong answer, where a throw is either a skipped test or an <c>unsupported</c>
/// oracle row - both of which say "not known yet" rather than "known to be this".
/// </remarks>
internal static class Seam
{
    /// <summary>The seam for an opcode no slice has implemented yet.</summary>
    /// <param name="op">The opcode reached.</param>
    /// <returns>The exception to throw.</returns>
    internal static NotImplementedException For(Opcode op) => new($"needs:{Tag(op)} - the matcher has no {op} yet");

    /// <summary>The seam for a construct that is not an opcode of its own.</summary>
    /// <param name="tag">The capability tag.</param>
    /// <param name="what">What was reached.</param>
    /// <returns>The exception to throw.</returns>
    internal static NotImplementedException For(string tag, string what) => new($"needs:{tag} - {what}");

    /// <summary>
    /// Which capability tag an opcode waits on, so a skipped test and an <c>unsupported</c> oracle
    /// row name the same slice. The slice each tag belongs to is in <c>docs/plan/slices/</c>.
    /// </summary>
    /// <param name="op">The opcode.</param>
    /// <returns>The tag, without the <c>needs:</c> prefix.</returns>
    private static string Tag(Opcode op) =>
        op switch
        {
            Opcode.AnyAllRev
            or Opcode.AnyRev
            or Opcode.AnyURev
            or Opcode.CharacterIgnRev
            or Opcode.CharacterRev
            or Opcode.PropertyIgnRev
            or Opcode.PropertyRev
            or Opcode.RangeIgnRev
            or Opcode.RangeRev
            or Opcode.RefGroupFldRev
            or Opcode.RefGroupIgnRev
            or Opcode.RefGroupRev
            or Opcode.SetDiffIgnRev
            or Opcode.SetDiffRev
            or Opcode.SetInterIgnRev
            or Opcode.SetInterRev
            or Opcode.SetSymDiffIgnRev
            or Opcode.SetSymDiffRev
            or Opcode.SetUnionIgnRev
            or Opcode.SetUnionRev
            or Opcode.StringFldRev
            or Opcode.StringIgnRev
            or Opcode.StringRev => "right-to-left",

            // S17 delivered the forward, case-sensitive PROPERTY, RANGE and SET_* opcodes, so what
            // is left of those families is the case-insensitive half, and that is S22's. Naming the
            // delivered tag here would put a capability the status board says we have on an
            // oracle 'unsupported' row and in a stack trace (found by the S17 blind review).
            Opcode.CharacterIgn
            or Opcode.PropertyIgn
            or Opcode.RangeIgn
            or Opcode.SetDiffIgn
            or Opcode.SetInterIgn
            or Opcode.SetSymDiffIgn
            or Opcode.SetUnionIgn
            or Opcode.RefGroupFld
            or Opcode.RefGroupIgn
            or Opcode.StringFld
            or Opcode.StringIgn => "ignore-case",

            // S19 delivered the whole GREEDY_REPEAT / LAZY_REPEAT / *_REPEAT_ONE family and the
            // BODY_*, MATCH_* and TAIL_START backtrack markers, so 'quantifiers' has no arm here -
            // naming a delivered tag would put a capability the status board says we have on an
            // oracle 'unsupported' row and in a stack trace (the S17 blind review found that).
            // S21 delivered REF_GROUP and GROUP_EXISTS, so 'backrefs' has no arm here at all and
            // 'conditionals' covers only CONDITIONAL - the lookaround-condition form - which stays
            // Phase 4's. REF_GROUP_IGN and REF_GROUP_FLD moved up to 'ignore-case' beside
            // STRING_IGN and STRING_FLD, for the reason the S17 note above gives.
            Opcode.Conditional or Opcode.EndConditional => "conditionals",

            Opcode.CallRef or Opcode.GroupCall or Opcode.GroupReturn => "recursion",

            Opcode.EndLookaround or Opcode.Lookaround => "lookaround",

            Opcode.EndFuzzy or Opcode.Fuzzy or Opcode.FuzzyExt or Opcode.FuzzyInsert => "fuzzy-matching",

            // S20 delivered BOUNDARY, DEFAULT_BOUNDARY, DEFAULT_START_OF_WORD, DEFAULT_END_OF_WORD,
            // START_OF_WORD, END_OF_WORD, GRAPHEME_BOUNDARY, KEEP, ATOMIC and END_ATOMIC, so
            // 'word-flag', 'grapheme', 'keep-marker' and 'atomic' have no arm here either - same
            // reason as 'quantifiers' above.
            Opcode.Prune or Opcode.Skip => "backtracking-verbs",

            // Every remaining opcode either has a case above in the dispatch switch or never
            // reaches the matcher at all (END, NEXT, GROUP - which build_GROUP consumes into a
            // START_GROUP/END_GROUP pair - and the values-only words).
            _ => "basic-matching",
        };
}

/// <summary>
/// The matching engine. Port of <c>basic_match</c> (<c>upstream/src/_regex.c</c> lines
/// 11714-17403) and the drivers above it, <c>do_exact_match</c> (<c>:18064</c>),
/// <c>do_match_2</c> (<c>:18099</c>) and <c>do_match</c> (<c>:18121</c>), together with the
/// <c>try_match_*</c> and <c>matches_*</c> predicates the dispatch switch calls.
/// </summary>
/// <remarks>
/// <para>
/// The shape is upstream's and deliberately so (DECISIONS 2026-08-31): an explicit stack machine
/// inside one dispatch switch, with <c>goto</c> where upstream has <c>goto</c>. Most future upstream
/// fixes land in <c>basic_match</c>, and a version rewritten into recursion or a strategy table
/// would have to be re-derived at every sync instead of diffed.
/// </para>
/// <para>
/// <b>Deferred to Phase 7</b>, all of them semantically transparent prefilters: the required-string
/// locator (<c>locate_required_string</c>, <c>:11082</c>), <c>search_start</c> and the
/// <c>string_search</c> family (<c>:5231-6918</c>), and the test-node fast path (<c>try_match</c>,
/// <c>:7671</c>). Without them the search tries the pattern at every position, which is slower and
/// answers the same. <b>They are not only a speed matter</b>: the required-string locator is the
/// whole reason upstream answers <c>'(a|a)*b'</c> against a subject holding no <c>'b'</c>
/// instantly, where this port runs the exponential search. Measured 2026-08-31; DECISIONS has the
/// numbers.
/// </para>
/// <para>
/// The test-node fast path (<c>try_match</c>, <c>:7671</c>) is deferred too, and S19 measured what
/// that costs before putting it back: nothing it could find. See <see cref="TryMatch"/>.
/// </para>
/// </remarks>
internal static class Matcher
{
    /// <summary>
    /// Upstream <c>same_char</c> (<c>upstream/src/_regex.c</c> line 2838).
    /// </summary>
    /// <param name="ch1">One codepoint.</param>
    /// <param name="ch2">The other.</param>
    /// <returns><see langword="true"/> if they are the same.</returns>
    internal static bool SameChar(uint ch1, uint ch2) => ch1 == ch2;

    /// <summary>Upstream <c>matches_ANY</c> (line 2900): anything except a newline.</summary>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if it is not a newline.</returns>
    internal static bool MatchesAny(uint ch) => ch != '\n';

    /// <summary>Upstream <c>matches_ANY_U</c> (line 2906): anything except a line separator.</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if it does not separate lines.</returns>
    internal static bool MatchesAnyU(CaseEncoding encoding, uint ch) => !Encodings.IsLineSep(encoding, ch);

    /// <summary>Upstream <c>matches_CHARACTER</c> (line 2912).</summary>
    /// <param name="node">The <c>CHARACTER</c> node.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if they are the same.</returns>
    internal static bool MatchesCharacter(Node node, uint ch) => SameChar(node.Values[0], ch);

    /// <summary>Upstream <c>in_range</c> (line 2816).</summary>
    /// <param name="lower">The lowest codepoint in the range.</param>
    /// <param name="upper">The highest.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if it is in the range.</returns>
    internal static bool InRange(uint lower, uint upper, uint ch) => lower <= ch && ch <= upper;

    /// <summary>Upstream <c>matches_RANGE</c> (line 3003).</summary>
    /// <param name="node">The <c>RANGE</c> node.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if it is in the node's range.</returns>
    internal static bool MatchesRange(Node node, uint ch) => InRange(node.Values[0], node.Values[1], ch);

    /// <summary>
    /// Upstream's <c>ENCODING_KIND(node)</c> switch, which <c>matches_PROPERTY</c> (line 2926) and
    /// <c>matches_member</c> (line 3041) both open with: a node compiled under a scoped
    /// <c>(?a:...)</c> or <c>(?u:...)</c> carries its own encoding and ignores the pattern's.
    /// </summary>
    /// <remarks>
    /// This is the whole of the scoped ASCII flag's matching story. Without it <c>(?a:\d)</c>
    /// matched U+FF19 FULLWIDTH DIGIT NINE, because the pattern-level encoding is Unicode
    /// (upstream answers <see langword="false"/>; probed against regex 2026.7.19, 2026-08-31).
    /// </remarks>
    /// <param name="encoding">The pattern's encoding.</param>
    /// <param name="node">The node.</param>
    /// <returns>The encoding to answer this node's property lookups in.</returns>
    internal static CaseEncoding NodeEncoding(CaseEncoding encoding, Node node) =>
        NodeStatus.EncodingKind(node) switch
        {
            NodeStatus.AsciiEncoding => CaseEncoding.Ascii,
            NodeStatus.UnicodeEncoding => CaseEncoding.Unicode,
            _ => encoding,
        };

    /// <summary>Upstream <c>matches_PROPERTY</c> (line 2924).</summary>
    /// <param name="encoding">The pattern's encoding.</param>
    /// <param name="node">The <c>PROPERTY</c> node.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if the codepoint has the property.</returns>
    internal static bool MatchesProperty(CaseEncoding encoding, Node node, uint ch) =>
        Encodings.HasProperty(NodeEncoding(encoding, node), node.Values[0], ch);

    /// <summary>Upstream <c>matches_member</c> (line 3025).</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="member">The member node.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if the codepoint matches the member.</returns>
    internal static bool MatchesMember(CaseEncoding encoding, Node member, uint ch)
    {
        switch (member.Op)
        {
            case Opcode.AnyAll:
                return true;
            case Opcode.Character:
                return ch == member.Values[0];
            case Opcode.Property:
                return MatchesProperty(encoding, member, ch);
            case Opcode.Range:
                return InRange(member.Values[0], member.Values[1], ch);
            case Opcode.SetDiff:
                return InSetDiff(encoding, member, ch);
            case Opcode.SetInter:
                return InSetInter(encoding, member, ch);
            case Opcode.SetSymDiff:
                return InSetSymDiff(encoding, member, ch);
            case Opcode.SetUnion:
                return InSetUnion(encoding, member, ch);
            case Opcode.String:
                // A STRING inside a set is a set of single characters, not a sequence: build_SET
                // routes it through build_STRING with 'is_charset' (NodeCompiler, :1637). Upstream's
                // hand-written scan over 'values' is a 'Contains' here, which S3267 asks for and
                // which is the same linear scan.
                return member.Values.Contains(ch);
            default:
                return false;
        }
    }

    /// <summary>Upstream <c>in_set_diff</c> (line 3155): in the first member and in no other.</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="node">The set node.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if the codepoint is in the difference.</returns>
    internal static bool InSetDiff(CaseEncoding encoding, Node node, uint ch)
    {
        Node? member = node.Next2.Node;

        if (MatchesMember(encoding, member!, ch) != member!.Match)
        {
            return false;
        }

        member = member.Next1.Node;

        while (member is not null)
        {
            if (MatchesMember(encoding, member, ch) == member.Match)
            {
                return false;
            }

            member = member.Next1.Node;
        }

        return true;
    }

    /// <summary>Upstream <c>in_set_inter</c> (line 3201).</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="node">The set node.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if the codepoint is in every member.</returns>
    internal static bool InSetInter(CaseEncoding encoding, Node node, uint ch)
    {
        Node? member = node.Next2.Node;

        while (member is not null)
        {
            if (MatchesMember(encoding, member, ch) != member.Match)
            {
                return false;
            }

            member = member.Next1.Node;
        }

        return true;
    }

    /// <summary>Upstream <c>in_set_sym_diff</c> (line 3236).</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="node">The set node.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if the codepoint is in an odd number of members.</returns>
    internal static bool InSetSymDiff(CaseEncoding encoding, Node node, uint ch)
    {
        Node? member = node.Next2.Node;
        bool result = false;

        while (member is not null)
        {
            if (MatchesMember(encoding, member, ch) == member.Match)
            {
                result = !result;
            }

            member = member.Next1.Node;
        }

        return result;
    }

    /// <summary>Upstream <c>in_set_union</c> (line 3278).</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="node">The set node.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if the codepoint is in any member.</returns>
    internal static bool InSetUnion(CaseEncoding encoding, Node node, uint ch)
    {
        Node? member = node.Next2.Node;

        while (member is not null)
        {
            if (MatchesMember(encoding, member, ch) == member.Match)
            {
                return true;
            }

            member = member.Next1.Node;
        }

        return false;
    }

    /// <summary>
    /// Which <c>matches_*</c> predicate a single-character opcode uses. Not an upstream function:
    /// upstream picks it by having a separate <c>case</c> per opcode in the dispatch switch, and
    /// this is that choice pulled out so the eleven lines around it are written once.
    /// </summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="node">The node.</param>
    /// <param name="ch">The codepoint at the text position.</param>
    /// <returns>What the node's predicate says, before <c>node-&gt;match</c> is applied.</returns>
    private static bool MatchesOne(CaseEncoding encoding, Node node, uint ch) =>
        node.Op switch
        {
            Opcode.Character => MatchesCharacter(node, ch),
            Opcode.Property => MatchesProperty(encoding, node, ch),
            Opcode.Range => MatchesRange(node, ch),
            _ => MatchesSet(encoding, node, ch),
        };

    /// <summary>Upstream <c>matches_SET</c> (line 3313).</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="node">The set node.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if the codepoint is in the set.</returns>
    internal static bool MatchesSet(CaseEncoding encoding, Node node, uint ch) =>
        node.Op switch
        {
            Opcode.SetDiff or Opcode.SetDiffRev => InSetDiff(encoding, node, ch),
            Opcode.SetInter or Opcode.SetInterRev => InSetInter(encoding, node, ch),
            Opcode.SetSymDiff or Opcode.SetSymDiffRev => InSetSymDiff(encoding, node, ch),
            Opcode.SetUnion or Opcode.SetUnionRev => InSetUnion(encoding, node, ch),
            _ => false,
        };

    /// <summary>
    /// What the <c>match_many_*</c> family asks of one character, for the opcodes a
    /// <c>*_REPEAT_ONE</c> node can repeat. Not an upstream function: upstream writes a
    /// <c>match_many_X</c> per opcode - ANY (<c>upstream/src/_regex.c</c> line 3537), ANY_U
    /// (<c>:3647</c>), CHARACTER (<c>:3803</c>), PROPERTY (<c>:4045</c>), RANGE (<c>:4485</c>) and
    /// SET (<c>:4737</c>) - and each is the same loop over three character widths with a different
    /// predicate. This is the predicate; <see cref="CountOne"/> is the loop.
    /// </summary>
    /// <remarks>
    /// Upstream's ANY and ANY_U compare against the <c>match</c> argument their caller passed, where
    /// the CHARACTER, PROPERTY, RANGE and SET family folds <c>node-&gt;match</c> in first
    /// (<c>match = node-&gt;match == match</c>, <c>:3809</c>, <c>:4053</c> and so on). Every
    /// <c>count_one</c> call passes <c>TRUE</c>, so the difference is only that ANY ignores
    /// <c>node-&gt;match</c>; <c>.</c> is never negated, so the two agree, and the distinction is
    /// kept because keeping it is free. ANY_ALL has no <c>match_many</c> at all - <c>count_one</c>
    /// takes the whole slice for it (<c>:5009</c>) - which is what a predicate of
    /// <see langword="true"/> says.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="node">The repeated node.</param>
    /// <param name="pos">The position.</param>
    /// <returns><see langword="true"/> if the character there is one more repeat.</returns>
    private static bool MatchesMany(MatchState state, Node node, int pos)
    {
        uint ch = state.CharAt(pos);

        return node.Op switch
        {
            Opcode.Any => MatchesAny(ch),
            Opcode.AnyAll => true,
            Opcode.AnyU => MatchesAnyU(state.Encoding, ch),
            _ => MatchesOne(state.Encoding, node, ch) == node.Match,
        };
    }

    /// <summary>
    /// Upstream <c>count_one</c> (<c>upstream/src/_regex.c</c> line 4989), reduced to the forward,
    /// case-sensitive opcodes this port matches: how many times
    /// <paramref name="node"/> repeats from <paramref name="textPos"/>, up to
    /// <paramref name="maxCount"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream's arms are each three statements - clamp the count to what is left of the slice, run
    /// the opcode's <c>match_many_X</c> as far as that, then subtract the positions to get the count
    /// back - and both bounds only ever stop the same walk, so one loop carrying both says the same
    /// thing. Doing it in one pass also avoids walking the subject twice, which the codepoint /
    /// UTF-16 split would otherwise force.
    /// </para>
    /// <para>
    /// <b><paramref name="endPos"/> is not an upstream out-parameter.</b> Upstream's callers do
    /// <c>text_pos += (Py_ssize_t)count * node-&gt;step</c>, which works because it indexes the
    /// subject by codepoint, so a character count and a subject offset are the same number. Ours are
    /// UTF-16 code unit indices, so the walk's end position is returned rather than recomputed by
    /// multiplication - see <see cref="StepBy"/> for the sites where a position genuinely has to be
    /// derived from a count.
    /// </para>
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="node">The repeated node, which matches exactly one character.</param>
    /// <param name="textPos">Where to start counting.</param>
    /// <param name="maxCount">The most repeats to count.</param>
    /// <param name="isPartial">
    /// Receives upstream's <c>*is_partial</c>: the count ran out of subject rather than out of
    /// matches, and a partial match on the right was asked for.
    /// </param>
    /// <param name="endPos">Receives the position the count reached.</param>
    /// <returns>The number of repeats.</returns>
    internal static long CountOne(
        MatchState state,
        Node node,
        int textPos,
        long maxCount,
        out bool isPartial,
        out int endPos
    )
    {
        isPartial = false;
        endPos = textPos;

        if (maxCount < 1)
        {
            return 0;
        }

        switch (node.Op)
        {
            case Opcode.Any:
            case Opcode.AnyAll:
            case Opcode.AnyU:
            case Opcode.Character:
            case Opcode.Property:
            case Opcode.Range:
            case Opcode.SetDiff:
            case Opcode.SetInter:
            case Opcode.SetSymDiff:
            case Opcode.SetUnion:
                break;
            default:
                // Upstream's switch has no default at all, so an opcode it does not list falls off
                // the end of the function with 'count' uninitialised. Every opcode that reaches
                // here is one 'SequenceMatchesOne' accepted, so the ones missing from the list
                // above are the case-insensitive (S22) and reverse (S23) halves.
                throw Seam.For(node.Op);
        }

        long count = 0;
        int pos = textPos;

        while (count < maxCount && pos < state.SliceEnd && MatchesMany(state, node, pos))
        {
            pos = state.NextPos(pos);
            ++count;
        }

        endPos = pos;

        // Upstream's 'count == (size_t)(state->text_end - text_pos)': the walk consumed everything
        // there was, which in code-unit indices is the walk having stopped at 'text_end'.
        isPartial = pos == state.TextEnd && count < maxCount && state.PartialSide == MatchState.PartialRight;

        return count;
    }

    /// <summary>
    /// Upstream <c>text_pos + (Py_ssize_t)count * step</c>: <paramref name="count"/> characters
    /// along from <paramref name="pos"/>, in the direction <paramref name="step"/> runs.
    /// </summary>
    /// <remarks>
    /// Not an upstream function, and it exists for exactly one reason: upstream indexes the subject
    /// by codepoint, so it can multiply a character count by a step to get an offset, and this port
    /// indexes by UTF-16 code unit, where an astral character is one character and two units. The
    /// walk stops at the slice bound, which upstream gets for free - its own product cannot pass the
    /// bound, because every count it multiplies came from <see cref="CountOne"/> or from a count of
    /// what is left of the slice.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="pos">The position to start from.</param>
    /// <param name="count">How many characters to step.</param>
    /// <param name="step">Upstream's step: 1 forwards, -1 backwards.</param>
    /// <returns>The stepped position.</returns>
    private static int StepBy(MatchState state, int pos, long count, long step)
    {
        if (step > 0)
        {
            for (long i = 0; i < count && pos < state.SliceEnd; i++)
            {
                pos = state.NextPos(pos);
            }
        }
        else if (step < 0)
        {
            for (long i = 0; i < count && pos > state.SliceStart; i++)
            {
                pos = state.PrevPos(pos);
            }
        }

        return pos;
    }

    /// <summary>
    /// Upstream <c>abs_ssize_t(pos - state-&gt;text_pos)</c> where that difference is a count of
    /// characters (<c>upstream/src/_regex.c</c> lines 16297 and 17058), and
    /// <c>slice_end - text_pos</c> where that is (<c>:16486</c>).
    /// </summary>
    /// <remarks>
    /// The inverse of <see cref="StepBy"/> and there for the same reason. A subtraction of UTF-16
    /// indices counts code units, and every one of these sites means characters.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="from">One position.</param>
    /// <param name="to">The other.</param>
    /// <returns>How many characters lie between them.</returns>
    private static long CountBetween(MatchState state, int from, int to)
    {
        int pos = Math.Min(from, to);
        int end = Math.Max(from, to);
        long count = 0;

        while (pos < end)
        {
            pos = state.NextPos(pos);
            ++count;
        }

        return count;
    }

    /// <summary>
    /// Upstream <c>ascii_at_line_start</c> / <c>unicode_at_line_start</c>
    /// (<c>upstream/src/_regex.c</c> lines 899 and 1942). Upstream reaches these through the
    /// encoding table; they take the state, so they live here rather than in
    /// <see cref="Encodings"/>.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns><see langword="true"/> if a line starts there.</returns>
    internal static bool AtLineStart(MatchState state, int textPos)
    {
        if (textPos <= state.TextStart)
        {
            return true;
        }

        uint ch = state.CharBefore(textPos);

        if (ch == 0x0D)
        {
            if (textPos >= state.TextEnd)
            {
                return true;
            }

            // No line break inside CRLF.
            return state.CharAt(textPos) != 0x0A;
        }

        return Encodings.IsLineSep(state.Encoding, ch);
    }

    /// <summary>
    /// Upstream <c>ascii_at_line_end</c> / <c>unicode_at_line_end</c> (lines 919 and 1963).
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns><see langword="true"/> if a line ends there.</returns>
    internal static bool AtLineEnd(MatchState state, int textPos)
    {
        if (textPos >= state.TextEnd)
        {
            return true;
        }

        uint ch = state.CharAt(textPos);

        if (ch == 0x0A)
        {
            if (textPos <= state.TextStart)
            {
                return true;
            }

            // No line break inside CRLF.
            return state.CharBefore(textPos) != 0x0D;
        }

        return Encodings.IsLineSep(state.Encoding, ch);
    }

    /// <summary>
    /// Upstream <c>ascii_word_left</c> / <c>unicode_word_left</c> (<c>upstream/src/_regex.c</c>
    /// lines 849 and 1447).
    /// </summary>
    /// <remarks>
    /// Upstream writes the pair out per encoding and they differ only in which
    /// <c>has_property</c> they call, which <see cref="Encodings.HasProperty(CaseEncoding, uint, uint)"/>
    /// already decides from the encoding.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="textPos">The position.</param>
    /// <returns><see langword="true"/> if a word character lies to the left.</returns>
    private static bool WordLeft(MatchState state, CaseEncoding encoding, int textPos) =>
        textPos > state.TextStart && Encodings.HasProperty(encoding, UnicodeTables.PropWord, state.CharBefore(textPos));

    /// <summary>Upstream <c>ascii_word_right</c> / <c>unicode_word_right</c> (lines 855 and 1453).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="textPos">The position.</param>
    /// <returns><see langword="true"/> if a word character lies to the right.</returns>
    private static bool WordRight(MatchState state, CaseEncoding encoding, int textPos) =>
        textPos < state.TextEnd && Encodings.HasProperty(encoding, UnicodeTables.PropWord, state.CharAt(textPos));

    /// <summary>
    /// Upstream <c>ascii_at_boundary</c> / <c>unicode_at_boundary</c> (lines 861 and 1460).
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="textPos">The position.</param>
    /// <returns><see langword="true"/> if a word boundary is there.</returns>
    internal static bool AtBoundary(MatchState state, CaseEncoding encoding, int textPos) =>
        WordLeft(state, encoding, textPos) != WordRight(state, encoding, textPos);

    /// <summary>
    /// Upstream <c>ascii_at_word_start</c> / <c>unicode_at_word_start</c> (lines 872 and 1471).
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="textPos">The position.</param>
    /// <returns><see langword="true"/> if a word starts there.</returns>
    internal static bool AtWordStart(MatchState state, CaseEncoding encoding, int textPos) =>
        !WordLeft(state, encoding, textPos) && WordRight(state, encoding, textPos);

    /// <summary>
    /// Upstream <c>ascii_at_word_end</c> / <c>unicode_at_word_end</c> (lines 883 and 1482).
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="textPos">The position.</param>
    /// <returns><see langword="true"/> if a word ends there.</returns>
    internal static bool AtWordEnd(MatchState state, CaseEncoding encoding, int textPos) =>
        WordLeft(state, encoding, textPos) && !WordRight(state, encoding, textPos);

    /// <summary>Upstream <c>is_unicode_vowel</c> (line 1496).</summary>
    /// <remarks>
    /// Upstream's <c>Py_UNICODE_TOLOWER</c> is CPython's simple lowercase mapping, and
    /// <see cref="PythonStr.Lower"/> is the same UCD mapping with SpecialCasing's unconditional rows
    /// over it. Only its first codepoint can be one of the twenty this compares against, so taking it
    /// is upstream's answer. Allocating per call is fine here: the only caller reaches this when the
    /// character to its left is an apostrophe.
    /// </remarks>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> for the vowels upstream lists.</returns>
    private static bool IsUnicodeVowel(uint ch) =>
        (uint)PythonStr.Lower([(int)ch])[0]
            is 'a'
                or 0xE0
                or 0xE1
                or 0xE2
                or 'e'
                or 0xE8
                or 0xE9
                or 0xEA
                or 'i'
                or 0xEC
                or 0xED
                or 0xEE
                or 'o'
                or 0xF2
                or 0xF3
                or 0xF4
                or 'u'
                or 0xF9
                or 0xFA
                or 0xFB;

    /// <summary>Upstream <c>is_unicode_apostrophe</c> (line 1514).</summary>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> for U+0027 and U+2019.</returns>
    private static bool IsUnicodeApostrophe(uint ch) => ch is 0x27 or 0x2019;

    /// <summary>Upstream <c>IS_AHLETTER</c> (line 1518).</summary>
    /// <param name="v">A word-break property value.</param>
    /// <returns><see langword="true"/> for ALetter and Hebrew_Letter.</returns>
    private static bool IsAhLetter(uint v) => v is UnicodeTables.WbreakAletter or UnicodeTables.WbreakHebrewletter;

    /// <summary>Upstream <c>IS_MIDNUMLETQ</c> (line 1522).</summary>
    /// <param name="v">A word-break property value.</param>
    /// <returns><see langword="true"/> for MidNumLet and Single_Quote.</returns>
    private static bool IsMidNumLetQ(uint v) => v is UnicodeTables.WbreakMidnumlet or UnicodeTables.WbreakSinglequote;

    /// <summary>
    /// Upstream <c>unicode_at_default_boundary</c> (<c>upstream/src/_regex.c</c> lines 1531-1749):
    /// the UAX #29 default word-boundary rules, WB1 to WB999.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream's <c>left_pos</c> is inclusive - the index of the character to the left - and its
    /// <c>left_pos - 1</c> and <c>right_pos + 1</c> are the characters beyond those, which in UTF-16
    /// are <see cref="MatchState.PrevPos"/> and <see cref="MatchState.NextPos"/> rather than
    /// arithmetic.
    /// </para>
    /// <para>
    /// WB15/WB16 count regional indicators, not code units. Upstream reads the count off
    /// <c>left_pos - pos</c> because its indices are codepoints; every regional indicator is astral,
    /// so the same subtraction here would always be even and the rule would never fire, which is why
    /// the walk carries its own counter.
    /// </para>
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns><see langword="true"/> if a default word boundary is there.</returns>
    internal static bool AtDefaultBoundary(MatchState state, int textPos)
    {
        // Break at the start and end of text, unless the text is empty.
        // WB1 and WB2
        if (textPos <= state.TextStart || textPos >= state.TextEnd)
        {
            return state.TextEnd > state.TextStart;
        }

        int leftPos = state.PrevPos(textPos);
        int rightPos = textPos;
        uint leftChar = state.CharAt(leftPos);
        uint rightChar = state.CharAt(rightPos);

        // Do not break within CRLF.
        // WB3
        uint leftProp = UnicodeTables.GetWordBreak(leftChar);
        uint rightProp = UnicodeTables.GetWordBreak(rightChar);

        if (leftProp == UnicodeTables.WbreakCr && rightProp == UnicodeTables.WbreakLf)
        {
            return false;
        }

        // Otherwise break before and after Newlines (including CR and LF).
        // WB3a
        if (leftProp is UnicodeTables.WbreakNewline or UnicodeTables.WbreakCr or UnicodeTables.WbreakLf)
        {
            return true;
        }

        // WB3b
        if (rightProp is UnicodeTables.WbreakNewline or UnicodeTables.WbreakCr or UnicodeTables.WbreakLf)
        {
            return true;
        }

        // Do not break within emoji zwj sequences.
        // WB3c
        if (leftProp == UnicodeTables.WbreakZwj && UnicodeTables.GetExtendedPictographic(rightChar) != 0)
        {
            return false;
        }

        // Keep horizontal whitespace together.
        // WB3d
        if (leftProp == UnicodeTables.WbreakWsegspace && rightProp == UnicodeTables.WbreakWsegspace)
        {
            return false;
        }

        // Ignore Format and Extend characters, except after sot, CR, LF, and Newline. This also has
        // the effect of: Any x (Format || Extend || ZWJ)
        // WB4
        if (rightProp is UnicodeTables.WbreakExtend or UnicodeTables.WbreakFormat or UnicodeTables.WbreakZwj)
        {
            return false;
        }

        while (leftProp is UnicodeTables.WbreakExtend or UnicodeTables.WbreakFormat or UnicodeTables.WbreakZwj)
        {
            if (leftPos <= state.TextStart)
            {
                return false;
            }

            leftPos = state.PrevPos(leftPos);
            leftChar = state.CharAt(leftPos);
            leftProp = UnicodeTables.GetWordBreak(leftChar);
        }

        // Do not break between most letters.
        // WB5
        if (IsAhLetter(leftProp) && IsAhLetter(rightProp))
        {
            return false;
        }

        // Break between apostrophe and vowels (French, Italian).
        // WB5a
        if (IsUnicodeApostrophe(leftChar) && IsUnicodeVowel(rightChar))
        {
            return false;
        }

        // Do not break letters across certain punctuation.
        // WB6
        int rightRightPos = state.NextPos(rightPos);
        uint rightRightProp = 0;
        bool hasRightRight = rightRightPos < state.TextEnd;

        if (hasRightRight)
        {
            rightRightProp = UnicodeTables.GetWordBreak(state.CharAt(rightRightPos));

            if (
                IsAhLetter(leftProp)
                && (rightProp == UnicodeTables.WbreakMidletter || IsMidNumLetQ(rightProp))
                && IsAhLetter(rightRightProp)
            )
            {
                return false;
            }
        }

        // WB7
        int leftLeftPos = state.PrevPos(leftPos);
        uint leftLeftProp = 0;
        bool hasLeftLeft = leftPos > state.TextStart;

        if (hasLeftLeft)
        {
            leftLeftProp = UnicodeTables.GetWordBreak(state.CharAt(leftLeftPos));

            if (
                IsAhLetter(leftLeftProp)
                && (leftProp == UnicodeTables.WbreakMidletter || IsMidNumLetQ(leftProp))
                && IsAhLetter(rightProp)
            )
            {
                return false;
            }
        }

        // WB7a
        if (leftProp == UnicodeTables.WbreakHebrewletter && rightProp == UnicodeTables.WbreakSinglequote)
        {
            return false;
        }

        // WB7b
        if (
            hasRightRight
            && leftProp == UnicodeTables.WbreakHebrewletter
            && rightProp == UnicodeTables.WbreakDoublequote
            && rightRightProp == UnicodeTables.WbreakHebrewletter
        )
        {
            return false;
        }

        // WB7c
        if (
            hasLeftLeft
            && leftLeftProp == UnicodeTables.WbreakHebrewletter
            && leftProp == UnicodeTables.WbreakDoublequote
            && rightProp == UnicodeTables.WbreakHebrewletter
        )
        {
            return false;
        }

        // Do not break within sequences of digits, or digits adjacent to letters ("3a", or "A3").
        // WB8
        if (leftProp == UnicodeTables.WbreakNumeric && rightProp == UnicodeTables.WbreakNumeric)
        {
            return false;
        }

        // WB9
        if (IsAhLetter(leftProp) && rightProp == UnicodeTables.WbreakNumeric)
        {
            return false;
        }

        // WB10
        if (leftProp == UnicodeTables.WbreakNumeric && IsAhLetter(rightProp))
        {
            return false;
        }

        // Do not break within sequences, such as "3.2" or "3,456.789".
        // WB11
        if (
            hasLeftLeft
            && leftLeftProp == UnicodeTables.WbreakNumeric
            && (leftProp == UnicodeTables.WbreakMidnum || IsMidNumLetQ(leftProp))
            && rightProp == UnicodeTables.WbreakNumeric
        )
        {
            return false;
        }

        // WB12
        if (
            hasRightRight
            && leftProp == UnicodeTables.WbreakNumeric
            && (rightProp == UnicodeTables.WbreakMidnum || IsMidNumLetQ(rightProp))
            && rightRightProp == UnicodeTables.WbreakNumeric
        )
        {
            return false;
        }

        // Do not break between Katakana.
        // WB13
        if (leftProp == UnicodeTables.WbreakKatakana && rightProp == UnicodeTables.WbreakKatakana)
        {
            return false;
        }

        // Do not break from extenders.
        // WB13a
        if (
            (
                IsAhLetter(leftProp)
                || leftProp
                    is UnicodeTables.WbreakNumeric
                        or UnicodeTables.WbreakKatakana
                        or UnicodeTables.WbreakExtendnumlet
            )
            && rightProp == UnicodeTables.WbreakExtendnumlet
        )
        {
            return false;
        }

        // WB13b
        if (
            leftProp == UnicodeTables.WbreakExtendnumlet
            && (IsAhLetter(rightProp) || rightProp is UnicodeTables.WbreakNumeric or UnicodeTables.WbreakKatakana)
        )
        {
            return false;
        }

        // Do not break within emoji flag sequences. That is, do not break between regional indicator
        // (RI) symbols if there is an odd number of RI characters before the break point.
        // WB15 and WB16
        if (
            CountRegionalIndicatorsLeft(
                state,
                leftPos,
                UnicodeTables.GetWordBreak,
                UnicodeTables.WbreakRegionalindicator
            ) % 2
            == 1
        )
        {
            return false;
        }

        // Otherwise, break everywhere (including around ideographs).
        // WB999
        return true;
    }

    /// <summary>
    /// Upstream's regional-indicator walk, which WB15/WB16 (line 1737) and GB12/GB13 (line 1919)
    /// write out identically bar the property they read.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="leftPos">The index of the character to the left of the position being tested.</param>
    /// <param name="getBreak">The break property to read.</param>
    /// <param name="regionalIndicator">That property's regional-indicator value.</param>
    /// <returns>How many regional indicators run leftwards from <paramref name="leftPos"/>.</returns>
    private static long CountRegionalIndicatorsLeft(
        MatchState state,
        int leftPos,
        Func<uint, uint> getBreak,
        uint regionalIndicator
    )
    {
        long count = 0;
        int pos = leftPos;

        while (pos >= state.TextStart && getBreak(state.CharAt(pos)) == regionalIndicator)
        {
            ++count;
            pos = state.PrevPos(pos);
        }

        return count;
    }

    /// <summary>
    /// Upstream <c>unicode_at_default_word_start_or_end</c> (<c>upstream/src/_regex.c</c> line 1752).
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <param name="atStart">Whether the caller wants the start of a word rather than the end.</param>
    /// <returns><see langword="true"/> if the position is that edge of a word.</returns>
    internal static bool AtDefaultWordStartOrEnd(MatchState state, int textPos, bool atStart)
    {
        // Is it at a boundary?
        if (!AtDefaultBoundary(state, textPos))
        {
            return false;
        }

        // Look at the 2 characters either side of the boundary. Are they part of a word?
        bool before = WordLeft(state, CaseEncoding.Unicode, textPos);
        bool after = WordRight(state, CaseEncoding.Unicode, textPos);

        return before != atStart && after == atStart;
    }

    /// <summary>
    /// Upstream <c>unicode_at_grapheme_boundary</c> (<c>upstream/src/_regex.c</c> lines 1786-1933):
    /// the UAX #29 extended grapheme cluster rules, GB1 to GB999.
    /// </summary>
    /// <remarks>
    /// The same two UTF-16 translations as <see cref="AtDefaultBoundary"/>: neighbours are stepped
    /// rather than offset, and GB12/GB13 counts regional indicators rather than subtracting indices.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns><see langword="true"/> if a grapheme cluster boundary is there.</returns>
    internal static bool AtGraphemeBoundary(MatchState state, int textPos)
    {
        // Break at the start and end of text, unless the text is empty.
        // GB1 and GB2
        if (textPos <= state.TextStart || textPos >= state.TextEnd)
        {
            return state.TextEnd > state.TextStart;
        }

        int leftPos = state.PrevPos(textPos);
        int rightPos = textPos;
        uint leftChar = state.CharAt(leftPos);
        uint rightChar = state.CharAt(rightPos);

        // Do not break between a CR and LF. Otherwise, break before and after controls.
        // GB3
        uint leftProp = UnicodeTables.GetGraphemeClusterBreak(leftChar);
        uint rightProp = UnicodeTables.GetGraphemeClusterBreak(rightChar);

        if (leftProp == UnicodeTables.GbreakCr && rightProp == UnicodeTables.GbreakLf)
        {
            return false;
        }

        // GB4
        if (leftProp is UnicodeTables.GbreakControl or UnicodeTables.GbreakCr or UnicodeTables.GbreakLf)
        {
            return true;
        }

        // GB5
        if (rightProp is UnicodeTables.GbreakControl or UnicodeTables.GbreakCr or UnicodeTables.GbreakLf)
        {
            return true;
        }

        // Do not break Hangul syllable sequences.
        // GB6
        if (
            leftProp == UnicodeTables.GbreakL
            && rightProp
                is UnicodeTables.GbreakL
                    or UnicodeTables.GbreakV
                    or UnicodeTables.GbreakLv
                    or UnicodeTables.GbreakLvt
        )
        {
            return false;
        }

        // GB7
        if (
            leftProp is UnicodeTables.GbreakLv or UnicodeTables.GbreakV
            && rightProp is UnicodeTables.GbreakV or UnicodeTables.GbreakT
        )
        {
            return false;
        }

        // GB8
        if (leftProp is UnicodeTables.GbreakLvt or UnicodeTables.GbreakT && rightProp == UnicodeTables.GbreakT)
        {
            return false;
        }

        // Do not break before extending characters or ZWJ.
        // GB9
        if (rightProp is UnicodeTables.GbreakExtend or UnicodeTables.GbreakZwj)
        {
            return false;
        }

        // The GB9a and GB9b rules only apply to extended grapheme clusters: Do not break before
        // SpacingMarks, or after Prepend characters.
        // GB9a
        if (rightProp == UnicodeTables.GbreakSpacingmark)
        {
            return false;
        }

        // GB9b
        if (leftProp == UnicodeTables.GbreakPrepend)
        {
            return false;
        }

        // The GB9c rule only applies to extended grapheme clusters: Do not break within certain
        // combinations with Indic_Conjunct_Break (InCB)=Linker.
        // GB9c
        if (UnicodeTables.GetIndicConjunctBreak(rightChar) == UnicodeTables.IncbConsonant)
        {
            bool hasLinker = false;
            int pos = leftPos;

            // Upstream's do-while reads the character at 'pos' and only then tests the bound, so a
            // walk that steps off the start of the text stops without reading.
            while (true)
            {
                uint prop = UnicodeTables.GetIndicConjunctBreak(state.CharAt(pos));

                if (prop == UnicodeTables.IncbLinker)
                {
                    hasLinker = true;
                }
                else if (prop == UnicodeTables.IncbConsonant)
                {
                    if (hasLinker)
                    {
                        return false;
                    }

                    goto end_GB9c;
                }
                else if (prop != UnicodeTables.IncbExtend)
                {
                    goto end_GB9c;
                }

                pos = state.PrevPos(pos);

                if (pos < state.TextStart)
                {
                    break;
                }
            }
        }

        end_GB9c:
        // Do not break within emoji modifier sequences or emoji zwj sequences.
        // GB11
        if (leftProp == UnicodeTables.GbreakZwj && UnicodeTables.GetExtendedPictographic(rightChar) != 0)
        {
            int pos = state.PrevPos(leftPos);

            while (
                pos >= state.TextStart
                && UnicodeTables.GetGraphemeClusterBreak(state.CharAt(pos)) == UnicodeTables.GbreakExtend
            )
            {
                pos = state.PrevPos(pos);
            }

            if (pos >= state.TextStart && UnicodeTables.GetExtendedPictographic(state.CharAt(pos)) != 0)
            {
                return false;
            }
        }

        // The \p{Extended_Pictographic} values are provided as a part of the Emoji data in [UTS51].
        // Do not break within emoji flag sequences. That is, do not break between regional indicator
        // (RI) symbols if there is an odd number of RI characters before the break point.
        // GB12 and GB13
        if (
            rightProp == UnicodeTables.GbreakRegionalindicator
            && CountRegionalIndicatorsLeft(
                state,
                leftPos,
                UnicodeTables.GetGraphemeClusterBreak,
                UnicodeTables.GbreakRegionalindicator
            ) % 2
                == 1
        )
        {
            return false;
        }

        // Otherwise, break everywhere.
        // GB999
        return true;
    }

    /// <summary>Upstream <c>try_match_ANY</c> (<c>upstream/src/_regex.c</c> line 6919).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchAny(MatchState state, int textPos)
    {
        if (textPos >= state.TextEnd)
        {
            return state.PartialSide == MatchState.PartialRight ? MatchStatus.Partial : MatchStatus.Failure;
        }

        return MatchStatus.From(textPos < state.SliceEnd && MatchesAny(state.CharAt(textPos)));
    }

    /// <summary>Upstream <c>try_match_ANY_ALL</c> (line 6934).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchAnyAll(MatchState state, int textPos)
    {
        if (textPos >= state.TextEnd)
        {
            return state.PartialSide == MatchState.PartialRight ? MatchStatus.Partial : MatchStatus.Failure;
        }

        return MatchStatus.From(textPos < state.SliceEnd);
    }

    /// <summary>Upstream <c>try_match_ANY_U</c> (line 6978).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchAnyU(MatchState state, int textPos)
    {
        if (textPos >= state.TextEnd)
        {
            return state.PartialSide == MatchState.PartialRight ? MatchStatus.Partial : MatchStatus.Failure;
        }

        return MatchStatus.From(textPos < state.SliceEnd && MatchesAnyU(state.Encoding, state.CharAt(textPos)));
    }

    /// <summary>Upstream <c>try_match_BOUNDARY</c> (line 7010).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="node">The node.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchBoundary(MatchState state, Node node, int textPos) =>
        MatchStatus.From(AtBoundary(state, NodeEncoding(state.Encoding, node), textPos) == node.Match);

    /// <summary>
    /// Upstream <c>try_match_DEFAULT_BOUNDARY</c> (line 7087).
    /// </summary>
    /// <remarks>
    /// The ASCII and locale rows of upstream's encoding tables (lines 1008-1010 and 1345-1347) point
    /// all three <c>DEFAULT_</c> slots back at the plain word predicates - "no special default word
    /// boundary for ASCII" - so the dispatch that upstream does through a table of function pointers
    /// is a test of the encoding here.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="node">The node.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchDefaultBoundary(MatchState state, Node node, int textPos) =>
        MatchStatus.From(
            (
                state.Encoding == CaseEncoding.Ascii
                    ? AtBoundary(state, CaseEncoding.Ascii, textPos)
                    : AtDefaultBoundary(state, textPos)
            ) == node.Match
        );

    /// <summary>Upstream <c>try_match_DEFAULT_END_OF_WORD</c> (line 7094).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchDefaultEndOfWord(MatchState state, int textPos) =>
        MatchStatus.From(
            state.Encoding == CaseEncoding.Ascii
                ? AtWordEnd(state, CaseEncoding.Ascii, textPos)
                : AtDefaultWordStartOrEnd(state, textPos, false)
        );

    /// <summary>Upstream <c>try_match_DEFAULT_START_OF_WORD</c> (line 7101).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchDefaultStartOfWord(MatchState state, int textPos) =>
        MatchStatus.From(
            state.Encoding == CaseEncoding.Ascii
                ? AtWordStart(state, CaseEncoding.Ascii, textPos)
                : AtDefaultWordStartOrEnd(state, textPos, true)
        );

    /// <summary>Upstream <c>try_match_END_OF_WORD</c> (line 7141).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchEndOfWord(MatchState state, int textPos) =>
        MatchStatus.From(AtWordEnd(state, state.Encoding, textPos));

    /// <summary>Upstream <c>try_match_START_OF_WORD</c> (line 7376).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchStartOfWord(MatchState state, int textPos) =>
        MatchStatus.From(AtWordStart(state, state.Encoding, textPos));

    /// <summary>
    /// Upstream <c>try_match_GRAPHEME_BOUNDARY</c> (line 7147). The ASCII and locale encodings have
    /// no grapheme rules at all and point this slot at <c>at_boundary_always</c> (lines 1011 and
    /// 1348), which answers <see langword="true"/> everywhere.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchGraphemeBoundary(MatchState state, int textPos) =>
        MatchStatus.From(state.Encoding == CaseEncoding.Ascii || AtGraphemeBoundary(state, textPos));

    /// <summary>Upstream <c>try_match_END_OF_LINE</c> (line 7108).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchEndOfLine(MatchState state, int textPos) =>
        MatchStatus.From(textPos >= state.SliceEnd || state.CharAt(textPos) == '\n');

    /// <summary>Upstream <c>try_match_END_OF_LINE_U</c> (line 7115).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchEndOfLineU(MatchState state, int textPos) =>
        MatchStatus.From(AtLineEnd(state, textPos));

    /// <summary>Upstream <c>try_match_END_OF_STRING</c> (line 7121).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchEndOfString(MatchState state, int textPos) =>
        MatchStatus.From(textPos >= state.TextEnd);

    /// <summary>Upstream <c>try_match_END_OF_STRING_LINE</c> (line 7127).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchEndOfStringLine(MatchState state, int textPos) =>
        MatchStatus.From(textPos >= state.TextEnd || textPos == state.FinalNewline);

    /// <summary>Upstream <c>try_match_END_OF_STRING_LINE_U</c> (line 7134).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchEndOfStringLineU(MatchState state, int textPos) =>
        MatchStatus.From(textPos >= state.TextEnd || textPos == state.FinalLineSep);

    /// <summary>Upstream <c>try_match_START_OF_LINE</c> (line 7358).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchStartOfLine(MatchState state, int textPos) =>
        MatchStatus.From(textPos <= state.TextStart || state.CharBefore(textPos) == '\n');

    /// <summary>Upstream <c>try_match_START_OF_LINE_U</c> (line 7365).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchStartOfLineU(MatchState state, int textPos) =>
        MatchStatus.From(AtLineStart(state, textPos));

    /// <summary>Upstream <c>try_match_START_OF_STRING</c> (line 7371).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchStartOfString(MatchState state, int textPos) =>
        MatchStatus.From(textPos <= state.TextStart);

    /// <summary>
    /// Upstream <c>try_match_CHARACTER</c> (<c>upstream/src/_regex.c</c> line 7026),
    /// <c>try_match_PROPERTY</c> (<c>:7154</c>), <c>try_match_RANGE</c> (<c>:7220</c>) and
    /// <c>try_match_SET</c> (<c>:7292</c>), which are the same six lines with a different
    /// <c>matches_*</c> predicate - the switch <see cref="MatchesOne"/> already makes.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="node">The node.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchOne(MatchState state, Node node, int textPos)
    {
        if (textPos >= state.TextEnd)
        {
            return state.PartialSide == MatchState.PartialRight ? MatchStatus.Partial : MatchStatus.Failure;
        }

        return MatchStatus.From(
            textPos < state.SliceEnd && MatchesOne(state.Encoding, node, state.CharAt(textPos)) == node.Match
        );
    }

    /// <summary>
    /// Upstream <c>match_one</c> (<c>upstream/src/_regex.c</c> line 11373), reduced to the forward,
    /// case-sensitive opcodes this port matches.
    /// </summary>
    /// <remarks>
    /// Upstream's default arm answers <c>FALSE</c> for an opcode it has no <c>try_match_*</c> for.
    /// Here that would turn a construct a later slice delivers into a silent "no repeat here", so
    /// the leaf throws instead - the S07 rule. The only caller is the <c>LAZY_REPEAT_ONE</c>
    /// backtrack case, whose node is whatever <c>SequenceMatchesOne</c> accepted, so what is missing
    /// from the list is the case-insensitive (S22) and reverse (S23) halves.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="node">The node.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int MatchOne(MatchState state, Node node, int textPos) =>
        node.Op switch
        {
            Opcode.Any => TryMatchAny(state, textPos),
            Opcode.AnyAll => TryMatchAnyAll(state, textPos),
            Opcode.AnyU => TryMatchAnyU(state, textPos),
            Opcode.Character
            or Opcode.Property
            or Opcode.Range
            or Opcode.SetDiff
            or Opcode.SetInter
            or Opcode.SetSymDiff
            or Opcode.SetUnion => TryMatchOne(state, node, textPos),
            _ => throw Seam.For(node.Op),
        };

    /// <summary>Upstream <c>at_end</c> (<c>upstream/src/_regex.c</c> line 11628).</summary>
    /// <param name="state">The match state.</param>
    /// <returns><see langword="true"/> if matching has reached the far end of the slice.</returns>
    private static bool AtEnd(MatchState state) =>
        state.Reverse ? state.TextPos == state.SliceStart : state.TextPos == state.SliceEnd;

    /// <summary>Upstream <c>same_span</c> (<c>upstream/src/_regex.c</c> line 11634).</summary>
    /// <param name="span1">One span.</param>
    /// <param name="span2">The other.</param>
    /// <returns><see langword="true"/> if they are the same span.</returns>
    internal static bool SameSpan(GroupSpan span1, GroupSpan span2) =>
        span1.Start == span2.Start && span1.End == span2.End;

    /// <summary>Upstream <c>same_span_as_group</c> (line 11639).</summary>
    /// <remarks>
    /// This is where upstream distinguishes a group that matched nothing from one that did not match
    /// at all: a group whose <see cref="GroupData.Current"/> is negative took no part in the match,
    /// so no span of it can be the same as anything, where a group that matched empty has a real
    /// <c>(pos, pos)</c> span that can be.
    /// </remarks>
    /// <param name="group">The group.</param>
    /// <param name="span">The span to compare against its current capture.</param>
    /// <returns><see langword="true"/> if the group's current capture is that span.</returns>
    internal static bool SameSpanAsGroup(GroupData group, GroupSpan span) =>
        group.Current >= 0 && SameSpan(group.Captures[group.Current], span);

    /// <summary>
    /// Port of <c>RE_GroupStateData</c> (<c>upstream/src/_regex.c</c> lines 416-422): what
    /// <c>START_GROUP</c> and <c>END_GROUP</c> put on the backtracking stack so their own backtrack
    /// case can undo them.
    /// </summary>
    /// <param name="TextPos">
    /// The end of the span for <c>START_GROUP</c> and its start for <c>END_GROUP</c> - whichever the
    /// opcode took off the structure stack, so that backtracking can put it back.
    /// </param>
    /// <param name="Current">The group's <see cref="GroupData.Current"/> before the capture.</param>
    /// <param name="CaptureChange">The state's capture change counter before the capture.</param>
    /// <param name="PrivateIndex">The private group number.</param>
    /// <param name="PublicIndex">The public group number.</param>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct GroupStateData(
        int TextPos,
        int Current,
        long CaptureChange,
        int PrivateIndex,
        int PublicIndex
    );

    /// <summary>
    /// Upstream <c>push_captures</c> (<c>upstream/src/_regex.c</c> line 2513).
    /// </summary>
    /// <remarks>
    /// Only each group's <c>count</c> and <c>current</c> are saved, never the spans: a group's
    /// capture list is append-only within a match, so restoring the count discards everything written
    /// since.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="stack">The stack to push onto.</param>
    private static void PushCaptures(MatchState state, ByteStack stack)
    {
        foreach (GroupData group in state.Groups)
        {
            stack.PushSize(group.Count);
            stack.PushSize(group.Current);
        }
    }

    /// <summary>Upstream <c>pop_captures</c> (line 2685).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="stack">The stack to pop from.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    private static bool PopCaptures(MatchState state, ByteStack stack)
    {
        for (int g = state.Groups.Length - 1; g >= 0; g--)
        {
            if (!stack.PopSize(out long current) || !stack.PopSize(out long count))
            {
                return false;
            }

            state.Groups[g].Current = (int)current;
            state.Groups[g].Count = (int)count;
        }

        return true;
    }

    /// <summary>
    /// Upstream's <c>ByteStack_push_block(..., &amp;data_g, sizeof(data_g))</c>, field by field.
    /// </summary>
    /// <remarks>
    /// Upstream pushes the struct's bytes, padding and all; this pushes the five fields as five
    /// 8-byte words. The stack is internal to the engine and the only requirement on it is that the
    /// pop is the mirror image of the push, so the difference is 8 bytes of stack per group entry
    /// and nothing else.
    /// </remarks>
    /// <param name="stack">The backtracking stack.</param>
    /// <param name="data">What to push.</param>
    private static void PushGroupStateData(ByteStack stack, GroupStateData data)
    {
        stack.PushSize(data.TextPos);
        stack.PushSize(data.Current);
        stack.PushSize(data.CaptureChange);
        stack.PushSize(data.PrivateIndex);
        stack.PushSize(data.PublicIndex);
    }

    /// <summary>Upstream's matching <c>ByteStack_pop_block</c>.</summary>
    /// <param name="stack">The backtracking stack.</param>
    /// <param name="data">Receives what was pushed.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    private static bool PopGroupStateData(ByteStack stack, out GroupStateData data)
    {
        data = default;

        if (
            !stack.PopSize(out long publicIndex)
            || !stack.PopSize(out long privateIndex)
            || !stack.PopSize(out long captureChange)
            || !stack.PopSize(out long current)
            || !stack.PopSize(out long textPos)
        )
        {
            return false;
        }

        data = new GroupStateData((int)textPos, (int)current, captureChange, (int)privateIndex, (int)publicIndex);
        return true;
    }

    /// <summary>
    /// Port of <c>RE_BodyEndStateData</c> (<c>upstream/src/_regex.c</c> lines 433-438): what
    /// <c>END_GREEDY_REPEAT</c> and <c>END_LAZY_REPEAT</c> park so that backtracking into the body
    /// can restore the repeat to what it was before this iteration.
    /// </summary>
    /// <param name="Count">The repeat's count before this iteration.</param>
    /// <param name="Start">Where this iteration of the body started.</param>
    /// <param name="CaptureChange">The repeat's capture-change counter before this iteration.</param>
    /// <param name="Index">The repeat index.</param>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct BodyEndStateData(long Count, int Start, long CaptureChange, int Index);

    /// <summary>
    /// Port of <c>RE_RepeatStateData</c> (lines 440-446): what <c>GREEDY_REPEAT</c> and
    /// <c>LAZY_REPEAT</c> park so that backtracking out of the repeat altogether restores the
    /// enclosing one - the same repeat index is reused by every nesting level.
    /// </summary>
    /// <param name="Count">The enclosing repeat's count.</param>
    /// <param name="Start">The enclosing repeat's start.</param>
    /// <param name="CaptureChange">The enclosing repeat's capture-change counter.</param>
    /// <param name="Index">The repeat index.</param>
    /// <param name="TextPos">Where the repeat was entered.</param>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct RepeatStateData(long Count, int Start, long CaptureChange, int Index, int TextPos);

    /// <summary>
    /// Port of <c>RE_MatchBodyTailStateData</c> (lines 424-431): what a repeat parks when
    /// <b>both</b> its body and its tail could match, so that the loser of the two can still be
    /// tried before backtracking any further.
    /// </summary>
    /// <param name="Position">Where to resume - the node and the text position.</param>
    /// <param name="Count">The repeat's count to restore first.</param>
    /// <param name="Start">The repeat's start to restore first.</param>
    /// <param name="CaptureChange">The repeat's capture-change counter to restore first.</param>
    /// <param name="Index">The repeat index.</param>
    /// <param name="TextPos">The position the loser is being tried at, for its own guard.</param>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct MatchBodyTailStateData(
        Position Position,
        long Count,
        int Start,
        long CaptureChange,
        int Index,
        int TextPos
    );

    /// <summary>
    /// Port of <c>RE_RepeatOneStateData</c> (lines 448-453): what <c>GREEDY_REPEAT_ONE</c> and
    /// <c>LAZY_REPEAT_ONE</c> park so their backtrack case can walk the repeat one character at a
    /// time.
    /// </summary>
    /// <param name="Count">The enclosing repeat's count.</param>
    /// <param name="Start">The enclosing repeat's start.</param>
    /// <param name="Node">The repeat node itself, which the backtrack case resumes from.</param>
    /// <param name="Index">The repeat index.</param>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct RepeatOneStateData(long Count, int Start, Node Node, int Index);

    /// <summary>Upstream's <c>ByteStack_push_block(..., &amp;data_be, sizeof(data_be))</c>.</summary>
    /// <param name="stack">The backtracking stack.</param>
    /// <param name="data">What to push.</param>
    private static void PushBodyEndStateData(ByteStack stack, BodyEndStateData data)
    {
        stack.PushSize(data.Count);
        stack.PushSize(data.Start);
        stack.PushSize(data.CaptureChange);
        stack.PushSize(data.Index);
    }

    /// <summary>Upstream's matching <c>ByteStack_pop_block</c>.</summary>
    /// <param name="stack">The backtracking stack.</param>
    /// <param name="data">Receives what was pushed.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    private static bool PopBodyEndStateData(ByteStack stack, out BodyEndStateData data)
    {
        data = default;

        if (
            !stack.PopSize(out long index)
            || !stack.PopSize(out long captureChange)
            || !stack.PopSize(out long start)
            || !stack.PopSize(out long count)
        )
        {
            return false;
        }

        data = new BodyEndStateData(count, (int)start, captureChange, (int)index);
        return true;
    }

    /// <summary>Upstream's <c>ByteStack_push_block(..., &amp;data_r, sizeof(data_r))</c>.</summary>
    /// <param name="stack">The backtracking stack.</param>
    /// <param name="data">What to push.</param>
    private static void PushRepeatStateData(ByteStack stack, RepeatStateData data)
    {
        stack.PushSize(data.Count);
        stack.PushSize(data.Start);
        stack.PushSize(data.CaptureChange);
        stack.PushSize(data.Index);
        stack.PushSize(data.TextPos);
    }

    /// <summary>Upstream's matching <c>ByteStack_pop_block</c>.</summary>
    /// <param name="stack">The backtracking stack.</param>
    /// <param name="data">Receives what was pushed.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    private static bool PopRepeatStateData(ByteStack stack, out RepeatStateData data)
    {
        data = default;

        if (
            !stack.PopSize(out long textPos)
            || !stack.PopSize(out long index)
            || !stack.PopSize(out long captureChange)
            || !stack.PopSize(out long start)
            || !stack.PopSize(out long count)
        )
        {
            return false;
        }

        data = new RepeatStateData(count, (int)start, captureChange, (int)index, (int)textPos);
        return true;
    }

    /// <summary>Upstream's <c>ByteStack_push_block(..., &amp;data_mbt, sizeof(data_mbt))</c>.</summary>
    /// <param name="stack">The backtracking stack.</param>
    /// <param name="data">What to push.</param>
    private static void PushMatchBodyTailStateData(ByteStack stack, MatchBodyTailStateData data)
    {
        stack.PushNode(data.Position.Node);
        stack.PushSize(data.Position.TextPos);
        stack.PushSize(data.Count);
        stack.PushSize(data.Start);
        stack.PushSize(data.CaptureChange);
        stack.PushSize(data.Index);
        stack.PushSize(data.TextPos);
    }

    /// <summary>Upstream's matching <c>ByteStack_pop_block</c>.</summary>
    /// <param name="pattern">The pattern the parked node index is into.</param>
    /// <param name="stack">The backtracking stack.</param>
    /// <param name="data">Receives what was pushed.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    private static bool PopMatchBodyTailStateData(
        PatternObject pattern,
        ByteStack stack,
        out MatchBodyTailStateData data
    )
    {
        data = default;

        if (
            !stack.PopSize(out long textPos)
            || !stack.PopSize(out long index)
            || !stack.PopSize(out long captureChange)
            || !stack.PopSize(out long start)
            || !stack.PopSize(out long count)
            || !stack.PopSize(out long positionTextPos)
            || !stack.PopNode(pattern, out Node? positionNode)
        )
        {
            return false;
        }

        data = new MatchBodyTailStateData(
            new Position(positionNode!, (int)positionTextPos),
            count,
            (int)start,
            captureChange,
            (int)index,
            (int)textPos
        );
        return true;
    }

    /// <summary>Upstream's <c>ByteStack_push_block(..., &amp;data_ro, sizeof(data_ro))</c>.</summary>
    /// <param name="stack">The backtracking stack.</param>
    /// <param name="data">What to push.</param>
    private static void PushRepeatOneStateData(ByteStack stack, RepeatOneStateData data)
    {
        stack.PushSize(data.Count);
        stack.PushSize(data.Start);
        stack.PushNode(data.Node);
        stack.PushSize(data.Index);
    }

    /// <summary>Upstream's matching <c>ByteStack_pop_block</c>.</summary>
    /// <param name="pattern">The pattern the parked node index is into.</param>
    /// <param name="stack">The backtracking stack.</param>
    /// <param name="data">Receives what was pushed.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    private static bool PopRepeatOneStateData(PatternObject pattern, ByteStack stack, out RepeatOneStateData data)
    {
        data = default;

        if (
            !stack.PopSize(out long index)
            || !stack.PopNode(pattern, out Node? node)
            || !stack.PopSize(out long start)
            || !stack.PopSize(out long count)
        )
        {
            return false;
        }

        data = new RepeatOneStateData(count, (int)start, node!, (int)index);
        return true;
    }

    /// <summary>
    /// Upstream <c>try_match</c> (<c>upstream/src/_regex.c</c> line 7671), <b>reduced to its default
    /// arm</b> (<c>:7843</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream looks at <c>next-&gt;test</c>, asks that node's <c>try_match_*</c> predicate whether
    /// there is any point entering this branch or repeat body at all, and on success resumes at
    /// <c>next-&gt;match_next</c> with the tested character already consumed. That is the test-node
    /// fast path this port defers to Phase 7 (see the class remarks). Deferring it is semantically
    /// transparent: the default arm reports success and leaves the position alone, so the branch is
    /// entered and its first node is tested by the dispatch loop in the ordinary way. Where upstream
    /// would have refused the branch, this port enters it and backtracks straight back out - slower,
    /// same answer.
    /// </para>
    /// <para>
    /// <b>S19 ported the full version and reverted it</b>, because the exponential blowup it was
    /// meant to fix was not caused by this at all - see DECISIONS 2026-08-31. It measurably fixed
    /// nothing, and Phase 7 is where an optimisation gets made behind a benchmark.
    /// </para>
    /// </remarks>
    /// <param name="next">The exit to test.</param>
    /// <param name="textPos">The position to test at.</param>
    /// <param name="nextPosition">Where to continue from.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int TryMatch(NextNode next, int textPos, out Position nextPosition)
    {
        nextPosition = new Position(next.Node!, textPos);
        return MatchStatus.Success;
    }

    /// <summary>
    /// Upstream <c>safe_check_cancel</c> (<c>upstream/src/_regex.c</c> line 2266) less the
    /// <c>PyErr_CheckSignals</c> half, which is CPython's Ctrl-C handling and has no counterpart in
    /// a library call.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <returns><see langword="true"/> if matching should be abandoned.</returns>
    private static bool SafeCheckCancel(MatchState state) => state.CheckTimedOut();

    /// <summary>
    /// One codepoint along in the direction the pattern runs: upstream's
    /// <c>text_pos + pattern_step</c> and <c>text_pos += node-&gt;step</c>, which are codepoint
    /// counts where ours are UTF-16 code unit indices.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="pos">The position.</param>
    /// <param name="step">
    /// Upstream's step: 1 forwards, -1 backwards, and <b>0 for a zero-width node</b>. The third case
    /// is not theoretical: the optimiser hoists the first character test of a pattern that starts
    /// with an anchor in front of that anchor as a <c>CHARACTER</c> node carrying
    /// <c>RE_ZEROWIDTH_OP</c> and <c>step == 0</c>, so <c>'^a'</c> compiles to
    /// <c>CHARACTER(step 0) - START_OF_STRING - CHARACTER(step 1)</c>. Treating that as a step of
    /// one consumed the character before the anchor was tested, and every pattern with a leading
    /// anchor failed to match. Found by the differential oracle, 2026-08-31, 41 rows of 2400.
    /// </param>
    /// <returns>The stepped position.</returns>
    private static int Step(MatchState state, int pos, long step) =>
        step switch
        {
            0 => pos,
            > 0 => state.NextPos(pos),
            _ => state.PrevPos(pos),
        };

    /// <summary>
    /// Port of <c>basic_match</c> (<c>upstream/src/_regex.c</c> lines 11714-17403).
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="search">Whether to advance the start position until something matches.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int BasicMatch(MatchState state, bool search)
    {
        PatternObject pattern = state.Pattern;
        Node startNode = pattern.StartNode!;

        // Look beyond any initial group node.
        Node startTest = pattern.StartTest!;

        // Is the pattern anchored to the start or end of the string?
        switch (startTest.Op)
        {
            case Opcode.EndOfString:
                if (state.Reverse)
                {
                    // Searching backwards.
                    if (state.TextPos != state.TextEnd)
                    {
                        return MatchStatus.Failure;
                    }

                    // Don't bother to search further because it's anchored.
                    search = false;
                }

                break;
            case Opcode.StartOfString:
                if (!state.Reverse)
                {
                    // Searching forwards.
                    if (state.TextPos != state.TextStart)
                    {
                        return MatchStatus.Failure;
                    }

                    // Don't bother to search further because it's anchored.
                    search = false;
                }

                break;
            default:
                break;
        }

        long patternStep = state.Reverse ? -1 : 1;
        int stringPos = -1;
        state.FewestErrors = state.MaxErrors;

        // 'do_search_start' and the required-string locator are Phase 7 prefilters, so the search
        // takes the slow path that tries the pattern at every position - upstream's 'next_match_2'.
        Node node;
        int status;

        start_match:
        if (state.Iterations == 0 && SafeCheckCancel(state))
        {
            return MatchStatus.Cancelled;
        }

        state.Bstack.PushUInt8((byte)Opcode.Failure);
        state.Pstack.PushSize(state.Bstack.Count);

        /* sstack: -
         *
         * bstack: FAILURE
         *
         * pstack: bstack
         */

        // NOT PORTED: clearing the fuzzy counts (Phase 5) and the pattern-call guard list (Phase 4).

        // Locate the required string, if there's one: deferred to Phase 7, so the start position
        // stands.
        int foundPos = state.TextPos;

        if (search)
        {
            state.TextPos = foundPos;

            // Avoiding 'search_start', which is not ported.
            node = startNode;

            next_match_2:
            if (state.Reverse)
            {
                if (state.TextPos < state.SliceStart)
                {
                    return state.PartialSide == MatchState.PartialLeft ? MatchStatus.Partial : MatchStatus.Failure;
                }
            }
            else
            {
                if (state.TextPos > state.SliceEnd)
                {
                    return state.PartialSide == MatchState.PartialRight ? MatchStatus.Partial : MatchStatus.Failure;
                }
            }

            state.MatchPos = state.TextPos;

            if (node.Op == Opcode.Success)
            {
                // Must the match advance past its start?
                if (state.TextPos != state.SearchAnchor || !state.MustAdvance)
                {
                    bool succeeded =
                        !state.MatchAll
                        || (state.Reverse ? state.TextPos == state.SliceStart : state.TextPos == state.SliceEnd);

                    if (succeeded)
                    {
                        return MatchStatus.Success;
                    }
                }

                state.TextPos = Step(state, state.MatchPos, patternStep);
                goto next_match_2;
            }
        }
        else
        {
            // The start position is anchored to the current position.
            if (foundPos != state.TextPos)
            {
                return MatchStatus.Failure;
            }

            node = startNode;
        }

        advance:
        // The main matching loop.
        while (true)
        {
            // Should we abort the matching?
            state.Iterations = (ushort)(state.Iterations + 0x100);

            if (state.Iterations == 0 && SafeCheckCancel(state))
            {
                return MatchStatus.Cancelled;
            }

            switch (node.Op)
            {
                case Opcode.Any: // Any character except a newline.
                    status = TryMatchAny(state, state.TextPos);
                    if (status < 0)
                    {
                        return status;
                    }

                    if (status == MatchStatus.Success)
                    {
                        state.TextPos = state.NextPos(state.TextPos);
                        node = node.Next1.Node!;
                    }
                    else if ((node.Status & NodeStatus.Fuzzy) != 0)
                    {
                        throw Seam.For(Opcode.Fuzzy);
                    }
                    else
                    {
                        goto backtrack;
                    }

                    break;
                case Opcode.AnyAll: // Any character at all.
                    status = TryMatchAnyAll(state, state.TextPos);
                    if (status < 0)
                    {
                        return status;
                    }

                    if (status == MatchStatus.Success)
                    {
                        state.TextPos = state.NextPos(state.TextPos);
                        node = node.Next1.Node!;
                    }
                    else if ((node.Status & NodeStatus.Fuzzy) != 0)
                    {
                        throw Seam.For(Opcode.Fuzzy);
                    }
                    else
                    {
                        goto backtrack;
                    }

                    break;
                case Opcode.AnyU: // Any character except a line separator.
                    status = TryMatchAnyU(state, state.TextPos);
                    if (status < 0)
                    {
                        return status;
                    }

                    if (status == MatchStatus.Success)
                    {
                        state.TextPos = state.NextPos(state.TextPos);
                        node = node.Next1.Node!;
                    }
                    else if ((node.Status & NodeStatus.Fuzzy) != 0)
                    {
                        throw Seam.For(Opcode.Fuzzy);
                    }
                    else
                    {
                        goto backtrack;
                    }

                    break;
                case Opcode.Atomic: // Start of an atomic group.
                    PushCaptures(state, state.Bstack);

                    // NOT PORTED: push_fuzzy_counts (Phase 5). The pop is left out to match, so the
                    // block on the stack is the same shape at both ends.
                    state.Bstack.PushSize(state.CaptureChange);
                    state.Bstack.PushSize(state.Sstack.Count);
                    state.Bstack.PushUInt8((byte)Opcode.Atomic);
                    state.Pstack.PushSize(state.Bstack.Count);

                    /* bstack: captures capture_change sstack ATOMIC
                     *
                     * pstack: bstack
                     */

                    node = node.Next1.Node!;
                    break;
                case Opcode.EndAtomic: // End of an atomic group.
                {
                    /* sstack: ...
                     *
                     * bstack: captures capture_change sstack ATOMIC ...
                     *
                     * pstack: bstack
                     */

                    // Discarding everything the group pushed while it matched is what makes the group
                    // atomic: after this there is nothing left to backtrack into.
                    if (!state.Pstack.PopSize(out long atomicBstackCount))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.Bstack.Count = (int)atomicBstackCount;

                    if (!state.Bstack.Drop() || !state.Bstack.PopSize(out long atomicSstackCount))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.Sstack.Count = (int)atomicSstackCount;
                    state.Bstack.PushUInt8((byte)Opcode.EndAtomic);

                    /* bstack: captures capture_change END_ATOMIC
                     *
                     * pstack: -
                     */

                    node = node.Next1.Node!;
                    break;
                }
                case Opcode.Branch: // 2-way branch.
                {
                    status = TryMatch(node.Next1, state.TextPos, out Position nextPosition);
                    if (status < 0)
                    {
                        return status;
                    }

                    if (status == MatchStatus.Success)
                    {
                        state.Bstack.PushSize(state.TextPos);
                        state.Bstack.PushNode(node.Next2.Node!);
                        state.Bstack.PushUInt8((byte)Opcode.Branch);

                        /* bstack: text_pos node BRANCH */

                        node = nextPosition.Node;
                        state.TextPos = nextPosition.TextPos;
                    }
                    else
                    {
                        node = node.Next2.Node!;
                    }

                    break;
                }
                // END_GROUP (:12686) and START_GROUP (:14569). Upstream writes the two out
                // separately and they differ in exactly one expression: which end of the span the
                // opcode takes off the structure stack. Only one of the pair captures - build_GROUP
                // sets 'values[2]' on whichever node closes the group in the direction the pattern
                // runs (NodeCompiler, :968-969) - and the other just parks the position for it, so
                // in a forward match END_GROUP captures and in a reverse match START_GROUP does.
                case Opcode.EndGroup: // End of a capture group.
                case Opcode.StartGroup: // Start of a capture group.
                {
                    // Capture group indexes are 1-based (excluding group 0, which is the entire
                    // matched string).
                    int privateIndex = (int)node.Values[0];
                    int publicIndex = (int)node.Values[1];
                    GroupData group = state.Groups[privateIndex - 1];
                    bool capture = node.Values[2] != 0;

                    if (capture)
                    {
                        /* sstack: end (START_GROUP) or start (END_GROUP)
                         *
                         * bstack: -
                         */

                        if (!state.Sstack.PopSize(out long parked))
                        {
                            return MatchStatus.Illegal;
                        }

                        GroupSpan span =
                            node.Op == Opcode.StartGroup
                                ? new GroupSpan(state.TextPos, (int)parked)
                                : new GroupSpan((int)parked, state.TextPos);

                        PushGroupStateData(
                            state.Bstack,
                            new GroupStateData(
                                (int)parked,
                                group.Current,
                                state.CaptureChange,
                                privateIndex,
                                publicIndex
                            )
                        );

                        if (pattern.GroupInfoAt(privateIndex).Referenced && !SameSpanAsGroup(group, span))
                        {
                            ++state.CaptureChange;
                        }

                        state.SaveCapture(privateIndex, publicIndex, span);

                        // Upstream reads the count of the group it looked up by 'private_index'
                        // while save_capture appended to the one it looked up by 'public_index'
                        // (:9256). The two numbers differ only for a branch-reset group, so the
                        // asymmetry is kept rather than tidied - see MatchState.SaveCapture.
                        group.Current = group.Count - 1;
                    }
                    else
                    {
                        state.Sstack.PushSize(state.TextPos);
                    }

                    state.Bstack.PushBool(capture);
                    state.Bstack.PushUInt8((byte)node.Op);

                    /* If capturing:
                     *
                     * sstack: -
                     *
                     * bstack: parked current capture_change private_index public_index TRUE op
                     *
                     * else:
                     *
                     * sstack: text_pos
                     *
                     * bstack: FALSE op
                     */

                    node = node.Next1.Node!;
                    break;
                }
                case Opcode.EndGreedyRepeat: // End of a greedy repeat.
                {
                    // Repeat indexes are 0-based.
                    int index = (int)node.Values[0];
                    RepeatData rpData = state.Repeats[index];

                    // The body has matched successfully at this position.
                    state.GuardRepeat(index, rpData.Start, NodeStatus.Body, false);

                    ++rpData.Count;

                    // Have we advanced through the text or has a capture group change?
                    bool changed = rpData.CaptureChange != state.CaptureChange || state.TextPos != rpData.Start;

                    // Additional checks are needed if there's fuzzy matching. Unreachable in this
                    // slice: a fuzzy pattern throws in do_match_2 before it gets here.
                    if (changed && state.IsFuzzy && rpData.Count >= node.Values[1])
                    {
                        changed = !(
                            node.Step == 1 ? state.TextPos >= state.SliceEnd : state.TextPos <= state.SliceStart
                        );
                    }

                    // Could the body or tail match?
                    bool tryBody =
                        changed
                        && (rpData.Count < node.Values[2] || ~node.Values[2] == 0)
                        && !state.IsRepeatGuarded(index, state.TextPos, NodeStatus.Body);
                    int bodyStatus;
                    Position nextBodyPosition = default;

                    if (tryBody)
                    {
                        bodyStatus = TryMatch(node.Next1, state.TextPos, out nextBodyPosition);
                        if (bodyStatus < 0)
                        {
                            // Unreachable until Phase 7 gives 'try_match' its test-node arm back,
                            // which is the only thing that can answer PARTIAL here.
                            if (bodyStatus == MatchStatus.Partial && rpData.Count >= node.Values[1] && AtEnd(state))
                            {
                                bodyStatus = MatchStatus.Failure;
                            }
                            else
                            {
                                return bodyStatus;
                            }
                        }

                        if (bodyStatus == MatchStatus.Failure)
                        {
                            tryBody = false;
                        }
                    }
                    else
                    {
                        bodyStatus = MatchStatus.Failure;
                    }

                    bool tryTail =
                        (!changed || rpData.Count >= node.Values[1])
                        && !state.IsRepeatGuarded(index, state.TextPos, NodeStatus.Tail);
                    int tailStatus;
                    Position nextTailPosition = default;

                    if (tryTail)
                    {
                        tailStatus = TryMatch(node.Next2, state.TextPos, out nextTailPosition);
                        if (tailStatus < 0)
                        {
                            return tailStatus;
                        }

                        if (tailStatus == MatchStatus.Failure)
                        {
                            tryTail = false;
                        }
                    }
                    else
                    {
                        tailStatus = MatchStatus.Failure;
                    }

                    if (!tryBody && !tryTail)
                    {
                        // Neither the body nor the tail could match.
                        --rpData.Count;
                        goto backtrack;
                    }

                    if (bodyStatus < 0 || (bodyStatus == MatchStatus.Failure && tailStatus < 0))
                    {
                        return MatchStatus.Partial;
                    }

                    // Record info in case we backtrack into the body.
                    PushBodyEndStateData(
                        state.Bstack,
                        new BodyEndStateData(rpData.Count - 1, rpData.Start, rpData.CaptureChange, index)
                    );
                    state.Bstack.PushUInt8((byte)Opcode.BodyEnd);

                    /* bstack: count start capture_change index BODY_END */

                    if (tryBody)
                    {
                        if (tryTail)
                        {
                            // Both the body and the tail could match, but the body takes precedence.
                            // If the body fails to match then we want to try the tail before
                            // backtracking further.
                            PushMatchBodyTailStateData(
                                state.Bstack,
                                new MatchBodyTailStateData(
                                    nextTailPosition,
                                    rpData.Count,
                                    state.TextPos,
                                    state.CaptureChange,
                                    index,
                                    state.TextPos
                                )
                            );
                            state.Bstack.PushUInt8((byte)Opcode.MatchTail);

                            /* bstack: position count start capture_change index text_pos MATCH_TAIL */
                        }

                        // Record backtracking info in case the body fails to match.
                        state.Bstack.PushSize(index);
                        state.Bstack.PushSize(state.TextPos);
                        state.Bstack.PushUInt8((byte)Opcode.BodyStart);

                        /* bstack: index text_pos BODY_START */

                        rpData.CaptureChange = state.CaptureChange;
                        rpData.Start = state.TextPos;

                        // Advance into the body.
                        node = nextBodyPosition.Node;
                        state.TextPos = nextBodyPosition.TextPos;
                    }
                    else
                    {
                        // Only the tail could match.

                        // Record backtracking info in case the tail fails to match.
                        state.Bstack.PushSize(index);
                        state.Bstack.PushSize(state.TextPos);
                        state.Bstack.PushUInt8((byte)Opcode.TailStart);

                        /* bstack: index text_pos TAIL_START */

                        // Advance into the tail.
                        node = nextTailPosition.Node;
                        state.TextPos = nextTailPosition.TextPos;
                    }

                    break;
                }
                case Opcode.EndLazyRepeat: // End of a lazy repeat.
                {
                    // Repeat indexes are 0-based.
                    int index = (int)node.Values[0];
                    RepeatData rpData = state.Repeats[index];

                    // The body has matched successfully at this position.
                    state.GuardRepeat(index, rpData.Start, NodeStatus.Body, false);

                    ++rpData.Count;

                    // Have we advanced through the text or has a capture group change?
                    bool changed = rpData.CaptureChange != state.CaptureChange || state.TextPos != rpData.Start;

                    // Additional checks are needed if there's fuzzy matching. Unreachable in this
                    // slice, as in END_GREEDY_REPEAT above.
                    if (changed && state.IsFuzzy && rpData.Count >= node.Values[1])
                    {
                        changed = !(
                            node.Step == 1 ? state.TextPos >= state.SliceEnd : state.TextPos <= state.SliceStart
                        );
                    }

                    // Could the body or tail match?
                    bool tryBody =
                        changed
                        && (rpData.Count < node.Values[2] || ~node.Values[2] == 0)
                        && !state.IsRepeatGuarded(index, state.TextPos, NodeStatus.Body);
                    int bodyStatus;
                    Position nextBodyPosition = default;

                    if (tryBody)
                    {
                        // Upstream's END_GREEDY_REPEAT turns a PARTIAL here into a FAILURE when the
                        // minimum has been reached and the text has run out (:12575); this case does
                        // not, and the asymmetry is upstream's own.
                        bodyStatus = TryMatch(node.Next1, state.TextPos, out nextBodyPosition);
                        if (bodyStatus < 0)
                        {
                            return bodyStatus;
                        }

                        if (bodyStatus == MatchStatus.Failure)
                        {
                            tryBody = false;
                        }
                    }
                    else
                    {
                        bodyStatus = MatchStatus.Failure;
                    }

                    bool tryTail =
                        (!changed || rpData.Count >= node.Values[1])
                        && !state.IsRepeatGuarded(index, state.TextPos, NodeStatus.Tail);
                    int tailStatus;
                    Position nextTailPosition = default;

                    if (tryTail)
                    {
                        tailStatus = TryMatch(node.Next2, state.TextPos, out nextTailPosition);
                        if (tailStatus < 0)
                        {
                            return tailStatus;
                        }

                        if (tailStatus == MatchStatus.Failure)
                        {
                            tryTail = false;
                        }
                    }
                    else
                    {
                        tailStatus = MatchStatus.Failure;
                    }

                    if (!tryBody && !tryTail)
                    {
                        // Neither the body nor the tail could match.
                        --rpData.Count;
                        goto backtrack;
                    }

                    if (bodyStatus < 0 || (bodyStatus == MatchStatus.Failure && tailStatus < 0))
                    {
                        return MatchStatus.Partial;
                    }

                    // Record info in case we backtrack into the body.
                    PushBodyEndStateData(
                        state.Bstack,
                        new BodyEndStateData(rpData.Count - 1, rpData.Start, rpData.CaptureChange, index)
                    );
                    state.Bstack.PushUInt8((byte)Opcode.BodyEnd);

                    /* bstack: count start capture_change index BODY_END */

                    if (tryTail)
                    {
                        if (tryBody)
                        {
                            // Both the body and the tail could match, but the tail takes precedence.
                            // If the tail fails to match then we want to try the body before
                            // backtracking further.
                            PushMatchBodyTailStateData(
                                state.Bstack,
                                new MatchBodyTailStateData(
                                    nextBodyPosition,
                                    rpData.Count,
                                    state.TextPos,
                                    state.CaptureChange,
                                    index,
                                    state.TextPos
                                )
                            );
                            state.Bstack.PushUInt8((byte)Opcode.MatchBody);

                            /* bstack: position count start capture_change index text_pos MATCH_BODY */
                        }

                        // Record backtracking info in case the tail fails to match.
                        state.Bstack.PushSize(index);
                        state.Bstack.PushSize(state.TextPos);
                        state.Bstack.PushUInt8((byte)Opcode.TailStart);

                        /* bstack: index text_pos TAIL_START */

                        // Advance into the tail.
                        node = nextTailPosition.Node;
                        state.TextPos = nextTailPosition.TextPos;
                    }
                    else
                    {
                        // Only the body could match.

                        // Record backtracking info in case the body fails to match.
                        state.Bstack.PushSize(index);
                        state.Bstack.PushSize(state.TextPos);
                        state.Bstack.PushUInt8((byte)Opcode.BodyStart);

                        /* bstack: index text_pos BODY_START */

                        rpData.CaptureChange = state.CaptureChange;
                        rpData.Start = state.TextPos;

                        // Advance into the body.
                        node = nextBodyPosition.Node;
                        state.TextPos = nextBodyPosition.TextPos;
                    }

                    break;
                }
                // Upstream gives each of these its own case with the same eleven-line tail copied
                // out (:12968, :13804, :13914, :14446-14449), differing only in which 'matches_*'
                // predicate it calls. One case group with the predicate chosen by a switch says the
                // same thing, and each arm still maps one-for-one onto upstream's case.
                case Opcode.Character: // A character.
                case Opcode.Property: // A property.
                case Opcode.Range: // A range.
                case Opcode.SetDiff: // Set difference.
                case Opcode.SetInter: // Set intersection.
                case Opcode.SetSymDiff: // Set symmetric difference.
                case Opcode.SetUnion: // Set union.
                    if (state.TextPos >= state.TextEnd && state.PartialSide == MatchState.PartialRight)
                    {
                        return MatchStatus.Partial;
                    }

                    if (
                        state.TextPos < state.SliceEnd
                        && MatchesOne(state.Encoding, node, state.CharAt(state.TextPos)) == node.Match
                    )
                    {
                        state.TextPos = Step(state, state.TextPos, node.Step);
                        node = node.Next1.Node!;
                    }
                    else if ((node.Status & NodeStatus.Fuzzy) != 0)
                    {
                        throw Seam.For(Opcode.Fuzzy);
                    }
                    else
                    {
                        goto backtrack;
                    }

                    break;
                // Upstream gives each of these its own case with the same four-line tail copied out
                // (:12060, :12255, :12274, :12294, :13014, :13033, :13052, :13071, :13091, :13111,
                // :13157, :14431, :14643, :14662, :14681, :14700). One case group with the predicate
                // chosen by a switch says the same thing, and each arm still maps one-for-one onto
                // upstream's case.
                case Opcode.Boundary: // At a word boundary.
                case Opcode.DefaultBoundary: // At a default word boundary.
                case Opcode.DefaultEndOfWord: // At the default end of a word.
                case Opcode.DefaultStartOfWord: // At the default start of a word.
                case Opcode.EndOfLine: // At the end of a line.
                case Opcode.EndOfLineU: // At the end of a line.
                case Opcode.EndOfString: // At the end of the string.
                case Opcode.EndOfStringLine: // At the end of the string or the final newline.
                case Opcode.EndOfStringLineU: // At the end of the string or the final line separator.
                case Opcode.EndOfWord: // At the end of a word.
                case Opcode.GraphemeBoundary: // On a grapheme boundary.
                case Opcode.SearchAnchor: // At the start of the search.
                case Opcode.StartOfLine: // At the start of a line.
                case Opcode.StartOfLineU: // At the start of a line.
                case Opcode.StartOfString: // At the start of the string.
                case Opcode.StartOfWord: // At the start of a word.
                    status = node.Op switch
                    {
                        Opcode.Boundary => TryMatchBoundary(state, node, state.TextPos),
                        Opcode.DefaultBoundary => TryMatchDefaultBoundary(state, node, state.TextPos),
                        Opcode.DefaultEndOfWord => TryMatchDefaultEndOfWord(state, state.TextPos),
                        Opcode.DefaultStartOfWord => TryMatchDefaultStartOfWord(state, state.TextPos),
                        Opcode.EndOfLine => TryMatchEndOfLine(state, state.TextPos),
                        Opcode.EndOfLineU => TryMatchEndOfLineU(state, state.TextPos),
                        Opcode.EndOfString => TryMatchEndOfString(state, state.TextPos),
                        Opcode.EndOfStringLine => TryMatchEndOfStringLine(state, state.TextPos),
                        Opcode.EndOfStringLineU => TryMatchEndOfStringLineU(state, state.TextPos),
                        Opcode.EndOfWord => TryMatchEndOfWord(state, state.TextPos),
                        Opcode.GraphemeBoundary => TryMatchGraphemeBoundary(state, state.TextPos),
                        Opcode.SearchAnchor => MatchStatus.From(state.TextPos == state.SearchAnchor),
                        Opcode.StartOfLine => TryMatchStartOfLine(state, state.TextPos),
                        Opcode.StartOfLineU => TryMatchStartOfLineU(state, state.TextPos),
                        Opcode.StartOfWord => TryMatchStartOfWord(state, state.TextPos),
                        _ => TryMatchStartOfString(state, state.TextPos),
                    };

                    if (status < 0)
                    {
                        return status;
                    }

                    if (status == MatchStatus.Success)
                    {
                        node = node.Next1.Node!;
                    }
                    else if ((node.Status & NodeStatus.Fuzzy) != 0)
                    {
                        throw Seam.For(Opcode.Fuzzy);
                    }
                    else
                    {
                        goto backtrack;
                    }

                    break;
                case Opcode.Failure: // Failure.
                    goto backtrack;
                case Opcode.GreedyRepeat: // Greedy repeat.
                {
                    // Repeat indexes are 0-based.
                    int index = (int)node.Values[0];
                    RepeatData rpData = state.Repeats[index];

                    // We might need to backtrack into the head, so save the current repeat.
                    PushRepeatStateData(
                        state.Bstack,
                        new RepeatStateData(rpData.Count, rpData.Start, rpData.CaptureChange, index, state.TextPos)
                    );
                    state.Bstack.PushUInt8((byte)Opcode.GreedyRepeat);

                    /* bstack: count start capture_change index text_pos GREEDY_REPEAT */

                    // Initialise the new repeat.
                    rpData.Count = 0;
                    rpData.Start = state.TextPos;
                    rpData.CaptureChange = state.CaptureChange;

                    // Could the body or tail match?
                    bool tryBody = node.Values[2] > 0 && !state.IsRepeatGuarded(index, state.TextPos, NodeStatus.Body);
                    int bodyStatus;
                    Position nextBodyPosition = default;

                    if (tryBody)
                    {
                        bodyStatus = TryMatch(node.Next1, state.TextPos, out nextBodyPosition);
                        if (bodyStatus < 0)
                        {
                            return bodyStatus;
                        }

                        if (bodyStatus == MatchStatus.Failure)
                        {
                            tryBody = false;
                        }
                    }
                    else
                    {
                        bodyStatus = MatchStatus.Failure;
                    }

                    bool tryTail = node.Values[1] == 0;
                    int tailStatus;
                    Position nextTailPosition = default;

                    if (tryTail)
                    {
                        tailStatus = TryMatch(node.Next2, state.TextPos, out nextTailPosition);
                        if (tailStatus < 0)
                        {
                            return tailStatus;
                        }

                        if (tailStatus == MatchStatus.Failure)
                        {
                            tryTail = false;
                        }
                    }
                    else
                    {
                        tailStatus = MatchStatus.Failure;
                    }

                    if (!tryBody && !tryTail)
                    {
                        // Neither the body nor the tail could match.
                        goto backtrack;
                    }

                    if (bodyStatus < 0 || (bodyStatus == MatchStatus.Failure && tailStatus < 0))
                    {
                        return MatchStatus.Partial;
                    }

                    if (tryBody)
                    {
                        if (tryTail)
                        {
                            // Both the body and the tail could match, but the body takes precedence.
                            // If the body fails to match then we want to try the tail before
                            // backtracking further.
                            //
                            // Upstream parks the *repeat's* start and capture change here
                            // (:13265-13266), where END_GREEDY_REPEAT parks the state's text_pos and
                            // capture change (:12635-12636). The repeat was reinitialised three
                            // statements ago, so the two agree on 'start'; the asymmetry is
                            // upstream's own and is kept.
                            PushMatchBodyTailStateData(
                                state.Bstack,
                                new MatchBodyTailStateData(
                                    nextTailPosition,
                                    rpData.Count,
                                    rpData.Start,
                                    rpData.CaptureChange,
                                    index,
                                    state.TextPos
                                )
                            );
                            state.Bstack.PushUInt8((byte)Opcode.MatchTail);

                            /* bstack: position count start capture_change index text_pos MATCH_TAIL */
                        }

                        // Record backtracking info in case the body fails to match.
                        state.Bstack.PushSize(index);
                        state.Bstack.PushSize(state.TextPos);
                        state.Bstack.PushUInt8((byte)Opcode.BodyStart);

                        /* bstack: index text_pos BODY_START */

                        // Advance into the body.
                        node = nextBodyPosition.Node;
                        state.TextPos = nextBodyPosition.TextPos;
                    }
                    else
                    {
                        // Only the tail could match.

                        // Record backtracking info in case the tail fails to match.
                        state.Bstack.PushSize(index);
                        state.Bstack.PushSize(state.TextPos);
                        state.Bstack.PushUInt8((byte)Opcode.TailStart);

                        /* bstack: index text_pos TAIL_START */

                        // Advance into the tail.
                        node = nextTailPosition.Node;
                        state.TextPos = nextTailPosition.TextPos;
                    }

                    break;
                }
                case Opcode.GreedyRepeatOne: // Greedy repeat for one character.
                {
                    // Repeat indexes are 0-based.
                    int index = (int)node.Values[0];
                    RepeatData rpData = state.Repeats[index];

                    if (state.IsRepeatGuarded(index, state.TextPos, NodeStatus.Body))
                    {
                        goto backtrack;
                    }

                    // Count how many times the character repeats, up to the maximum.
                    long count = CountOne(
                        state,
                        node.Next2.Node!,
                        state.TextPos,
                        node.Values[2],
                        out bool isPartial,
                        out int pos
                    );
                    if (isPartial)
                    {
                        state.TextPos = pos;
                        return MatchStatus.Partial;
                    }

                    // Unmatch until it's not guarded. 'pos' is upstream's
                    // 'state->text_pos + (Py_ssize_t)count * node->step', stepped back a character
                    // at a time as the count comes down rather than recomputed by multiplication.
                    bool match = false;
                    while (true)
                    {
                        if (count < node.Values[1])
                        {
                            // The number of repeats is below the minimum.
                            break;
                        }

                        if (!state.IsRepeatGuarded(index, pos, NodeStatus.Tail))
                        {
                            // It's not guarded at this position.
                            match = true;
                            break;
                        }

                        if (count == 0)
                        {
                            break;
                        }

                        --count;
                        pos = Step(state, pos, -node.Step);
                    }

                    if (!match)
                    {
                        // The repeat has failed to match at this position.
                        state.GuardRepeat(index, state.TextPos, NodeStatus.Body, true);
                        goto backtrack;
                    }

                    if (count > node.Values[1])
                    {
                        // Record the backtracking info.
                        PushRepeatOneStateData(
                            state.Bstack,
                            new RepeatOneStateData(rpData.Count, rpData.Start, node, index)
                        );
                        state.Bstack.PushUInt8((byte)Opcode.GreedyRepeatOne);

                        /* bstack: count start node index GREEDY_REPEAT_ONE */

                        rpData.Start = state.TextPos;
                        rpData.Count = count;
                    }

                    // Advance into the tail.
                    state.TextPos = pos;
                    node = node.Next1.Node!;
                    break;
                }
                // GROUP_EXISTS (:13442). Nothing is pushed to the backtracking stack: the opcode
                // only picks an exit, and both exits are reachable from the enclosing structure's
                // own backtracking, so there is nothing here to undo.
                case Opcode.GroupExists: // Capture group exists.
                {
                    // Capture group indexes are 1-based (excluding group 0, which is the entire
                    // matched string).
                    //
                    // Check whether the captured text, if any, exists at this position in the
                    // string.
                    //
                    // A group index of 0, however, means that it's a DEFINE, which we should skip.
                    int groupExistsIndex = (int)node.Values[0];

                    if (groupExistsIndex == 0)
                    {
                        // Skip past the body.
                        node = node.Next2.Node!;
                    }
                    else
                    {
                        // 'current' indexes the capture this attempt is holding, and is -1 until the
                        // group captures and again after backtracking past it, so this is "matched
                        // so far in this attempt" and not "exists in the pattern".
                        GroupData groupExistsGroup = state.Groups[groupExistsIndex - 1];

                        node =
                            groupExistsGroup.Current >= 0
                                ? node.Next1.Node! // The 'true' branch.
                                : node.Next2.Node!; // The 'false' branch.
                    }

                    break;
                }
                case Opcode.Keep: // Keep.
                    state.Bstack.PushSize(state.MatchPos);
                    state.Bstack.PushUInt8((byte)Opcode.Keep);

                    /* bstack: match_pos KEEP */

                    state.MatchPos = state.TextPos;

                    node = node.Next1.Node!;
                    break;
                case Opcode.LazyRepeat: // Lazy repeat.
                {
                    // Repeat indexes are 0-based.
                    int index = (int)node.Values[0];
                    RepeatData rpData = state.Repeats[index];

                    // We might need to backtrack into the head, so save the current repeat.
                    PushRepeatStateData(
                        state.Bstack,
                        new RepeatStateData(rpData.Count, rpData.Start, rpData.CaptureChange, index, state.TextPos)
                    );
                    state.Bstack.PushUInt8((byte)Opcode.LazyRepeat);

                    /* bstack: count start capture_change index text_pos LAZY_REPEAT */

                    // Initialise the new repeat.
                    rpData.Count = 0;
                    rpData.Start = state.TextPos;
                    rpData.CaptureChange = state.CaptureChange;

                    // Could the body or tail match?
                    bool tryBody = node.Values[2] > 0 && !state.IsRepeatGuarded(index, state.TextPos, NodeStatus.Body);
                    int bodyStatus;
                    Position nextBodyPosition = default;

                    if (tryBody)
                    {
                        bodyStatus = TryMatch(node.Next1, state.TextPos, out nextBodyPosition);
                        if (bodyStatus < 0)
                        {
                            return bodyStatus;
                        }

                        if (bodyStatus == MatchStatus.Failure)
                        {
                            tryBody = false;
                        }
                    }
                    else
                    {
                        bodyStatus = MatchStatus.Failure;
                    }

                    bool tryTail = node.Values[1] == 0;
                    int tailStatus;
                    Position nextTailPosition = default;

                    if (tryTail)
                    {
                        tailStatus = TryMatch(node.Next2, state.TextPos, out nextTailPosition);
                        if (tailStatus < 0)
                        {
                            return tailStatus;
                        }

                        if (tailStatus == MatchStatus.Failure)
                        {
                            tryTail = false;
                        }
                    }
                    else
                    {
                        tailStatus = MatchStatus.Failure;
                    }

                    if (!tryBody && !tryTail)
                    {
                        // Neither the body nor the tail could match.
                        goto backtrack;
                    }

                    if (bodyStatus < 0 || (bodyStatus == MatchStatus.Failure && tailStatus < 0))
                    {
                        return MatchStatus.Partial;
                    }

                    if (tryTail)
                    {
                        if (tryBody)
                        {
                            // Both the body and the tail could match, but the tail takes precedence.
                            // If the tail fails to match then we want to try the body before
                            // backtracking further.
                            PushMatchBodyTailStateData(
                                state.Bstack,
                                new MatchBodyTailStateData(
                                    nextBodyPosition,
                                    rpData.Count,
                                    rpData.Start,
                                    rpData.CaptureChange,
                                    index,
                                    state.TextPos
                                )
                            );
                            state.Bstack.PushUInt8((byte)Opcode.MatchBody);

                            /* bstack: position count start capture_change index text_pos MATCH_BODY */
                        }

                        // Record backtracking info in case the tail fails to match.
                        state.Bstack.PushSize(index);
                        state.Bstack.PushSize(state.TextPos);
                        state.Bstack.PushUInt8((byte)Opcode.TailStart);

                        /* bstack: index text_pos TAIL_START */

                        // Advance into the tail.
                        node = nextTailPosition.Node;
                        state.TextPos = nextTailPosition.TextPos;
                    }
                    else
                    {
                        // Only the body could match.

                        // Record backtracking info in case the body fails to match.
                        state.Bstack.PushSize(index);
                        state.Bstack.PushSize(state.TextPos);
                        state.Bstack.PushUInt8((byte)Opcode.BodyStart);

                        /* bstack: index text_pos BODY_START */

                        // Advance into the body.
                        node = nextBodyPosition.Node;
                        state.TextPos = nextBodyPosition.TextPos;
                    }

                    break;
                }
                case Opcode.LazyRepeatOne: // Lazy repeat for one character.
                {
                    // Repeat indexes are 0-based.
                    int index = (int)node.Values[0];
                    RepeatData rpData = state.Repeats[index];

                    if (state.IsRepeatGuarded(index, state.TextPos, NodeStatus.Body))
                    {
                        goto backtrack;
                    }

                    // Count how many times the character repeats, up to the minimum.
                    long count = CountOne(
                        state,
                        node.Next2.Node!,
                        state.TextPos,
                        node.Values[1],
                        out bool isPartial,
                        out int pos
                    );
                    if (isPartial)
                    {
                        state.TextPos = pos;
                        return MatchStatus.Partial;
                    }

                    // Have we matched at least the minimum?
                    if (count < node.Values[1])
                    {
                        // The repeat has failed to match at this position.
                        state.GuardRepeat(index, state.TextPos, NodeStatus.Body, true);
                        goto backtrack;
                    }

                    if (count < node.Values[2])
                    {
                        // The match is shorter than the maximum, so we might need to backtrack the
                        // repeat to consume more.
                        PushRepeatOneStateData(
                            state.Bstack,
                            new RepeatOneStateData(rpData.Count, rpData.Start, node, index)
                        );
                        state.Bstack.PushUInt8((byte)Opcode.LazyRepeatOne);

                        /* bstack: count start node index LAZY_REPEAT_ONE */

                        rpData.Start = state.TextPos;
                        rpData.Count = count;
                    }

                    // Advance into the tail.
                    state.TextPos = pos;
                    node = node.Next1.Node!;
                    break;
                }
                // REF_GROUP (:14004). Like STRING, it pushes nothing to the backtracking stack: the
                // only state it carries between visits is 'stringPos', which the fuzzy retry block
                // (:17269) reads and which Phase 5 will need a backtrack arm for.
                case Opcode.RefGroup: // Reference to a capture group.
                {
                    // Capture group indexes are 1-based (excluding group 0, which is the entire
                    // matched string).
                    //
                    // Check whether the captured text, if any, exists at this position in the
                    // string.

                    // Did the group capture anything?
                    GroupData refGroup = state.Groups[(int)node.Values[0] - 1];
                    if (refGroup.Current < 0)
                    {
                        // A reference to a group that has not captured fails; it does not match
                        // empty. A group that captured an empty span has 'current' >= 0 and an
                        // empty span, so it falls through and matches empty.
                        goto backtrack;
                    }

                    GroupSpan span = refGroup.Captures[refGroup.Current];

                    if (stringPos < 0)
                    {
                        stringPos = span.Start;
                    }

                    // Try comparing. Upstream's '++string_pos' and '++state->text_pos' are one
                    // codepoint each, so both walk with 'NextPos' here - the S16 stepping - and an
                    // astral character advances both by two code units, leaving 'stringPos' exactly
                    // on 'span.End'.
                    //
                    // A plain '++' on both is in fact indistinguishable here, and deliberately not
                    // used: measured 2026-08-31, it agreed on all 600 rows of the S21 negative
                    // control and on the whole ported suite. Both operands index the *same* string,
                    // so a code-unit walk stays in lockstep with a codepoint walk - it just tests
                    // each surrogate half separately and reaches the same end. Kept as 'NextPos'
                    // because upstream's '++' means one character, every other opcode in this port
                    // spells that 'NextPos', and Phase 5's fuzzy retry moves 'stringPos' on its own,
                    // where the lockstep argument no longer holds.
                    while (stringPos < span.End)
                    {
                        if (state.TextPos >= state.TextEnd && state.PartialSide == MatchState.PartialRight)
                        {
                            return MatchStatus.Partial;
                        }

                        if (
                            state.TextPos < state.SliceEnd
                            && SameChar(state.CharAt(state.TextPos), state.CharAt(stringPos))
                        )
                        {
                            stringPos = state.NextPos(stringPos);
                            state.TextPos = state.NextPos(state.TextPos);
                        }
                        else if ((node.Status & NodeStatus.Fuzzy) != 0)
                        {
                            throw Seam.For(Opcode.Fuzzy);
                        }
                        else
                        {
                            stringPos = -1;
                            goto backtrack;
                        }
                    }

                    stringPos = -1;

                    // Successful match.
                    node = node.Next1.Node!;
                    break;
                }
                case Opcode.String: // A string.
                {
                    if ((node.Status & NodeStatus.Required) != 0 && state.TextPos == state.ReqPos && stringPos < 0)
                    {
                        // Unreachable until Phase 7 ports the required-string locator, which is the
                        // only thing that sets 'req_pos'.
                        state.TextPos = state.ReqEnd;
                    }
                    else
                    {
                        int length = node.Values.Count;

                        if (stringPos < 0)
                        {
                            stringPos = 0;
                        }

                        // Try comparing.
                        while (stringPos < length)
                        {
                            if (state.TextPos >= state.TextEnd && state.PartialSide == MatchState.PartialRight)
                            {
                                return MatchStatus.Partial;
                            }

                            if (
                                state.TextPos < state.SliceEnd
                                && SameChar(state.CharAt(state.TextPos), node.Values[stringPos])
                            )
                            {
                                ++stringPos;
                                state.TextPos = state.NextPos(state.TextPos);
                            }
                            else if ((node.Status & NodeStatus.Fuzzy) != 0)
                            {
                                throw Seam.For(Opcode.Fuzzy);
                            }
                            else
                            {
                                stringPos = -1;
                                goto backtrack;
                            }
                        }
                    }

                    if ((node.Status & NodeStatus.Fuzzy) != 0)
                    {
                        throw Seam.For(Opcode.Fuzzy);
                    }

                    stringPos = -1;

                    // Successful match.
                    node = node.Next1.Node!;
                    break;
                }
                case Opcode.Success: // Success.
                    // Must the match advance past its start?
                    if (state.TextPos == state.SearchAnchor && state.MustAdvance)
                    {
                        goto backtrack;
                    }

                    if (state.MatchAll)
                    {
                        // We want to match all of the slice.
                        if (state.Reverse)
                        {
                            if (state.TextPos != state.SliceStart)
                            {
                                goto backtrack;
                            }
                        }
                        else if (state.TextPos != state.SliceEnd)
                        {
                            goto backtrack;
                        }
                    }

                    if ((pattern.Flags & RegexFlags.Posix) != 0)
                    {
                        // If we're looking for a POSIX match, check whether this one is better and
                        // then keep looking.
                        throw Seam.For("posix-matching", "POSIX leftmost-longest matching is not implemented yet");
                    }

                    return MatchStatus.Success;
                default:
                    throw Seam.For(node.Op);
            }
        }

        backtrack:
        while (true)
        {
            // Should we abort the matching?
            state.Iterations = (ushort)(state.Iterations + 0x100);

            if (state.Iterations == 0 && SafeCheckCancel(state))
            {
                return MatchStatus.Cancelled;
            }

            if (!state.Bstack.PopUInt8(out byte op))
            {
                return MatchStatus.Illegal;
            }

            switch ((Opcode)op)
            {
                case Opcode.Atomic: // Start of an atomic group.
                {
                    /* sstack: ...
                     *
                     * bstack: captures capture_change sstack
                     *
                     * pstack: bstack
                     */

                    if (!state.Pstack.DropSize() || !state.Bstack.PopSize(out long atomicSstackCount))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.Sstack.Count = (int)atomicSstackCount;

                    if (!state.Bstack.PopSize(out long atomicCaptureChange))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.CaptureChange = atomicCaptureChange;

                    // NOT PORTED: pop_fuzzy_counts (Phase 5), matching the push.
                    if (!PopCaptures(state, state.Bstack))
                    {
                        return MatchStatus.Illegal;
                    }

                    break;
                }
                case Opcode.EndAtomic: // End of an atomic group.
                {
                    /* bstack: captures capture_change */

                    if (!state.Bstack.PopSize(out long endAtomicCaptureChange))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.CaptureChange = endAtomicCaptureChange;

                    // NOT PORTED: pop_fuzzy_counts (Phase 5), matching the push.
                    if (!PopCaptures(state, state.Bstack))
                    {
                        return MatchStatus.Illegal;
                    }

                    break;
                }
                case Opcode.Keep: // Keep.
                {
                    /* bstack: match_pos */

                    if (!state.Bstack.PopSize(out long keepMatchPos))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.MatchPos = (int)keepMatchPos;
                    break;
                }
                case Opcode.BodyEnd:
                {
                    /* bstack: count start capture_change index */

                    if (!PopBodyEndStateData(state.Bstack, out BodyEndStateData dataBe))
                    {
                        return MatchStatus.Illegal;
                    }

                    // We're backtracking into the body.
                    RepeatData rpData = state.Repeats[dataBe.Index];

                    // Restore the repeat info.
                    rpData.Count = dataBe.Count;
                    rpData.Start = dataBe.Start;
                    rpData.CaptureChange = dataBe.CaptureChange;
                    break;
                }
                case Opcode.BodyStart:
                {
                    /* bstack: index text_pos */

                    if (!state.Bstack.PopSize(out long bodyTextPos) || !state.Bstack.PopSize(out long bodyIndex))
                    {
                        return MatchStatus.Illegal;
                    }

                    // The body may have failed to match at this position.
                    state.GuardRepeat((int)bodyIndex, (int)bodyTextPos, NodeStatus.Body, true);
                    break;
                }
                case Opcode.Branch: // 2-way branch.
                {
                    /* sstack: -
                     *
                     * bstack: text_pos node
                     */

                    if (!state.Bstack.PopNode(pattern, out Node? other) || !state.Bstack.PopSize(out long textPos))
                    {
                        return MatchStatus.Illegal;
                    }

                    node = other!;
                    state.TextPos = (int)textPos;
                    goto advance;
                }
                // END_GROUP (:15596) and START_GROUP (:17307). Upstream writes the two out
                // separately and their bodies are identical: the position going back onto the
                // structure stack is the group's start in one and its end in the other, and putting
                // it back is the same act either way.
                case Opcode.EndGroup: // End of a capture group.
                case Opcode.StartGroup: // Start of a capture group.
                {
                    /* If capturing:
                     *
                     * sstack: -
                     *
                     * bstack: parked current capture_change private_index public_index TRUE
                     *
                     * else:
                     *
                     * sstack: text_pos
                     *
                     * bstack: FALSE
                     */

                    if (!state.Bstack.PopBool(out bool capture))
                    {
                        return MatchStatus.Illegal;
                    }

                    if (capture)
                    {
                        if (!PopGroupStateData(state.Bstack, out GroupStateData data))
                        {
                            return MatchStatus.Illegal;
                        }

                        state.CaptureChange = data.CaptureChange;
                        state.Groups[data.PrivateIndex - 1].Current = data.Current;
                        state.Sstack.PushSize(data.TextPos);

                        state.UnsaveCapture(data.PrivateIndex, data.PublicIndex);

                        /* sstack: parked
                         *
                         * bstack: -
                         */
                    }
                    else if (!state.Sstack.DropSize())
                    {
                        return MatchStatus.Illegal;
                    }

                    break;
                }
                case Opcode.Failure: // Failure.
                    // Have we been looking for a POSIX match?
                    if (state.FoundMatch)
                    {
                        // Unreachable: the SUCCESS case throws before this can be set.
                        throw Seam.For("posix-matching", "POSIX leftmost-longest matching is not implemented yet");
                    }

                    // Do we have to advance?
                    if (!search)
                    {
                        return MatchStatus.Failure;
                    }

                    // Can we advance?
                    state.TextPos = state.MatchPos;

                    if (state.Reverse)
                    {
                        if (state.TextPos <= state.SliceStart)
                        {
                            return MatchStatus.Failure;
                        }
                    }
                    else if (state.TextPos >= state.SliceEnd)
                    {
                        return MatchStatus.Failure;
                    }

                    // Skip over any repeated leading characters.
                    if (startNode.Op is Opcode.GreedyRepeatOne or Opcode.LazyRepeatOne)
                    {
                        // How many characters did the repeat actually match?
                        long skipCount = CountOne(
                            state,
                            startNode.Next2.Node!,
                            state.TextPos,
                            startNode.Values[2],
                            out bool _,
                            out int skipEnd
                        );

                        // If it's fewer than the maximum then skip over those characters. Upstream's
                        // 'state->text_pos += (Py_ssize_t)count * pattern_step' is the position
                        // CountOne walked to: the pattern step and the repeated node's direction
                        // agree here, because the repeat is the pattern's own start node, and
                        // CountOne throws for a reverse node in any case (S23).
                        if (skipCount < startNode.Values[2])
                        {
                            state.TextPos = skipEnd;
                        }
                    }

                    // Advance and try to match again. We also need to check whether we need to skip.
                    if (state.Reverse)
                    {
                        state.TextPos = state.TextPos > state.SliceEnd ? state.SliceEnd : state.PrevPos(state.TextPos);
                    }
                    else
                    {
                        state.TextPos =
                            state.TextPos < state.SliceStart ? state.SliceStart : state.NextPos(state.TextPos);
                    }

                    // Clear the groups.
                    state.ClearGroups();

                    // Reset the guards.
                    state.ResetGuards();

                    // Reset the stacks.
                    state.Sstack.Reset();
                    state.Bstack.Reset();
                    state.Pstack.Reset();
                    goto start_match;
                // GREEDY_REPEAT (:15778) and LAZY_REPEAT (:15779), which upstream gives one body:
                // the repeat failed, so the enclosing repeat's state goes back and the position it
                // was entered at is guarded against the body being tried there again.
                case Opcode.GreedyRepeat: // Greedy repeat.
                case Opcode.LazyRepeat: // Lazy repeat.
                {
                    /* bstack: count start capture_change index text_pos */

                    if (!PopRepeatStateData(state.Bstack, out RepeatStateData dataR))
                    {
                        return MatchStatus.Illegal;
                    }

                    // The repeat failed to match.
                    RepeatData rpData = state.Repeats[dataR.Index];

                    // The body may have failed to match at this position.
                    state.GuardRepeat(dataR.Index, dataR.TextPos, NodeStatus.Body, true);

                    // Restore the previous repeat.
                    rpData.Count = dataR.Count;
                    rpData.Start = dataR.Start;
                    rpData.CaptureChange = dataR.CaptureChange;
                    break;
                }
                case Opcode.GreedyRepeatOne: // Greedy repeat for one character.
                {
                    /* bstack: count start node index */

                    if (!PopRepeatOneStateData(pattern, state.Bstack, out RepeatOneStateData dataRo))
                    {
                        return MatchStatus.Illegal;
                    }

                    int index = dataRo.Index;
                    node = dataRo.Node;
                    int start = dataRo.Start;
                    long savedCount = dataRo.Count;

                    RepeatData rpData = state.Repeats[index];

                    // Unmatch one character at a time until the tail could match or we have reached
                    // the minimum.
                    state.TextPos = rpData.Start;

                    long count = rpData.Count;
                    long step = node.Next2.Test!.Step;
                    int pos = StepBy(state, state.TextPos, count, step);
                    int limit = StepBy(state, state.TextPos, node.Values[1], step);

                    // The tail failed to match at this position.
                    state.GuardRepeat(index, pos, NodeStatus.Tail, true);

                    // A (*SKIP) might have changed the size of the slice.
                    if (step > 0)
                    {
                        if (limit < state.SliceStart)
                        {
                            limit = state.SliceStart;
                        }
                    }
                    else if (limit > state.SliceEnd)
                    {
                        limit = state.SliceEnd;
                    }

                    if (pos == limit)
                    {
                        // We've backtracked the repeat as far as we can.
                        rpData.Start = start;
                        rpData.Count = savedCount;
                        break;
                    }

                    Node test = node.Next1.Test!;

                    if ((test.Status & NodeStatus.Fuzzy) != 0)
                    {
                        // Upstream's fuzzy retreat loop (:15881).
                        throw Seam.For(Opcode.Fuzzy);
                    }

                    // Upstream follows this with a switch on 'test->op' whose CHARACTER,
                    // CHARACTER_IGN, CHARACTER_IGN_REV, CHARACTER_REV, STRING, STRING_FLD,
                    // STRING_FLD_REV, STRING_IGN, STRING_IGN_REV and STRING_REV arms
                    // (:15907-16270) are optimisations of the default arm below: "a repeated
                    // single-character match is often followed by a literal, so checking specially
                    // for it can be a good optimisation when working with long strings". Only the
                    // default arm (:16271) is ported, for two reasons. The string arms are built on
                    // 'string_search_rev', which is the Phase 7 deferral (DECISIONS 2026-08-31); and
                    // the character arms consult 'test' themselves, which is exactly the test-node
                    // fast path this port already defers, so porting them here would half-deliver
                    // it. The default arm retreats one character and re-enters the dispatch loop,
                    // which tests the tail node in the ordinary way and comes straight back here if
                    // it fails - slower, same answer.
                    //
                    // One thing the string arms do that is not an optimisation: they answer PARTIAL
                    // for a partial string match at the retreated position. Partial matching is not
                    // delivered yet, and a partial-match test that reaches here should be tagged
                    // 'needs:partial' rather than treated as this deferral's fault.
                    bool match = false;

                    while (true)
                    {
                        pos = Step(state, pos, -step);

                        status = TryMatch(node.Next1, pos, out _);
                        if (status < 0)
                        {
                            return status;
                        }

                        if (status == MatchStatus.Success && !state.IsRepeatGuarded(index, pos, NodeStatus.Tail))
                        {
                            match = true;
                            break;
                        }

                        if (pos == limit)
                        {
                            break;
                        }
                    }

                    if (match)
                    {
                        count = CountBetween(state, pos, state.TextPos);

                        // The tail could match.
                        if (count > node.Values[1])
                        {
                            // The match is longer than the minimum, so we might need to backtrack the
                            // repeat again to consume less.
                            rpData.Count = count;

                            PushRepeatOneStateData(
                                state.Bstack,
                                new RepeatOneStateData(savedCount, start, node, index)
                            );
                            state.Bstack.PushUInt8((byte)Opcode.GreedyRepeatOne);

                            /* bstack: count start node index GREEDY_REPEAT_ONE */
                        }
                        else
                        {
                            // We've reached or passed the minimum, so we won't need to backtrack the
                            // repeat again.
                            rpData.Start = start;
                            rpData.Count = savedCount;

                            // Have we passed the minimum?
                            if (count < node.Values[1])
                            {
                                goto backtrack;
                            }
                        }

                        node = node.Next1.Node!;
                        state.TextPos = pos;
                        goto advance;
                    }

                    // Don't try this repeated match again.
                    if (step > 0)
                    {
                        state.GuardRepeatRange(index, limit, pos, NodeStatus.Body, true);
                    }
                    else if (step < 0)
                    {
                        state.GuardRepeatRange(index, pos, limit, NodeStatus.Body, true);
                    }

                    // We've backtracked the repeat as far as we can.
                    rpData.Start = start;
                    rpData.Count = savedCount;
                    break;
                }
                case Opcode.LazyRepeatOne: // Lazy repeat for one character.
                {
                    /* bstack: count start node index */

                    if (!PopRepeatOneStateData(pattern, state.Bstack, out RepeatOneStateData data))
                    {
                        return MatchStatus.Illegal;
                    }

                    int index = data.Index;
                    node = data.Node;
                    int start = data.Start;
                    long savedCount = data.Count;

                    RepeatData rpData = state.Repeats[index];

                    // Match one character at a time until the tail could match or we have reached
                    // the maximum.
                    state.TextPos = rpData.Start;
                    long count = rpData.Count;

                    long step = node.Next2.Test!.Step;
                    int pos = StepBy(state, state.TextPos, count, step);
                    long available =
                        step > 0
                            ? CountBetween(state, state.TextPos, state.SliceEnd)
                            : CountBetween(state, state.SliceStart, state.TextPos);
                    long maxCount = Math.Min(available, node.Values[2]);
                    int limit = StepBy(state, state.TextPos, maxCount, step);

                    Node repeated = node.Next2.Node!;
                    Node test = node.Next1.Test!;

                    if ((test.Status & NodeStatus.Fuzzy) != 0)
                    {
                        // Upstream's fuzzy advance loop (:16500).
                        throw Seam.For(Opcode.Fuzzy);
                    }

                    // Only upstream's default arm (:17024) is ported, for the reasons given in the
                    // GREEDY_REPEAT_ONE case above. Upstream's 'skip_pos' goes with the string arms
                    // that are not ported - they are the only thing that sets it, so its
                    // '-1' branch is all that is left and it does nothing.
                    bool match = false;

                    while (true)
                    {
                        status = MatchOne(state, repeated, pos);
                        if (status < 0)
                        {
                            return status;
                        }

                        if (status == MatchStatus.Failure)
                        {
                            break;
                        }

                        pos = Step(state, pos, step);

                        status = TryMatch(node.Next1, pos, out _);
                        if (status < 0)
                        {
                            // Upstream returns RE_ERROR_PARTIAL here rather than 'status', where the
                            // GREEDY_REPEAT_ONE arm above returns 'status' (:17040 against :16280).
                            // Ported as written; unreachable either way until Phase 7 restores
                            // try_match's test-node arm.
                            return MatchStatus.Partial;
                        }

                        if (status == MatchStatus.Success && !state.IsRepeatGuarded(index, pos, NodeStatus.Tail))
                        {
                            match = true;
                            break;
                        }

                        if (pos == limit)
                        {
                            break;
                        }
                    }

                    if (match)
                    {
                        // The tail could match.
                        count = CountBetween(state, pos, state.TextPos);
                        state.TextPos = pos;

                        if (count < maxCount)
                        {
                            // The match is shorter than the maximum, so we might need to backtrack
                            // the repeat again to consume more.
                            rpData.Count = count;

                            PushRepeatOneStateData(
                                state.Bstack,
                                new RepeatOneStateData(savedCount, start, node, index)
                            );
                            state.Bstack.PushUInt8((byte)Opcode.LazyRepeatOne);

                            /* bstack: count start node index LAZY_REPEAT_ONE */
                        }
                        else
                        {
                            // We've reached or passed the maximum, so we won't need to backtrack the
                            // repeat again.
                            rpData.Start = start;
                            rpData.Count = savedCount;

                            // Have we passed the maximum?
                            if (count > maxCount)
                            {
                                goto backtrack;
                            }
                        }

                        node = node.Next1.Node!;
                        goto advance;
                    }

                    // The tail couldn't match.
                    rpData.Start = start;
                    rpData.Count = savedCount;
                    break;
                }
                case Opcode.MatchBody:
                {
                    /* bstack: position count start capture_change index text_pos */

                    if (!PopMatchBodyTailStateData(pattern, state.Bstack, out MatchBodyTailStateData dataMbt))
                    {
                        return MatchStatus.Illegal;
                    }

                    // We want to match the body.
                    RepeatData rpData = state.Repeats[dataMbt.Index];

                    // Restore the repeat info.
                    rpData.Count = dataMbt.Count;
                    rpData.Start = dataMbt.Start;
                    rpData.CaptureChange = dataMbt.CaptureChange;

                    // Record backtracking info in case the body fails to match.
                    state.Bstack.PushSize(dataMbt.Index);
                    state.Bstack.PushSize(dataMbt.TextPos);
                    state.Bstack.PushUInt8((byte)Opcode.BodyStart);

                    /* bstack: index text_pos BODY_START */

                    // Advance into the body.
                    node = dataMbt.Position.Node;
                    state.TextPos = dataMbt.Position.TextPos;
                    goto advance;
                }
                case Opcode.MatchTail:
                {
                    /* bstack: position count start capture_change index text_pos */

                    if (!PopMatchBodyTailStateData(pattern, state.Bstack, out MatchBodyTailStateData dataMbt))
                    {
                        return MatchStatus.Illegal;
                    }

                    // We want to match the tail.
                    RepeatData rpData = state.Repeats[dataMbt.Index];

                    // Restore the repeat info.
                    rpData.Count = dataMbt.Count;
                    rpData.Start = dataMbt.Start;
                    rpData.CaptureChange = dataMbt.CaptureChange;

                    // Record backtracking info in case the tail fails to match.
                    state.Bstack.PushSize(dataMbt.Index);
                    state.Bstack.PushSize(dataMbt.TextPos);
                    state.Bstack.PushUInt8((byte)Opcode.TailStart);

                    /* bstack: index text_pos TAIL_START */

                    // Advance into the tail.
                    node = dataMbt.Position.Node;
                    state.TextPos = dataMbt.Position.TextPos;
                    goto advance;
                }
                case Opcode.TailStart:
                {
                    /* bstack: index text_pos */

                    if (!state.Bstack.PopSize(out long tailTextPos) || !state.Bstack.PopSize(out long tailIndex))
                    {
                        return MatchStatus.Illegal;
                    }

                    // The tail may have failed to match at this position.
                    state.GuardRepeat((int)tailIndex, (int)tailTextPos, NodeStatus.Tail, true);
                    break;
                }
                default:
                    // Nothing else is ever pushed by the opcodes ported so far. CHARACTER, STRING,
                    // the ANY family and S17's PROPERTY, RANGE and SET_* only put themselves on the
                    // backtracking stack to retry a fuzzy match (upstream's shared one-character
                    // block, :15210-15243, is nothing but 'retry_fuzzy_match_item'), which the
                    // dispatch loop refuses above; BRANCH, START_GROUP, END_GROUP and S19's
                    // BODY_END, BODY_START, GREEDY_REPEAT, LAZY_REPEAT, GREEDY_REPEAT_ONE,
                    // LAZY_REPEAT_ONE, MATCH_BODY, MATCH_TAIL and TAIL_START have their own cases
                    // here.
                    throw Seam.For((Opcode)op);
            }
        }
    }

    /// <summary>
    /// Upstream <c>do_exact_match</c> (<c>upstream/src/_regex.c</c> line 18064).
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="search">Whether to search rather than anchor at the start position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int DoExactMatch(MatchState state, bool search)
    {
        // Upstream counts codepoints and this counts UTF-16 code units, of which there are never
        // fewer, so this early-out stays sound: 'units < min_width' implies 'codepoints <
        // min_width'. Where a surrogate pair makes the two differ, the engine simply does the work
        // and fails in the dispatch loop instead - the same answer, and only for astral subjects.
        int available = state.Reverse ? state.TextPos - state.SliceStart : state.SliceEnd - state.TextPos;

        // The maximum permitted cost.
        state.MaxErrors = 0;

        // NOT PORTED: best_match_pos / best_text_pos, which only the POSIX and BESTMATCH paths read.

        // Initialise the state.
        state.InitMatch();

        // An exact match, and partial matches not permitted. Upstream nests the width check inside
        // the "exact and not partial" one; one condition says the same thing.
        int status =
            state.MaxErrors == 0
            && state.PartialSide == MatchState.PartialNone
            && (available < state.MinWidth || (available == 0 && state.MustAdvance))
                ? MatchStatus.Failure
                : MatchStatus.Success;

        if (status == MatchStatus.Success)
        {
            status = BasicMatch(state, search);
        }

        return status;
    }

    /// <summary>
    /// Upstream <c>do_match_2</c> (<c>upstream/src/_regex.c</c> line 18099): the fuzzy strategies
    /// are seams until Phase 5.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="search">Whether to search rather than anchor at the start position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int DoMatch2(MatchState state, bool search)
    {
        PatternObject pattern = state.Pattern;

        if (!pattern.IsFuzzy)
        {
            return DoExactMatch(state, search);
        }

        if ((pattern.Flags & RegexFlags.BestMatch) != 0)
        {
            // Upstream do_best_fuzzy_match (:17584).
            throw Seam.For("fuzzy-bestmatch", "BESTMATCH fuzzy matching is not implemented yet");
        }

        if ((pattern.Flags & RegexFlags.EnhanceMatch) != 0)
        {
            // Upstream do_enhanced_fuzzy_match (:17862).
            throw Seam.For("fuzzy-enhancematch", "ENHANCEMATCH fuzzy matching is not implemented yet");
        }

        // Upstream do_simple_fuzzy_match (:18027).
        throw Seam.For("fuzzy-matching", "fuzzy matching is not implemented yet");
    }

    /// <summary>
    /// Upstream <c>do_match</c> (<c>upstream/src/_regex.c</c> line 18121), less the GIL handling.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="search">Whether to search rather than anchor at the start position.</param>
    /// <returns>
    /// A <see cref="MatchStatus"/>. <see cref="MatchStatus.Cancelled"/> means the timeout expired;
    /// the caller turns that into an exception, because it is the caller that has the pattern text
    /// the exception carries.
    /// </returns>
    internal static int DoMatch(MatchState state, bool search)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Is there enough to search?
        if (state.Reverse)
        {
            if (state.TextPos < state.SliceStart)
            {
                return MatchStatus.Failure;
            }
        }
        else if (state.TextPos > state.SliceEnd)
        {
            return MatchStatus.Failure;
        }

        int status;

        // Perform a normal match, but fall back to a partial match if requested.
        if (state.PartialSide == MatchState.PartialNone)
        {
            // Normal match.
            status = DoMatch2(state, search);
        }
        else
        {
            int partialSide = state.PartialSide;
            int textPos = state.TextPos;

            // Try a normal match first.
            state.PartialSide = MatchState.PartialNone;

            status = DoMatch2(state, search);

            state.PartialSide = partialSide;

            if (status == MatchStatus.Failure)
            {
                // Fall back to the partial match as originally requested.
                state.TextPos = textPos;
                status = DoMatch2(state, search);
            }
        }

        if (status is MatchStatus.Success or MatchStatus.Partial)
        {
            // Store the results.
            state.LastIndex = -1;
            state.LastGroup = -1;
            long maxEndIndex = -1;

            if (status == MatchStatus.Partial)
            {
                // We've matched up to the limit of the slice.
                state.TextPos = state.Reverse ? state.SliceStart : state.SliceEnd;
            }

            // Store the capture groups. 'lastindex' is not the highest-numbered group that took
            // part but the one that closed last, which is why 'end_index' is consulted rather than
            // the group number: `regex.match('((a))', 'a').lastindex` is 1, not 2.
            PatternObject pattern = state.Pattern;

            for (int g = 0; g < pattern.PublicGroupCount; g++)
            {
                GroupInfo info = pattern.GroupInfoAt(g + 1);

                if (state.Groups[g].Current >= 0 && info.EndIndex > maxEndIndex)
                {
                    maxEndIndex = info.EndIndex;
                    state.LastIndex = g + 1;
                    if (info.HasName)
                    {
                        state.LastGroup = g + 1;
                    }
                }
            }
        }

        // Upstream's tail here is `if (status < 0 && status != RE_ERROR_PARTIAL) set_error(status)`,
        // whose default arm is "internal error in regular expression engine". Cancellation is the
        // caller's to raise; anything else negative is a bug in this engine, not an answer.
        if (status is < 0 and not (MatchStatus.Partial or MatchStatus.Cancelled))
        {
            throw new InvalidOperationException($"internal error in regular expression engine (status {status})");
        }

        return status;
    }
}
