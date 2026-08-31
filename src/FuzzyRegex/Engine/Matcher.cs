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
            or Opcode.StringFld
            or Opcode.StringIgn => "ignore-case",

            Opcode.Branch => "alternation",

            Opcode.BodyEnd
            or Opcode.BodyStart
            or Opcode.EndGreedyRepeat
            or Opcode.EndLazyRepeat
            or Opcode.GreedyRepeat
            or Opcode.GreedyRepeatOne
            or Opcode.LazyRepeat
            or Opcode.LazyRepeatOne
            or Opcode.MatchBody
            or Opcode.MatchTail
            or Opcode.TailStart => "quantifiers",

            Opcode.EndGroup or Opcode.Group or Opcode.StartGroup => "groups",

            Opcode.RefGroup or Opcode.RefGroupFld or Opcode.RefGroupIgn => "backrefs",

            Opcode.Conditional or Opcode.EndConditional or Opcode.GroupExists => "conditionals",

            Opcode.CallRef or Opcode.GroupCall or Opcode.GroupReturn => "recursion",

            Opcode.EndLookaround or Opcode.Lookaround => "lookaround",

            Opcode.Atomic or Opcode.EndAtomic => "atomic",

            Opcode.EndFuzzy or Opcode.Fuzzy or Opcode.FuzzyExt or Opcode.FuzzyInsert => "fuzzy-matching",

            Opcode.Boundary
            or Opcode.DefaultBoundary
            or Opcode.DefaultEndOfWord
            or Opcode.DefaultStartOfWord
            or Opcode.EndOfWord
            or Opcode.StartOfWord => "word-flag",

            Opcode.GraphemeBoundary => "grapheme",

            Opcode.Keep => "keep-marker",

            Opcode.Prune or Opcode.Skip => "backtracking-verbs",

            // Every remaining opcode either has a case above in the dispatch switch or never
            // reaches the matcher at all (END, NEXT, and the values-only words).
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
/// <c>:6919-7686</c>). Without them the search tries the pattern at every position, which is slower
/// and answers the same.
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

        // Upstream's 'advance:' label is not here: the only jumps to it come from the backtrack
        // cases that retry a fuzzy match, and every one of those throws in this slice. S18 and S19
        // reintroduce it with their first real backtrack case; C# rejects an unreferenced label.

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
                // (:13014, :13033, :13052, :13071, :13091, :14431, :14643, :14662, :14681). One
                // case group with the predicate chosen by a switch says the same thing, and each
                // arm still maps one-for-one onto upstream's case.
                case Opcode.EndOfLine: // At the end of a line.
                case Opcode.EndOfLineU: // At the end of a line.
                case Opcode.EndOfString: // At the end of the string.
                case Opcode.EndOfStringLine: // At the end of the string or the final newline.
                case Opcode.EndOfStringLineU: // At the end of the string or the final line separator.
                case Opcode.SearchAnchor: // At the start of the search.
                case Opcode.StartOfLine: // At the start of a line.
                case Opcode.StartOfLineU: // At the start of a line.
                case Opcode.StartOfString: // At the start of the string.
                    status = node.Op switch
                    {
                        Opcode.EndOfLine => TryMatchEndOfLine(state, state.TextPos),
                        Opcode.EndOfLineU => TryMatchEndOfLineU(state, state.TextPos),
                        Opcode.EndOfString => TryMatchEndOfString(state, state.TextPos),
                        Opcode.EndOfStringLine => TryMatchEndOfStringLine(state, state.TextPos),
                        Opcode.EndOfStringLineU => TryMatchEndOfStringLineU(state, state.TextPos),
                        Opcode.SearchAnchor => MatchStatus.From(state.TextPos == state.SearchAnchor),
                        Opcode.StartOfLine => TryMatchStartOfLine(state, state.TextPos),
                        Opcode.StartOfLineU => TryMatchStartOfLineU(state, state.TextPos),
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

                    // Skip over any repeated leading characters. Unreachable in this slice: a
                    // pattern whose start node is a repeat throws at that opcode above.
                    if (startNode.Op is Opcode.GreedyRepeatOne or Opcode.LazyRepeatOne)
                    {
                        throw Seam.For(startNode.Op);
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

                    // NOT PORTED: clear_groups (:3369) and reset_guards (:3383). There are no
                    // group spans until S18 and no repeat guards until S19, and a pattern needing
                    // either throws at its own opcode before it can reach here.

                    // Reset the stacks.
                    state.Sstack.Reset();
                    state.Bstack.Reset();
                    state.Pstack.Reset();
                    goto start_match;
                default:
                    // Nothing else is ever pushed by the opcodes ported so far: CHARACTER, STRING,
                    // the ANY family and S17's PROPERTY, RANGE and SET_* only put themselves on the
                    // backtracking stack to retry a fuzzy match (upstream's shared one-character
                    // block, :15210-15243, is nothing but 'retry_fuzzy_match_item'), which the
                    // dispatch loop refuses above.
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

            if (status == MatchStatus.Partial)
            {
                // We've matched up to the limit of the slice.
                state.TextPos = state.Reverse ? state.SliceStart : state.SliceEnd;
            }

            // NOT PORTED: the loop over the capture groups that sets 'lastindex' and 'lastgroup'.
            // There are no group spans to read until S18, and any pattern with a group throws at
            // START_GROUP before it gets here.
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
