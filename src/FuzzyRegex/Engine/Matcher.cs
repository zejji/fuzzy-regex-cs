using Fuzzy.Text.RegularExpressions.Parsing;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// The status codes the matcher passes around. Port of the <c>RE_ERROR_*</c> defines
/// (<c>upstream/src/_regex.c</c> lines 103-122), reduced to the ones this port can produce: the rest
/// describe CPython argument errors that our own signatures make unrepresentable.
/// </summary>
/// <summary>
/// What one attempt at a fuzzy error is working on. Port of <c>RE_FuzzyData</c>
/// (<c>upstream/src/_regex.c</c> lines 677-690), reduced to the fields the one-character and
/// zero-width items use.
/// </summary>
/// <remarks>
/// <para>
/// NOT PORTED: <c>limit</c>. <c>fuzzy_match_item</c> writes it (<c>:10203</c>, <c>:10206</c>) and
/// nothing anywhere reads it - <c>grep -n 'limit' upstream/src/_regex.c | grep 'data[.-]'</c> on
/// 2026-09-13 gives those two lines and nothing else. The string, folded and group fields
/// (<c>new_string_pos</c>, <c>new_folded_pos</c>, <c>folded_len</c>, <c>new_gfolded_pos</c>,
/// <c>new_group_pos</c>) arrive with the string and backreference arms in S39, which is where they
/// first have a reader.
/// </para>
/// <para>
/// A struct passed by <c>ref</c>, because upstream passes <c>RE_FuzzyData*</c> and
/// <c>next_fuzzy_match_item</c> writes back through it. At namespace scope rather than nested inside
/// <see cref="Matcher"/>, which is where upstream declares it too.
/// </para>
/// </remarks>
internal struct FuzzyData
{
    /// <summary>Upstream <c>new_node</c>: where matching carries on if this error is taken.</summary>
    internal Node? NewNode;

    /// <summary>Upstream <c>new_text_pos</c>, a UTF-16 code unit index.</summary>
    internal int NewTextPos;

    /// <summary>
    /// Upstream <c>fuzzy_type</c>: which error is being tried, and after a successful call which one
    /// was taken. An <c>int</c> rather than a byte because the retry loop starts it at
    /// <c>fuzzy_type + 1</c>.
    /// </summary>
    internal int FuzzyType;

    /// <summary>
    /// Upstream <c>step</c>: which way the item travels, as a <b>character</b> step of <c>1</c>,
    /// <c>-1</c> or <c>0</c>. Never used as an offset directly, because one character is one or two
    /// code units here.
    /// </summary>
    internal sbyte Step;

    /// <summary>Upstream <c>permit_insertion</c>.</summary>
    internal bool PermitInsertion;

    /// <summary>
    /// Upstream <c>new_string_pos</c>: how far into the item the comparison has got. For a
    /// <c>STRING*</c> node that is an index into the node's values; for a <c>REF_GROUP*</c> node it
    /// is a position in the subject, which is what <see cref="StringPosIsText"/> distinguishes.
    /// </summary>
    internal int NewStringPos;

    /// <summary>
    /// Not upstream's. In C a subject position and a values index are both codepoint counts, so
    /// <c>data-&gt;new_string_pos += step</c> serves for either; here a subject position is a UTF-16
    /// code unit index and has to move by <see cref="Matcher.Step"/> instead.
    /// </summary>
    internal bool StringPosIsText;

    /// <summary>Upstream <c>new_folded_pos</c>: how far into the subject's folding it has got.</summary>
    internal int NewFoldedPos;

    /// <summary>Upstream <c>folded_len</c>: the length of that folding.</summary>
    internal int FoldedLen;

    /// <summary>
    /// Upstream <c>new_gfolded_pos</c>: how far into the referenced group's own folding it has got.
    /// Only <c>REF_GROUP_FLD</c> and <c>REF_GROUP_FLD_REV</c> have two foldings to keep apart.
    /// </summary>
    internal int NewGfoldedPos;
}

/// <summary>
/// Upstream <c>RE_BestEntry</c> (<c>upstream/src/_regex.c</c> line 692): one equal-best candidate a
/// <c>BESTMATCH</c> search found, as the span it covered.
/// </summary>
/// <remarks>
/// NOT PORTED, because <see cref="List{T}"/> already is them: <c>RE_BestList</c> (<c>:697</c>) and its
/// <c>init_best_list</c>, <c>fini_best_list</c>, <c>clear_best_list</c> and <c>add_to_best_list</c>
/// (<c>:17532-17583</c>), which are a hand-rolled growable array and the four calls that manage it.
/// A collection expression, going out of scope, <see cref="List{T}.Clear"/> and
/// <see cref="List{T}.Add"/> are the same four operations, and PORTMAP row 428 says so.
/// </remarks>
/// <param name="MatchPos">Where the candidate match started.</param>
/// <param name="TextPos">Where it ended.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct BestEntry(int MatchPos, int TextPos);

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
            // S19 delivered the whole GREEDY_REPEAT / LAZY_REPEAT / *_REPEAT_ONE family and the
            // BODY_*, MATCH_* and TAIL_START backtrack markers, so 'quantifiers' has no arm here -
            // naming a delivered tag would put a capability the status board says we have on an
            // oracle 'unsupported' row and in a stack trace (the S17 blind review found that).
            // S21 delivered REF_GROUP and GROUP_EXISTS, so 'backrefs' has no arm here at all and
            // 'conditionals' covers only CONDITIONAL - the lookaround-condition form - which stays
            // Phase 4's. S22 delivered every forward _IGN and _FLD opcode - CHARACTER_IGN,
            // PROPERTY_IGN, RANGE_IGN, the four SET_*_IGN, STRING_IGN, STRING_FLD, REF_GROUP_IGN
            // and REF_GROUP_FLD - so 'ignore-case' and 'case-folding' have no arm here either.
            // S23 delivered every _REV opcode, so 'right-to-left' has no arm here either.
            Opcode.Conditional or Opcode.EndConditional => "conditionals",

            Opcode.CallRef or Opcode.GroupCall or Opcode.GroupReturn => "recursion",

            Opcode.EndLookaround or Opcode.Lookaround => "lookaround",

            Opcode.EndFuzzy or Opcode.Fuzzy or Opcode.FuzzyExt or Opcode.FuzzyInsert => "fuzzy-matching",

            // S20 delivered BOUNDARY, DEFAULT_BOUNDARY, DEFAULT_START_OF_WORD, DEFAULT_END_OF_WORD,
            // START_OF_WORD, END_OF_WORD, GRAPHEME_BOUNDARY, KEEP, ATOMIC and END_ATOMIC, so
            // 'word-flag', 'grapheme', 'keep-marker' and 'atomic' have no arm here either - same
            // reason as 'quantifiers' above. S29 delivered PRUNE and SKIP, so 'backtracking-verbs'
            // is gone from here too.

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
/// <b>Landed in S60</b>: the required-string locator (<c>locate_required_string</c>, <c>:11082</c>)
/// and the case-sensitive forward arm of the <c>string_search</c> family it calls. That is the whole
/// reason upstream answers <c>'(a|a)*b'</c> against a subject holding no <c>'b'</c> instantly, where
/// this port ran the exponential search until S60 - so it was never only a speed matter. It is
/// deliberately less than upstream's: the start-position JUMP is withheld from any pattern holding a
/// <c>(*SKIP)</c>, and the reverse, folded and ignore-case arms of the locator are still to come.
/// See <see cref="LocateRequiredString"/>.
/// </para>
/// <para>
/// <b>Still deferred to Phase 7</b>: <c>search_start</c>, the rest of the <c>string_search</c> family
/// (<c>:5231-6918</c>), and the test-node fast path (<c>try_match</c>, <c>:7671</c>). Without them a
/// search that the required string does not refuse outright still tries the pattern at every
/// position.
/// </para>
/// <para>
/// <b>Nor are they uniformly transparent.</b> They answer the same for the case-sensitive opcodes,
/// and S22 found three places where they do not for the case-insensitive ones: <c>search_start</c>
/// screens a cased property under an ASCII encoding with a predicate that does not fold;
/// <c>string_search_fld</c> compares with <c>same_char_ign_turkic</c>, which nothing else reaches;
/// and the <c>GREEDY_REPEAT_ONE</c> retreat fast path clamps by a folded length it recomputes from
/// already-folded values. In each, this port finds a match upstream's own search misses (or, for
/// the first, the other way round). All three are pinned as known differences in
/// <c>tests/FuzzyRegex.Tests/Gaps/Engine/CaseInsensitiveMatchingTests.cs</c>, with the
/// <c>regex.match</c> output beside them; DECISIONS 2026-08-31 has the reasoning. <b>A Phase 7
/// slice restoring any of the three is expected to change those tests, and should.</b>
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
    /// Upstream <c>same_char_ign</c> (<c>upstream/src/_regex.c</c> line 2849).
    /// </summary>
    /// <remarks>
    /// The comparison is not symmetric in form - only <paramref name="ch1"/>'s other cases are
    /// enumerated - but it is in result, because <c>all_cases</c> returns a whole case set: if
    /// <c>ch2</c> is one of <c>ch1</c>'s cases then <c>ch1</c> is one of <c>ch2</c>'s. Upstream's
    /// callers still pass a particular one first, and this port keeps their argument order so a
    /// future change in either can be diffed.
    /// </remarks>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="ch1">One codepoint.</param>
    /// <param name="ch2">The other.</param>
    /// <returns><see langword="true"/> if they are the same, ignoring case.</returns>
    internal static bool SameCharIgn(CaseEncoding encoding, uint ch1, uint ch2)
    {
        if (ch1 == ch2)
        {
            return true;
        }

        Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
        int count = Encodings.AllCases(encoding, ch1, cases);

        // From 1: 'cases[0]' is 'ch1' itself, which the equality above already rejected.
        for (int i = 1; i < count; i++)
        {
            if (cases[i] == ch2)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Upstream <c>in_range_ign</c> (<c>upstream/src/_regex.c</c> line 2821).</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="lower">The lowest codepoint in the range.</param>
    /// <param name="upper">The highest.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if any case of the codepoint is in the range.</returns>
    internal static bool InRangeIgn(CaseEncoding encoding, uint lower, uint upper, uint ch)
    {
        Span<uint> cases = stackalloc uint[UnicodeTables.MaxCases];
        int count = Encodings.AllCases(encoding, ch, cases);

        for (int i = 0; i < count; i++)
        {
            if (InRange(lower, upper, cases[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Upstream <c>matches_CHARACTER_IGN</c> (<c>upstream/src/_regex.c</c> line 2918).</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="node">The <c>CHARACTER_IGN</c> node.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if they are the same, ignoring case.</returns>
    internal static bool MatchesCharacterIgn(CaseEncoding encoding, Node node, uint ch) =>
        SameCharIgn(encoding, node.Values[0], ch);

    /// <summary>Upstream <c>matches_RANGE_IGN</c> (<c>upstream/src/_regex.c</c> line 3009).</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="node">The <c>RANGE_IGN</c> node.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if the codepoint is in the range, ignoring case.</returns>
    internal static bool MatchesRangeIgn(CaseEncoding encoding, Node node, uint ch) =>
        InRangeIgn(encoding, node.Values[0], node.Values[1], ch);

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

    /// <summary>Upstream <c>matches_PROPERTY_IGN</c> (<c>upstream/src/_regex.c</c> line 2937).</summary>
    /// <remarks>
    /// <para>
    /// Under <c>IGNORECASE</c> the three cased general categories collapse into "is it a cased
    /// letter" and Uppercase / Lowercase into <c>Cased</c>, because a case-insensitive
    /// <c>\p{Lu}</c> that still refused lowercase would be answering a different question from the
    /// one the flag asked. Upstream writes that collapse out once per encoding arm, so <b>it
    /// applies under ASCII too</b> - which is not what the encoding table's own
    /// <c>has_property_ign</c> slot does (<c>ascii_has_property_ign</c>, <c>:832</c>, is
    /// <c>ascii_has_property</c> with a comment). Only this one is reached from the matcher.
    /// </para>
    /// <para>
    /// <b>Confirm against <c>regex.match</c>, never <c>regex.search</c>.</b> Upstream's
    /// search-start prefilter tests a candidate position with a predicate that does *not* collapse,
    /// so <c>search</c> refuses positions its own matcher would accept and the two operations
    /// disagree. Measured against regex 2026.7.19 on 2026-08-31:
    /// </para>
    /// <code>
    /// regex.match(r'(?ai)\p{Ll}', 'A')    -> (0, 1)     regex.fullmatch(...) -> (0, 1)
    /// regex.search(r'(?ai)\p{Ll}', 'A')   -> None
    /// regex.search(r'(?ai)x?\p{Ll}', 'A') -> (0, 1)     # the prefilter no longer applies
    /// </code>
    /// <para>
    /// The prefilter is <c>search_start</c>, which this port defers to Phase 7, so our
    /// <c>search</c> agrees with upstream's <c>match</c> here and not with upstream's
    /// <c>search</c>. That is the deferral showing through rather than a difference in this
    /// predicate; DECISIONS 2026-08-31 records it and the gap tests pin both halves.
    /// </para>
    /// </remarks>
    /// <param name="encoding">The pattern's encoding.</param>
    /// <param name="node">The <c>PROPERTY_IGN</c> node.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if the codepoint has the property, ignoring case.</returns>
    internal static bool MatchesPropertyIgn(CaseEncoding encoding, Node node, uint ch)
    {
        uint property = node.Values[0];
        uint prop = property >> 16;

        // Upstream's Unicode and ASCII arms are the same three tests; only the fall-through
        // differs, and Encodings.HasProperty is where that difference already lives.
        if (property is Encodings.PropGcLu or Encodings.PropGcLl or Encodings.PropGcLt)
        {
            uint value = UnicodeTables.GetGeneralCategory(ch);

            return value is UnicodeTables.PropLu or UnicodeTables.PropLl or UnicodeTables.PropLt;
        }

        if (prop is UnicodeTables.PropUppercase or UnicodeTables.PropLowercase)
        {
            return UnicodeTables.GetCased(ch) != 0;
        }

        // The property is case-insensitive.
        return Encodings.HasProperty(NodeEncoding(encoding, node), property, ch);
    }

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

    /// <summary>Upstream <c>matches_member_ign</c> (<c>upstream/src/_regex.c</c> line 3085).</summary>
    /// <remarks>
    /// <para>
    /// The case set is computed once by <see cref="MatchesSetIgn"/> and threaded down, so a nested
    /// set is tested against each case of the subject character rather than re-folding at every
    /// level. That is upstream's shape, <c>case_count</c> and <c>cases</c> and all.
    /// </para>
    /// <para>
    /// Three deliberate differences from <see cref="MatchesMember"/>, all upstream's:
    /// <c>ANY_ALL</c> has no arm and falls to the default; the default answers
    /// <see langword="true"/> where the case-sensitive version answers <see langword="false"/>; and
    /// the <c>PROPERTY</c> arm calls the encoding's plain <c>has_property</c> without the
    /// <c>ENCODING_KIND(member)</c> switch, so a scoped <c>(?a:...)</c> inside a
    /// case-insensitive set is not honoured here. The nested <c>SET_*</c> arms likewise recurse
    /// into the case-*sensitive* <c>in_set_*</c> with one case at a time.
    /// </para>
    /// </remarks>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="member">The member node.</param>
    /// <param name="cases">The cases of the subject character.</param>
    /// <returns><see langword="true"/> if any case of the character matches the member.</returns>
    internal static bool MatchesMemberIgn(CaseEncoding encoding, Node member, ReadOnlySpan<uint> cases)
    {
        for (int i = 0; i < cases.Length; i++)
        {
            switch (member.Op)
            {
                case Opcode.Character:
                    if (cases[i] == member.Values[0])
                    {
                        return true;
                    }

                    break;
                case Opcode.Property:
                    if (Encodings.HasProperty(encoding, member.Values[0], cases[i]))
                    {
                        return true;
                    }

                    break;
                case Opcode.Range:
                    if (InRange(member.Values[0], member.Values[1], cases[i]))
                    {
                        return true;
                    }

                    break;
                case Opcode.SetDiff:
                    if (InSetDiff(encoding, member, cases[i]))
                    {
                        return true;
                    }

                    break;
                case Opcode.SetInter:
                    if (InSetInter(encoding, member, cases[i]))
                    {
                        return true;
                    }

                    break;
                case Opcode.SetSymDiff:
                    if (InSetSymDiff(encoding, member, cases[i]))
                    {
                        return true;
                    }

                    break;
                case Opcode.SetUnion:
                    if (InSetUnion(encoding, member, cases[i]))
                    {
                        return true;
                    }

                    break;
                case Opcode.String:
                    // A STRING inside a set is a set of single characters; see MatchesMember.
                    if (member.Values.Contains(cases[i]))
                    {
                        return true;
                    }

                    break;
                default:
                    return true;
            }
        }

        return false;
    }

    /// <summary>Upstream <c>in_set_diff_ign</c> (<c>upstream/src/_regex.c</c> line 3177).</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="node">The set node.</param>
    /// <param name="cases">The cases of the subject character.</param>
    /// <returns><see langword="true"/> if the character is in the difference, ignoring case.</returns>
    internal static bool InSetDiffIgn(CaseEncoding encoding, Node node, ReadOnlySpan<uint> cases)
    {
        Node? member = node.Next2.Node;

        if (MatchesMemberIgn(encoding, member!, cases) != member!.Match)
        {
            return false;
        }

        member = member.Next1.Node;

        while (member is not null)
        {
            if (MatchesMemberIgn(encoding, member, cases) == member.Match)
            {
                return false;
            }

            member = member.Next1.Node;
        }

        return true;
    }

    /// <summary>Upstream <c>in_set_inter_ign</c> (<c>upstream/src/_regex.c</c> line 3218).</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="node">The set node.</param>
    /// <param name="cases">The cases of the subject character.</param>
    /// <returns><see langword="true"/> if the character is in every member, ignoring case.</returns>
    internal static bool InSetInterIgn(CaseEncoding encoding, Node node, ReadOnlySpan<uint> cases)
    {
        Node? member = node.Next2.Node;

        while (member is not null)
        {
            if (MatchesMemberIgn(encoding, member, cases) != member.Match)
            {
                return false;
            }

            member = member.Next1.Node;
        }

        return true;
    }

    /// <summary>Upstream <c>in_set_sym_diff_ign</c> (<c>upstream/src/_regex.c</c> line 3257).</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="node">The set node.</param>
    /// <param name="cases">The cases of the subject character.</param>
    /// <returns><see langword="true"/> if the character is in an odd number of members.</returns>
    internal static bool InSetSymDiffIgn(CaseEncoding encoding, Node node, ReadOnlySpan<uint> cases)
    {
        Node? member = node.Next2.Node;
        bool result = false;

        while (member is not null)
        {
            if (MatchesMemberIgn(encoding, member, cases) == member.Match)
            {
                result = !result;
            }

            member = member.Next1.Node;
        }

        return result;
    }

    /// <summary>Upstream <c>in_set_union_ign</c> (<c>upstream/src/_regex.c</c> line 3295).</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="node">The set node.</param>
    /// <param name="cases">The cases of the subject character.</param>
    /// <returns><see langword="true"/> if the character is in any member, ignoring case.</returns>
    internal static bool InSetUnionIgn(CaseEncoding encoding, Node node, ReadOnlySpan<uint> cases)
    {
        Node? member = node.Next2.Node;

        while (member is not null)
        {
            if (MatchesMemberIgn(encoding, member, cases) == member.Match)
            {
                return true;
            }

            member = member.Next1.Node;
        }

        return false;
    }

    /// <summary>Upstream <c>matches_SET_IGN</c> (<c>upstream/src/_regex.c</c> line 3334).</summary>
    /// <param name="encoding">The encoding in force.</param>
    /// <param name="node">The set node.</param>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if the codepoint is in the set, ignoring case.</returns>
    internal static bool MatchesSetIgn(CaseEncoding encoding, Node node, uint ch)
    {
        Span<uint> allCases = stackalloc uint[UnicodeTables.MaxCases];
        int caseCount = Encodings.AllCases(encoding, ch, allCases);
        ReadOnlySpan<uint> cases = allCases[..caseCount];

        return node.Op switch
        {
            Opcode.SetDiffIgn or Opcode.SetDiffIgnRev => InSetDiffIgn(encoding, node, cases),
            Opcode.SetInterIgn or Opcode.SetInterIgnRev => InSetInterIgn(encoding, node, cases),
            Opcode.SetSymDiffIgn or Opcode.SetSymDiffIgnRev => InSetSymDiffIgn(encoding, node, cases),
            Opcode.SetUnionIgn or Opcode.SetUnionIgnRev => InSetUnionIgn(encoding, node, cases),
            _ => false,
        };
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
            Opcode.Character or Opcode.CharacterRev => MatchesCharacter(node, ch),
            Opcode.CharacterIgn or Opcode.CharacterIgnRev => MatchesCharacterIgn(encoding, node, ch),
            Opcode.Property or Opcode.PropertyRev => MatchesProperty(encoding, node, ch),
            Opcode.PropertyIgn or Opcode.PropertyIgnRev => MatchesPropertyIgn(encoding, node, ch),
            Opcode.Range or Opcode.RangeRev => MatchesRange(node, ch),
            Opcode.RangeIgn or Opcode.RangeIgnRev => MatchesRangeIgn(encoding, node, ch),
            Opcode.SetDiffIgn
            or Opcode.SetDiffIgnRev
            or Opcode.SetInterIgn
            or Opcode.SetInterIgnRev
            or Opcode.SetSymDiffIgn
            or Opcode.SetSymDiffIgnRev
            or Opcode.SetUnionIgn
            or Opcode.SetUnionIgnRev => MatchesSetIgn(encoding, node, ch),
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
    /// <param name="pos">
    /// Where the character to test starts. Upstream's <c>_REV</c> steppers read <c>text_ptr[-1]</c>
    /// and this port's cannot, because one code unit back is not one character back, so
    /// <see cref="CountOne"/> passes the already-stepped position and the predicate is the same
    /// either way.
    /// </param>
    /// <returns><see langword="true"/> if the character there is one more repeat.</returns>
    private static bool MatchesMany(MatchState state, Node node, int pos)
    {
        uint ch = state.CharAt(pos);

        return node.Op switch
        {
            Opcode.Any or Opcode.AnyRev => MatchesAny(ch),
            Opcode.AnyAll or Opcode.AnyAllRev => true,
            Opcode.AnyU or Opcode.AnyURev => MatchesAnyU(state.Encoding, ch),
            _ => MatchesOne(state.Encoding, node, ch) == node.Match,
        };
    }

    /// <summary>
    /// Upstream <c>count_one</c> (<c>upstream/src/_regex.c</c> line 4989): how many times
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
    /// matches, and a partial match on the side it ran out of was asked for.
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

        // Upstream splits forwards from backwards by having a separate case per opcode; the two
        // families differ only in which way the walk below runs and which bound it stops at.
        bool reverse;

        switch (node.Op)
        {
            case Opcode.Any:
            case Opcode.AnyAll:
            case Opcode.AnyU:
            case Opcode.Character:
            case Opcode.CharacterIgn:
            case Opcode.Property:
            case Opcode.PropertyIgn:
            case Opcode.Range:
            case Opcode.RangeIgn:
            case Opcode.SetDiff:
            case Opcode.SetDiffIgn:
            case Opcode.SetInter:
            case Opcode.SetInterIgn:
            case Opcode.SetSymDiff:
            case Opcode.SetSymDiffIgn:
            case Opcode.SetUnion:
            case Opcode.SetUnionIgn:
                reverse = false;
                break;
            case Opcode.AnyRev:
            case Opcode.AnyAllRev:
            case Opcode.AnyURev:
            case Opcode.CharacterRev:
            case Opcode.CharacterIgnRev:
            case Opcode.PropertyRev:
            case Opcode.PropertyIgnRev:
            case Opcode.RangeRev:
            case Opcode.RangeIgnRev:
            case Opcode.SetDiffRev:
            case Opcode.SetDiffIgnRev:
            case Opcode.SetInterRev:
            case Opcode.SetInterIgnRev:
            case Opcode.SetSymDiffRev:
            case Opcode.SetSymDiffIgnRev:
            case Opcode.SetUnionRev:
            case Opcode.SetUnionIgnRev:
                reverse = true;
                break;
            default:
                // Upstream's switch has no default at all, so an opcode it does not list falls off
                // the end of the function with 'count' uninitialised. Every opcode that reaches
                // here is one 'SequenceMatchesOne' accepted, so nothing is missing from the two
                // lists above.
                throw Seam.For(node.Op);
        }

        long count = 0;
        int pos = textPos;

        while (
            count < maxCount
            && (reverse ? pos > state.SliceStart : pos < state.SliceEnd)
            && MatchesMany(state, node, reverse ? state.PrevPos(pos) : pos)
        )
        {
            pos = reverse ? state.PrevPos(pos) : state.NextPos(pos);
            ++count;
        }

        endPos = pos;

        // Upstream's 'count == (size_t)(state->text_end - text_pos)' forwards and
        // 'count == (size_t)(text_pos)' backwards: the walk consumed everything there was, which in
        // code-unit indices is the walk having stopped at 'text_end' or at 'text_start'. Backwards,
        // 'RanOutOnTheLeft' asks 'slice_start' rather than upstream's always-zero 'text_start'.
        //
        // The EQUALITY is upstream's and is kept deliberately, where the opcode arms that ask the
        // same helper spell the test '<='. In codepoints the two cannot differ, because this walk
        // stops at 'slice_start'; in UTF-16 they can, because a 'beginning' that SPLITS a surrogate
        // pair lets 'PrevPos' step two code units and land one BELOW 'SliceStart'. S52d moves the
        // bound and not the comparison, so that case answers exactly what it did before the ruling.
        // Raised by S52d's blind review and reproduced: '(?r)\A[\s\S]*' over "a\U0001F600" at the
        // slice (2, 3), which splits the pair.
        isPartial = reverse
            ? count < maxCount && pos == state.SliceStart && RanOutOnTheLeft(state, pos)
            : pos == state.TextEnd && count < maxCount && state.PartialSide == MatchState.PartialRight;

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
        // The walks below run 'for (i = 0; i < count; ...)', so a count of zero or less steps
        // nowhere; the arithmetic arms would step backwards, so they get the same floor.
        if (count < 0)
        {
            count = 0;
        }

        if (step > 0)
        {
            if (!state.OneUnitPerCharacter)
            {
                return state.GetCharacterIndex().StepForward(pos, count, state.SliceEnd);
            }

            // Upstream's arithmetic, restored: one character is one code unit, so the walk this
            // replaces can only end at 'pos + count' or at the bound it stops on.
            return pos >= state.SliceEnd ? pos : (int)Math.Min(pos + count, state.SliceEnd);
        }

        if (step < 0)
        {
            if (!state.OneUnitPerCharacter)
            {
                return state.GetCharacterIndex().StepBackward(pos, count, state.SliceStart);
            }

            return pos <= state.SliceStart ? pos : (int)Math.Max(pos - count, state.SliceStart);
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
        if (state.OneUnitPerCharacter)
        {
            // Upstream's subtraction, restored: see StepBy.
            return Math.Abs(to - from);
        }

        return state.GetCharacterIndex().CountBetween(from, to);
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

    /// <summary>Upstream <c>try_match_ANY_REV</c> (line 6962).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchAnyRev(MatchState state, int textPos)
    {
        if (RanOutOnTheLeft(state, textPos))
        {
            return MatchStatus.Partial;
        }

        return MatchStatus.From(textPos > state.SliceStart && MatchesAny(state.CharBefore(textPos)));
    }

    /// <summary>Upstream <c>try_match_ANY_ALL_REV</c> (line 6947).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchAnyAllRev(MatchState state, int textPos)
    {
        if (RanOutOnTheLeft(state, textPos))
        {
            return MatchStatus.Partial;
        }

        return MatchStatus.From(textPos > state.SliceStart);
    }

    /// <summary>Upstream <c>try_match_ANY_U_REV</c> (line 6995).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchAnyURev(MatchState state, int textPos)
    {
        if (RanOutOnTheLeft(state, textPos))
        {
            return MatchStatus.Partial;
        }

        return MatchStatus.From(textPos > state.SliceStart && MatchesAnyU(state.Encoding, state.CharBefore(textPos)));
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
    /// <remarks>
    /// DELIBERATE DIVERGENCE, S35: upstream bounds this one with <c>slice_end</c> where every other
    /// zero-width assertion it has bounds itself with <c>text_end</c>, and this reads
    /// <see cref="MatchState.TextEnd"/> like the rest of them. In everything the port implements
    /// today, only a <c>(*SKIP)</c> moves the slice inside an attempt (the <see cref="Opcode.Skip"/>
    /// arm, <c>:14545</c>), and a verb moves where the next attempt starts and nothing else - PCRE2
    /// pcre2pattern, "Verbs that act after backtracking" - so an assertion about the text must not
    /// read a bound a verb has moved. <b>Phase 5 gets a second mover</b>, flagged by S35's blind
    /// review: <c>do_best_fuzzy_match</c> (<c>:17802</c>) and <c>do_enhanced_fuzzy_match</c>
    /// (<c>:17976</c>) narrow the slice around a candidate and re-run <c>basic_match</c> inside it.
    /// That one is a genuine narrowing of the text under consideration rather than a bumpalong, so
    /// whoever ports it has to decide what <c>$</c> means inside it rather than assume this line
    /// already answers.
    /// Reading <c>slice_end</c> made <c>$</c> true one character early: on <c>"\nb"</c>,
    /// <c>(?r)(?:a*(*SKIP)b|[^a-f])$</c> under MULTILINE reported a second match at (0, 1) whose
    /// <c>$</c> sits at position 1, where the next character is <c>b</c>. Upstream answers the one
    /// match, but from its <c>search_start_END_OF_LINE_rev</c> fast path (<c>:8055</c>), which does
    /// bound itself with <c>text_end</c>; its slow path has the identical fault, so the divergence
    /// is invisible to it and to us until a <c>(*SKIP)</c> is present. Pinned by
    /// <c>Gaps/Engine/BacktrackingVerbTests.cs</c>,
    /// <c>Multiline_dollar_after_a_skip_reads_the_text_end_and_not_the_moved_slice</c>.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchEndOfLine(MatchState state, int textPos) =>
        MatchStatus.From(textPos >= state.TextEnd || state.CharAt(textPos) == '\n');

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
    /// <c>try_match_PROPERTY</c> (<c>:7154</c>), <c>try_match_RANGE</c> (<c>:7220</c>),
    /// <c>try_match_SET</c> (<c>:7292</c>) and their four <c>_IGN</c> counterparts, which are the
    /// same six lines with a different <c>matches_*</c> predicate - the switch
    /// <see cref="MatchesOne"/> already makes.
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
    /// The <c>_REV</c> half of <see cref="TryMatchOne"/>: upstream's
    /// <c>try_match_CHARACTER_REV</c> (<c>upstream/src/_regex.c</c> line 7072),
    /// <c>try_match_PROPERTY_REV</c> (<c>:7205</c>), <c>try_match_RANGE_REV</c> (<c>:7271</c>),
    /// <c>try_match_SET_REV</c> (<c>:7343</c>) and their four <c>_IGN_REV</c> counterparts.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="node">The node.</param>
    /// <param name="textPos">The position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    internal static int TryMatchOneRev(MatchState state, Node node, int textPos)
    {
        if (RanOutOnTheLeft(state, textPos))
        {
            return MatchStatus.Partial;
        }

        return MatchStatus.From(
            textPos > state.SliceStart && MatchesOne(state.Encoding, node, state.CharBefore(textPos)) == node.Match
        );
    }

    /// <summary>
    /// Upstream <c>match_one</c> (<c>upstream/src/_regex.c</c> line 11373).
    /// </summary>
    /// <remarks>
    /// Upstream's default arm answers <c>FALSE</c> for an opcode it has no <c>try_match_*</c> for.
    /// Here that would turn a construct a later slice delivers into a silent "no repeat here", so
    /// the leaf throws instead - the S07 rule. The only caller is the <c>LAZY_REPEAT_ONE</c>
    /// backtrack case, whose node is whatever <c>SequenceMatchesOne</c> accepted, so nothing that
    /// can reach here is missing from the list.
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
            Opcode.AnyRev => TryMatchAnyRev(state, textPos),
            Opcode.AnyAllRev => TryMatchAnyAllRev(state, textPos),
            Opcode.AnyURev => TryMatchAnyURev(state, textPos),
            Opcode.CharacterRev
            or Opcode.CharacterIgnRev
            or Opcode.PropertyRev
            or Opcode.PropertyIgnRev
            or Opcode.RangeRev
            or Opcode.RangeIgnRev
            or Opcode.SetDiffRev
            or Opcode.SetDiffIgnRev
            or Opcode.SetInterRev
            or Opcode.SetInterIgnRev
            or Opcode.SetSymDiffRev
            or Opcode.SetSymDiffIgnRev
            or Opcode.SetUnionRev
            or Opcode.SetUnionIgnRev => TryMatchOneRev(state, node, textPos),
            Opcode.Character
            or Opcode.CharacterIgn
            or Opcode.Property
            or Opcode.PropertyIgn
            or Opcode.Range
            or Opcode.RangeIgn
            or Opcode.SetDiff
            or Opcode.SetDiffIgn
            or Opcode.SetInter
            or Opcode.SetInterIgn
            or Opcode.SetSymDiff
            or Opcode.SetSymDiffIgn
            or Opcode.SetUnion
            or Opcode.SetUnionIgn => TryMatchOne(state, node, textPos),
            _ => throw Seam.For(node.Op),
        };

    /// <summary>Upstream <c>at_end</c> (<c>upstream/src/_regex.c</c> line 11628).</summary>
    /// <param name="state">The match state.</param>
    /// <returns><see langword="true"/> if matching has reached the far end of the slice.</returns>
    private static bool AtEnd(MatchState state) =>
        state.Reverse ? state.TextPos == state.SliceStart : state.TextPos == state.SliceEnd;

    /// <summary>
    /// Upstream <c>save_best_match</c> (<c>upstream/src/_regex.c</c> line 11493): saves the match as
    /// the best POSIX match (leftmost longest) found so far.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream's lazy allocation and <c>capacity</c> arithmetic (<c>:11508-11554</c>) are the
    /// manual-memory half of this function and have no counterpart here: <see cref="GroupData.Copy"/>
    /// already produces a snapshot holding exactly the live spans. Reusing the storage across saves
    /// is a Phase 7 question, not a correctness one.
    /// </para>
    /// <para>
    /// <b>The fuzzy half, and where this port deliberately does more than upstream (S43).</b>
    /// Upstream copies <c>best_fuzzy_counts</c> here (<c>:11501</c>) and <c>fuzzy_counts</c> back in
    /// <c>restore_best_match</c> (<c>:11575</c>), and copies the CHANGES neither way. A candidate
    /// that loses therefore leaves its changes behind in the live list while the winner's counts are
    /// put back over the top, so the match reports a count and a change list that contradict each
    /// other - and in C the stale list is read through freed storage, which is why
    /// <c>regex.compile(r'(?p)(?:abc){e&lt;=1}').match('axc').fuzzy_changes</c> faults the
    /// interpreter with an access violation rather than answering. Measured 2026-09-13 against
    /// <c>regex</c> 2026.7.19; <c>python tools/probes/upstream-posix-fuzzy-changes-crash.py</c>.
    /// </para>
    /// <para>
    /// So BOTH are saved and restored here. Copying only the counts, as upstream does, would
    /// reproduce the contradiction memory-safely and leave this port answering a match whose
    /// <c>FuzzyChanges</c> belong to a candidate that lost - an inherited bug, which design spec
    /// amendment 20 says is fixed here rather than carried. Ledger entry 9, closed;
    /// Gaps/Engine/FuzzyPosixTests.cs pins the answers.
    /// </para>
    /// </remarks>
    /// <param name="state">The match state.</param>
    private static void SaveBestMatch(MatchState state)
    {
        state.BestMatchPos = state.MatchPos;
        state.BestTextPos = state.TextPos;
        state.FoundMatch = true;

        state.FuzzyCounts.CopyTo(state.BestFuzzyCounts, 0);
        state.BestFuzzyChanges.Clear();
        state.BestFuzzyChanges.AddRange(state.FuzzyChanges);

        // And the two running totals the ranking modes read, which upstream leaves stale - see
        // 'RestoreBestMatch'.
        state.BestTotalErrors = state.TotalErrors;
        state.BestTotalCost = state.TotalCost;

        state.BestMatchGroups = GroupData.CopyGroups(state.Groups, state.Groups.Length);
    }

    /// <summary>
    /// Upstream <c>restore_best_match</c> (<c>upstream/src/_regex.c</c> line 11565): puts the best
    /// POSIX match back into the state, so that the caller reads it as the match.
    /// </summary>
    /// <remarks>
    /// The spans are copied into the live <see cref="GroupData"/> objects rather than the array
    /// being swapped, exactly as upstream's <c>Py_MEMCPY</c> does: a group's captures array is grown
    /// in place elsewhere, so the identity of these objects is what the rest of the match holds.
    /// </remarks>
    /// <param name="state">The match state.</param>
    private static void RestoreBestMatch(MatchState state)
    {
        if (!state.FoundMatch)
        {
            return;
        }

        state.MatchPos = state.BestMatchPos;
        state.TextPos = state.BestTextPos;

        // Upstream's `fuzzy_counts` copy (:11575), plus the CHANGES copy upstream does not have.
        // Both, or the match reports errors it cannot account for. See 'SaveBestMatch'.
        state.BestFuzzyCounts.CopyTo(state.FuzzyCounts, 0);
        state.FuzzyChanges.Clear();
        state.FuzzyChanges.AddRange(state.BestFuzzyChanges);

        // AND THE TWO RUNNING TOTALS, WHICH UPSTREAM LEAVES STALE - ledger entry 9's remaining
        // port-side bug, fixed here (S48b). 'restore_best_match' (:11565) copies 'fuzzy_counts' and
        // not 'total_errors', so after a POSIX restore the counts describe the winning candidate and
        // the running total still describes the last one to be tried and fail. That is invisible
        // upstream wherever nothing reads 'total_errors' afterwards, and this port's ranking does:
        // 'DoEnhancedFuzzyMatch' reads it to decide both whether to keep a run and whether to walk
        // on, and 'TotalCost' beside it is this port's own field and so was never upstream's to
        // leave stale.
        //
        // Measured before the fix, 2026-09-14, '(?e)(?r)(?:\w.){1<=e<=2:\w}(?:[^a-f]a\w)
        // {s<=1,i<=1,d<=1}' fullmatching '+ aBA' under POSIX: the walk's second run restores counts
        // (0,1,1) - two errors, and the right answer - while 'TotalErrors' still reads 3 from the
        // candidate that lost, so 'TotalErrors >= fewestErrors' cuts the walk and the match keeps the
        // first run's three errors for the same span. Without POSIX the same walk answers (0,1,1),
        // which is what made it self-refuting rather than a ranking difference.
        state.TotalErrors = state.BestTotalErrors;
        state.TotalCost = state.BestTotalCost;

        RestoreGroups(state, state.BestMatchGroups!);
    }

    /// <summary>
    /// Upstream <c>check_posix_match</c> (<c>upstream/src/_regex.c</c> line 11602): keeps the new
    /// match if it is longer than the best one so far.
    /// </summary>
    /// <remarks>
    /// Only the <i>length</i> is compared, never the start: the leftmost start is already settled by
    /// the time this is first reached, because the FAILURE backtrack case returns the best match
    /// rather than advancing the search once <see cref="MatchState.FoundMatch"/> is set. Upstream
    /// measures that length from <c>match_pos</c> rather than from <c>best_match_pos</c> for the
    /// same reason - within one <c>basic_match</c> the two are equal.
    /// </remarks>
    /// <param name="state">The match state.</param>
    private static void CheckPosixMatch(MatchState state)
    {
        if (!state.FoundMatch)
        {
            SaveBestMatch(state);
            return;
        }

        int bestLength;
        int newLength;

        if (state.Reverse)
        {
            // We're searching backwards.
            bestLength = state.MatchPos - state.BestTextPos;
            newLength = state.MatchPos - state.TextPos;
        }
        else
        {
            // We're searching forwards.
            bestLength = state.BestTextPos - state.MatchPos;
            newLength = state.TextPos - state.MatchPos;
        }

        if (newLength > bestLength)
        {
            // It's a longer match.
            SaveBestMatch(state);
        }
    }

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
    /// Port of <c>RE_LookaroundStateData</c> (<c>upstream/src/_regex.c</c> lines 409-414): what
    /// <c>LOOKAROUND</c> parks on the <i>structure</i> stack so that <c>END_LOOKAROUND</c> and the
    /// backtrack case can put the text position and the slice back.
    /// </summary>
    /// <remarks>
    /// The lookaround node itself is parked because both readers need its <see cref="Node.Match"/>
    /// to tell a positive lookaround from a negative one, and the negative one needs its
    /// <see cref="Node.Next2"/> as the 'false' branch.
    /// </remarks>
    /// <param name="Node">The <c>LOOKAROUND</c> node.</param>
    /// <param name="SliceStart">The slice start before the lookaround widened it.</param>
    /// <param name="SliceEnd">The slice end before the lookaround widened it.</param>
    /// <param name="TextPos">The text position the lookaround started at, and returns to.</param>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct LookaroundStateData(Node Node, int SliceStart, int SliceEnd, int TextPos);

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

    /// <summary>Upstream <c>push_groups</c> (<c>upstream/src/_regex.c</c> line 2490).</summary>
    /// <remarks>
    /// Only each group's <c>current</c>, where <see cref="PushCaptures"/> also saves its
    /// <c>count</c>. A group call leaves the callee's captures in place on purpose - a
    /// <c>(?&amp;name)</c> call that matches records a capture the caller can read afterwards - and
    /// restores only where each group's span currently is.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="stack">The stack to push onto.</param>
    private static void PushGroups(MatchState state, ByteStack stack)
    {
        foreach (GroupData group in state.Groups)
        {
            stack.PushSize(group.Current);
        }
    }

    /// <summary>Upstream <c>pop_groups</c> (line 2662).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="stack">The stack to pop from.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    private static bool PopGroups(MatchState state, ByteStack stack)
    {
        for (int g = state.Groups.Length - 1; g >= 0; g--)
        {
            if (!stack.PopSize(out long current))
            {
                return false;
            }

            state.Groups[g].Current = (int)current;
        }

        return true;
    }

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

    /// <summary>Upstream <c>push_repeat_data</c> (<c>upstream/src/_regex.c</c> line 2552).</summary>
    /// <param name="stack">The stack to push onto.</param>
    /// <param name="repeatData">The repeat to save.</param>
    private static void PushRepeatData(ByteStack stack, RepeatData repeatData)
    {
        repeatData.BodyGuardList.PushTo(stack);
        repeatData.TailGuardList.PushTo(stack);
        stack.PushSize(repeatData.Count);
        stack.PushSize(repeatData.Start);
        stack.PushSize(repeatData.CaptureChange);
    }

    /// <summary>Upstream <c>pop_repeat_data</c> (line 2726).</summary>
    /// <param name="stack">The stack to pop from.</param>
    /// <param name="repeatData">The repeat to restore.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    private static bool PopRepeatData(ByteStack stack, RepeatData repeatData)
    {
        if (
            !stack.PopSize(out long captureChange)
            || !stack.PopSize(out long start)
            || !stack.PopSize(out long count)
            || !repeatData.TailGuardList.PopFrom(stack)
            || !repeatData.BodyGuardList.PopFrom(stack)
        )
        {
            return false;
        }

        repeatData.CaptureChange = captureChange;
        repeatData.Start = (int)start;
        repeatData.Count = count;
        return true;
    }

    /// <summary>Upstream <c>push_repeats</c> (line 2570): every repeat's state, in index order.</summary>
    /// <remarks>
    /// Unlike the captures, this saves the guard lists in full. A conditional's test can run a repeat
    /// and guard positions inside it, and those guards have to go when the test is undone - otherwise
    /// the yes-branch inherits "already failed here" from a subpattern that was only being asked
    /// about.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="stack">The stack to push onto.</param>
    private static void PushRepeats(MatchState state, ByteStack stack)
    {
        foreach (RepeatData repeat in state.Repeats)
        {
            PushRepeatData(stack, repeat);
        }
    }

    /// <summary>Upstream <c>pop_repeats</c> (line 2744).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="stack">The stack to pop from.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    private static bool PopRepeats(MatchState state, ByteStack stack)
    {
        for (int r = state.Repeats.Length - 1; r >= 0; r--)
        {
            if (!PopRepeatData(stack, state.Repeats[r]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The key <see cref="MatchState.ActiveCalls"/> holds for one open group call: which group, and
    /// where the call was made.
    /// </summary>
    /// <param name="callIndex">The call-ref index the <c>GROUP_CALL</c> node carries.</param>
    /// <param name="textPos">Where matching had got to when the call was made.</param>
    /// <returns>The set key.</returns>
    private static long ActiveCallKey(int callIndex, int textPos) => ((long)callIndex << 32) | (uint)textPos;

    /// <summary>
    /// Closes the innermost open group call, whether it returned or was backtracked past, and gives
    /// back its key.
    /// </summary>
    /// <remarks>
    /// Calls nest, so the innermost open one is always the last entry. Both callers are reached only
    /// with a call open - <c>GROUP_RETURN</c>'s called arm and <c>GROUP_CALL</c>'s backtrack arm -
    /// so an empty list would be a bug in the bookkeeping rather than a state to handle.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <returns>The closed call's key.</returns>
    private static long PopOpenCall(MatchState state)
    {
        (long key, _) = state.OpenCalls[^1];
        state.OpenCalls.RemoveAt(state.OpenCalls.Count - 1);
        state.ActiveCalls.Remove(key);
        return key;
    }

    /// <summary>
    /// Closes every group call whose saved-stack frame has just been discarded by a restore of
    /// <see cref="ByteStack.Count"/>.
    /// </summary>
    /// <remarks>
    /// NOT UPSTREAM'S, and the reason <see cref="MatchState.OpenCalls"/> records a depth at all - see
    /// its remarks. Called after each of the six sites that put back a saved
    /// <c>state.Sstack.Count</c>: <c>END_ATOMIC</c>, <c>END_CONDITIONAL</c> and
    /// <c>END_LOOKAROUND</c> forward, and <c>ATOMIC</c>, <c>CONDITIONAL</c> and <c>LOOKAROUND</c> in
    /// the backtrack switch. Ordinarily it pops nothing, because a call opened inside one of those
    /// constructs has returned before the construct ends; it earns its keep when a
    /// <c>(*PRUNE)</c> or <c>(*SKIP)</c> has already thrown the call's <c>GROUP_CALL</c> entry off
    /// the backtracking stack, so the arm that would have closed the call can never run.
    /// </remarks>
    /// <param name="state">The match state.</param>
    private static void CloseCallsAbove(MatchState state)
    {
        while (state.OpenCalls.Count > 0 && state.OpenCalls[^1].SstackDepth > state.Sstack.Count)
        {
            PopOpenCall(state);
        }
    }

    /// <summary>Upstream <c>top_bstack</c> (line 2811).</summary>
    /// <remarks>
    /// Reads the top entry of the pruning stack straight into <see cref="ByteStack.Count"/> of the
    /// backtracking stack without popping it, which truncates the backtracking stack back to the
    /// size it had when the most recent <c>push_bstack</c> ran. Upstream ignores the return value at
    /// both call sites, so an empty pruning stack leaves the backtracking stack alone - and cannot
    /// happen anyway, because <c>start_match</c> pushes one entry before the first opcode runs.
    /// </remarks>
    /// <param name="state">The match state.</param>
    private static void TopBstack(MatchState state)
    {
        if (state.Pstack.TopSize(out long bstackCount))
        {
            state.Bstack.Count = (int)bstackCount;
        }
    }

    /// <summary>
    /// Upstream's <c>ByteStack_push_block(..., &amp;data_l, sizeof(data_l))</c>, field by field.
    /// </summary>
    /// <param name="stack">The structure stack.</param>
    /// <param name="data">What to push.</param>
    private static void PushLookaroundStateData(ByteStack stack, LookaroundStateData data)
    {
        stack.PushNode(data.Node);
        stack.PushSize(data.SliceStart);
        stack.PushSize(data.SliceEnd);
        stack.PushSize(data.TextPos);
    }

    /// <summary>Upstream's matching <c>ByteStack_pop_block</c>.</summary>
    /// <param name="pattern">The pattern the parked node index is into.</param>
    /// <param name="stack">The structure stack.</param>
    /// <param name="data">Receives what was pushed.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    private static bool PopLookaroundStateData(PatternObject pattern, ByteStack stack, out LookaroundStateData data)
    {
        data = default;

        if (
            !stack.PopSize(out long textPos)
            || !stack.PopSize(out long sliceEnd)
            || !stack.PopSize(out long sliceStart)
            || !stack.PopNode(pattern, out Node? node)
        )
        {
            return false;
        }

        data = new LookaroundStateData(node!, (int)sliceStart, (int)sliceEnd, (int)textPos);
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
    /// <para>
    /// <b>S31 found the one way the reduction is NOT transparent, and restored exactly that.</b>
    /// Reporting <c>FAILURE</c> as <c>SUCCESS</c> costs a wasted branch and nothing else, because
    /// the dispatch loop tests the same node a moment later and backtracks straight out. Reporting
    /// <c>PARTIAL</c> as <c>SUCCESS</c> is a different answer: every caller below treats a negative
    /// status as "stop here and report it", so upstream stops INSIDE the branch or repeat, where
    /// this port carried on, entered it, and let the opcode raise the partial one step later - by
    /// which time an enclosing group had closed again. Found by the S31 oracle wave, seed 31, rows
    /// 214 and 518; minimised to <c>regex.search(r'(\D*?)z', 'a', partial=True)</c>, where upstream
    /// leaves group 1 unset and this port had it as (0,1). So the test node is consulted, and only
    /// a partial answer is propagated; <c>FAILURE</c> still reads as <c>SUCCESS</c> and the fast
    /// path is still Phase 7's. Nothing outside partial matching can change, because
    /// <see cref="TryMatchOne"/> and its siblings answer <c>PARTIAL</c> only when
    /// <c>partial_side</c> is set, which no ordinary match sets.
    /// </para>
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="next">The exit to test.</param>
    /// <param name="textPos">The position to test at.</param>
    /// <param name="nextPosition">Where to continue from.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int TryMatch(MatchState state, NextNode next, int textPos, out Position nextPosition)
    {
        nextPosition = new Position(next.Node!, textPos);

        // Upstream's own first line: a fuzzy test node is not consulted at all (:7678).
        Node? test = next.Test;
        if (test is null || (test.Status & NodeStatus.Fuzzy) != 0)
        {
            return MatchStatus.Success;
        }

        if (IsStringTest(test.Op))
        {
            return IsStringTestPartial(state, test, textPos) ? MatchStatus.Partial : MatchStatus.Success;
        }

        if (!IsOneCharacterTest(test.Op))
        {
            return MatchStatus.Success;
        }

        int status = MatchOne(state, test, textPos);

        return status == MatchStatus.Partial ? status : MatchStatus.Success;
    }

    /// <summary>
    /// Whether <c>try_match</c> hands this test node to one of its six <c>try_match_STRING*</c> arms
    /// (<c>upstream/src/_regex.c</c> <c>:7811-7827</c>).
    /// </summary>
    /// <param name="op">The test node's opcode.</param>
    /// <returns><see langword="true"/> if it is a string test.</returns>
    private static bool IsStringTest(Opcode op) =>
        op
            is Opcode.String
                or Opcode.StringIgn
                or Opcode.StringFld
                or Opcode.StringRev
                or Opcode.StringIgnRev
                or Opcode.StringFldRev;

    /// <summary>
    /// The partial-match answer of upstream's six <c>try_match_STRING*</c> arms:
    /// <c>try_match_STRING</c> (<c>upstream/src/_regex.c:7383</c>), <c>_STRING_FLD</c> (<c>:7415</c>),
    /// <c>_STRING_FLD_REV</c> (<c>:7488</c>), <c>_STRING_IGN</c> (<c>:7559</c>), <c>_STRING_IGN_REV</c>
    /// (<c>:7598</c>) and <c>_STRING_REV</c> (<c>:7635</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reduced the way <see cref="TryMatch"/> reduces the one-character tests: only a
    /// <c>RE_ERROR_PARTIAL</c> answer is propagated, because reporting <c>FAILURE</c> as
    /// <c>SUCCESS</c> costs a wasted branch and nothing else while reporting <c>PARTIAL</c> as
    /// <c>SUCCESS</c> is a different answer. So this returns "would upstream have said partial",
    /// which is "the string ran off the end of the SLICE before it mismatched".
    /// </para>
    /// <para>
    /// <b>Slice, not subject, and that is the whole point (S33).</b> These six bound by
    /// <c>slice_end</c> and <c>slice_start</c>, where the <c>STRING*</c> opcode arms in
    /// <c>basic_match</c> bound by <c>text_end</c> and <c>text_start</c> (<c>:14737</c>,
    /// <c>:15133</c>). <c>do_match</c> sets <c>text_end</c> and <c>slice_end</c> to the same
    /// <c>endpos</c> (<c>:18435</c>), so the forward halves cannot disagree at the top level; only
    /// <c>slice_start</c>, which is <c>pos</c>, differs from <c>text_start</c>, which is always 0.
    /// That asymmetry is the entire bug S33 fixes: without these arms
    /// <c>regex.compile(r'(?r)a(bc)*').match('abc', 1, 1, partial=True)</c> is a partial at (1, 1)
    /// upstream and no match here. It is an opcode-by-opcode answer and not a rule about
    /// <c>slice_start</c>: <c>try_match_CHARACTER_REV</c> (<c>:7137</c>) keeps the <c>text_start</c>
    /// bound, so <c>(?r)a</c> on the same slice is no match in both engines. See
    /// <c>Gaps/Engine/PartialMatchingTests.cs</c> and
    /// <c>docs/plan/2026-09-12-divergence-research.md</c>.
    /// </para>
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="test">The string test node.</param>
    /// <param name="textPos">The position to test at.</param>
    /// <returns><see langword="true"/> if the answer is a partial match.</returns>
    private static bool IsStringTestPartial(MatchState state, Node test, int textPos)
    {
        int length = test.Values.Count;
        Span<uint> folded = stackalloc uint[UnicodeTables.MaxFolded];
        int pos = textPos;
        int sPos = 0;
        int foldedPos = 0;
        int foldedLen = 0;

        switch (test.Op)
        {
            case Opcode.String:
            case Opcode.StringIgn:
                for (; sPos < length; sPos++)
                {
                    if (pos >= state.SliceEnd)
                    {
                        return state.PartialSide == MatchState.PartialRight;
                    }

                    if (!SameStringChar(state, test.Op, state.CharAt(pos), test.Values[sPos]))
                    {
                        return false;
                    }

                    pos = state.NextPos(pos);
                }

                return false;
            case Opcode.StringRev:
            case Opcode.StringIgnRev:
                for (; sPos < length; sPos++)
                {
                    if (pos <= state.SliceStart)
                    {
                        return RanOutOnTheLeft(state, pos);
                    }

                    if (!SameStringChar(state, test.Op, state.CharBefore(pos), test.Values[length - sPos - 1]))
                    {
                        return false;
                    }

                    pos = state.PrevPos(pos);
                }

                return false;
            case Opcode.StringFld:
                while (sPos < length)
                {
                    if (foldedPos >= foldedLen)
                    {
                        if (pos >= state.SliceEnd)
                        {
                            return state.PartialSide == MatchState.PartialRight;
                        }

                        foldedLen = Encodings.FullCaseFold(state.Encoding, state.CharAt(pos), folded);
                        foldedPos = 0;
                    }

                    if (!SameCharIgn(state.Encoding, test.Values[sPos], folded[foldedPos]))
                    {
                        return false;
                    }

                    ++sPos;
                    ++foldedPos;

                    if (foldedPos >= foldedLen)
                    {
                        pos = state.NextPos(pos);
                    }
                }

                return false;
            default:
                // StringFldRev.
                while (sPos < length)
                {
                    if (foldedPos >= foldedLen)
                    {
                        if (pos <= state.SliceStart)
                        {
                            return RanOutOnTheLeft(state, pos);
                        }

                        foldedLen = Encodings.FullCaseFold(state.Encoding, state.CharBefore(pos), folded);
                        foldedPos = 0;
                    }

                    if (!SameCharIgn(state.Encoding, test.Values[length - sPos - 1], folded[foldedLen - foldedPos - 1]))
                    {
                        return false;
                    }

                    ++sPos;
                    ++foldedPos;

                    if (foldedPos >= foldedLen)
                    {
                        pos = state.PrevPos(pos);
                    }
                }

                return false;
        }
    }

    /// <summary>
    /// <c>same_char</c> for the exact string tests and <c>same_char_ign</c> for the <c>_IGN</c> ones,
    /// which is the only line that differs between <c>try_match_STRING</c> and
    /// <c>try_match_STRING_IGN</c> (<c>upstream/src/_regex.c:7405</c> against <c>:7583</c>) and
    /// between their two reversed twins (<c>:7655</c> against <c>:7622</c>).
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="op">The string opcode.</param>
    /// <param name="ch">The character from the subject.</param>
    /// <param name="value">The character from the pattern.</param>
    /// <returns><see langword="true"/> if they match.</returns>
    private static bool SameStringChar(MatchState state, Opcode op, uint ch, uint value) =>
        op is Opcode.StringIgn or Opcode.StringIgnRev ? SameCharIgn(state.Encoding, ch, value) : SameChar(ch, value);

    /// <summary>
    /// Whether a <c>LAZY_REPEAT_ONE</c> backtrack has run out of text for its tail before it can
    /// extend the repeat, which upstream answers as a partial match from inside the repeat.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream spells this once per specialised tail arm of that backtrack switch rather than once:
    /// the four <c>CHARACTER</c> arms at <c>upstream/src/_regex.c</c> <c>:16546</c>, <c>:16583</c>,
    /// <c>:16621</c> and <c>:16659</c>, and the six <c>STRING</c> arms at <c>:16699</c>,
    /// <c>:16754</c>, <c>:16809</c>, <c>:16868</c>, <c>:16925</c> and <c>:16982</c>. Those arms are
    /// Phase 7 optimisations this port does not have - the slice file keeps
    /// <c>partial_string_match_ign</c> and its callers out on exactly that ground - so the ten
    /// guards are collected here and asked from the default arm instead. Upstream's own default arm
    /// (<c>:17024</c>) has no guard, which is why one cannot be added unconditionally: a tail op
    /// upstream leaves to the default arm must keep reaching its own opcode's partial arm later.
    /// </para>
    /// <para>
    /// Forwards the guard is <c>text_end</c> and not <c>slice_end</c> - it asks whether the SUBJECT
    /// has run out, where a narrowed slice ends the repeat by its own <c>limit</c> - and the two are
    /// the same number anyway, because <c>text_end</c> IS the slice end. Backwards the reversed arms
    /// go through <see cref="RanOutOnTheLeft"/>, which asks <c>slice_start</c> rather than
    /// upstream's always-zero <c>text_start</c>. A character tail is about to test the character one
    /// step on, so it guards a step further out than a string tail, which guards at
    /// <paramref name="pos"/> itself; upstream's <c>pos + 1</c> and <c>pos - 1</c> are codepoint
    /// steps, hence <see cref="MatchState.NextPos"/> and <see cref="MatchState.PrevPos"/> here.
    /// </para>
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="test">The repeat's tail test node.</param>
    /// <param name="pos">Where the repeat has reached.</param>
    /// <returns><see langword="true"/> if the answer is a partial match.</returns>
    private static bool IsTailPartial(MatchState state, Node test, int pos) =>
        test.Op switch
        {
            Opcode.Character or Opcode.CharacterIgn => state.NextPos(pos) >= state.TextEnd
                && state.PartialSide == MatchState.PartialRight,
            Opcode.CharacterRev or Opcode.CharacterIgnRev => RanOutOnTheLeft(state, state.PrevPos(pos)),
            Opcode.String or Opcode.StringIgn or Opcode.StringFld => pos >= state.TextEnd
                && state.PartialSide == MatchState.PartialRight,
            Opcode.StringRev or Opcode.StringIgnRev or Opcode.StringFldRev => RanOutOnTheLeft(state, pos),
            _ => false,
        };

    /// <summary>
    /// Whether <see cref="MatchOne"/> has an arm for this opcode, which is upstream's question of
    /// whether <c>try_match</c> has a <c>try_match_*</c> for it rather than its default arm.
    /// </summary>
    /// <param name="op">The test node's opcode.</param>
    /// <returns><see langword="true"/> if it is a one-character test.</returns>
    /// <remarks>
    /// Asked before the call rather than caught after it, because <see cref="MatchOne"/>'s default
    /// arm throws the S07 seam: a test node it has no arm for is a construct a later slice delivers,
    /// and upstream's <c>try_match</c> hands exactly those to its own default arm (<c>:7843</c>),
    /// which reports success without consuming anything.
    /// </remarks>
    private static bool IsOneCharacterTest(Opcode op) =>
        op
            is Opcode.Any
                or Opcode.AnyAll
                or Opcode.AnyU
                or Opcode.AnyRev
                or Opcode.AnyAllRev
                or Opcode.AnyURev
                or Opcode.Character
                or Opcode.CharacterIgn
                or Opcode.Property
                or Opcode.PropertyIgn
                or Opcode.Range
                or Opcode.RangeIgn
                or Opcode.SetDiff
                or Opcode.SetDiffIgn
                or Opcode.SetInter
                or Opcode.SetInterIgn
                or Opcode.SetSymDiff
                or Opcode.SetSymDiffIgn
                or Opcode.SetUnion
                or Opcode.SetUnionIgn
                or Opcode.CharacterRev
                or Opcode.CharacterIgnRev
                or Opcode.PropertyRev
                or Opcode.PropertyIgnRev
                or Opcode.RangeRev
                or Opcode.RangeIgnRev
                or Opcode.SetDiffRev
                or Opcode.SetDiffIgnRev
                or Opcode.SetInterRev
                or Opcode.SetInterIgnRev
                or Opcode.SetSymDiffRev
                or Opcode.SetSymDiffIgnRev
                or Opcode.SetUnionRev
                or Opcode.SetUnionIgnRev;

    /// <summary>
    /// Upstream <c>safe_check_cancel</c> (<c>upstream/src/_regex.c</c> line 2266). Upstream's two
    /// halves are <c>check_timed_out</c> and <c>PyErr_CheckSignals</c>, which is how a Python caller
    /// interrupts a long match; the second half is the caller's <see cref="CancellationToken"/>
    /// here, which is the only way a .NET caller has to do the same thing (S51).
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <returns><see langword="true"/> if matching should be abandoned.</returns>
    /// <remarks>
    /// This is the ONLY site either is read. The three matching-loop callers all reach it through
    /// the same <c>state.Iterations == 0</c> gate - upstream's <c>iterations</c>, a
    /// <see cref="ushort"/> stepped by <c>0x100</c> so that it wraps to zero once every 256 turns.
    /// So the token costs one predictable null test per 256 iterations of the matching or
    /// backtracking loop, on top of the <see cref="System.Diagnostics.Stopwatch"/> read that was
    /// already there. S60 added one more caller, <see cref="SimpleStringSearch"/>, on the same gate:
    /// it is a character-at-a-time loop and it can run the length of a subject.
    /// <see cref="StringSearch"/> deliberately has no poll - the reasoning, and the measurement
    /// behind it, are at the sweep itself.
    /// </remarks>
    private static bool SafeCheckCancel(MatchState state) =>
        state.CheckTimedOut() || state.Cancellation.IsCancellationRequested;

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

    /// <summary>Upstream <c>total_errors</c> (<c>upstream/src/_regex.c</c> line 9643).</summary>
    /// <param name="fuzzyCounts">The counts.</param>
    /// <returns>Their sum.</returns>
    private static long TotalErrors(ReadOnlySpan<long> fuzzyCounts) =>
        fuzzyCounts[FuzzyValue.Del] + fuzzyCounts[FuzzyValue.Ins] + fuzzyCounts[FuzzyValue.Sub];

    /// <summary>Upstream <c>total_cost</c> (line 9649).</summary>
    /// <param name="fuzzyCounts">The counts.</param>
    /// <param name="fuzzyNode">The <c>FUZZY</c> node carrying the per-error costs.</param>
    /// <returns>What those errors cost under that node's equation.</returns>
    private static long TotalCost(ReadOnlySpan<long> fuzzyCounts, Node fuzzyNode)
    {
        List<uint> values = fuzzyNode.Values;

        return (fuzzyCounts[FuzzyValue.Del] * values[FuzzyValue.DelCost])
            + (fuzzyCounts[FuzzyValue.Ins] * values[FuzzyValue.InsCost])
            + (fuzzyCounts[FuzzyValue.Sub] * values[FuzzyValue.SubCost]);
    }

    /// <summary>Upstream <c>any_error_permitted</c> (line 9660).</summary>
    /// <remarks>
    /// The last conjunct is <see cref="MatchState.MaxCost"/>, which upstream carried here itself
    /// until the 2015.11.5 rework: <c>state-&gt;total_cost &lt;= state-&gt;max_cost</c>,
    /// <c>_regex.c:9923</c> in release 2015.09.28. It is <see cref="long.MaxValue"/> in every mode
    /// but <c>BESTMATCH</c>.
    /// </remarks>
    /// <param name="state">The match state, whose <c>fuzzy_node</c> is the section in force.</param>
    /// <returns><see langword="true"/> if one more error of some kind could still fit.</returns>
    private static bool AnyErrorPermitted(MatchState state)
    {
        long[] fuzzyCounts = state.FuzzyCounts;
        Node fuzzyNode = state.FuzzyNode!;
        List<uint> values = fuzzyNode.Values;
        long cost = TotalCost(fuzzyCounts, fuzzyNode);

        return cost <= values[FuzzyValue.MaxCost]
            && TotalErrors(fuzzyCounts) < state.MaxErrors
            && cost <= state.MaxCost;
    }

    /// <summary>Upstream <c>this_error_permitted</c> (line 9676).</summary>
    /// <remarks>
    /// The last conjunct is <see cref="MatchState.MaxCost"/>, upstream's own until 2015.11.5:
    /// <c>state-&gt;total_cost + values[RE_FUZZY_VAL_COST_BASE + fuzzy_type] &lt;=
    /// state-&gt;max_cost</c>, <c>_regex.c:9936</c> in release 2015.09.28.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="fuzzyType">The error being tried.</param>
    /// <returns><see langword="true"/> if one more error of exactly that kind fits.</returns>
    private static bool ThisErrorPermitted(MatchState state, int fuzzyType)
    {
        long[] fuzzyCounts = state.FuzzyCounts;
        Node fuzzyNode = state.FuzzyNode!;
        List<uint> values = fuzzyNode.Values;
        long errorCount = TotalErrors(fuzzyCounts);
        long cost = TotalCost(fuzzyCounts, fuzzyNode);

        return fuzzyCounts[fuzzyType] < values[FuzzyValue.MaxBase + fuzzyType]
            && errorCount < values[FuzzyValue.MaxErr]
            && errorCount < state.MaxErrors
            && cost + values[FuzzyValue.CostBase + fuzzyType] <= values[FuzzyValue.MaxCost]
            && cost + values[FuzzyValue.CostBase + fuzzyType] <= state.MaxCost;
    }

    /// <summary>Upstream <c>insertion_permitted</c> (line 9695).</summary>
    /// <remarks>
    /// Takes the node and the counts rather than reading them off the state, because
    /// <c>END_FUZZY</c>'s backtrack arm asks the question of the <b>inner</b> section it has just
    /// popped rather than of the one now in force.
    /// </remarks>
    /// <param name="state">The match state, for its <c>max_errors</c>.</param>
    /// <param name="fuzzyNode">The section to ask about.</param>
    /// <param name="fuzzyCounts">That section's counts.</param>
    /// <returns><see langword="true"/> if one more insertion fits.</returns>
    private static bool InsertionPermitted(MatchState state, Node fuzzyNode, ReadOnlySpan<long> fuzzyCounts)
    {
        List<uint> values = fuzzyNode.Values;
        long errorCount = TotalErrors(fuzzyCounts);
        long cost = TotalCost(fuzzyCounts, fuzzyNode);

        return fuzzyCounts[FuzzyValue.Ins] < values[FuzzyValue.MaxIns]
            && errorCount < values[FuzzyValue.MaxErr]
            && cost + values[FuzzyValue.InsCost] <= values[FuzzyValue.MaxCost]
            && errorCount < state.MaxErrors
            // 2015.09.28 had no 'insertion_permitted' - it arrived with the same rework that removed
            // 'max_cost' - so this conjunct is 'this_error_permitted' (':9936') applied to the kind
            // of error this predicate is about.
            && cost + values[FuzzyValue.InsCost] <= state.MaxCost;
    }

    /// <summary>Upstream <c>fuzzy_within_constraints</c> (line 9712).</summary>
    /// <remarks>
    /// The only place a <c>min</c> is ever consulted: everything else in the fuzzy machinery asks
    /// whether one more error fits, and this asks whether the section as a whole is now legal - which
    /// is why <c>END_FUZZY</c> and not any single item is what calls it.
    /// </remarks>
    /// <param name="fuzzyCounts">The section's counts.</param>
    /// <param name="fuzzyNode">The section.</param>
    /// <param name="maxErrors">Upstream's <c>max_errors</c>.</param>
    /// <returns><see langword="true"/> if the counts satisfy every constraint the section declares.</returns>
    private static bool FuzzyWithinConstraints(ReadOnlySpan<long> fuzzyCounts, Node fuzzyNode, long maxErrors)
    {
        List<uint> values = fuzzyNode.Values;
        long delCount = fuzzyCounts[FuzzyValue.Del];
        long insCount = fuzzyCounts[FuzzyValue.Ins];
        long subCount = fuzzyCounts[FuzzyValue.Sub];

        if (delCount < values[FuzzyValue.MinDel] || delCount > values[FuzzyValue.MaxDel])
        {
            return false;
        }

        if (insCount < values[FuzzyValue.MinIns] || insCount > values[FuzzyValue.MaxIns])
        {
            return false;
        }

        if (subCount < values[FuzzyValue.MinSub] || subCount > values[FuzzyValue.MaxSub])
        {
            return false;
        }

        long errCount = delCount + insCount + subCount;

        if (errCount < values[FuzzyValue.MinErr] || errCount > values[FuzzyValue.MaxErr])
        {
            return false;
        }

        if (errCount > maxErrors)
        {
            return false;
        }

        return TotalCost(fuzzyCounts, fuzzyNode) <= values[FuzzyValue.MaxCost];
    }

    /// <summary>
    /// Whether a reversed match has run out of the text it is allowed to match, which is the
    /// question every left-hand partial match turns on. <b>This is the one place the left edge is
    /// decided</b>; every site that reports a partial on the left asks it here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edge is <see cref="MatchState.SliceStart"/> - the caller's <c>pos</c> - and deliberately
    /// NOT <see cref="MatchState.TextStart"/>, which is always 0. Upstream holds both rules and
    /// picks between them by optimisation path: its node handlers ask <c>text_start</c>
    /// (<c>upstream/src/_regex.c</c> <c>:12173</c>, <c>:13854</c>, <c>:13964</c>, <c>:14206</c> and
    /// the rest) while <c>search_start</c> (<c>:8400-8405</c>) and the three reversed string helpers
    /// (<c>:8335-8382</c>) ask <c>slice_start</c>, so upstream contradicts itself and this port
    /// inherited both. Upstream's own comment beside the bounds settles which is meant:
    /// </para>
    /// <code>
    /// /* init_match, :18435-18446 */
    /// /* The documentation says that the end of the slice behaves like the end of
    ///  * the string. */
    /// state-&gt;text_start = 0;          /* the real string start */
    /// state-&gt;text_end = end;          /* the SLICE end */
    /// state-&gt;slice_start = start;
    /// state-&gt;slice_end = end;
    /// </code>
    /// <para>
    /// For a reversed pattern the slice's end is its start, and a match may not consume text below
    /// <c>pos</c> at all, so at <c>pos</c> the matchable text really has run out. Owner's ruling of
    /// 2026-09-15, Option B of <c>docs/plan/upstream-reports/ledger-24-briefing.md</c>; spec
    /// amendment 16 outcome (c), ledger entry 24, and the <c>docs/DIVERGENCES.md</c> row "Reversed
    /// partial matches run out of text at the slice start". Nothing else about <c>pos</c> moves:
    /// <c>^</c>, <c>\A</c>, <c>\b</c>, <c>\B</c> and lookbehind keep reading
    /// <see cref="MatchState.TextStart"/> and the character before <c>pos</c>, exactly as Python
    /// <c>re</c> documents.
    /// </para>
    /// <para>
    /// The forward direction has no such split and needs no helper: <c>text_end</c> IS the slice
    /// end, so a forward partial has always fired at <c>endpos</c> on every path.
    /// </para>
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position the match has reached.</param>
    /// <returns><see langword="true"/> if that is a partial match on the left.</returns>
    private static bool RanOutOnTheLeft(MatchState state, int textPos) =>
        state.PartialSide == MatchState.PartialLeft && textPos <= state.SliceStart;

    /// <summary>
    /// The same edge as <see cref="RanOutOnTheLeft"/>, for a position that has already stepped PAST
    /// it rather than reached it.
    /// </summary>
    /// <remarks>
    /// Upstream spells this distinction too - <c>check_fuzzy_partial</c> and <c>search_start</c> use
    /// <c>&lt;</c> where the node handlers use <c>&lt;=</c> - because their positions have already
    /// been moved by an error or by a scan step, so being at the edge is still legal and only being
    /// below it is running out.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position the match has been moved to.</param>
    /// <returns><see langword="true"/> if that is a partial match on the left.</returns>
    private static bool SteppedPastTheLeft(MatchState state, int textPos) =>
        state.PartialSide == MatchState.PartialLeft && textPos < state.SliceStart;

    /// <summary>Upstream <c>check_fuzzy_partial</c> (line 9751).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="textPos">The position the error would have moved to.</param>
    /// <returns><see cref="MatchStatus.Partial"/> if the subject ran out, else failure.</returns>
    private static int CheckFuzzyPartial(MatchState state, int textPos) =>
        state.PartialSide switch
        {
            // 'slice_start', not upstream's 'text_start': see 'SteppedPastTheLeft'.
            MatchState.PartialLeft when SteppedPastTheLeft(state, textPos) => MatchStatus.Partial,
            MatchState.PartialRight when textPos > state.TextEnd => MatchStatus.Partial,
            _ => MatchStatus.Failure,
        };

    /// <summary>
    /// Upstream <c>save_fuzzy_changes</c> (line 9899): takes a copy of the errors the current match
    /// used, so that a later, worse run can be undone.
    /// </summary>
    /// <remarks>
    /// Upstream's <c>capacity</c> doubling and its <c>safe_realloc</c> (<c>:9901-9920</c>) are the
    /// manual-memory half and have no counterpart here. The saved list is reused across the runs of
    /// one <c>ENHANCEMATCH</c> or <c>BESTMATCH</c> loop exactly as upstream's buffer is.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="bestChanges">The list to save into.</param>
    private static void SaveFuzzyChanges(MatchState state, List<FuzzyChange> bestChanges)
    {
        bestChanges.Clear();
        bestChanges.AddRange(state.FuzzyChanges);
    }

    /// <summary>Upstream <c>restore_fuzzy_changes</c> (line 9930).</summary>
    /// <remarks>
    /// Upstream's <c>Py_MEMCPY</c> writes into <c>state-&gt;fuzzy_changes.items</c> without checking
    /// that it is long enough. It is safe there only because the saved list came out of that same
    /// buffer, which only ever grows; here the question does not arise.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="bestChanges">The list to restore from.</param>
    private static void RestoreFuzzyChanges(MatchState state, List<FuzzyChange> bestChanges)
    {
        state.FuzzyChanges.Clear();
        state.FuzzyChanges.AddRange(bestChanges);
    }

    /// <summary>
    /// Upstream <c>fuzzy_ext_match</c> (line 9938): the <c>{...:test}</c> constraint, which says
    /// which characters an error is allowed to touch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain <c>FUZZY</c> node has no second branch, so this returns <see langword="true"/> for
    /// every pattern without a constraint; only <c>FUZZY_EXT</c>, which the parser emits for
    /// <c>{...:test}</c>, has a test node and reaches the switch.
    /// </para>
    /// <para>
    /// <b>Upstream's switch has no <c>SET_*_REV</c> or <c>SET_*_IGN_REV</c> arm</b>, where it has
    /// both forward set arms and the reversed CHARACTER, PROPERTY and RANGE ones. An opcode the
    /// switch does not list falls off the end to <c>return TRUE</c>, so a reversed set test - and
    /// any test that is not a character class at all, such as <c>.</c>, which compiles to ANY -
    /// constrains nothing. That is upstream's behaviour, measured, not an omission here:
    /// <c>tools/probes/upstream-fuzzy-ext-test.py</c>, and
    /// <c>Gaps/Engine/FuzzyTestConstraintTests.cs</c> pins it.
    /// </para>
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="fuzzyNode">The section, which may be <see langword="null"/>.</param>
    /// <param name="pos">The position the error would touch.</param>
    /// <returns><see langword="true"/> if the constraint allows it.</returns>
    private static bool FuzzyExtMatch(MatchState state, Node? fuzzyNode, int pos)
    {
        Node? testNode = fuzzyNode?.Next2.Node;

        if (testNode is null)
        {
            return true;
        }

        // Upstream writes one case per opcode, each the same two lines with a different
        // 'matches_*' call; 'MatchesOne' is that choice already pulled out, so the arms group by
        // direction instead. The opcodes listed are exactly upstream's (:9949-10009).
        return testNode.Op switch
        {
            Opcode.Character
            or Opcode.CharacterIgn
            or Opcode.Property
            or Opcode.PropertyIgn
            or Opcode.Range
            or Opcode.RangeIgn
            or Opcode.SetDiff
            or Opcode.SetInter
            or Opcode.SetSymDiff
            or Opcode.SetUnion
            or Opcode.SetDiffIgn
            or Opcode.SetInterIgn
            or Opcode.SetSymDiffIgn
            or Opcode.SetUnionIgn => pos < state.SliceEnd
                && MatchesOne(state.Encoding, testNode, state.CharAt(pos)) == testNode.Match,
            Opcode.CharacterRev
            or Opcode.CharacterIgnRev
            or Opcode.PropertyRev
            or Opcode.PropertyIgnRev
            or Opcode.RangeRev
            or Opcode.RangeIgnRev => pos > state.SliceStart
                && MatchesOne(state.Encoding, testNode, state.CharBefore(pos)) == testNode.Match,
            _ => true,
        };
    }

    /// <summary>Upstream <c>next_fuzzy_match_item</c> (line 10116).</summary>
    /// <remarks>
    /// Tries one kind of error. The caller walks <c>fuzzy_type</c> from <see cref="FuzzyValue.Sub"/>
    /// upwards, so substitution is preferred to insertion and insertion to deletion.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="data">What the attempt is working on; written back through.</param>
    /// <param name="isString">
    /// Whether the caller is working through a multi-character item. A string moves
    /// <c>string_pos</c> where a single item moves to the next node, because a string's characters
    /// are one node between them.
    /// </param>
    /// <param name="step">
    /// The character step of the item, <c>0</c> for a zero-width one. Not the same as
    /// <c>data.Step</c>: a zero-width item passes <c>0</c> here and carries <c>1</c> or <c>-1</c>
    /// there, which is what lets an insertion move the position when nothing else can.
    /// </param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int NextFuzzyMatchItem(MatchState state, ref FuzzyData data, bool isString, sbyte step)
    {
        if (!ThisErrorPermitted(state, data.FuzzyType))
        {
            return MatchStatus.Failure;
        }

        data.NewTextPos = state.TextPos;

        int newPos;

        switch (data.FuzzyType)
        {
            case FuzzyValue.Del:
                // Could a character at text_pos have been deleted?
                if (step == 0)
                {
                    return MatchStatus.Failure;
                }

                AdvanceItem(state, ref data, isString, step);

                return MatchStatus.Success;
            case FuzzyValue.Ins:
                // Could the character at text_pos have been inserted?
                if (!data.PermitInsertion)
                {
                    return MatchStatus.Failure;
                }

                // Upstream's 'new_text_pos + step'. A zero-width item has no step of its own, so it
                // borrows the section's direction - which is the whole reason an insertion is the
                // only error that can get past a failing assertion.
                newPos = Step(state, data.NewTextPos, step == 0 ? data.Step : step);

                if (state.SliceStart <= newPos && newPos <= state.SliceEnd)
                {
                    if (!FuzzyExtMatch(state, state.FuzzyNode, data.NewTextPos))
                    {
                        return MatchStatus.Failure;
                    }

                    data.NewTextPos = newPos;

                    return MatchStatus.Success;
                }

                return CheckFuzzyPartial(state, data.NewTextPos);
            case FuzzyValue.Sub:
                // Could the character at text_pos have been substituted?
                if (step == 0)
                {
                    return MatchStatus.Failure;
                }

                newPos = Step(state, data.NewTextPos, step);

                if (state.SliceStart <= newPos && newPos <= state.SliceEnd)
                {
                    if (!FuzzyExtMatch(state, state.FuzzyNode, data.NewTextPos))
                    {
                        return MatchStatus.Failure;
                    }

                    data.NewTextPos = newPos;
                    AdvanceItem(state, ref data, isString, step);

                    return MatchStatus.Success;
                }

                return CheckFuzzyPartial(state, newPos);
            default:
                return MatchStatus.Failure;
        }
    }

    /// <summary>
    /// Upstream's <c>if (is_string) data-&gt;new_string_pos += step; else data-&gt;new_node =
    /// data-&gt;new_node-&gt;next_1.node;</c>, written once because
    /// <see cref="NextFuzzyMatchItem"/> has it twice (<c>upstream/src/_regex.c</c> lines 10131 and
    /// 10170).
    /// </summary>
    /// <param name="state">The match state, for the subject's own code-unit stepping.</param>
    /// <param name="data">The attempt being made.</param>
    /// <param name="isString">Whether the caller is working through a multi-character item.</param>
    /// <param name="step">The character step.</param>
    private static void AdvanceItem(MatchState state, ref FuzzyData data, bool isString, sbyte step)
    {
        if (!isString)
        {
            data.NewNode = data.NewNode!.Next1.Node;
            return;
        }

        data.NewStringPos = data.StringPosIsText ? Step(state, data.NewStringPos, step) : data.NewStringPos + step;
    }

    /// <summary>
    /// Where <c>record_fuzzy</c> is told an error happened. Upstream spells the same expression out
    /// in <c>fuzzy_match_item</c> (<c>upstream/src/_regex.c</c> line 10245) and
    /// <c>retry_fuzzy_match_item</c> (<c>:10329</c>).
    /// </summary>
    /// <remarks>
    /// A deletion is recorded where the position still is, because a deletion does not move it;
    /// anything else is recorded one character back along the direction of travel, which for a
    /// reverse match is one character <em>forward</em> of the character that changed. Upstream writes
    /// <c>new_text_pos - data.step</c>, which is a codepoint step and so is
    /// <see cref="Step"/> here.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="data">The attempt that succeeded.</param>
    /// <returns>The position to record.</returns>
    private static int FuzzyChangePos(MatchState state, in FuzzyData data) =>
        data.FuzzyType == FuzzyValue.Del ? data.NewTextPos : Step(state, data.NewTextPos, -data.Step);

    /// <summary>Upstream <c>fuzzy_match_item</c> (line 10185): a first try at fuzzing one item.</summary>
    /// <param name="state">The match state.</param>
    /// <param name="search">Whether this is a search rather than an anchored match.</param>
    /// <param name="node">The item that failed; on success, where matching carries on.</param>
    /// <param name="step">The item's character step, <c>0</c> for a zero-width one.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int FuzzyMatchItem(MatchState state, bool search, ref Node node, sbyte step)
    {
        long[] fuzzyCounts = state.FuzzyCounts;

        if (!AnyErrorPermitted(state))
        {
            return MatchStatus.Failure;
        }

        FuzzyData data = default;
        data.NewNode = node;

        // NOT PORTED: 'data.limit' (:10203, :10206), which nothing reads - see the remarks on
        // FuzzyData.
        if (step == 0)
        {
            data.Step = (node.Status & NodeStatus.Reverse) != 0 ? (sbyte)-1 : (sbyte)1;
        }
        else
        {
            data.Step = step;
        }

        // Permit insertion except initially when searching (it's better just to start searching one
        // character later).
        data.PermitInsertion = !search || state.TextPos != state.SearchAnchor;

        int status = MatchStatus.Failure;

        for (data.FuzzyType = 0; data.FuzzyType < FuzzyValue.Count; data.FuzzyType++)
        {
            status = NextFuzzyMatchItem(state, ref data, false, step);

            if (status < 0)
            {
                return status;
            }

            if (status == MatchStatus.Success)
            {
                break;
            }
        }

        if (status != MatchStatus.Success)
        {
            return MatchStatus.Failure;
        }

        state.Bstack.PushNode(node);
        state.Bstack.PushInt8(step);
        state.Bstack.PushSize(state.TextPos);
        state.Bstack.PushUInt8((byte)data.FuzzyType);
        state.Bstack.PushUInt8((byte)node.Op);

        /* bstack: node step text_pos fuzzy_type op */

        state.RecordFuzzy(data.FuzzyType, FuzzyChangePos(state, in data));

        ++fuzzyCounts[data.FuzzyType];
        ++state.CaptureChange;

        state.TextPos = data.NewTextPos;
        node = data.NewNode!;

        return MatchStatus.Success;
    }

    /// <summary>Upstream <c>retry_fuzzy_match_item</c> (line 10262): the next kind of error.</summary>
    /// <param name="state">The match state.</param>
    /// <param name="op">The opcode whose frame is being retried, which goes back on the stack.</param>
    /// <param name="search">Whether this is a search rather than an anchored match.</param>
    /// <param name="node">On success, where matching carries on.</param>
    /// <param name="advance">
    /// Whether the item consumes a character. <see langword="false"/> for a zero-width one, which is
    /// how a step of <c>0</c> reaches <see cref="NextFuzzyMatchItem"/> again.
    /// </param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int RetryFuzzyMatchItem(MatchState state, byte op, bool search, ref Node node, bool advance)
    {
        long[] fuzzyCounts = state.FuzzyCounts;

        state.UnrecordFuzzy();

        /* bstack: node step text_pos fuzzy_type */

        if (
            !state.Bstack.PopUInt8(out byte poppedType)
            || !state.Bstack.PopSize(out long poppedTextPos)
            || !state.Bstack.PopInt8(out sbyte step)
            || !state.Bstack.PopNode(state.Pattern, out Node? currNode)
        )
        {
            return MatchStatus.Illegal;
        }

        state.TextPos = (int)poppedTextPos;

        FuzzyData data = default;
        data.FuzzyType = poppedType;
        data.NewNode = currNode;

        // Upstream's 'data.step = step', where 'step' is what fuzzy_match_item PUSHED - so a
        // zero-width item retries with a step of 0 here where its first attempt carried 1 or -1.
        // That is upstream's behaviour, not an oversight in the port: an insertion on the retry of a
        // zero-width item therefore does not move the position.
        data.Step = step;

        /* bstack: - */

        // Upstream guards this with 'if (data.fuzzy_type >= 0)' on an RE_UINT8, which is always true.
        --fuzzyCounts[data.FuzzyType];

        // Permit insertion except initially when searching (it's better just to start searching one
        // character later).
        data.PermitInsertion = !search || state.TextPos != state.SearchAnchor;

        step = advance ? data.Step : (sbyte)0;

        int status = MatchStatus.Failure;

        for (++data.FuzzyType; data.FuzzyType < FuzzyValue.Count; data.FuzzyType++)
        {
            status = NextFuzzyMatchItem(state, ref data, false, step);

            if (status < 0)
            {
                return status;
            }

            if (status == MatchStatus.Success)
            {
                break;
            }
        }

        if (status != MatchStatus.Success)
        {
            return MatchStatus.Failure;
        }

        state.Bstack.PushNode(currNode);
        state.Bstack.PushInt8(step);
        state.Bstack.PushSize(state.TextPos);
        state.Bstack.PushUInt8((byte)data.FuzzyType);
        state.Bstack.PushUInt8(op);

        /* bstack: node step text_pos fuzzy_type op */

        state.RecordFuzzy(data.FuzzyType, FuzzyChangePos(state, in data));

        ++fuzzyCounts[data.FuzzyType];
        ++state.CaptureChange;

        state.TextPos = data.NewTextPos;
        node = data.NewNode!;

        return MatchStatus.Success;
    }

    /// <summary>
    /// Upstream <c>fuzzy_insert</c> (line 10346): tries a fuzzy insertion of characters, initially
    /// none, after a complete string.
    /// </summary>
    /// <remarks>
    /// A string that matched exactly has nothing left to fuzz, so this is the only way an insertion
    /// can be charged next to one. It records no error itself - it pushes a <c>FUZZY_INSERT</c>
    /// frame with a count of zero, and each backtrack into that frame lengthens the insertion by one
    /// character.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="step">Which way the string was travelling, <c>1</c> or <c>-1</c>.</param>
    /// <param name="node">Where matching carries on if the frame is never retried.</param>
    private static void FuzzyInsert(MatchState state, sbyte step, Node? node)
    {
        int limit = step > 0 ? state.SliceEnd : state.SliceStart;

        if (state.TextPos == limit || !InsertionPermitted(state, state.FuzzyNode!, state.FuzzyCounts))
        {
            return;
        }

        state.Bstack.PushInt8(step);
        state.Bstack.PushSize(state.TextPos);
        state.Bstack.PushSize(0);
        state.Bstack.PushNode(node);
        state.Bstack.PushUInt8((byte)Opcode.FuzzyInsert);

        /* bstack: step text_pos count node FUZZY_INSERT */
    }

    /// <summary>Upstream <c>retry_fuzzy_insert</c> (line 10372): one more inserted character.</summary>
    /// <param name="state">The match state.</param>
    /// <param name="node">On success, where matching carries on.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int RetryFuzzyInsert(MatchState state, ref Node node)
    {
        /* bstack: step text_pos count node */

        if (
            !state.Bstack.PopNode(state.Pattern, out Node? currNode)
            || !state.Bstack.PopSize(out long count)
            || !state.Bstack.PopSize(out long poppedTextPos)
            || !state.Bstack.PopInt8(out sbyte step)
        )
        {
            return MatchStatus.Illegal;
        }

        state.TextPos = (int)poppedTextPos;

        int limit = step > 0 ? state.SliceEnd : state.SliceStart;

        if (
            state.TextPos == limit
            || !InsertionPermitted(state, state.FuzzyNode!, state.FuzzyCounts)
            || !FuzzyExtMatch(state, state.FuzzyNode, state.TextPos)
        )
        {
            while (count > 0)
            {
                state.UnrecordFuzzy();
                --state.FuzzyCounts[FuzzyValue.Ins];
                --count;
            }

            return MatchStatus.Failure;
        }

        state.TextPos = Step(state, state.TextPos, step);
        ++count;

        state.Bstack.PushInt8(step);
        state.Bstack.PushSize(state.TextPos);
        state.Bstack.PushSize(count);
        state.Bstack.PushNode(currNode);
        state.Bstack.PushUInt8((byte)Opcode.FuzzyInsert);

        /* bstack: step text_pos count node FUZZY_INSERT */

        state.RecordFuzzy(FuzzyValue.Ins, Step(state, state.TextPos, (sbyte)-step));

        ++state.FuzzyCounts[FuzzyValue.Ins];
        ++state.CaptureChange;

        node = currNode!;

        return MatchStatus.Success;
    }

    /// <summary>Upstream <c>fuzzy_match_string</c> (line 10431): a first try at fuzzing a string.</summary>
    /// <remarks>
    /// Shared by the <c>STRING*</c> arms, where <paramref name="stringPos"/> indexes the node's own
    /// values, and by the <c>REF_GROUP*</c> arms, where it is a position in the subject. Upstream
    /// does not have to tell them apart; this port does, because only one of the two is measured in
    /// UTF-16 code units.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="search">Whether this is a search rather than an anchored match.</param>
    /// <param name="node">The item that failed, which goes on the backtracking stack.</param>
    /// <param name="stringPos">How far into the item the comparison had got; moved on success.</param>
    /// <param name="step">Which way the item travels, <c>1</c> or <c>-1</c>.</param>
    /// <param name="stringPosIsText">Whether <paramref name="stringPos"/> is a subject position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int FuzzyMatchString(
        MatchState state,
        bool search,
        Node node,
        ref int stringPos,
        sbyte step,
        bool stringPosIsText
    )
    {
        long[] fuzzyCounts = state.FuzzyCounts;

        if (!AnyErrorPermitted(state))
        {
            return MatchStatus.Failure;
        }

        FuzzyData data = default;
        data.NewStringPos = stringPos;
        data.StringPosIsText = stringPosIsText;
        data.Step = step;

        // Permit insertion except initially when searching (it's better just to start searching one
        // character later).
        data.PermitInsertion = !search || state.TextPos != state.SearchAnchor;

        int status = MatchStatus.Failure;

        for (data.FuzzyType = 0; data.FuzzyType < FuzzyValue.Count; data.FuzzyType++)
        {
            status = NextFuzzyMatchItem(state, ref data, true, data.Step);

            if (status < 0)
            {
                return status;
            }

            if (status == MatchStatus.Success)
            {
                break;
            }
        }

        if (status != MatchStatus.Success)
        {
            return MatchStatus.Failure;
        }

        state.Bstack.PushNode(node);
        state.Bstack.PushInt8(step);
        state.Bstack.PushSize(stringPos);
        state.Bstack.PushSize(state.TextPos);
        state.Bstack.PushUInt8((byte)data.FuzzyType);
        state.Bstack.PushUInt8((byte)node.Op);

        /* bstack: node step string_pos text_pos fuzzy_type op */

        // Upstream records the position the comparison had reached, not 'new_text_pos - step' the
        // way the single-item path does (:10483 against :10245): a string charges the error where it
        // stopped, whatever kind of error it turned out to be.
        state.RecordFuzzy(data.FuzzyType, state.TextPos);

        ++fuzzyCounts[data.FuzzyType];
        ++state.CaptureChange;

        state.TextPos = data.NewTextPos;
        stringPos = data.NewStringPos;

        return MatchStatus.Success;
    }

    /// <summary>Upstream <c>retry_fuzzy_match_string</c> (line 10499): the next kind of error.</summary>
    /// <param name="state">The match state.</param>
    /// <param name="op">The opcode whose frame is being retried, which goes back on the stack.</param>
    /// <param name="search">Whether this is a search rather than an anchored match.</param>
    /// <param name="node">On success, where matching carries on.</param>
    /// <param name="stringPos">Receives how far into the item the retry got to.</param>
    /// <param name="stringPosIsText">Whether <paramref name="stringPos"/> is a subject position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int RetryFuzzyMatchString(
        MatchState state,
        byte op,
        bool search,
        ref Node node,
        ref int stringPos,
        bool stringPosIsText
    )
    {
        long[] fuzzyCounts = state.FuzzyCounts;

        state.UnrecordFuzzy();

        /* bstack: node step string_pos text_pos fuzzy_type */

        if (
            !state.Bstack.PopUInt8(out byte poppedType)
            || !state.Bstack.PopSize(out long poppedTextPos)
            || !state.Bstack.PopSize(out long poppedStringPos)
            || !state.Bstack.PopInt8(out sbyte step)
            || !state.Bstack.PopNode(state.Pattern, out Node? newNode)
        )
        {
            return MatchStatus.Illegal;
        }

        state.TextPos = (int)poppedTextPos;
        stringPos = (int)poppedStringPos;

        FuzzyData data = default;
        data.FuzzyType = poppedType;
        data.Step = step;
        data.NewStringPos = stringPos;
        data.StringPosIsText = stringPosIsText;

        --fuzzyCounts[data.FuzzyType];

        // Permit insertion except initially when searching (it's better just to start searching one
        // character later).
        data.PermitInsertion = !search || state.TextPos != state.SearchAnchor;

        int status = MatchStatus.Failure;

        for (++data.FuzzyType; data.FuzzyType < FuzzyValue.Count; data.FuzzyType++)
        {
            status = NextFuzzyMatchItem(state, ref data, true, data.Step);

            if (status < 0)
            {
                return status;
            }

            if (status == MatchStatus.Success)
            {
                break;
            }
        }

        if (status != MatchStatus.Success)
        {
            return MatchStatus.Failure;
        }

        state.Bstack.PushNode(newNode);
        state.Bstack.PushInt8(data.Step);
        state.Bstack.PushSize(stringPos);
        state.Bstack.PushSize(state.TextPos);
        state.Bstack.PushUInt8((byte)data.FuzzyType);
        state.Bstack.PushUInt8(op);

        state.RecordFuzzy(data.FuzzyType, state.TextPos);

        /* bstack: node step string_pos text_pos fuzzy_type op */

        ++fuzzyCounts[data.FuzzyType];
        ++state.CaptureChange;

        state.TextPos = data.NewTextPos;
        node = newNode!;
        stringPos = data.NewStringPos;

        return MatchStatus.Success;
    }

    /// <summary>
    /// Upstream <c>next_fuzzy_match_string_fld</c> (line 10580): one kind of error against a string
    /// whose subject side is being full-case-folded.
    /// </summary>
    /// <remarks>
    /// The difference from <see cref="NextFuzzyMatchItem"/> is that every position this moves is a
    /// position in the <em>folding</em> of one subject character, not in the subject: one subject
    /// character can answer for three pattern characters, so an error inside a folding must not move
    /// <c>text_pos</c> at all. That is also why the deletion arm has no <c>step == 0</c> guard -
    /// a string item always travels.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="data">What the attempt is working on; written back through.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int NextFuzzyMatchStringFld(MatchState state, ref FuzzyData data)
    {
        if (!ThisErrorPermitted(state, data.FuzzyType))
        {
            return MatchStatus.Failure;
        }

        data.NewTextPos = state.TextPos;

        int newPos;

        switch (data.FuzzyType)
        {
            case FuzzyValue.Del:
                // Could a character at text_pos have been deleted?
                data.NewStringPos += data.Step;

                return MatchStatus.Success;
            case FuzzyValue.Ins:
                // Could the character at text_pos have been inserted?
                if (!data.PermitInsertion)
                {
                    return MatchStatus.Failure;
                }

                newPos = data.NewFoldedPos + data.Step;

                if (newPos >= 0 && newPos <= data.FoldedLen)
                {
                    if (!FuzzyExtMatch(state, state.FuzzyNode, data.NewTextPos))
                    {
                        return MatchStatus.Failure;
                    }

                    data.NewFoldedPos = newPos;

                    return MatchStatus.Success;
                }

                return CheckFuzzyPartial(state, newPos);
            case FuzzyValue.Sub:
                // Could the character at text_pos have been substituted?
                newPos = data.NewFoldedPos + data.Step;

                if (newPos >= 0 && newPos <= data.FoldedLen)
                {
                    if (!FuzzyExtMatch(state, state.FuzzyNode, data.NewTextPos))
                    {
                        return MatchStatus.Failure;
                    }

                    data.NewFoldedPos = newPos;
                    data.NewStringPos += data.Step;

                    return MatchStatus.Success;
                }

                return CheckFuzzyPartial(state, newPos);
            default:
                return MatchStatus.Failure;
        }
    }

    /// <summary>
    /// Upstream's "an insertion inside a folding is free" rule, spelled out four times
    /// (<c>upstream/src/_regex.c</c> lines 10659-10665, 10761-10767, 10905-10911 and, in a different
    /// shape, 11019).
    /// </summary>
    /// <remarks>
    /// Once the comparison is part way through a subject character's folding, the search anchor has
    /// already been left behind even though <c>text_pos</c> has not moved, so the "no insertion at
    /// the anchor" rule stops applying.
    /// </remarks>
    /// <param name="data">The attempt being set up.</param>
    /// <param name="search">Whether this is a search rather than an anchored match.</param>
    /// <param name="atAnchor">Whether <c>text_pos</c> is still at the search anchor.</param>
    /// <returns>Whether an insertion may be tried.</returns>
    private static bool PermitInsertionInFold(in FuzzyData data, bool search, bool atAnchor)
    {
        if (!search || !atAnchor)
        {
            return true;
        }

        return data.Step > 0 ? data.NewFoldedPos != 0 : data.NewFoldedPos != data.FoldedLen;
    }

    /// <summary>
    /// Upstream <c>fuzzy_match_string_fld</c> (line 10635): a first try at fuzzing a string whose
    /// subject side is being full-case-folded.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="search">Whether this is a search rather than an anchored match.</param>
    /// <param name="node">The item that failed, which goes on the backtracking stack.</param>
    /// <param name="stringPos">How far into the node's values the comparison had got.</param>
    /// <param name="foldedPos">How far into the subject character's folding it had got.</param>
    /// <param name="foldedLen">The length of that folding.</param>
    /// <param name="step">Which way the item travels, <c>1</c> or <c>-1</c>.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int FuzzyMatchStringFld(
        MatchState state,
        bool search,
        Node node,
        ref int stringPos,
        ref int foldedPos,
        int foldedLen,
        sbyte step
    )
    {
        long[] fuzzyCounts = state.FuzzyCounts;

        if (!AnyErrorPermitted(state))
        {
            return MatchStatus.Failure;
        }

        FuzzyData data = default;
        data.NewStringPos = stringPos;
        data.NewFoldedPos = foldedPos;
        data.FoldedLen = foldedLen;
        data.Step = step;
        data.PermitInsertion = PermitInsertionInFold(in data, search, state.TextPos == state.SearchAnchor);

        int status = MatchStatus.Failure;

        for (data.FuzzyType = 0; data.FuzzyType < FuzzyValue.Count; data.FuzzyType++)
        {
            status = NextFuzzyMatchStringFld(state, ref data);

            if (status < 0)
            {
                return status;
            }

            if (status == MatchStatus.Success)
            {
                break;
            }
        }

        if (status != MatchStatus.Success)
        {
            return MatchStatus.Failure;
        }

        state.Bstack.PushNode(node);
        state.Bstack.PushInt8(step);
        state.Bstack.PushSize(stringPos);
        state.Bstack.PushSize(foldedPos);
        state.Bstack.PushSize(foldedLen);
        state.Bstack.PushSize(state.TextPos);
        state.Bstack.PushUInt8((byte)data.FuzzyType);
        state.Bstack.PushUInt8((byte)node.Op);

        /* bstack: node step string_pos folded_pos folded_len text_pos fuzzy_type op */

        state.RecordFuzzy(data.FuzzyType, state.TextPos);

        ++fuzzyCounts[data.FuzzyType];
        ++state.CaptureChange;

        state.TextPos = data.NewTextPos;
        stringPos = data.NewStringPos;
        foldedPos = data.NewFoldedPos;

        return MatchStatus.Success;
    }

    /// <summary>Upstream <c>retry_fuzzy_match_string_fld</c> (line 10721).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="op">The opcode whose frame is being retried, which goes back on the stack.</param>
    /// <param name="search">Whether this is a search rather than an anchored match.</param>
    /// <param name="node">On success, where matching carries on.</param>
    /// <param name="stringPos">Receives how far into the node's values the retry got to.</param>
    /// <param name="foldedPos">Receives how far into the folding the retry got to.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int RetryFuzzyMatchStringFld(
        MatchState state,
        byte op,
        bool search,
        ref Node node,
        ref int stringPos,
        ref int foldedPos
    )
    {
        long[] fuzzyCounts = state.FuzzyCounts;

        state.UnrecordFuzzy();

        /* bstack: node step string_pos folded_pos folded_len text_pos fuzzy_type */

        if (
            !state.Bstack.PopUInt8(out byte poppedType)
            || !state.Bstack.PopSize(out long poppedTextPos)
            || !state.Bstack.PopSize(out long poppedFoldedLen)
            || !state.Bstack.PopSize(out long poppedFoldedPos)
            || !state.Bstack.PopSize(out long poppedStringPos)
            || !state.Bstack.PopInt8(out sbyte step)
            || !state.Bstack.PopNode(state.Pattern, out Node? newNode)
        )
        {
            return MatchStatus.Illegal;
        }

        state.TextPos = (int)poppedTextPos;
        stringPos = (int)poppedStringPos;

        int currFoldedPos = (int)poppedFoldedPos;

        FuzzyData data = default;
        data.FuzzyType = poppedType;
        data.FoldedLen = (int)poppedFoldedLen;
        data.Step = step;
        data.NewStringPos = stringPos;
        data.NewFoldedPos = currFoldedPos;

        --fuzzyCounts[data.FuzzyType];

        data.PermitInsertion = PermitInsertionInFold(in data, search, state.TextPos == state.SearchAnchor);

        int status = MatchStatus.Failure;

        for (++data.FuzzyType; data.FuzzyType < FuzzyValue.Count; data.FuzzyType++)
        {
            status = NextFuzzyMatchStringFld(state, ref data);

            if (status < 0)
            {
                return status;
            }

            if (status == MatchStatus.Success)
            {
                break;
            }
        }

        if (status != MatchStatus.Success)
        {
            return MatchStatus.Failure;
        }

        state.Bstack.PushNode(newNode);
        state.Bstack.PushInt8(data.Step);
        state.Bstack.PushSize(stringPos);
        state.Bstack.PushSize(currFoldedPos);
        state.Bstack.PushSize(data.FoldedLen);
        state.Bstack.PushSize(state.TextPos);
        state.Bstack.PushUInt8((byte)data.FuzzyType);
        state.Bstack.PushUInt8(op);

        state.RecordFuzzy(data.FuzzyType, state.TextPos);

        /* bstack: node step string_pos folded_pos folded_len text_pos fuzzy_type op */

        ++fuzzyCounts[data.FuzzyType];
        ++state.CaptureChange;

        state.TextPos = data.NewTextPos;
        node = newNode!;
        stringPos = data.NewStringPos;
        foldedPos = data.NewFoldedPos;

        return MatchStatus.Success;
    }

    /// <summary>
    /// Upstream <c>fuzzy_ext_match_group_fld</c> (line 10033): the <c>{...:test}</c> constraint
    /// asked of a character inside a folding rather than of a subject character.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The character the constraint sees is the one inside the subject character's folding, so every
    /// arm goes through <see cref="FoldedCharAt"/> rather than reading the subject directly, and the
    /// position it works from is <c>state-&gt;text_pos</c> rather than the caller's - the folded
    /// position indexes into the folding, not into the subject.
    /// </para>
    /// <para>
    /// <b>This switch is missing the <c>SET_*_IGN</c> arms that <see cref="FuzzyExtMatch"/> has</b>,
    /// as well as the <c>SET_*_REV</c> ones neither has. Since the function runs only when both
    /// sides are being full-case-folded - which needs <c>(?f)</c> with <c>(?i)</c> - every test node
    /// reaching it is an <c>_IGN</c> one, so a set test here always falls off the end of the switch
    /// to <see langword="true"/> and constrains nothing, where the same test forward would bite.
    /// Measured, and pinned by <c>Gaps/Engine/FuzzyTestConstraintTests.cs</c>.
    /// </para>
    /// <para>
    /// By the same argument the non-<c>_IGN</c> arms are unreachable: the fuzzy-test grammar accepts
    /// only a character set, so <c>{e&lt;=1:(?-i:x)}</c> is a parse error and a test cannot opt out
    /// of the enclosing <c>(?i)</c>. They are ported because upstream writes them.
    /// </para>
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="fuzzyNode">The section, which may be <see langword="null"/>.</param>
    /// <param name="foldedPos">The position in the folding the error would touch.</param>
    /// <returns><see langword="true"/> if the constraint allows it.</returns>
    private static bool FuzzyExtMatchGroupFld(MatchState state, Node? fuzzyNode, int foldedPos)
    {
        Node? testNode = fuzzyNode?.Next2.Node;

        if (testNode is null)
        {
            return true;
        }

        // The reversed arms read 'folded_char_at(text_pos - 1, folded_pos - 1)'. 'folded_pos - 1'
        // cannot be negative: the only two callers reach here from the INS and SUB arms of
        // 'NextFuzzyMatchGroupFld', both behind 'new_folded_pos + step >= 0', and a reversed test
        // node means a step of -1, so 'folded_pos' is at least 1 by the time the arm runs.
        return testNode.Op switch
        {
            Opcode.Character
            or Opcode.CharacterIgn
            or Opcode.Property
            or Opcode.PropertyIgn
            or Opcode.Range
            or Opcode.RangeIgn
            or Opcode.SetDiff
            or Opcode.SetInter
            or Opcode.SetSymDiff
            or Opcode.SetUnion => state.TextPos < state.SliceEnd
                && MatchesOne(state.Encoding, testNode, FoldedCharAt(state, state.TextPos, foldedPos))
                    == testNode.Match,
            Opcode.CharacterRev
            or Opcode.CharacterIgnRev
            or Opcode.PropertyRev
            or Opcode.PropertyIgnRev
            or Opcode.RangeRev
            or Opcode.RangeIgnRev => state.TextPos > state.SliceStart
                && MatchesOne(
                    state.Encoding,
                    testNode,
                    FoldedCharAt(state, state.PrevPos(state.TextPos), foldedPos - 1)
                ) == testNode.Match,
            _ => true,
        };
    }

    /// <summary>Upstream <c>folded_char_at</c> (line 10014).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="pos">The subject position whose character is folded.</param>
    /// <param name="foldedPos">Which character of the folding to return.</param>
    /// <returns>That character.</returns>
    private static uint FoldedCharAt(MatchState state, int pos, int foldedPos)
    {
        Span<uint> folded = stackalloc uint[UnicodeTables.MaxFolded];

        _ = Encodings.FullCaseFold(state.Encoding, state.CharAt(pos), folded);

        return folded[foldedPos];
    }

    /// <summary>
    /// Upstream <c>next_fuzzy_match_group_fld</c> (line 10824): one kind of error against a group
    /// reference where <b>both</b> sides are being full-case-folded.
    /// </summary>
    /// <remarks>
    /// Two foldings run at different speeds here, so an error moves the subject's
    /// <c>folded_pos</c> and the group's <c>gfolded_pos</c> separately, and neither is a subject
    /// position. A deletion moves only the group's side.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="data">What the attempt is working on; written back through.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int NextFuzzyMatchGroupFld(MatchState state, ref FuzzyData data)
    {
        if (!ThisErrorPermitted(state, data.FuzzyType))
        {
            return MatchStatus.Failure;
        }

        data.NewTextPos = state.TextPos;

        int newPos;

        switch (data.FuzzyType)
        {
            case FuzzyValue.Del:
                // Could a character at text_pos have been deleted?
                data.NewGfoldedPos += data.Step;

                return MatchStatus.Success;
            case FuzzyValue.Ins:
                // Could the character at text_pos have been inserted?
                if (!data.PermitInsertion)
                {
                    return MatchStatus.Failure;
                }

                newPos = data.NewFoldedPos + data.Step;

                if (newPos >= 0 && newPos <= data.FoldedLen)
                {
                    if (!FuzzyExtMatchGroupFld(state, state.FuzzyNode, data.NewFoldedPos))
                    {
                        return MatchStatus.Failure;
                    }

                    data.NewFoldedPos = newPos;

                    return MatchStatus.Success;
                }

                return CheckFuzzyPartial(state, newPos);
            case FuzzyValue.Sub:
                // Could the character at text_pos have been substituted?
                newPos = data.NewFoldedPos + data.Step;

                if (newPos >= 0 && newPos <= data.FoldedLen)
                {
                    if (!FuzzyExtMatchGroupFld(state, state.FuzzyNode, data.NewFoldedPos))
                    {
                        return MatchStatus.Failure;
                    }

                    data.NewFoldedPos = newPos;
                    data.NewGfoldedPos += data.Step;

                    return MatchStatus.Success;
                }

                return CheckFuzzyPartial(state, newPos);
            default:
                return MatchStatus.Failure;
        }
    }

    /// <summary>Upstream <c>fuzzy_match_group_fld</c> (line 10879).</summary>
    /// <remarks>
    /// Upstream's <c>new_group_pos</c> local is copied in from <c>*group_pos</c> and written back
    /// out unchanged (<c>:10896</c> against <c>:10961</c>) - nothing in between touches it, because
    /// <c>string_pos</c> moves only when <c>gfolded_pos</c> runs out, which the caller does. So the
    /// group position is not a parameter here.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="search">Whether this is a search rather than an anchored match.</param>
    /// <param name="node">The item that failed, which goes on the backtracking stack.</param>
    /// <param name="foldedPos">How far into the subject character's folding the comparison had got.</param>
    /// <param name="foldedLen">The length of that folding.</param>
    /// <param name="groupPos">The position in the referenced capture, pushed so a retry can restore it.</param>
    /// <param name="gfoldedPos">How far into the group character's folding the comparison had got.</param>
    /// <param name="gfoldedLen">The length of that folding.</param>
    /// <param name="step">Which way the item travels, <c>1</c> or <c>-1</c>.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int FuzzyMatchGroupFld(
        MatchState state,
        bool search,
        Node node,
        ref int foldedPos,
        int foldedLen,
        int groupPos,
        ref int gfoldedPos,
        int gfoldedLen,
        sbyte step
    )
    {
        long[] fuzzyCounts = state.FuzzyCounts;

        if (!AnyErrorPermitted(state))
        {
            return MatchStatus.Failure;
        }

        FuzzyData data = default;
        data.NewFoldedPos = foldedPos;
        data.FoldedLen = foldedLen;
        data.NewGfoldedPos = gfoldedPos;
        data.Step = step;
        data.PermitInsertion = PermitInsertionInFold(in data, search, state.TextPos == state.SearchAnchor);

        int status = MatchStatus.Failure;

        for (data.FuzzyType = 0; data.FuzzyType < FuzzyValue.Count; data.FuzzyType++)
        {
            status = NextFuzzyMatchGroupFld(state, ref data);

            if (status < 0)
            {
                return status;
            }

            if (status == MatchStatus.Success)
            {
                break;
            }
        }

        if (status != MatchStatus.Success)
        {
            return MatchStatus.Failure;
        }

        state.Bstack.PushNode(node);
        state.Bstack.PushInt8(step);
        state.Bstack.PushSize(gfoldedPos);
        state.Bstack.PushSize(gfoldedLen);
        state.Bstack.PushSize(groupPos);
        state.Bstack.PushSize(foldedPos);
        state.Bstack.PushSize(foldedLen);
        state.Bstack.PushSize(state.TextPos);
        state.Bstack.PushUInt8((byte)data.FuzzyType);
        state.Bstack.PushUInt8((byte)node.Op);

        /* bstack: node step gfolded_pos gfolded_len group_pos folded_pos folded_len text_pos
         * fuzzy_type op
         */

        state.RecordFuzzy(data.FuzzyType, state.TextPos);

        ++fuzzyCounts[data.FuzzyType];
        ++state.CaptureChange;

        state.TextPos = data.NewTextPos;
        foldedPos = data.NewFoldedPos;
        gfoldedPos = data.NewGfoldedPos;

        return MatchStatus.Success;
    }

    /// <summary>Upstream <c>retry_fuzzy_match_group_fld</c> (line 10972).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="op">The opcode whose frame is being retried, which goes back on the stack.</param>
    /// <param name="search">Whether this is a search rather than an anchored match.</param>
    /// <param name="node">On success, where matching carries on.</param>
    /// <param name="foldedPos">Receives how far into the subject's folding the retry got to.</param>
    /// <param name="groupPos">Receives the restored position in the referenced capture.</param>
    /// <param name="gfoldedPos">Receives how far into the group's folding the retry got to.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int RetryFuzzyMatchGroupFld(
        MatchState state,
        byte op,
        bool search,
        ref Node node,
        ref int foldedPos,
        ref int groupPos,
        ref int gfoldedPos
    )
    {
        long[] fuzzyCounts = state.FuzzyCounts;

        state.UnrecordFuzzy();

        /* bstack: node step gfolded_pos gfolded_len group_pos folded_pos folded_len text_pos
         * fuzzy_type
         */

        if (
            !state.Bstack.PopUInt8(out byte poppedType)
            || !state.Bstack.PopSize(out long poppedTextPos)
            || !state.Bstack.PopSize(out long poppedFoldedLen)
            || !state.Bstack.PopSize(out long poppedFoldedPos)
            || !state.Bstack.PopSize(out long poppedGroupPos)
            || !state.Bstack.PopSize(out long poppedGfoldedLen)
            || !state.Bstack.PopSize(out long poppedGfoldedPos)
            || !state.Bstack.PopInt8(out sbyte step)
            || !state.Bstack.PopNode(state.Pattern, out Node? newNode)
        )
        {
            return MatchStatus.Illegal;
        }

        state.TextPos = (int)poppedTextPos;

        int newFoldedPos = (int)poppedFoldedPos;
        int newGroupPos = (int)poppedGroupPos;
        int gfoldedLen = (int)poppedGfoldedLen;
        int newGfoldedPos = (int)poppedGfoldedPos;

        FuzzyData data = default;
        data.FuzzyType = poppedType;
        data.FoldedLen = (int)poppedFoldedLen;
        data.Step = step;
        data.NewFoldedPos = newFoldedPos;
        data.NewGfoldedPos = newGfoldedPos;

        --fuzzyCounts[data.FuzzyType];

        // Permit insertion except initially when searching. Upstream spells the folding half of the
        // rule differently here from the three places PermitInsertionInFold covers (:11019): one
        // '||' chain, and with no step test, so a reverse retry asks 'folded_pos != folded_len'
        // where the first attempt would have asked 'folded_pos != 0'. Ported as written.
        data.PermitInsertion = !search || state.TextPos != state.SearchAnchor || data.NewFoldedPos != data.FoldedLen;

        int status = MatchStatus.Failure;

        for (++data.FuzzyType; data.FuzzyType < FuzzyValue.Count; data.FuzzyType++)
        {
            status = NextFuzzyMatchGroupFld(state, ref data);

            if (status < 0)
            {
                return status;
            }

            if (status == MatchStatus.Success)
            {
                break;
            }
        }

        if (status != MatchStatus.Success)
        {
            return MatchStatus.Failure;
        }

        state.Bstack.PushNode(newNode);
        state.Bstack.PushInt8(data.Step);
        state.Bstack.PushSize(newGfoldedPos);
        state.Bstack.PushSize(gfoldedLen);
        state.Bstack.PushSize(newGroupPos);
        state.Bstack.PushSize(newFoldedPos);
        state.Bstack.PushSize(data.FoldedLen);
        state.Bstack.PushSize(state.TextPos);
        state.Bstack.PushUInt8((byte)data.FuzzyType);
        state.Bstack.PushUInt8(op);

        state.RecordFuzzy(data.FuzzyType, state.TextPos);

        /* bstack: node step gfolded_pos gfolded_len group_pos folded_pos folded_len text_pos
         * fuzzy_type op
         */

        ++fuzzyCounts[data.FuzzyType];
        ++state.CaptureChange;

        state.TextPos = data.NewTextPos;
        node = newNode!;
        groupPos = newGroupPos;
        foldedPos = data.NewFoldedPos;
        gfoldedPos = data.NewGfoldedPos;

        return MatchStatus.Success;
    }

    /// <summary>
    /// Port of <c>simple_string_search</c> (<c>upstream/src/_regex.c</c> lines 5231-5378): the
    /// character-at-a-time search for a string node's values, and the only arm that can report a
    /// string the subject truncated.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="node">The string node to look for.</param>
    /// <param name="textPos">Where to start looking.</param>
    /// <param name="limit">The position to stop at.</param>
    /// <param name="isPartial">Set when the string was found but ran off the end of the text.</param>
    /// <param name="cancelled">Set when the caller's timeout or token ended the search.</param>
    /// <returns>Where the string starts, or <c>-1</c>.</returns>
    /// <remarks>
    /// <para>
    /// Upstream's three <c>charsize</c> arms are one arm here, as everywhere else in this port: the
    /// subject is always UTF-16 and <see cref="MatchState.CharAt"/> decodes a surrogate pair. That
    /// is also why the inner walk steps with <see cref="MatchState.NextPos"/> rather than by one -
    /// upstream's <c>text_ptr[s_pos]</c> indexes CHARACTERS, and an astral character is two code
    /// units here.
    /// </para>
    /// </remarks>
    private static int SimpleStringSearch(
        MatchState state,
        Node node,
        int textPos,
        int limit,
        out bool isPartial,
        out bool cancelled
    )
    {
        isPartial = false;
        cancelled = false;

        int length = node.Values.Count;
        uint checkChar = node.Values[0];

        int pos = textPos;
        while (pos < limit)
        {
            // NOT UPSTREAM'S (S60). Upstream's prefilter runs uninterrupted, because its caller can
            // only be interrupted between attempts; this port promises a timeout and a
            // CancellationToken are honoured DURING one, so the prefilter polls on the same
            // 'Iterations' gate the matching loop uses (:5242, and :5102 at 'start_match:').
            state.Iterations = (ushort)(state.Iterations + 0x100);

            if (state.Iterations == 0 && SafeCheckCancel(state))
            {
                cancelled = true;
                return -1;
            }

            if (state.CharAt(pos) == checkChar)
            {
                int next = state.NextPos(pos);
                int stringPos = 1;

                // Upstream's 'for (;;)' (:5256): 'while (true)' is this file's idiom for it.
                while (true)
                {
                    if (stringPos >= length)
                    {
                        // End of search string.
                        return pos;
                    }

                    if (next >= limit)
                    {
                        // Off the end of the text.
                        if (state.PartialSide == MatchState.PartialRight)
                        {
                            isPartial = true;
                            return pos;
                        }

                        return -1;
                    }

                    if (!SameChar(state.CharAt(next), node.Values[stringPos]))
                    {
                        break;
                    }

                    next = state.NextPos(next);
                    ++stringPos;
                }
            }

            pos = state.NextPos(pos);
        }

        // Off the end of the text.
        if (state.PartialSide == MatchState.PartialRight)
        {
            isPartial = true;
            return pos;
        }

        return -1;
    }

    /// <summary>
    /// Port of <c>string_search</c> (<c>upstream/src/_regex.c</c> lines 6596-6633): find the
    /// required string, fast where the subject allows it.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="node">The string node to look for.</param>
    /// <param name="textPos">Where to start looking.</param>
    /// <param name="limit">The position to stop at.</param>
    /// <param name="isPartial">Set when the string was found but ran off the end of the text.</param>
    /// <param name="cancelled">Set when the caller's timeout or token ended the search.</param>
    /// <returns>Where the string starts, or <c>-1</c>.</returns>
    /// <remarks>
    /// <para>
    /// <b>The fast half is .NET's, not upstream's, and this is the one deliberate structural
    /// difference in S60.</b> Upstream builds Boyer-Moore bad-character and good-suffix tables on
    /// the node at first use (<c>build_fast_tables</c>, <c>:6298</c>, under a lock because the node
    /// is mutated) and walks them in <c>fast_string_search</c> (<c>:5846</c>).
    /// <see cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/> answers the same
    /// question - the first ordinal occurrence of a substring - with a vectorised search and no
    /// per-node mutable state at all, which is also why it keeps S52b's immutability contract that
    /// upstream's lock exists to paper over. The slice file asks for exactly this
    /// (<c>S60-prefilter-family.md</c> item 4: "implemented with the library primitives, not by
    /// hand").
    /// </para>
    /// <para>
    /// sync-divergence: upstream builds Boyer-Moore tables on the node under a lock and walks them
    /// in <c>fast_string_search</c> / one character at a time in <c>simple_string_search</c> / we
    /// call <c>MemoryExtensions.IndexOf</c> over the slice, against a needle built once in
    /// <c>Compile</c> / it is vectorised, allocation-free, and leaves the compiled pattern
    /// immutable, which is S52b's contract and what upstream's lock exists to paper over.
    /// Re-aligning: a sync slice need not follow changes to <c>build_fast_tables</c> or the skip
    /// loop at all; it must follow changes to which POSITIONS <c>string_search</c> may return, and
    /// those live in <see cref="LocateRequiredString"/>.
    /// </para>
    /// <para>
    /// <b>It is answer-identical rather than merely equivalent in spirit</b>, on one condition that
    /// <see cref="PatternObject.ReqStringText"/> enforces: the needle holds no unpaired surrogate.
    /// Given that, the UTF-16 encoding of the needle occurs at a code-unit index exactly where the
    /// codepoint sequence occurs at a character index, and no match can start inside a surrogate
    /// pair, because a position inside a pair holds a low surrogate and the needle's first unit
    /// never is one. Where the condition fails the needle text is not built and the whole search
    /// falls back to <see cref="SimpleStringSearch"/>.
    /// </para>
    /// <para>
    /// <b>The partial retry follows upstream's shape</b> (<c>:6622-6627</c>): the fast search cannot
    /// see a string the subject truncated, so when nothing was found and a partial match to the
    /// right is allowed, the scalar search runs again close to the end. Upstream starts that retry
    /// at <c>limit - (value_count - 1)</c> characters; this port steps back a whole
    /// <c>needle.Length</c> code units and then off a low surrogate, which is a WIDER window and so
    /// cannot lose an occurrence - the scalar search is exact, so a wider window cannot invent one
    /// either. It is wider because upstream's arithmetic is in characters and this one is in code
    /// units, and closing that gap exactly would cost a walk the widening avoids.
    /// </para>
    /// </remarks>
    private static int StringSearch(
        MatchState state,
        Node node,
        int textPos,
        int limit,
        out bool isPartial,
        out bool cancelled
    )
    {
        isPartial = false;
        cancelled = false;

        if (state.Pattern.ReqStringText is not string needle)
        {
            return SimpleStringSearch(state, node, textPos, limit, out isPartial, out cancelled);
        }

        // ONE SWEEP, NOT A POLLED ONE, and S60 tried it the other way first. The sweep was cut into
        // 64 Ki chunks with a cancellation poll between them, on the reasoning that an 'IndexOf' a
        // caller cannot interrupt is a hole in the promise S51 made. It is not, and the measurement
        // says why:
        //
        //   * Every attempt opens with a poll already. 'MatchState.InitMatch' sets 'Iterations' to 0
        //     (MatchState.cs:749) and 'basic_match' opens at 'start_match:' with
        //     'state.Iterations == 0 && SafeCheckCancel(state)' (:5102), so the gate is OPEN on
        //     every attempt and a spent budget is caught a few instructions before this method runs.
        //     The chunk poll could therefore only ever fire mid-sweep - a race with the machine, and
        //     no test can pin it.
        //   * The stretch it was capping is bounded and small. 'IndexOf' over 100,000,000 code units
        //     of a subject holding no occurrence took 14.1 ms measured here on 2026-09-20 (Release,
        //     busy machine), so the largest subject a .NET string can hold sweeps in about 150 ms.
        //   * The chunking cost something real: chunks have to overlap by 'needle.Length - 1' or an
        //     occurrence straddling a boundary is missed, which is an off-by-one this method would
        //     otherwise not own.
        //
        // 'SimpleStringSearch' keeps its poll, because it is a character-at-a-time loop with
        // iterations to count and no vectorised floor under it.
        // The chunk loop this replaced ran zero times when the bounds crossed; 'AsSpan' would throw.
        if (limit > textPos)
        {
            int found = state.Text.AsSpan(textPos, limit - textPos).IndexOf(needle.AsSpan());
            if (found >= 0)
            {
                return textPos + found;
            }
        }

        if (state.PartialSide == MatchState.PartialRight)
        {
            int retry = limit - needle.Length;
            if (retry > 0 && char.IsLowSurrogate(state.Text[retry]))
            {
                --retry;
            }

            if (retry < textPos)
            {
                retry = textPos;
            }

            return SimpleStringSearch(state, node, retry, limit, out isPartial, out cancelled);
        }

        return -1;
    }

    /// <summary>
    /// Port of <c>locate_required_string</c> (<c>upstream/src/_regex.c</c> lines 11082-11369): where
    /// the next match could possibly start, given the substring every match must contain.
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="search">Whether the caller is searching rather than matching at a fixed point.</param>
    /// <param name="cancelled">Set when the caller's timeout or token ended the search.</param>
    /// <returns>
    /// The position to start matching from, or <c>-1</c> when the required string is not in the
    /// slice at all and the whole match can therefore be refused.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Only the <c>STRING</c> arm is ported</b> (S60). Upstream's switch has six, and an opcode
    /// it does not list falls through to "start matching from the current position" - so the five
    /// unported arms take exactly the shape upstream already has for an unhandled opcode, and the
    /// search is the one this port has always run. The reasons they are separate are in
    /// <c>docs/plan/OPTIMISATION-NOTES.md</c>: <c>STRING_REV</c> needs <c>string_search_rev</c> and
    /// the mirrored offset arithmetic, and the four case-insensitive arms are where S22 measured
    /// upstream's prefilter answering DIFFERENTLY from upstream's own matcher, so they are a
    /// judgement about three pinned divergences rather than a transliteration.
    /// </para>
    /// <para>
    /// <b>The verb constraint, in two parts</b> (ROADMAP, owner rule 2026-09-12). The first is that
    /// everything here is read from the live state: <c>limit</c> reads
    /// <see cref="MatchState.SliceEnd"/>, the search starts at <see cref="MatchState.TextPos"/>, and
    /// the cached <see cref="MatchState.ReqPos"/> is discarded as soon as the search position passes
    /// it (<c>:11112</c>), so a position a verb committed past is never re-offered. That alone is
    /// NOT enough, and believing it was is what the first cut of this slice got wrong: it leaves the
    /// FIRST attempt free to start after a position whose own <c>(*SKIP)</c> would have moved the
    /// slice somewhere else entirely. The second part is <see cref="PatternObject.HasSkipVerb"/>,
    /// which withholds the jump from any pattern that can run one. Pinned by
    /// <c>Gaps/Engine/BacktrackingVerbTests.cs</c> - which caught exactly that, red - and by
    /// <c>Gaps/Engine/RequiredStringPrefilterTests.cs</c>.
    /// </para>
    /// <para>
    /// <b>The offset is in characters and the positions are in code units</b>, so the step back is a
    /// <see cref="MatchState.PrevPos"/> walk rather than a subtraction. Upstream can subtract
    /// because its <c>text_pos</c> counts characters. <c>ReqEnd</c> needs no walk: the needle's
    /// UTF-16 length is exactly what matched.
    /// </para>
    /// </remarks>
    private static int LocateRequiredString(MatchState state, bool search, out bool cancelled)
    {
        PatternObject pattern = state.Pattern;
        cancelled = false;

        if (pattern.ReqString is not Node reqString)
        {
            // There isn't a required string, so start matching from the current position.
            return state.TextPos;
        }

        // Search for the required string and calculate where to start matching.
        switch (reqString.Op)
        {
            case Opcode.String:
            {
                // The offset arm is withheld from a pattern holding a '(*SKIP)', which leaves the
                // prefilter free to refuse a subject but never to choose where the first attempt
                // starts. PatternObject.HasSkipVerb says why.
                bool useOffset = pattern.ReqOffset >= 0 && !pattern.HasSkipVerb;

                int limit;
                if (search || !useOffset)
                {
                    limit = state.SliceEnd;
                }
                else
                {
                    // Upstream adds 'req_offset + value_count' to 'slice_start' because its
                    // positions count characters (:11117). Ours count UTF-16 code units, so the
                    // same bound is that many NextPos steps: adding would stop an astral needle
                    // one code unit short of itself and refuse a subject that holds it.
                    limit = state.SliceStart;
                    for (long i = 0; i < pattern.ReqOffset + reqString.Values.Count && limit < state.SliceEnd; ++i)
                    {
                        limit = state.NextPos(limit);
                    }
                }

                bool isPartial;
                int foundPos;
                if (state.ReqPos < 0 || state.TextPos > state.ReqPos)
                {
                    // First time or already passed it.
                    foundPos = StringSearch(state, reqString, state.TextPos, limit, out isPartial, out cancelled);
                }
                else
                {
                    foundPos = state.ReqPos;
                    isPartial = false;
                }

                if (foundPos < 0)
                {
                    // The required string wasn't found.
                    return -1;
                }

                if (!isPartial)
                {
                    // Record where the required string matched. Upstream adds 'value_count', which
                    // is a character count; the needle's code-unit length is the same number of
                    // characters and is what actually matched.
                    state.ReqPos = foundPos;
                    state.ReqEnd = foundPos + (pattern.ReqStringText?.Length ?? reqString.Values.Count);
                }

                if (useOffset)
                {
                    // Step back from the required string to where we should start matching.
                    int startPos = foundPos;
                    for (long i = 0; i < pattern.ReqOffset && startPos > state.TextPos; ++i)
                    {
                        startPos = state.PrevPos(startPos);
                    }

                    if (startPos >= state.TextPos)
                    {
                        return startPos;
                    }
                }

                break;
            }

            default:
                // ponytail: Phase 7 - the STRING_REV, STRING_FLD, STRING_FLD_REV, STRING_IGN and
                // STRING_IGN_REV arms of 'locate_required_string' (:11143-11365). Falling through
                // costs the prefilter on reverse and case-insensitive patterns only; the lift is
                // 'string_search_rev' (:6867) and the three folding searches, and for the folding
                // ones a judgement about the divergences S22 pinned. OPTIMISATION-NOTES.md.
                break;
        }

        // Start matching from the current position.
        return state.TextPos;
    }

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

        // Upstream's 'folded_pos' and 'gfolded_pos' (:11727-11728) are left uninitialised: every
        // opcode that reads them sets them first, in the arm it takes when 'string_pos' is
        // negative. C# needs them definitely assigned, and 0 is the value those arms write.
        int foldedPos = 0;
        int gfoldedPos = 0;
        Span<uint> folded = stackalloc uint[UnicodeTables.MaxFolded];
        Span<uint> gfolded = stackalloc uint[UnicodeTables.MaxFolded];

        // The three scratch buffers FUZZY and END_FUZZY need, allocated here for the same reason
        // 'folded' is: a stackalloc inside either loop is not released until this method returns, so
        // one per backtrack step grows the stack without bound (CA2014). That analyzer does not see
        // a stackalloc nested inside a switch case, so this is the rule and not the tool.
        Span<long> fuzzyOuterCounts = stackalloc long[FuzzyValue.Count];
        Span<long> fuzzyTotalCounts = stackalloc long[FuzzyValue.Count];
        Span<long> fuzzyInnerCounts = stackalloc long[FuzzyValue.Count];

        state.FewestErrors = state.MaxErrors;

        // 'do_search_start' is still a Phase 7 prefilter, so a search the required string does not
        // refuse takes the slow path that tries the pattern at every position - upstream's
        // 'next_match_2'. The required-string locator itself landed in S60, just below.
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

        // Clear the fuzzy counts (:11790-11792). Ordinarily invisible, because the FUZZY backtrack
        // arm has already restored them by the time FAILURE jumps back here - but a verb that cuts
        // the backtracking drops the fuzzy frames instead of unwinding them, and then the counts are
        // still the abandoned attempt's. Found by S38's blind review, and pinned by
        // FuzzyMatchingTests.A_search_that_restarts_does_not_carry_the_abandoned_attempt_s_errors_into_the_next_one.
        //
        // NOT PORTED: the pattern-call guard list clear (:11797-11803), which S30 settled as
        // write-only upstream - see docs/PORTMAP.md's "deliberately not ported" table.
        //
        // THE CHANGE LIST IS CLEARED HERE AND UPSTREAM'S IS NOT - a deliberate divergence, S47,
        // ledger entry 11 mechanism A. Upstream clears the counts and leaves the list, and the two
        // therefore drift apart on a restart: `Match.FuzzyChanges` reports the first
        // `FuzzyCounts.Total` entries, which is upstream's own `for (i = 0; i < count; i++)`
        // (:20522), so a change left over from an abandoned attempt DISPLACES a real one and
        // upstream's own answer contradicts its own counts (re-run with
        // `python tools/probes/upstream-fuzzy-restart-leak.py`):
        //
        //   search(r'(?:[ab][bc](*PRUNE)[wx]){e<=2}', 'qab') -> counts=(0,0,1) changes=([0],[],[])
        //   search(r'(?:[ab][bc][wx]){e<=2}',         'qab') -> counts=(0,0,1) changes=([],[],[3])
        //
        // One deletion counted, a SUBSTITUTION reported - and the second line is upstream's own
        // control, the same section with no verb to cut the backtracking, which answers the
        // deletion at 3 that this port now answers on all three. A cleared count already asserts
        // that a fresh attempt has used no errors, so it can have no changes either; that is the
        // whole argument for this line. Pinned by
        // FuzzyMatchingTests.A_search_that_restarts_does_not_carry_the_abandoned_attempt_s_errors_into_the_next_one,
        // which carries the control.
        //
        // S40a MADE THIS EDIT AND REVERTED IT, because on its own it reddens the two rows S38 had
        // pinned - those rows pinned the contradiction, and re-judging them is what S47 did.
        //
        // It also REMOVES a divergence: `(?<=(?:[ab][cd]){e<=1})$` over 'axc' makes ONE attempt
        // upstream, because `$` has a `search_start_*` twin, and FOUR here, because the prefilter
        // is Phase 7's - so this port leaked where upstream did not and now agrees with it. Pinned
        // by FuzzyMatchingTests.A_search_attempt_that_fails_after_a_lookaround_leaves_nothing_behind_for_the_next_one.
        if (state.IsFuzzy)
        {
            Array.Clear(state.FuzzyCounts);
            state.FuzzyChanges.Clear();
        }

        // NOT UPSTREAM'S, and the same shape as the clear above: a fresh attempt has no group call
        // open, and the abandoned one may have left some - a verb that cuts the backtracking drops
        // the frames that would otherwise have closed them. See MatchState.OpenCalls.
        state.ActiveCalls.Clear();
        state.OpenCalls.Clear();

        // Locate the required string, if there's one, unless this is a recursive call of
        // 'basic_match' (:11806-11814). S60.
        int foundPos;
        if (state.Pattern.ReqString is null || state.TextPos < state.ReqPos)
        {
            foundPos = state.TextPos;
        }
        else
        {
            foundPos = LocateRequiredString(state, search, out bool prefilterCancelled);
            if (foundPos < 0)
            {
                return prefilterCancelled ? MatchStatus.Cancelled : MatchStatus.Failure;
            }
        }

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
                    return SteppedPastTheLeft(state, state.TextPos) ? MatchStatus.Partial : MatchStatus.Failure;
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
                        status = FuzzyMatchItem(state, search, ref node, 1);

                        if (status < 0)
                        {
                            return status;
                        }

                        if (status == MatchStatus.Failure)
                        {
                            goto backtrack;
                        }
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
                        status = FuzzyMatchItem(state, search, ref node, 1);

                        if (status < 0)
                        {
                            return status;
                        }

                        if (status == MatchStatus.Failure)
                        {
                            goto backtrack;
                        }
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
                        status = FuzzyMatchItem(state, search, ref node, 1);

                        if (status < 0)
                        {
                            return status;
                        }

                        if (status == MatchStatus.Failure)
                        {
                            goto backtrack;
                        }
                    }
                    else
                    {
                        goto backtrack;
                    }

                    break;
                // ANY_REV (:11977), ANY_ALL_REV (:11957) and ANY_U_REV (:12017). Upstream gives each
                // its own case, differing from the forward one only in the predicate it calls and in
                // stepping back rather than forward; one case group with the predicate chosen by a
                // switch says the same thing.
                case Opcode.AnyRev: // Any character except a newline, backwards.
                case Opcode.AnyAllRev: // Any character at all, backwards.
                case Opcode.AnyURev: // Any character except a line separator, backwards.
                    status = node.Op switch
                    {
                        Opcode.AnyRev => TryMatchAnyRev(state, state.TextPos),
                        Opcode.AnyAllRev => TryMatchAnyAllRev(state, state.TextPos),
                        _ => TryMatchAnyURev(state, state.TextPos),
                    };

                    if (status < 0)
                    {
                        return status;
                    }

                    if (status == MatchStatus.Success)
                    {
                        state.TextPos = state.PrevPos(state.TextPos);
                        node = node.Next1.Node!;
                    }
                    else if ((node.Status & NodeStatus.Fuzzy) != 0)
                    {
                        status = FuzzyMatchItem(state, search, ref node, -1);

                        if (status < 0)
                        {
                            return status;
                        }

                        if (status == MatchStatus.Failure)
                        {
                            goto backtrack;
                        }
                    }
                    else
                    {
                        goto backtrack;
                    }

                    break;
                case Opcode.Atomic: // Start of an atomic group.
                    PushCaptures(state, state.Bstack);
                    state.PushFuzzyCounts(state.Bstack, state.FuzzyCounts);
                    state.Bstack.PushSize(state.CaptureChange);
                    state.Bstack.PushSize(state.Sstack.Count);
                    state.Bstack.PushUInt8((byte)Opcode.Atomic);
                    state.Pstack.PushSize(state.Bstack.Count);

                    /* bstack: captures fuzzy_counts capture_change sstack ATOMIC
                     *
                     * pstack: bstack
                     */

                    node = node.Next1.Node!;
                    break;
                case Opcode.EndAtomic: // End of an atomic group.
                {
                    /* sstack: ...
                     *
                     * bstack: captures fuzzy_counts capture_change sstack ATOMIC ...
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
                    CloseCallsAbove(state);
                    state.Bstack.PushUInt8((byte)Opcode.EndAtomic);

                    /* bstack: captures fuzzy_counts capture_change END_ATOMIC
                     *
                     * pstack: -
                     */

                    node = node.Next1.Node!;
                    break;
                }
                case Opcode.Conditional: // Start of a conditional subpattern (:12213).
                {
                    // The condition is a lookaround, so it parks exactly what LOOKAROUND parks.
                    PushLookaroundStateData(
                        state.Sstack,
                        new LookaroundStateData(node, state.SliceStart, state.SliceEnd, state.TextPos)
                    );

                    // Upstream saves the captures unconditionally here, with no has-groups flag on
                    // the stack - LOOKAROUND's optimisation is not repeated for CONDITIONAL.
                    PushCaptures(state, state.Bstack);
                    PushRepeats(state, state.Bstack);
                    state.PushFuzzyCounts(state.Bstack, state.FuzzyCounts);
                    state.Bstack.PushSize(state.CaptureChange);
                    state.Bstack.PushSize(state.Sstack.Count);
                    state.Bstack.PushUInt8((byte)Opcode.Conditional);
                    state.Pstack.PushSize(state.Bstack.Count);

                    /* sstack: node slice_start slice_end text_pos
                     *
                     * bstack: captures repeats fuzzy_counts capture_change sstack CONDITIONAL
                     *
                     * pstack: bstack
                     */

                    // The condition may read outside the slice the match is confined to, for the same
                    // reason a lookaround may.
                    state.SliceStart = state.TextStart;
                    state.SliceEnd = state.TextEnd;

                    node = node.Next1.Node!;
                    break;
                }
                case Opcode.EndConditional: // End of a conditional subpattern (:12356).
                {
                    /* sstack: node slice_start slice_end text_pos ...
                     *
                     * bstack: captures repeats fuzzy_counts capture_change sstack CONDITIONAL ...
                     *
                     * pstack: bstack
                     */

                    // The condition matched. It is atomic, like a lookaround, so everything it pushed
                    // while matching goes: the condition is asked once and never re-asked.
                    if (!state.Pstack.PopSize(out long endCondBstackCount))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.Bstack.Count = (int)endCondBstackCount;

                    if (!state.Bstack.Drop() || !state.Bstack.PopSize(out long endCondSstackCount))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.Sstack.Count = (int)endCondSstackCount;
                    CloseCallsAbove(state);

                    if (!PopLookaroundStateData(pattern, state.Sstack, out LookaroundStateData endCondData))
                    {
                        return MatchStatus.Illegal;
                    }

                    // The condition consumes nothing: text position and slice go back to the
                    // CONDITIONAL's.
                    state.TextPos = endCondData.TextPos;
                    state.SliceEnd = endCondData.SliceEnd;
                    state.SliceStart = endCondData.SliceStart;
                    Node endCondNode = endCondData.Node;

                    /* sstack: -
                     *
                     * bstack: captures repeats fuzzy_counts capture_change
                     *
                     * pstack: -
                     */

                    if (endCondNode.Match)
                    {
                        // It's a positive lookaround that's succeeded, so the condition holds. The
                        // saved block stays on the stack for the backtrack case to undo.
                        state.Bstack.PushUInt8((byte)Opcode.EndConditional);

                        /* bstack: captures repeats fuzzy_counts capture_change END_CONDITIONAL */

                        // Go to the 'true' branch.
                        node = node.Next1.Node!;
                    }
                    else
                    {
                        // It's a negative lookaround that's succeeded, so the condition does not
                        // hold. Undo what the condition did before taking the other branch.
                        if (!state.Bstack.PopSize(out long endCondCaptureChange))
                        {
                            return MatchStatus.Illegal;
                        }

                        state.CaptureChange = endCondCaptureChange;

                        if (
                            !state.PopFuzzyCounts(state.Bstack, state.FuzzyCounts)
                            || !PopRepeats(state, state.Bstack)
                            || !PopCaptures(state, state.Bstack)
                        )
                        {
                            return MatchStatus.Illegal;
                        }

                        /* sstack: -
                         *
                         * bstack: -
                         *
                         * pstack: -
                         */

                        // Go to the 'false' branch.
                        node = endCondNode.Next2.Node!;
                    }

                    break;
                }
                case Opcode.Branch: // 2-way branch.
                {
                    status = TryMatch(state, node.Next1, state.TextPos, out Position nextPosition);
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
                case Opcode.EndFuzzy: // End of fuzzy matching (:12448).
                {
                    Span<long> outerCounts = fuzzyOuterCounts;
                    Span<long> totalCounts = fuzzyTotalCounts;

                    /* sstack: outer_counts outer_node
                     *
                     * bstack: -
                     */

                    // Are the inner constraints OK? This is the one place a 'min' is consulted: an
                    // item asks whether one more error fits, and only the end of the section can ask
                    // whether the section as a whole is legal.
                    if (!FuzzyWithinConstraints(state.FuzzyCounts, state.FuzzyNode!, state.MaxErrors))
                    {
                        goto backtrack;
                    }

                    // MERGING, not restoring: the section's own changes are part of the answer this
                    // END_FUZZY is building, and taking the outer counts back off the stack must not
                    // take them with it. This is the arm ledger entry 11 names as the one that has to
                    // stay a merge. See 'MatchState.PopFuzzyCountsMerging'.
                    if (
                        !state.Sstack.PopNode(pattern, out Node? outerNode)
                        || !state.PopFuzzyCountsMerging(state.Sstack, outerCounts, out _)
                    )
                    {
                        return MatchStatus.Illegal;
                    }

                    /* sstack: - */

                    // Add the inner counts to the outer counts.
                    totalCounts[FuzzyValue.Sub] = outerCounts[FuzzyValue.Sub] + state.FuzzyCounts[FuzzyValue.Sub];
                    totalCounts[FuzzyValue.Ins] = outerCounts[FuzzyValue.Ins] + state.FuzzyCounts[FuzzyValue.Ins];
                    totalCounts[FuzzyValue.Del] = outerCounts[FuzzyValue.Del] + state.FuzzyCounts[FuzzyValue.Del];

                    // Is the total number of errors OK?
                    state.TotalErrors = TotalErrors(totalCounts);

                    // This port's own line: what those errors cost, under the section just closed.
                    // See 'MatchState.TotalCost' for why it is recorded here and what it means when
                    // sections nest. Upstream has no equivalent.
                    state.TotalCost = TotalCost(totalCounts, state.FuzzyNode!);

                    // THE SECOND TEST IS THIS PORT'S, AND IT IS THE COST TWIN OF UPSTREAM'S OWN LINE.
                    // The three constraint predicates bound the cost of the section CURRENTLY OPEN,
                    // exactly as they bound its error count; what bounds the WHOLE MATCH is this
                    // line, which upstream writes for the error count (':12486') and cannot write
                    // for the cost because it has no running cost to write it about.
                    //
                    // Without it the cost budget is not a budget at all wherever one section is
                    // entered more than once - a group call, a recursion, a repeat around the
                    // section - because each entry starts from zero counts and passes the predicate
                    // while the accumulated cost recorded above climbs past the budget. The first
                    // walk of 'DoBestFuzzyMatch' then never sees a strictly cheaper run, 'start_pos'
                    // never advances, and IT HANGS: '(?b)((?:a){1i+2d+1s<=1})(?1)' over 'bb' re-found
                    // the same cost-2, two-substitution match under a cost-1 budget until it was
                    // killed, where the unit-cost spelling of the same pattern answers in 80ms.
                    // Found by S42's blind review, 2026-09-13; pinned by
                    // 'Gaps.Engine.FuzzyBestMatchTests.Bestmatch_bounds_the_cost_of_the_whole_match_not_of_one_section'.
                    if (state.TotalErrors > state.MaxErrors || state.TotalCost > state.MaxCost)
                    {
                        state.PushFuzzyCounts(state.Sstack, outerCounts);
                        state.Sstack.PushNode(outerNode);

                        /* sstack: outer_counts outer_node */
                        goto backtrack;
                    }

                    // Save the inner fuzzy info. The zero is the count of trailing insertions this
                    // section has been asked to try, which the backtrack arm raises one at a time.
                    state.PushFuzzyCounts(state.Bstack, state.FuzzyCounts);
                    state.Bstack.PushSize(0);
                    state.Bstack.PushNode(state.FuzzyNode);
                    state.Bstack.PushSize(state.TextPos);
                    state.Bstack.PushNode(node);
                    state.Bstack.PushUInt8((byte)Opcode.EndFuzzy);

                    totalCounts.CopyTo(state.FuzzyCounts);
                    state.FuzzyNode = outerNode;

                    /* sstack: -
                     *
                     * bstack: inner_counts insertions inner_node text_pos end_fuzzy_node END_FUZZY
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
                        bodyStatus = TryMatch(state, node.Next1, state.TextPos, out nextBodyPosition);
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
                        tailStatus = TryMatch(state, node.Next2, state.TextPos, out nextTailPosition);
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
                        bodyStatus = TryMatch(state, node.Next1, state.TextPos, out nextBodyPosition);
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
                        tailStatus = TryMatch(state, node.Next2, state.TextPos, out nextTailPosition);
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
                case Opcode.EndLookaround: // End of a lookaround subpattern (:12918).
                {
                    /* sstack: node slice_start slice_end text_pos ...
                     *
                     * bstack: [captures TRUE | FALSE] fuzzy_counts capture_change sstack LOOKAROUND ...
                     *
                     * pstack: bstack
                     */

                    // Everything the body pushed while it matched goes: a lookaround is atomic, so
                    // there is nothing left to backtrack into once it has succeeded.
                    if (!state.Pstack.PopSize(out long endLookBstackCount))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.Bstack.Count = (int)endLookBstackCount;

                    if (!state.Bstack.Drop() || !state.Bstack.PopSize(out long endLookSstackCount))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.Sstack.Count = (int)endLookSstackCount;
                    CloseCallsAbove(state);

                    if (!PopLookaroundStateData(pattern, state.Sstack, out LookaroundStateData endLookData))
                    {
                        return MatchStatus.Illegal;
                    }

                    // A lookaround consumes nothing: the text position and the slice both go back to
                    // what they were at the LOOKAROUND.
                    state.TextPos = endLookData.TextPos;
                    state.SliceEnd = endLookData.SliceEnd;
                    state.SliceStart = endLookData.SliceStart;
                    Node endLookNode = endLookData.Node;

                    /* sstack: -
                     *
                     * bstack: [captures TRUE | FALSE] fuzzy_counts capture_change
                     *
                     * pstack: -
                     */

                    if (endLookNode.Match)
                    {
                        // It's a positive lookaround that's succeeded. The captures its body made
                        // stay visible; the block stays on the stack for the backtrack case to undo.
                        state.Bstack.PushUInt8((byte)Opcode.EndLookaround);

                        /* bstack: [captures TRUE | FALSE] fuzzy_counts capture_change END_LOOKAROUND */

                        // Go to the 'true' branch.
                        node = node.Next1.Node!;
                    }
                    else
                    {
                        // It's a negative lookaround that's succeeded, which means the whole
                        // lookaround has failed. Undo the body's captures before failing.
                        if (!state.Bstack.PopSize(out long endLookCaptureChange))
                        {
                            return MatchStatus.Illegal;
                        }

                        state.CaptureChange = endLookCaptureChange;

                        if (!state.PopFuzzyCounts(state.Bstack, state.FuzzyCounts))
                        {
                            return MatchStatus.Illegal;
                        }

                        if (!state.Bstack.PopBool(out bool endLookHasGroups))
                        {
                            return MatchStatus.Illegal;
                        }

                        if (endLookHasGroups && !PopCaptures(state, state.Bstack))
                        {
                            return MatchStatus.Illegal;
                        }

                        // Go to the 'false' branch.
                        goto backtrack;
                    }

                    break;
                }
                case Opcode.CallRef: // A group call reference (:12106).
                    // The node a called group is entered through. Reached in the ordinary forward
                    // direction it means the group is being matched where it was written rather
                    // than called, so the NULL tells the matching GROUP_RETURN there is no caller
                    // to return to. GROUP_CALL enters the group by skipping over this node.
                    state.Sstack.PushNode(null);
                    state.Bstack.PushUInt8((byte)Opcode.CallRef);

                    /* sstack: NULL
                     *
                     * bstack: CALL_REF
                     */

                    node = node.Next1.Node!;
                    break;
                // Upstream gives each of these its own case with the same eleven-line tail copied
                // out (:12968, :13804, :13914, :14446-14449 case-sensitive; :12146, :13827, :13937,
                // :14471-14474 ignoring case), differing only in which 'matches_*' predicate it
                // calls. One case group with the predicate chosen by a switch says the same thing,
                // and each arm still maps one-for-one onto upstream's case.
                case Opcode.Character: // A character.
                case Opcode.CharacterIgn: // A character, ignoring case.
                case Opcode.Property: // A property.
                case Opcode.PropertyIgn: // A property, ignoring case.
                case Opcode.Range: // A range.
                case Opcode.RangeIgn: // A range, ignoring case.
                case Opcode.SetDiff: // Set difference.
                case Opcode.SetDiffIgn: // Set difference, ignoring case.
                case Opcode.SetInter: // Set intersection.
                case Opcode.SetInterIgn: // Set intersection, ignoring case.
                case Opcode.SetSymDiff: // Set symmetric difference.
                case Opcode.SetSymDiffIgn: // Set symmetric difference, ignoring case.
                case Opcode.SetUnion: // Set union.
                case Opcode.SetUnionIgn: // Set union, ignoring case.
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
                        status = FuzzyMatchItem(state, search, ref node, 1);

                        if (status < 0)
                        {
                            return status;
                        }

                        if (status == MatchStatus.Failure)
                        {
                            goto backtrack;
                        }
                    }
                    else
                    {
                        goto backtrack;
                    }

                    break;
                // The same block backwards (:12191, :12169, :13872, :13850, :13982, :13960,
                // :14520-14523, :14496-14499). Three things change and nothing else does: the
                // partial side, the bound the position is tested against, and reading the character
                // *before* the position rather than at it.
                case Opcode.CharacterRev: // A character, backwards.
                case Opcode.CharacterIgnRev: // A character, backwards, ignoring case.
                case Opcode.PropertyRev: // A property, backwards.
                case Opcode.PropertyIgnRev: // A property, backwards, ignoring case.
                case Opcode.RangeRev: // A range, backwards.
                case Opcode.RangeIgnRev: // A range, backwards, ignoring case.
                case Opcode.SetDiffRev: // Set difference, backwards.
                case Opcode.SetDiffIgnRev: // Set difference, backwards, ignoring case.
                case Opcode.SetInterRev: // Set intersection, backwards.
                case Opcode.SetInterIgnRev: // Set intersection, backwards, ignoring case.
                case Opcode.SetSymDiffRev: // Set symmetric difference, backwards.
                case Opcode.SetSymDiffIgnRev: // Set symmetric difference, backwards, ignoring case.
                case Opcode.SetUnionRev: // Set union, backwards.
                case Opcode.SetUnionIgnRev: // Set union, backwards, ignoring case.
                    if (RanOutOnTheLeft(state, state.TextPos))
                    {
                        return MatchStatus.Partial;
                    }

                    if (
                        state.TextPos > state.SliceStart
                        && MatchesOne(state.Encoding, node, state.CharBefore(state.TextPos)) == node.Match
                    )
                    {
                        state.TextPos = Step(state, state.TextPos, node.Step);
                        node = node.Next1.Node!;
                    }
                    else if ((node.Status & NodeStatus.Fuzzy) != 0)
                    {
                        status = FuzzyMatchItem(state, search, ref node, -1);

                        if (status < 0)
                        {
                            return status;
                        }

                        if (status == MatchStatus.Failure)
                        {
                            goto backtrack;
                        }
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
                        // A step of 0: a zero-width item consumes nothing, so it can be neither
                        // deleted nor substituted, and an insertion is the only error left.
                        status = FuzzyMatchItem(state, search, ref node, 0);

                        if (status < 0)
                        {
                            return status;
                        }

                        if (status == MatchStatus.Failure)
                        {
                            goto backtrack;
                        }
                    }
                    else
                    {
                        goto backtrack;
                    }

                    break;
                case Opcode.Failure: // Failure.
                    goto backtrack;
                case Opcode.Fuzzy: // Fuzzy matching (:13132).
                    // Save the outer fuzzy info. A nested fuzzy section counts its own errors from
                    // zero and END_FUZZY adds them back in, which is how an inner budget can be
                    // tighter than the outer one without either being ignored.
                    state.PushFuzzyCounts(state.Sstack, state.FuzzyCounts);
                    state.Sstack.PushNode(state.FuzzyNode);

                    // Initialise the inner fuzzy info.
                    Array.Clear(state.FuzzyCounts);
                    state.FuzzyNode = node;

                    state.Bstack.PushUInt8((byte)Opcode.Fuzzy);

                    /* sstack: outer_counts outer_node
                     *
                     * bstack: FUZZY
                     */

                    node = node.Next1.Node!;
                    break;
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
                        bodyStatus = TryMatch(state, node.Next1, state.TextPos, out nextBodyPosition);
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
                        tailStatus = TryMatch(state, node.Next2, state.TextPos, out nextTailPosition);
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
                case Opcode.GroupCall: // Group call (:13394).
                {
                    int groupCallIndex = (int)node.Values[0];
                    Node groupCallReturnNode = node.Next1.Node!;

                    // NOT UPSTREAM'S: refuse a call that re-enters this group where a call of it is
                    // already open. See MatchState.ActiveCalls and ledger entry 14 - the path is
                    // infinite, and failing it leaves every other path alone.
                    long groupCallKey = ActiveCallKey(groupCallIndex, state.TextPos);

                    if (!state.ActiveCalls.Add(groupCallKey))
                    {
                        goto backtrack;
                    }

                    // For the caller.
                    PushGroups(state, state.Sstack);
                    PushRepeats(state, state.Sstack);
                    state.Sstack.PushSize(state.CaptureChange);
                    state.Sstack.PushNode(groupCallReturnNode);
                    state.Bstack.PushUInt8((byte)Opcode.GroupCall);

                    // The call is open, and the frame it belongs to ends here. See
                    // MatchState.OpenCalls: the depth is what lets a saved-stack restore tell which
                    // open calls it has just thrown away.
                    state.OpenCalls.Add((groupCallKey, state.Sstack.Count));

                    /* sstack: caller_groups caller_repeats capture_change return_node
                     *
                     * bstack: GROUP_CALL
                     */

                    // Clear the repeat guards for the group call. They'll be restored on return -
                    // 'push_repeats' above saved the guard lists in full. Without this a repeat
                    // inside the called group would inherit "already failed at this position" from
                    // the caller's own pass over the same text, and a recursive pattern visits the
                    // same positions by design.
                    foreach (RepeatData groupCallRepeat in state.Repeats)
                    {
                        groupCallRepeat.BodyGuardList.Reset();
                        groupCallRepeat.TailGuardList.Reset();
                    }

                    // Call a group, skipping its CALL_REF node. Be aware that we might be calling
                    // the entire pattern, which has no CALL_REF node of its own.
                    Node? groupCallRefNode = pattern.CallRefInfoList[groupCallIndex].Node;
                    node = groupCallRefNode is not null ? groupCallRefNode.Next1.Node! : pattern.StartNode!;
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
                case Opcode.GroupReturn: // Group return (:13474).
                {
                    /* If called:
                     *
                     * sstack: caller_groups caller_repeats capture_change return_node
                     *
                     * bstack: -
                     *
                     * else:
                     *
                     * sstack: NULL
                     *
                     * bstack: -
                     */

                    if (!state.Sstack.PopNode(pattern, out Node? groupReturnNode))
                    {
                        return MatchStatus.Illegal;
                    }

                    if (groupReturnNode is not null)
                    {
                        // The group was called.
                        node = groupReturnNode;

                        // The call is closed, so it is no longer one this position may not re-enter.
                        // It is the innermost open one - calls nest - and its key goes on the
                        // backtracking stack so the arm below can re-open it.
                        long groupReturnCallKey = PopOpenCall(state);

                        // For the callee.
                        PushGroups(state, state.Bstack);
                        PushRepeats(state, state.Bstack);
                        state.Bstack.PushSize(state.CaptureChange);
                        state.Bstack.PushSize(groupReturnCallKey);
                        state.Bstack.PushNode(groupReturnNode);
                        state.Bstack.PushUInt8((byte)Opcode.GroupReturn);

                        // For the caller. The callee's group spans and repeats go on the
                        // backtracking stack and the caller's come back off the saved stack, so
                        // matching resumes exactly where the call left it. The captures themselves
                        // are untouched, which is how a called group's capture survives the return.
                        if (
                            !state.Sstack.PopSize(out long groupReturnCaptureChange)
                            || !PopRepeats(state, state.Sstack)
                            || !PopGroups(state, state.Sstack)
                        )
                        {
                            return MatchStatus.Illegal;
                        }

                        state.CaptureChange = groupReturnCaptureChange;
                    }
                    else
                    {
                        // The group was not called.
                        state.Bstack.PushNode(null);
                        state.Bstack.PushUInt8((byte)Opcode.GroupReturn);

                        node = node.Next1.Node!;
                    }

                    /* If called:
                     *
                     * sstack: -
                     *
                     * bstack: callee_groups callee_repeats capture_change call_key return_node
                     *         GROUP_RETURN
                     *
                     * else:
                     *
                     * sstack: -
                     *
                     * bstack: NULL GROUP_RETURN
                     */

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
                        bodyStatus = TryMatch(state, node.Next1, state.TextPos, out nextBodyPosition);
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
                        tailStatus = TryMatch(state, node.Next2, state.TextPos, out nextTailPosition);
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
                case Opcode.Lookaround: // Start of a lookaround subpattern (:13758).
                {
                    PushLookaroundStateData(
                        state.Sstack,
                        new LookaroundStateData(node, state.SliceStart, state.SliceEnd, state.TextPos)
                    );

                    bool lookHasGroups = (node.Status & NodeStatus.HasGroups) != 0;
                    if (lookHasGroups)
                    {
                        PushCaptures(state, state.Bstack);
                    }

                    state.Bstack.PushBool(lookHasGroups);
                    state.PushFuzzyCounts(state.Bstack, state.FuzzyCounts);
                    state.Bstack.PushSize(state.CaptureChange);
                    state.Bstack.PushSize(state.Sstack.Count);
                    state.Bstack.PushUInt8((byte)Opcode.Lookaround);
                    state.Pstack.PushSize(state.Bstack.Count);

                    /* sstack: node slice_start slice_end text_pos
                     *
                     * bstack: [captures TRUE | FALSE] fuzzy_counts capture_change sstack LOOKAROUND
                     *
                     * pstack: bstack
                     */

                    // A lookaround may read outside the slice the match is confined to: '(?<=a)b'
                    // against 'ab' searched from position 1 has to see the 'a'.
                    state.SliceStart = state.TextStart;
                    state.SliceEnd = state.TextEnd;

                    node = node.Next1.Node!;
                    break;
                }
                case Opcode.Prune: // Prune the backtracking (:13894).
                {
                    /* bstack: ... | ...
                     *
                     * pstack: bstack
                     */

                    // Prune the backtracking back to an appropriate backtracking point.
                    TopBstack(state);

                    /* bstack: ...
                     *
                     * pstack: bstack
                     */

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
                            // 'stringPos' is a position in the subject here, not an index into the
                            // node's values, so the fuzzy walk has to step it in code units.
                            status = FuzzyMatchString(state, search, node, ref stringPos, 1, true);

                            if (status < 0)
                            {
                                return status;
                            }

                            if (status == MatchStatus.Failure)
                            {
                                stringPos = -1;
                                goto backtrack;
                            }
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
                // REF_GROUP_REV (:14375). The same walk from the other end: 'stringPos' starts at
                // the capture's end and both sides retreat.
                case Opcode.RefGroupRev: // Reference to a capture group, backwards.
                {
                    // Did the group capture anything?
                    GroupData refGroup = state.Groups[(int)node.Values[0] - 1];
                    if (refGroup.Current < 0)
                    {
                        goto backtrack;
                    }

                    GroupSpan span = refGroup.Captures[refGroup.Current];

                    if (stringPos < 0)
                    {
                        stringPos = span.End;
                    }

                    // Try comparing.
                    while (stringPos > span.Start)
                    {
                        if (RanOutOnTheLeft(state, state.TextPos))
                        {
                            return MatchStatus.Partial;
                        }

                        if (
                            state.TextPos > state.SliceStart
                            && SameChar(state.CharBefore(state.TextPos), state.CharBefore(stringPos))
                        )
                        {
                            stringPos = state.PrevPos(stringPos);
                            state.TextPos = state.PrevPos(state.TextPos);
                        }
                        else if ((node.Status & NodeStatus.Fuzzy) != 0)
                        {
                            status = FuzzyMatchString(state, search, node, ref stringPos, -1, true);

                            if (status < 0)
                            {
                                return status;
                            }

                            if (status == MatchStatus.Failure)
                            {
                                stringPos = -1;
                                goto backtrack;
                            }
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
                // REF_GROUP_IGN_REV (:14318).
                case Opcode.RefGroupIgnRev: // Reference to a capture group, backwards, ignoring case.
                {
                    // Did the group capture anything?
                    GroupData refGroup = state.Groups[(int)node.Values[0] - 1];
                    if (refGroup.Current < 0)
                    {
                        goto backtrack;
                    }

                    GroupSpan span = refGroup.Captures[refGroup.Current];

                    if (stringPos < 0)
                    {
                        stringPos = span.End;
                    }

                    // Try comparing.
                    while (stringPos > span.Start)
                    {
                        if (RanOutOnTheLeft(state, state.TextPos))
                        {
                            return MatchStatus.Partial;
                        }

                        if (
                            state.TextPos > state.SliceStart
                            && SameCharIgn(state.Encoding, state.CharBefore(state.TextPos), state.CharBefore(stringPos))
                        )
                        {
                            stringPos = state.PrevPos(stringPos);
                            state.TextPos = state.PrevPos(state.TextPos);
                        }
                        else if ((node.Status & NodeStatus.Fuzzy) != 0)
                        {
                            status = FuzzyMatchString(state, search, node, ref stringPos, -1, true);

                            if (status < 0)
                            {
                                return status;
                            }

                            if (status == MatchStatus.Failure)
                            {
                                stringPos = -1;
                                goto backtrack;
                            }
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
                // REF_GROUP_FLD_REV (:14161). REF_GROUP_FLD with both foldings consumed from their
                // last character back, so each side's position counts down from its length instead
                // of up from zero. S22 and S23 disapplied S1854 over this case because every store to
                // 'gfoldedLen' was dead until Phase 5 supplied its reader; S39 is that slice, and
                // 'FuzzyMatchGroupFld(..., gfoldedLen, -1)' below reads it, so the disapplication is
                // gone.
                case Opcode.RefGroupFldRev: // Reference to a capture group, backwards, ignoring case.
                {
                    // Did the group capture anything?
                    GroupData refGroup = state.Groups[(int)node.Values[0] - 1];
                    if (refGroup.Current < 0)
                    {
                        goto backtrack;
                    }

                    GroupSpan span = refGroup.Captures[refGroup.Current];
                    int foldedLen;
                    int gfoldedLen;

                    if (stringPos < 0)
                    {
                        stringPos = span.End;
                        foldedPos = 0;
                        foldedLen = 0;
                        gfoldedPos = 0;
                        gfoldedLen = 0;
                    }
                    else
                    {
                        // Only S39's RetryFuzzyMatchGroupFld leaves 'stringPos' non-negative on the
                        // way in, so that is the one thing that reaches this arm.
                        foldedLen = Encodings.FullCaseFold(state.Encoding, state.CharBefore(state.TextPos), folded);
                        gfoldedLen = Encodings.FullCaseFold(state.Encoding, state.CharBefore(stringPos), gfolded);
                    }

                    // Try comparing.
                    while (stringPos > span.Start)
                    {
                        // Case-fold at current position in text.
                        if (foldedPos <= 0)
                        {
                            if (RanOutOnTheLeft(state, state.TextPos))
                            {
                                return MatchStatus.Partial;
                            }

                            foldedLen =
                                state.TextPos > state.SliceStart
                                    ? Encodings.FullCaseFold(state.Encoding, state.CharBefore(state.TextPos), folded)
                                    : 0;

                            foldedPos = foldedLen;
                        }

                        // Case-fold at current position in group.
                        if (gfoldedPos <= 0)
                        {
                            gfoldedLen = Encodings.FullCaseFold(state.Encoding, state.CharBefore(stringPos), gfolded);
                            gfoldedPos = gfoldedLen;
                        }

                        if (
                            foldedPos > 0
                            && SameCharIgn(state.Encoding, gfolded[gfoldedPos - 1], folded[foldedPos - 1])
                        )
                        {
                            --foldedPos;
                            --gfoldedPos;
                        }
                        else if ((node.Status & NodeStatus.Fuzzy) != 0)
                        {
                            status = FuzzyMatchGroupFld(
                                state,
                                search,
                                node,
                                ref foldedPos,
                                foldedLen,
                                stringPos,
                                ref gfoldedPos,
                                gfoldedLen,
                                -1
                            );

                            if (status < 0)
                            {
                                return status;
                            }

                            if (status == MatchStatus.Failure)
                            {
                                stringPos = -1;
                                goto backtrack;
                            }
                        }
                        else
                        {
                            stringPos = -1;
                            goto backtrack;
                        }

                        if (foldedPos <= 0 && foldedLen > 0)
                        {
                            state.TextPos = state.PrevPos(state.TextPos);
                        }

                        if (gfoldedPos <= 0)
                        {
                            stringPos = state.PrevPos(stringPos);
                        }
                    }

                    stringPos = -1;

                    // A folding that ran out on one side but not the other did not line up.
                    if (foldedPos > 0 || gfoldedPos > 0)
                    {
                        goto backtrack;
                    }

                    // Successful match.
                    node = node.Next1.Node!;
                    break;
                }
                // REF_GROUP_FLD (:14060). The hard one: the captured text and the subject are both
                // full-case-folded, and the two foldings need not be the same length, so each side
                // has its own buffer and its own position and only advances when its buffer runs
                // out. 'gfolded' is the group's side, 'folded' the subject's.
                case Opcode.RefGroupFld: // Reference to a capture group, ignoring case.
                {
                    // Did the group capture anything?
                    GroupData refGroup = state.Groups[(int)node.Values[0] - 1];
                    if (refGroup.Current < 0)
                    {
                        goto backtrack;
                    }

                    GroupSpan span = refGroup.Captures[refGroup.Current];
                    int foldedLen;
                    int gfoldedLen;

                    if (stringPos < 0)
                    {
                        stringPos = span.Start;
                        foldedPos = 0;
                        foldedLen = 0;
                        gfoldedPos = 0;
                        gfoldedLen = 0;
                    }
                    else
                    {
                        // Only S39's RetryFuzzyMatchGroupFld leaves 'stringPos' non-negative on the
                        // way in, so that is the one thing that reaches this arm.
                        foldedLen = Encodings.FullCaseFold(state.Encoding, state.CharAt(state.TextPos), folded);
                        gfoldedLen = Encodings.FullCaseFold(state.Encoding, state.CharAt(stringPos), gfolded);
                    }

                    // Try comparing.
                    while (stringPos < span.End)
                    {
                        // Case-fold at current position in text.
                        if (foldedPos >= foldedLen)
                        {
                            if (state.TextPos >= state.TextEnd && state.PartialSide == MatchState.PartialRight)
                            {
                                return MatchStatus.Partial;
                            }

                            foldedLen =
                                state.TextPos < state.SliceEnd
                                    ? Encodings.FullCaseFold(state.Encoding, state.CharAt(state.TextPos), folded)
                                    : 0;

                            foldedPos = 0;
                        }

                        // Case-fold at current position in group.
                        if (gfoldedPos >= gfoldedLen)
                        {
                            gfoldedLen = Encodings.FullCaseFold(state.Encoding, state.CharAt(stringPos), gfolded);
                            gfoldedPos = 0;
                        }

                        if (
                            foldedPos < foldedLen
                            && SameCharIgn(state.Encoding, gfolded[gfoldedPos], folded[foldedPos])
                        )
                        {
                            ++foldedPos;
                            ++gfoldedPos;
                        }
                        else if ((node.Status & NodeStatus.Fuzzy) != 0)
                        {
                            status = FuzzyMatchGroupFld(
                                state,
                                search,
                                node,
                                ref foldedPos,
                                foldedLen,
                                stringPos,
                                ref gfoldedPos,
                                gfoldedLen,
                                1
                            );

                            if (status < 0)
                            {
                                return status;
                            }

                            if (status == MatchStatus.Failure)
                            {
                                stringPos = -1;
                                goto backtrack;
                            }
                        }
                        else
                        {
                            stringPos = -1;
                            goto backtrack;
                        }

                        // Upstream's '++state->text_pos' and '++string_pos' are one codepoint each;
                        // both index this subject, so both walk with 'NextPos' - the S21 reasoning
                        // on REF_GROUP applies unchanged.
                        if (foldedPos >= foldedLen && foldedLen > 0)
                        {
                            state.TextPos = state.NextPos(state.TextPos);
                        }

                        if (gfoldedPos >= gfoldedLen)
                        {
                            stringPos = state.NextPos(stringPos);
                        }
                    }

                    stringPos = -1;

                    // A folding that ran out on one side but not the other did not line up.
                    if (foldedPos < foldedLen || gfoldedPos < gfoldedLen)
                    {
                        goto backtrack;
                    }

                    // Successful match.
                    node = node.Next1.Node!;
                    break;
                }
                // REF_GROUP_IGN (:14262): REF_GROUP with 'same_char_ign' in place of 'same_char'.
                // Simple folding, so both sides still advance one character at a time.
                case Opcode.RefGroupIgn: // Reference to a capture group, ignoring case.
                {
                    // Did the group capture anything?
                    GroupData refGroup = state.Groups[(int)node.Values[0] - 1];
                    if (refGroup.Current < 0)
                    {
                        goto backtrack;
                    }

                    GroupSpan span = refGroup.Captures[refGroup.Current];

                    if (stringPos < 0)
                    {
                        stringPos = span.Start;
                    }

                    // Try comparing.
                    while (stringPos < span.End)
                    {
                        if (state.TextPos >= state.TextEnd && state.PartialSide == MatchState.PartialRight)
                        {
                            return MatchStatus.Partial;
                        }

                        if (
                            state.TextPos < state.SliceEnd
                            && SameCharIgn(state.Encoding, state.CharAt(state.TextPos), state.CharAt(stringPos))
                        )
                        {
                            stringPos = state.NextPos(stringPos);
                            state.TextPos = state.NextPos(state.TextPos);
                        }
                        else if ((node.Status & NodeStatus.Fuzzy) != 0)
                        {
                            status = FuzzyMatchString(state, search, node, ref stringPos, 1, true);

                            if (status < 0)
                            {
                                return status;
                            }

                            if (status == MatchStatus.Failure)
                            {
                                stringPos = -1;
                                goto backtrack;
                            }
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
                case Opcode.Skip: // Skip the part of the text already matched (:14544).
                {
                    /* bstack: ... | ...
                     *
                     * pstack: bstack
                     */

                    if ((node.Status & NodeStatus.Reverse) != 0)
                    {
                        state.SliceEnd = state.TextPos;
                    }
                    else
                    {
                        state.SliceStart = state.TextPos;
                    }

                    // Prune the backtracking back to an appropriate backtracking point.
                    TopBstack(state);

                    /* bstack: ...
                     *
                     * pstack: bstack
                     */

                    node = node.Next1.Node!;
                    break;
                }
                case Opcode.String: // A string.
                {
                    if ((node.Status & NodeStatus.Required) != 0 && state.TextPos == state.ReqPos && stringPos < 0)
                    {
                        // LIVE SINCE S60, and dead code before it: the locator is the only thing
                        // that sets 'req_pos', and this is the one arm it sets it for. The string
                        // the prefilter has already compared is not compared a second time.
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
                                status = FuzzyMatchString(state, search, node, ref stringPos, 1, false);

                                if (status < 0)
                                {
                                    return status;
                                }

                                if (status == MatchStatus.Failure)
                                {
                                    stringPos = -1;
                                    goto backtrack;
                                }
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
                        FuzzyInsert(state, 1, node.Next1.Node);
                    }

                    stringPos = -1;

                    // Successful match.
                    node = node.Next1.Node!;
                    break;
                }
                // STRING_FLD (:14776). The pattern's values are already folded - the parser's
                // 'Sequence._fix_full_casefold' (S10) put them there - so only the subject is
                // folded here, and one subject character can answer for up to three pattern
                // characters. 'text_pos' advances only when the subject's folding is used up,
                // which is why 'foldedPos' has to survive a backtrack into this node.
                case Opcode.StringFld: // A string, ignoring case.
                {
                    int foldedLen;

                    if ((node.Status & NodeStatus.Required) != 0 && state.TextPos == state.ReqPos && stringPos < 0)
                    {
                        // Unreachable until Phase 7 ports this arm of the required-string locator
                        // (:11143-11365); S60 ported the case-sensitive forward one only.
                        state.TextPos = state.ReqEnd;
                    }
                    else
                    {
                        int length = node.Values.Count;

                        if (stringPos < 0)
                        {
                            stringPos = 0;
                            foldedPos = 0;
                            foldedLen = 0;
                        }
                        else
                        {
                            // Only Phase 5's fuzzy retry reaches this arm.
                            foldedLen = Encodings.FullCaseFold(state.Encoding, state.CharAt(state.TextPos), folded);

                            if (foldedPos >= foldedLen)
                            {
                                if (state.TextPos >= state.SliceEnd)
                                {
                                    goto backtrack;
                                }

                                state.TextPos = state.NextPos(state.TextPos);
                                foldedPos = 0;
                                foldedLen = 0;
                            }
                        }

                        // Try comparing.
                        while (stringPos < length)
                        {
                            if (foldedPos >= foldedLen)
                            {
                                if (state.TextPos >= state.TextEnd && state.PartialSide == MatchState.PartialRight)
                                {
                                    return MatchStatus.Partial;
                                }

                                foldedLen =
                                    state.TextPos < state.SliceEnd
                                        ? Encodings.FullCaseFold(state.Encoding, state.CharAt(state.TextPos), folded)
                                        : 0;

                                foldedPos = 0;
                            }

                            if (
                                foldedPos < foldedLen
                                && SameCharIgn(state.Encoding, node.Values[stringPos], folded[foldedPos])
                            )
                            {
                                ++stringPos;
                                ++foldedPos;

                                if (foldedPos >= foldedLen)
                                {
                                    state.TextPos = state.NextPos(state.TextPos);
                                }
                            }
                            else if ((node.Status & NodeStatus.Fuzzy) != 0)
                            {
                                status = FuzzyMatchStringFld(
                                    state,
                                    search,
                                    node,
                                    ref stringPos,
                                    ref foldedPos,
                                    foldedLen,
                                    1
                                );

                                if (status < 0)
                                {
                                    return status;
                                }

                                if (status == MatchStatus.Failure)
                                {
                                    stringPos = -1;
                                    goto backtrack;
                                }

                                if (foldedPos >= foldedLen && foldedLen > 0)
                                {
                                    state.TextPos = state.NextPos(state.TextPos);
                                }
                            }
                            else
                            {
                                stringPos = -1;
                                goto backtrack;
                            }
                        }

                        // The pattern ran out part way through the subject character's folding, and
                        // a fuzzy string is allowed to charge the leftovers as errors rather than
                        // fail (:14855). Every other arm reaches its 'goto backtrack' below instead.
                        if ((node.Status & NodeStatus.Fuzzy) != 0)
                        {
                            while (foldedPos < foldedLen)
                            {
                                status = FuzzyMatchStringFld(
                                    state,
                                    search,
                                    node,
                                    ref stringPos,
                                    ref foldedPos,
                                    foldedLen,
                                    1
                                );

                                if (status < 0)
                                {
                                    return status;
                                }

                                if (status == MatchStatus.Failure)
                                {
                                    stringPos = -1;
                                    goto backtrack;
                                }

                                if (foldedPos >= foldedLen && foldedLen > 0)
                                {
                                    state.TextPos = state.NextPos(state.TextPos);
                                }
                            }
                        }

                        stringPos = -1;

                        // The subject character's folding was longer than what the pattern
                        // consumed, so this string is only part of it.
                        if (foldedPos < foldedLen)
                        {
                            goto backtrack;
                        }
                    }

                    // Successful match.
                    node = node.Next1.Node!;
                    break;
                }
                // STRING_IGN (:14989): STRING with 'same_char_ign' in place of 'same_char'.
                case Opcode.StringIgn: // A string, ignoring case.
                {
                    if ((node.Status & NodeStatus.Required) != 0 && state.TextPos == state.ReqPos && stringPos < 0)
                    {
                        // Unreachable until Phase 7 ports this arm of the required-string locator
                        // (:11143-11365); S60 ported the case-sensitive forward one only.
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
                                && SameCharIgn(state.Encoding, state.CharAt(state.TextPos), node.Values[stringPos])
                            )
                            {
                                ++stringPos;
                                state.TextPos = state.NextPos(state.TextPos);
                            }
                            else if ((node.Status & NodeStatus.Fuzzy) != 0)
                            {
                                status = FuzzyMatchString(state, search, node, ref stringPos, 1, false);

                                if (status < 0)
                                {
                                    return status;
                                }

                                if (status == MatchStatus.Failure)
                                {
                                    stringPos = -1;
                                    goto backtrack;
                                }
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
                        FuzzyInsert(state, 1, node.Next1.Node);
                    }

                    stringPos = -1;

                    // Successful match.
                    node = node.Next1.Node!;
                    break;
                }
                // STRING_REV (:15103). The pattern's values are consumed from the last back, and the
                // subject retreats with them.
                case Opcode.StringRev: // A string, backwards.
                {
                    if ((node.Status & NodeStatus.Required) != 0 && state.TextPos == state.ReqPos && stringPos < 0)
                    {
                        // Unreachable until Phase 7 ports this arm of the required-string locator
                        // (:11143-11365); S60 ported the case-sensitive forward one only.
                        state.TextPos = state.ReqEnd;
                    }
                    else
                    {
                        if (stringPos < 0)
                        {
                            stringPos = node.Values.Count;
                        }

                        // Try comparing.
                        while (stringPos > 0)
                        {
                            if (RanOutOnTheLeft(state, state.TextPos))
                            {
                                return MatchStatus.Partial;
                            }

                            if (
                                state.TextPos > state.SliceStart
                                && SameChar(state.CharBefore(state.TextPos), node.Values[stringPos - 1])
                            )
                            {
                                --stringPos;
                                state.TextPos = state.PrevPos(state.TextPos);
                            }
                            else if ((node.Status & NodeStatus.Fuzzy) != 0)
                            {
                                status = FuzzyMatchString(state, search, node, ref stringPos, -1, false);

                                if (status < 0)
                                {
                                    return status;
                                }

                                if (status == MatchStatus.Failure)
                                {
                                    stringPos = -1;
                                    goto backtrack;
                                }
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
                        FuzzyInsert(state, -1, node.Next1.Node);
                    }

                    stringPos = -1;

                    // Successful match.
                    node = node.Next1.Node!;
                    break;
                }
                // STRING_IGN_REV (:15046): STRING_REV with 'same_char_ign' in place of 'same_char'.
                case Opcode.StringIgnRev: // A string, backwards, ignoring case.
                {
                    if ((node.Status & NodeStatus.Required) != 0 && state.TextPos == state.ReqPos && stringPos < 0)
                    {
                        // Unreachable until Phase 7 ports this arm of the required-string locator
                        // (:11143-11365); S60 ported the case-sensitive forward one only.
                        state.TextPos = state.ReqEnd;
                    }
                    else
                    {
                        if (stringPos < 0)
                        {
                            stringPos = node.Values.Count;
                        }

                        // Try comparing.
                        while (stringPos > 0)
                        {
                            if (RanOutOnTheLeft(state, state.TextPos))
                            {
                                return MatchStatus.Partial;
                            }

                            if (
                                state.TextPos > state.SliceStart
                                && SameCharIgn(
                                    state.Encoding,
                                    state.CharBefore(state.TextPos),
                                    node.Values[stringPos - 1]
                                )
                            )
                            {
                                --stringPos;
                                state.TextPos = state.PrevPos(state.TextPos);
                            }
                            else if ((node.Status & NodeStatus.Fuzzy) != 0)
                            {
                                status = FuzzyMatchString(state, search, node, ref stringPos, -1, false);

                                if (status < 0)
                                {
                                    return status;
                                }

                                if (status == MatchStatus.Failure)
                                {
                                    stringPos = -1;
                                    goto backtrack;
                                }
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
                        FuzzyInsert(state, -1, node.Next1.Node);
                    }

                    stringPos = -1;

                    // Successful match.
                    node = node.Next1.Node!;
                    break;
                }
                // STRING_FLD_REV (:14882). STRING_FLD with the subject's folding consumed from its
                // last character back, so 'foldedPos' counts down from 'foldedLen' to zero and
                // 'text_pos' retreats when it reaches zero. S22's S1854 disapplication is gone for the
                // same reason as REF_GROUP_FLD_REV's above: 'FuzzyMatchStringFld(..., foldedLen, -1)'
                // is the reader it was waiting for.
                case Opcode.StringFldRev: // A string, backwards, ignoring case.
                {
                    int foldedLen;

                    if ((node.Status & NodeStatus.Required) != 0 && state.TextPos == state.ReqPos && stringPos < 0)
                    {
                        // Unreachable until Phase 7 ports this arm of the required-string locator
                        // (:11143-11365); S60 ported the case-sensitive forward one only.
                        state.TextPos = state.ReqEnd;
                    }
                    else
                    {
                        int length = node.Values.Count;

                        if (stringPos < 0)
                        {
                            stringPos = length;
                            foldedPos = 0;
                            foldedLen = 0;
                        }
                        else
                        {
                            // Only Phase 5's fuzzy retry reaches this arm.
                            foldedLen = Encodings.FullCaseFold(state.Encoding, state.CharBefore(state.TextPos), folded);

                            if (foldedPos <= 0)
                            {
                                if (state.TextPos <= state.SliceStart)
                                {
                                    goto backtrack;
                                }

                                state.TextPos = state.PrevPos(state.TextPos);
                                foldedPos = 0;
                                foldedLen = 0;
                            }
                        }

                        // Try comparing.
                        while (stringPos > 0)
                        {
                            if (foldedPos <= 0)
                            {
                                if (RanOutOnTheLeft(state, state.TextPos))
                                {
                                    return MatchStatus.Partial;
                                }

                                foldedLen =
                                    state.TextPos > state.SliceStart
                                        ? Encodings.FullCaseFold(
                                            state.Encoding,
                                            state.CharBefore(state.TextPos),
                                            folded
                                        )
                                        : 0;

                                foldedPos = foldedLen;
                            }

                            if (
                                foldedPos > 0
                                && SameCharIgn(state.Encoding, node.Values[stringPos - 1], folded[foldedPos - 1])
                            )
                            {
                                --stringPos;
                                --foldedPos;

                                if (foldedPos <= 0)
                                {
                                    state.TextPos = state.PrevPos(state.TextPos);
                                }
                            }
                            else if ((node.Status & NodeStatus.Fuzzy) != 0)
                            {
                                status = FuzzyMatchStringFld(
                                    state,
                                    search,
                                    node,
                                    ref stringPos,
                                    ref foldedPos,
                                    foldedLen,
                                    -1
                                );

                                if (status < 0)
                                {
                                    return status;
                                }

                                if (status == MatchStatus.Failure)
                                {
                                    stringPos = -1;
                                    goto backtrack;
                                }

                                if (foldedPos <= 0 && foldedLen > 0)
                                {
                                    state.TextPos = state.PrevPos(state.TextPos);
                                }
                            }
                            else
                            {
                                stringPos = -1;
                                goto backtrack;
                            }
                        }

                        // The pattern ran out part way through the subject character's folding
                        // (:14962), the mirror of STRING_FLD's own loop.
                        if ((node.Status & NodeStatus.Fuzzy) != 0)
                        {
                            while (foldedPos > 0)
                            {
                                status = FuzzyMatchStringFld(
                                    state,
                                    search,
                                    node,
                                    ref stringPos,
                                    ref foldedPos,
                                    foldedLen,
                                    -1
                                );

                                if (status < 0)
                                {
                                    return status;
                                }

                                if (status == MatchStatus.Failure)
                                {
                                    stringPos = -1;
                                    goto backtrack;
                                }

                                if (foldedPos <= 0 && foldedLen > 0)
                                {
                                    state.TextPos = state.PrevPos(state.TextPos);
                                }
                            }
                        }

                        stringPos = -1;

                        // The subject character's folding was longer than what the pattern
                        // consumed, so this string is only part of it.
                        if (foldedPos > 0)
                        {
                            goto backtrack;
                        }
                    }

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
                        CheckPosixMatch(state);

                        goto backtrack;
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
                // Upstream's shared one-character block (:15210-15243): every one of these opcodes
                // is on the backtracking stack for exactly one reason, which is that fuzzy_match_item
                // put it there. 'advance: true' because the item consumes a character.
                case Opcode.Any:
                case Opcode.AnyAll:
                case Opcode.AnyAllRev:
                case Opcode.AnyRev:
                case Opcode.AnyU:
                case Opcode.AnyURev:
                case Opcode.Character:
                case Opcode.CharacterIgn:
                case Opcode.CharacterIgnRev:
                case Opcode.CharacterRev:
                case Opcode.Property:
                case Opcode.PropertyIgn:
                case Opcode.PropertyIgnRev:
                case Opcode.PropertyRev:
                case Opcode.Range:
                case Opcode.RangeIgn:
                case Opcode.RangeIgnRev:
                case Opcode.RangeRev:
                case Opcode.SetDiff:
                case Opcode.SetDiffIgn:
                case Opcode.SetDiffIgnRev:
                case Opcode.SetDiffRev:
                case Opcode.SetInter:
                case Opcode.SetInterIgn:
                case Opcode.SetInterIgnRev:
                case Opcode.SetInterRev:
                case Opcode.SetSymDiff:
                case Opcode.SetSymDiffIgn:
                case Opcode.SetSymDiffIgnRev:
                case Opcode.SetSymDiffRev:
                case Opcode.SetUnion:
                case Opcode.SetUnionIgn:
                case Opcode.SetUnionIgnRev:
                case Opcode.SetUnionRev:
                    status = RetryFuzzyMatchItem(state, op, search, ref node, advance: true);

                    if (status < 0)
                    {
                        return status;
                    }

                    if (status == MatchStatus.Success)
                    {
                        goto advance;
                    }

                    break;
                // Upstream's shared zero-width block (:15330-15344). 'advance: false', which is what
                // puts a step of 0 back into next_fuzzy_match_item.
                case Opcode.Boundary:
                case Opcode.DefaultBoundary:
                case Opcode.DefaultEndOfWord:
                case Opcode.DefaultStartOfWord:
                case Opcode.EndOfLine:
                case Opcode.EndOfLineU:
                case Opcode.EndOfString:
                case Opcode.EndOfStringLine:
                case Opcode.EndOfStringLineU:
                case Opcode.EndOfWord:
                case Opcode.GraphemeBoundary:
                case Opcode.StartOfLine:
                case Opcode.StartOfLineU:
                case Opcode.StartOfString:
                case Opcode.StartOfWord:
                    status = RetryFuzzyMatchItem(state, op, search, ref node, advance: false);

                    if (status < 0)
                    {
                        return status;
                    }

                    if (status == MatchStatus.Success)
                    {
                        goto advance;
                    }

                    break;
                // Upstream's shared string block (:17269-17290). The REF_GROUP rows pass a subject
                // position where the STRING rows pass an index into the node's values, which is the
                // one thing upstream does not have to say and this port does.
                case Opcode.RefGroup:
                case Opcode.RefGroupIgn:
                case Opcode.RefGroupIgnRev:
                case Opcode.RefGroupRev:
                case Opcode.String:
                case Opcode.StringIgn:
                case Opcode.StringIgnRev:
                case Opcode.StringRev:
                {
                    bool stringPosIsText =
                        (Opcode)op
                        is Opcode.RefGroup
                            or Opcode.RefGroupIgn
                            or Opcode.RefGroupIgnRev
                            or Opcode.RefGroupRev;

                    status = RetryFuzzyMatchString(state, op, search, ref node, ref stringPos, stringPosIsText);

                    if (status < 0)
                    {
                        return status;
                    }

                    if (status == MatchStatus.Success)
                    {
                        goto advance;
                    }

                    stringPos = -1;
                    break;
                }
                // Upstream :17291-17306.
                case Opcode.RefGroupFld:
                case Opcode.RefGroupFldRev:
                    status = RetryFuzzyMatchGroupFld(
                        state,
                        op,
                        search,
                        ref node,
                        ref foldedPos,
                        ref stringPos,
                        ref gfoldedPos
                    );

                    if (status < 0)
                    {
                        return status;
                    }

                    if (status == MatchStatus.Success)
                    {
                        goto advance;
                    }

                    stringPos = -1;
                    break;
                // Upstream :17361-17376.
                case Opcode.StringFld:
                case Opcode.StringFldRev:
                    status = RetryFuzzyMatchStringFld(state, op, search, ref node, ref stringPos, ref foldedPos);

                    if (status < 0)
                    {
                        return status;
                    }

                    if (status == MatchStatus.Success)
                    {
                        goto advance;
                    }

                    stringPos = -1;
                    break;
                case Opcode.FuzzyInsert: // One more inserted character after a string (:15766).
                    status = RetryFuzzyInsert(state, ref node);

                    if (status < 0)
                    {
                        return status;
                    }

                    if (status == MatchStatus.Success)
                    {
                        goto advance;
                    }

                    stringPos = -1;
                    break;
                case Opcode.Atomic: // Start of an atomic group.
                {
                    /* sstack: ...
                     *
                     * bstack: captures fuzzy_counts capture_change sstack
                     *
                     * pstack: bstack
                     */

                    if (!state.Pstack.DropSize() || !state.Bstack.PopSize(out long atomicSstackCount))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.Sstack.Count = (int)atomicSstackCount;
                    CloseCallsAbove(state);

                    if (!state.Bstack.PopSize(out long atomicCaptureChange))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.CaptureChange = atomicCaptureChange;

                    if (!state.PopFuzzyCounts(state.Bstack, state.FuzzyCounts) || !PopCaptures(state, state.Bstack))
                    {
                        return MatchStatus.Illegal;
                    }

                    break;
                }
                case Opcode.EndAtomic: // End of an atomic group.
                {
                    /* bstack: captures fuzzy_counts capture_change */

                    if (!state.Bstack.PopSize(out long endAtomicCaptureChange))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.CaptureChange = endAtomicCaptureChange;

                    if (!state.PopFuzzyCounts(state.Bstack, state.FuzzyCounts) || !PopCaptures(state, state.Bstack))
                    {
                        return MatchStatus.Illegal;
                    }

                    break;
                }
                case Opcode.CallRef: // A group call ref (:15367).
                    /* sstack: NULL
                     *
                     * bstack: -
                     */

                    if (!state.Sstack.DropSize())
                    {
                        return MatchStatus.Illegal;
                    }

                    break;
                case Opcode.Conditional: // Conditional subpattern (:15378).
                {
                    /* sstack: node slice_start slice_end text_pos ...
                     *
                     * bstack: captures repeats capture_change sstack
                     *
                     * pstack: bstack
                     */

                    // The condition failed to match. As with LOOKAROUND, the condition's own bstack
                    // entries have already been popped by the backtracking that got here, so the
                    // pstack entry is simply dropped.
                    if (!state.Pstack.DropSize() || !state.Bstack.PopSize(out long condSstackCount))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.Sstack.Count = (int)condSstackCount;
                    CloseCallsAbove(state);

                    if (!PopLookaroundStateData(pattern, state.Sstack, out LookaroundStateData condData))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.TextPos = condData.TextPos;
                    state.SliceEnd = condData.SliceEnd;
                    state.SliceStart = condData.SliceStart;
                    Node condNode = condData.Node;

                    /* sstack: -
                     *
                     * bstack: captures repeats capture_change
                     *
                     * pstack: -
                     */

                    if (!state.Bstack.PopSize(out long condCaptureChange))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.CaptureChange = condCaptureChange;

                    if (
                        !state.PopFuzzyCounts(state.Bstack, state.FuzzyCounts)
                        || !PopRepeats(state, state.Bstack)
                        || !PopCaptures(state, state.Bstack)
                    )
                    {
                        return MatchStatus.Illegal;
                    }

                    // A positive condition that failed means the condition does not hold, so take the
                    // 'false' branch; a negative one that failed means it does, so take the 'true'
                    // branch. Neither is a failure of the conditional itself, so this goes forward
                    // rather than on backtracking.
                    node = condNode.Match ? condNode.Next2.Node! : condNode.TrueNode!;
                    goto advance;
                }
                case Opcode.EndConditional: // End of a conditional subpattern (:15460).
                {
                    /* bstack: captures repeats capture_change */

                    if (!state.Bstack.PopSize(out long endCondCaptureChange))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.CaptureChange = endCondCaptureChange;

                    if (
                        !state.PopFuzzyCounts(state.Bstack, state.FuzzyCounts)
                        || !PopRepeats(state, state.Bstack)
                        || !PopCaptures(state, state.Bstack)
                    )
                    {
                        return MatchStatus.Illegal;
                    }

                    break;
                }
                case Opcode.EndFuzzy: // End of fuzzy matching (:15488).
                {
                    Span<long> innerCounts = fuzzyInnerCounts;

                    /* sstack: -
                     *
                     * bstack: inner_counts insertions inner_node text_pos end_fuzzy_node
                     */

                    if (
                        !state.Bstack.PopNode(pattern, out Node? endFuzzyNode)
                        || !state.Bstack.PopSize(out long endFuzzyTextPos)
                        || !state.Bstack.PopNode(pattern, out Node? innerNode)
                        || !state.Bstack.PopSize(out long insertions)
                        // MERGING, for the reason the forward END_FUZZY arm gives: the section's
                        // changes outlive its counts coming off the stack, and the trailing-insertion
                        // retry below goes on to ADD to them.
                        || !state.PopFuzzyCountsMerging(state.Bstack, innerCounts, out _)
                    )
                    {
                        return MatchStatus.Illegal;
                    }

                    node = endFuzzyNode!;
                    state.TextPos = (int)endFuzzyTextPos;

                    // Try one more insertion after the section. This is the only place a trailing
                    // insertion can come from: every item inside the section has already been tried.
                    //
                    // THE SECOND TEST IS UPSTREAM'S WITH ITS DOUBLE COUNT REMOVED - a deliberate
                    // divergence, ledger entry 12, fixed by S46 (2026-09-14). Upstream writes
                    // 'total_errors(state->fuzzy_counts) + total_errors(inner_counts) <
                    // state->max_errors' (:15515-15517), and END_FUZZY has already merged
                    // 'inner_counts' INTO 'state->fuzzy_counts' twenty lines earlier (:12473-12484),
                    // so the two terms are the same errors added twice. Every other 'max_errors'
                    // test in upstream's file asks about ONE set of counts ('any_error_permitted'
                    // :9672, 'this_error_permitted' :9690, 'insertion_permitted' :9708), and
                    // 'insertion_permitted' on the line above already applies the section's own
                    // limits to 'inner_counts', so nothing is lost by dropping the second term.
                    //
                    // The double count is invisible wherever 'max_errors' is unbounded, which is
                    // plain fuzzy matching ('DoSimpleFuzzyMatch' sets it to 'long.MaxValue', as
                    // upstream's ':18027' sets PY_SSIZE_T_MAX). It bites in 'DoBestFuzzyMatch',
                    // whose second pass climbs 'max_errors' only to 'fewest_errors' - so the budget
                    // the guard is tested against is the match's own cost, the caller cannot raise
                    // it, and '(?b)' loses a match the same engine finds the moment the flag is
                    // deleted. On '(?:x){e<=N}' that reads 'n > 2n-2', false for every n >= 2 at
                    // every budget, WHICH IS THAT PATTERN'S BOUNDARY AND NOT THE DEFECT'S: a fit
                    // needing ONE insertion beside one other error is lost too, and a width-2
                    // section body survives counts a width-1 body does not (S52 sitting 16, blocks
                    // 6 and 7 of tools/probes/upstream-bestmatch-trailing-insertions.py). Held by
                    // 'Gaps.Engine.FuzzyBestMatchTests
                    // .Bestmatch_keeps_a_match_that_needs_two_trailing_insertions', the (k, N)
                    // matrix beside it, and
                    // '.Bestmatch_keeps_a_match_whose_single_trailing_insertion_is_not_its_only_error'.
                    //
                    // The third test is this port's, for the reason the second test at 'END_FUZZY'
                    // spells out: 'InsertionPermitted' bounds the cost of the section it is handed,
                    // and nothing here bounds the cost of the WHOLE MATCH, which is the quantity
                    // 'DoBestFuzzyMatch' ranks by. It is upstream's second test with cost in place of
                    // the error count, over 'state.FuzzyCounts' - the counts 'END_FUZZY' has already
                    // merged - and it refuses only insertions the budget has already said are
                    // unaffordable, so it cannot lose a match that fits.
                    //
                    // ponytail: NO TEST PINS THIS ONE, and it is kept anyway. Deleting it changes
                    // nothing measurable - the 5854-test suite stays green, all three default-wave
                    // seeds stay green, S42's blind review swept 1,425 weighted-cost '(?b)' rows
                    // across two seeds and found no row it affects, and four hand-built
                    // group-call-plus-trailing-insertion patterns behave identically with and
                    // without it. What it defends is a HANG rather than a wrong answer: on
                    // re-entering one section the merged live counts can outrun the per-entry bound
                    // 'InsertionPermitted' applies, and walk 0 then sees a run it cannot improve on.
                    // A hang costs an unattended slice where one comparison on a backtrack arm costs
                    // nothing, so the asymmetry decides it. Upgrade path: if a case is ever
                    // constructed, it becomes a test here and this note goes.
                    if (
                        InsertionPermitted(state, innerNode!, innerCounts)
                        && TotalErrors(state.FuzzyCounts) < state.MaxErrors
                        && TotalCost(state.FuzzyCounts, innerNode!) + innerNode!.Values[FuzzyValue.InsCost]
                            <= state.MaxCost
                        && FuzzyExtMatch(state, innerNode, state.TextPos)
                    )
                    {
                        bool endFuzzyReverse = (innerNode.Status & NodeStatus.Reverse) != 0;
                        int endFuzzyLimit = endFuzzyReverse ? state.SliceStart : state.SliceEnd;

                        if (state.TextPos != endFuzzyLimit)
                        {
                            state.RecordFuzzy(FuzzyValue.Ins, state.TextPos);
                            ++innerCounts[FuzzyValue.Ins];

                            state.TextPos = Step(state, state.TextPos, endFuzzyReverse ? -1 : 1);

                            // Save the inner fuzzy info.
                            state.PushFuzzyCounts(state.Bstack, innerCounts);
                            state.Bstack.PushSize(insertions + 1);
                            state.Bstack.PushNode(innerNode);
                            state.Bstack.PushSize(state.TextPos);
                            state.Bstack.PushNode(node);
                            state.Bstack.PushUInt8((byte)Opcode.EndFuzzy);

                            /* bstack: inner_counts insertions inner_node text_pos end_fuzzy_node
                             * END_FUZZY
                             */

                            ++state.FuzzyCounts[FuzzyValue.Ins];
                            state.TotalErrors = TotalErrors(state.FuzzyCounts);

                            // This port's own line - see the matching one in the END_FUZZY case
                            // above. The section that used these errors is the inner one just
                            // popped, which is the node the trailing insertion was tried against.
                            state.TotalCost = TotalCost(state.FuzzyCounts, innerNode);

                            node = node.Next1.Node!;
                            goto advance;
                        }
                    }

                    // Subtract the inner counts from the outer counts.
                    state.FuzzyCounts[FuzzyValue.Sub] -= innerCounts[FuzzyValue.Sub];
                    state.FuzzyCounts[FuzzyValue.Ins] -= innerCounts[FuzzyValue.Ins];
                    state.FuzzyCounts[FuzzyValue.Del] -= innerCounts[FuzzyValue.Del];

                    // Save the outer fuzzy info.
                    state.PushFuzzyCounts(state.Sstack, state.FuzzyCounts);
                    state.Sstack.PushNode(state.FuzzyNode);

                    /* sstack: outer_counts outer_node
                     *
                     * bstack: -
                     */

                    innerCounts[FuzzyValue.Ins] -= insertions;

                    while (insertions > 0)
                    {
                        state.UnrecordFuzzy();
                        --insertions;
                    }

                    // Restore the inner fuzzy info.
                    innerCounts.CopyTo(state.FuzzyCounts);
                    state.FuzzyNode = innerNode;
                    break;
                }
                case Opcode.GroupCall: // Group call (:16354).
                {
                    /* sstack: caller_groups caller_repeats capture_change return_node
                     *
                     * bstack: -
                     */

                    // The call is no longer open: backtracking past it means it never happened.
                    PopOpenCall(state);

                    // The return node is dropped rather than popped: backtracking past the call
                    // means there is nowhere to return to.
                    if (
                        !state.Sstack.DropSize()
                        || !state.Sstack.PopSize(out long groupCallCaptureChange)
                        || !PopRepeats(state, state.Sstack)
                        || !PopGroups(state, state.Sstack)
                    )
                    {
                        return MatchStatus.Illegal;
                    }

                    // For the caller.
                    state.CaptureChange = groupCallCaptureChange;
                    break;
                }
                case Opcode.GroupReturn: // Group return (:16375).
                {
                    /* If called:
                     *
                     * sstack: -
                     *
                     * bstack: callee_groups callee_repeats capture_change call_key return_node
                     *
                     * else:
                     *
                     * sstack: -
                     *
                     * bstack: NULL
                     */

                    if (!state.Bstack.PopNode(pattern, out Node? groupReturnBackNode))
                    {
                        return MatchStatus.Illegal;
                    }

                    if (groupReturnBackNode is not null)
                    {
                        // Backtracking into the call re-opens it, so its key comes back off the
                        // backtracking stack.
                        if (!state.Bstack.PopSize(out long groupReturnBackKey))
                        {
                            return MatchStatus.Illegal;
                        }

                        // For the caller. The forward arm's exchange, run the other way round: the
                        // caller's state goes back on the saved stack and the callee's comes off
                        // the backtracking stack, so the callee resumes inside the call.
                        PushGroups(state, state.Sstack);
                        PushRepeats(state, state.Sstack);
                        state.Sstack.PushSize(state.CaptureChange);
                        state.Sstack.PushNode(groupReturnBackNode);

                        // The frame is back, so the call is open again and ends where it now ends.
                        state.ActiveCalls.Add(groupReturnBackKey);
                        state.OpenCalls.Add((groupReturnBackKey, state.Sstack.Count));

                        /* sstack: caller_groups caller_repeats capture_change return_node
                         *
                         * bstack: callee_groups callee_repeats capture_change
                         */

                        // For the callee.
                        if (
                            !state.Bstack.PopSize(out long groupReturnBackCaptureChange)
                            || !PopRepeats(state, state.Bstack)
                            || !PopGroups(state, state.Bstack)
                        )
                        {
                            return MatchStatus.Illegal;
                        }

                        state.CaptureChange = groupReturnBackCaptureChange;
                    }
                    else
                    {
                        state.Sstack.PushNode(null);
                    }

                    /* If called:
                     *
                     * sstack: caller_groups caller_repeats capture_change return_node
                     *
                     * bstack: -
                     *
                     * else:
                     *
                     * sstack: NULL
                     *
                     * bstack: -
                     */

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
                case Opcode.EndLookaround: // End of a lookaround subpattern (:15650).
                {
                    /* bstack: [captures TRUE | FALSE] fuzzy_counts capture_change */

                    // The 'true' branch of a positive lookaround is being given up, so the captures
                    // its body made go with it. Leaving this out is invisible to any test that does
                    // not backtrack past the lookaround.
                    if (!state.Bstack.PopSize(out long endLookCaptureChange))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.CaptureChange = endLookCaptureChange;

                    if (!state.PopFuzzyCounts(state.Bstack, state.FuzzyCounts))
                    {
                        return MatchStatus.Illegal;
                    }

                    if (!state.Bstack.PopBool(out bool endLookHasGroups))
                    {
                        return MatchStatus.Illegal;
                    }

                    if (endLookHasGroups && !PopCaptures(state, state.Bstack))
                    {
                        return MatchStatus.Illegal;
                    }

                    break;
                }
                case Opcode.Failure: // Failure.
                    // Have we been looking for a POSIX match?
                    if (state.FoundMatch)
                    {
                        // Upstream writes 'return RE_OP_SUCCESS' here (:15688) where every other arm
                        // returns an RE_ERROR_* code. The two constants are both 1 (_regex.h:20,
                        // _regex.c:104), so the slip is invisible; this is the code it means.
                        RestoreBestMatch(state);

                        return MatchStatus.Success;
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
                case Opcode.Fuzzy: // Fuzzy matching (:15752).
                    /* sstack: outer_counts outer_node
                     *
                     * bstack: -
                     */

                    // Restore the outer fuzzy info. MERGING: backing out through FUZZY means every
                    // item in the section has already backtracked and unwound its own change, so
                    // there is nothing left to truncate - and the length on the stack here may have
                    // been re-pushed by the END_FUZZY backtrack arm, which cannot know the one the
                    // section was entered with.
                    if (
                        !state.Sstack.PopNode(pattern, out Node? outerFuzzyNode)
                        || !state.PopFuzzyCountsMerging(state.Sstack, state.FuzzyCounts, out _)
                    )
                    {
                        return MatchStatus.Illegal;
                    }

                    state.FuzzyNode = outerFuzzyNode;
                    break;
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
                    //
                    // The second clamp in each direction is issue 613, commit b77694a, released in
                    // 2026.8.30 and ported by S44. The clamp above can raise 'limit' PAST 'pos' -
                    // a (*SKIP) that fired to the right of this repeat moves 'slice_start' above
                    // the position the retreat starts from - and the stop below is an equality,
                    // so once that happens the retreat walks away from its own limit instead of
                    // reaching it. Upstream reads off the end of the buffer doing so; measured
                    // 2026-09-13, upstream 2026.7.19 never returns on 70 of the 1,296 calls in
                    // `tools/probes/upstream-skip-in-atomic-hang.py --grid` and 2026.9.10 on none.
                    if (step > 0)
                    {
                        if (limit < state.SliceStart)
                        {
                            limit = state.SliceStart;
                        }

                        if (pos < limit)
                        {
                            limit = pos;
                        }
                    }
                    else
                    {
                        if (limit > state.SliceEnd)
                        {
                            limit = state.SliceEnd;
                        }

                        if (pos > limit)
                        {
                            limit = pos;
                        }
                    }

                    if (pos == limit)
                    {
                        // We've backtracked the repeat as far as we can.
                        rpData.Start = start;
                        rpData.Count = savedCount;
                        break;
                    }

                    // NOT PORTED: upstream's `test = node->next_1.test` and its
                    // `if (test->status & RE_STATUS_FUZZY)` retreat loop
                    // (:15881). Two separate reasons, and the first is that it would be the same
                    // code: that loop is character for character upstream's own default arm below
                    // (:16271), because `status != RE_ERROR_FAILURE` after the `status < 0` return is
                    // `status == RE_ERROR_SUCCESS`. So a fuzzy tail already gets upstream's fuzzy
                    // behaviour from the loop that is here.
                    //
                    // The second is that no pattern can reach it. `sequence_matches_one` (:24056)
                    // refuses a REPEAT_ONE whose body node carries RE_STATUS_FUZZY, and every
                    // one-character op in `node_matches_one_character` (:3433) is emitted by a
                    // parser class that sets FUZZY_OP when compiled inside a section
                    // (`_regex_core.py`: Any, Character, Property, Range, SetBase, SetUnion,
                    // ZeroWidthBase) - so a REPEAT_ONE is never inside one. Outside one, the node
                    // after it is FUZZY or FUZZY_EXT, which `Fuzzy._compile` (`:2839`) emits with
                    // REVERSE_OP and never FUZZY_OP, and `can_test_past` (:23697) does not walk past
                    // either. Measured 2026-09-13 over all 1,534 compile-parity patterns: 639
                    // REPEAT_ONE nodes, none with a fuzzy test node. Pinned by
                    // RepeatTests.No_repeat_one_node_in_the_corpus_has_a_fuzzy_test_node, which is
                    // what fails if a future sync makes this branch reachable.
                    //
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

                        status = TryMatch(state, node.Next1, pos, out _);
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

                    // NOT PORTED: upstream's fuzzy advance loop (:16500), unreachable for the reason
                    // set out in the GREEDY_REPEAT_ONE case above and pinned by the same test. It is
                    // not quite its own default arm here - it lacks the partial-side guard the arm
                    // below borrows from upstream's specialised cases, and it returns 'status' where
                    // the default arm returns PARTIAL (:16515 against :17040) - so this one is a
                    // deletion on the unreachability alone, not on sameness.
                    //
                    // Only upstream's default arm (:17024) is ported, for the reasons given in the
                    // GREEDY_REPEAT_ONE case above. Upstream's 'skip_pos' goes with the string arms
                    // that are not ported - they are the only thing that sets it, so its
                    // '-1' branch is all that is left and it does nothing.
                    bool match = false;

                    while (true)
                    {
                        // ...plus the one guard those arms carry that is behaviour and not
                        // optimisation. Every specialised arm asks partial_side at the TOP of its
                        // loop, before it tries to extend the repeat at all (:16546, :16583,
                        // :16621, :16659 for the four CHARACTER tails; :16699, :16754, :16809,
                        // :16868, :16925, :16982 for the six STRING ones), and the default arm has
                        // no such check. The gap is only visible when the repeat CANNOT extend: for
                        // `regex.match(r'([^a-f]{3,}?)x', '__AAb', partial=True)` upstream answers
                        // a partial at (0,5) from here, while this port asked MatchOne first, was
                        // refused by the 'b', broke out of the loop and reported no match at all.
                        // Found by the S31 oracle wave, seed 7, row 581.
                        if (IsTailPartial(state, test, pos))
                        {
                            return MatchStatus.Partial;
                        }

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

                        // The tail's REAL answer, not TryMatch's transparent success. This is the
                        // one call site where the reduction described on TryMatch is not
                        // transparent even for a FAILURE, because the loop's exit condition is
                        // exactly this status: a tail that reports success when it cannot match
                        // ends the walk at the first position instead of carrying on to the next
                        // one, where the guard above would have answered PARTIAL. Upstream's own
                        // default arm calls try_match, which does test the node (:17037), and its
                        // specialised arms compare the tail character inline (:16561) - so both
                        // arms of upstream's switch get the real answer here and only this port did
                        // not. Found by the S31 blind review: `regex.match('ba??x', 'baa',
                        // partial=True)` is (0,3) partial upstream and was no match here.
                        //
                        // 'nextPosition' is discarded at this call site, so nothing depends on
                        // upstream's fast-path rule that a successful try_match consumes the
                        // character it tested - which is why asking the predicate here does not
                        // reinstate the Phase 7 fast path S19 measured and reverted.
                        status = TryMatch(state, node.Next1, pos, out _);

                        if (status < 0)
                        {
                            // Upstream returns RE_ERROR_PARTIAL here rather than 'status', where the
                            // GREEDY_REPEAT_ONE arm above returns 'status' (:17040 against :16280).
                            // Ported as written.
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
                case Opcode.Lookaround: // Lookaround subpattern (:17109).
                {
                    /* sstack: node slice_start slice_end text_pos ...
                     *
                     * bstack: [captures TRUE | FALSE] fuzzy_counts capture_change sstack
                     *
                     * pstack: bstack
                     */

                    // The body failed to match. Unlike END_LOOKAROUND, which truncated the bstack to
                    // the parked count, here the body's own entries have already been popped by the
                    // backtracking that brought us here, so the pstack entry is simply dropped.
                    if (!state.Pstack.DropSize() || !state.Bstack.PopSize(out long lookSstackCount))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.Sstack.Count = (int)lookSstackCount;
                    CloseCallsAbove(state);

                    if (!PopLookaroundStateData(pattern, state.Sstack, out LookaroundStateData lookData))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.TextPos = lookData.TextPos;
                    state.SliceEnd = lookData.SliceEnd;
                    state.SliceStart = lookData.SliceStart;
                    Node lookNode = lookData.Node;

                    /* sstack: -
                     *
                     * bstack: [captures TRUE | FALSE] fuzzy_counts capture_change
                     *
                     * pstack: -
                     */

                    if (!state.Bstack.PopSize(out long lookCaptureChange))
                    {
                        return MatchStatus.Illegal;
                    }

                    state.CaptureChange = lookCaptureChange;

                    if (!state.PopFuzzyCounts(state.Bstack, state.FuzzyCounts))
                    {
                        return MatchStatus.Illegal;
                    }

                    if (!state.Bstack.PopBool(out bool lookHasGroups))
                    {
                        return MatchStatus.Illegal;
                    }

                    if (lookHasGroups && !PopCaptures(state, state.Bstack))
                    {
                        return MatchStatus.Illegal;
                    }

                    if (!lookNode.Match)
                    {
                        // It's a negative lookaround that's failed, which means the lookaround as a
                        // whole has succeeded.
                        node = lookNode.Next2.Node!;
                        goto advance;
                    }

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
                    // the ANY family, S17's PROPERTY, RANGE and SET_* and S22's _IGN and _FLD
                    // variants of all of them only put themselves on the backtracking stack to
                    // retry a fuzzy match (upstream's shared one-character block, :15210-15243, is
                    // nothing but 'retry_fuzzy_match_item', and the REF_GROUP and STRING rows at
                    // :17269-17291 are the same), which the dispatch loop refuses above; BRANCH,
                    // START_GROUP, END_GROUP and S19's
                    // BODY_END, BODY_START, GREEDY_REPEAT, LAZY_REPEAT, GREEDY_REPEAT_ONE,
                    // LAZY_REPEAT_ONE, MATCH_BODY, MATCH_TAIL and TAIL_START have their own cases
                    // here.
                    throw Seam.For((Opcode)op);
            }
        }
    }

    /// <summary>
    /// Upstream <c>save_captures</c> (<c>upstream/src/_regex.c</c> line 17403): snapshots every
    /// group, so that a fuzzy ranking mode can put the best run's captures back at the end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Upstream's whole function past the two <c>Py_MEMCPY</c>s is allocation: it reuses the buffer
    /// the previous save returned and grows a group's capture array only when it has to
    /// (<c>:17413-17440</c>). <see cref="GroupData.CopyGroups"/> already produces a snapshot holding
    /// exactly the live spans, so this is the same shape as <see cref="SaveBestMatch"/> and reusing
    /// the storage across runs is a Phase 7 question rather than a correctness one.
    /// </para>
    /// <para>
    /// NOT PORTED: <c>discard_groups</c> (<c>:17500</c>), which is upstream's <c>free</c> for the same
    /// snapshot and has nothing to do on a garbage-collected heap. Dropping the reference is the
    /// whole of it, so the call site says so in a comment rather than calling an empty method.
    /// </para>
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <returns>The snapshot.</returns>
    private static GroupData[] SaveCaptures(MatchState state) =>
        GroupData.CopyGroups(state.Groups, state.Groups.Length);

    /// <summary>
    /// Upstream <c>restore_groups</c> (<c>upstream/src/_regex.c</c> line 17468), less its
    /// <c>re_dealloc</c> half.
    /// </summary>
    /// <remarks>
    /// The spans are copied into the live <see cref="GroupData"/> objects rather than the array being
    /// swapped, exactly as upstream's <c>Py_MEMCPY</c> does: a group's captures array is grown in
    /// place elsewhere, so the identity of these objects is what the rest of the match holds.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="saved">The snapshot to put back.</param>
    private static void RestoreGroups(MatchState state, GroupData[] saved)
    {
        for (int g = 0; g < state.Groups.Length; g++)
        {
            GroupData group = state.Groups[g];
            GroupData savedGroup = saved[g];

            group.Count = savedGroup.Count;
            group.Current = savedGroup.Current;

            // The saved count can never exceed the array this group already had when it was saved,
            // and a captures array only ever grows, so upstream's unchecked memcpy is safe here too.
            savedGroup.Captures.AsSpan(0, savedGroup.Count).CopyTo(group.Captures);
        }
    }

    /// <summary>Upstream <c>save_fuzzy_counts</c> (<c>upstream/src/_regex.c</c> line 17520).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="fuzzyCounts">Where to save them.</param>
    private static void SaveFuzzyCounts(MatchState state, Span<long> fuzzyCounts) =>
        state.FuzzyCounts.CopyTo(fuzzyCounts);

    /// <summary>Upstream <c>restore_fuzzy_counts</c> (<c>upstream/src/_regex.c</c> line 17526).</summary>
    /// <param name="state">The match state.</param>
    /// <param name="fuzzyCounts">Where to restore them from.</param>
    private static void RestoreFuzzyCounts(MatchState state, ReadOnlySpan<long> fuzzyCounts) =>
        fuzzyCounts.CopyTo(state.FuzzyCounts);

    /// <summary>
    /// Upstream <c>do_exact_match</c> (<c>upstream/src/_regex.c</c> line 18064).
    /// </summary>
    /// <param name="state">The match state.</param>
    /// <param name="search">Whether to search rather than anchor at the start position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int DoExactMatch(MatchState state, bool search)
    {
        // CHARACTERS, NOT CODE UNITS, and S40c is the slice that proved the difference is visible.
        // 'min_width' is a character count, this port's positions are UTF-16 indexes, and a code-unit
        // subtraction over-counts by one per surrogate pair.
        //
        // The comment that stood here argued the mismatch was safe: over-counting can only make the
        // early-out fail to fire, and "the engine simply does the work and fails in the dispatch loop
        // instead - the same answer". That is true of a plain match and FALSE of a partial one. The
        // early-out is guarded by 'partial_side == RE_PARTIAL_NONE' below, so it fires on the
        // non-partial pass only, and DoMatch falls back to the partial pass exactly when the
        // non-partial one FAILS. Skipping the early-out therefore does not reach the same answer by a
        // slower road: the non-partial pass runs and SUCCEEDS, and the partial retry upstream would
        // have performed never happens.
        //
        // Measured 2026-09-13. 'search(r"(?P<g1>\U00010400)(?:(?<=(?P>g1))\w)?", "\U00010400",
        // partial=True)' is a PARTIAL upstream - one character available against a min_width of 2 -
        // and was a complete match here, because 'available' read the astral character's two code
        // units as two characters. The ASCII spelling of the same pattern was never affected and
        // always agreed, which is what made the family look like a group-call defect for two slices.
        // Pinned by 'PartialMatchingTests.The_width_early_out_that_skips_the_non_partial_pass_
        // counts_characters_not_code_units'; upstream's own threshold is measured over three callee
        // widths by tools/probes/upstream-min-width-partial-retry.py.
        //
        // CountBetween rounds outward, so a caller that slices into the middle of a surrogate pair
        // still costs a whole character at that end. That is the old over-counting, surviving only
        // for a bound upstream cannot express, and it keeps 'available == 0' meaning what it did.
        long available = CountBetween(state, state.TextPos, state.Reverse ? state.SliceStart : state.SliceEnd);

        // The maximum permitted cost.
        state.MaxErrors = 0;

        // No cost bound: 'max_errors' of 0 already forbids every error, so the bound 'BESTMATCH'
        // needs would be redundant here. Set all the same, because one state serves a whole scan and
        // a previous 'BESTMATCH' call must not leave its bound behind.
        state.MaxCost = long.MaxValue;

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
    /// Whether one run of a fuzzy ranking mode beat the best run so far. <b>This port's own rule, and
    /// a deliberate divergence from upstream.</b> <c>ENHANCEMATCH</c> is its only caller today;
    /// <c>BESTMATCH</c> is a seam until S42 and is to call this rather than spell the rule again, so
    /// that the two modes cannot drift apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lowest cost wins; ties go to the fewer errors; ties on both go to the run found first, which
    /// is what returning <see langword="false"/> for "no better" gives.
    /// </para>
    /// <para>
    /// Upstream ranks by error COUNT alone - <c>better = state-&gt;total_errors &lt; fewest_errors</c>
    /// (<c>:17930</c>) and the same test at <c>:17647</c> - and never consults <c>total_cost</c>,
    /// which is upstream's open issue 470: <c>(?b)(voices){1i+1d+2s&lt;=2}</c> over
    /// <c>voixes voicees</c> answers with the cost-2 substitution rather than the cost-1 insertion,
    /// because both are one error. Releases up to 2015.09.28 ranked by cost
    /// (<c>state-&gt;max_cost = state-&gt;total_cost - 1</c>) and the 2015.11.5 rework replaced
    /// <c>max_cost</c> with <c>max_errors</c> throughout, so the count rule is a regression rather
    /// than a design. Owner decision, DECISIONS 2026-09-12.
    /// </para>
    /// <para>
    /// <b>With unit costs the two rules agree</b>, which is why no ported test changes: a cost
    /// equation is the only place they can differ, and those cases are pinned by gap tests and by a
    /// strict <c>ExpectedDivergences</c> entry.
    /// </para>
    /// </remarks>
    /// <param name="cost">This run's cost.</param>
    /// <param name="errors">This run's error count.</param>
    /// <param name="bestCost">The best cost so far.</param>
    /// <param name="bestErrors">The error count of the run that produced it.</param>
    /// <returns><see langword="true"/> if this run is the better one.</returns>
    private static bool IsBetterFuzzyMatch(long cost, long errors, long bestCost, long bestErrors) =>
        cost != bestCost ? cost < bestCost : errors < bestErrors;

    /// <summary>
    /// Upstream <c>do_enhanced_fuzzy_match</c> (<c>upstream/src/_regex.c</c> line 17862): find a
    /// fuzzy match, then keep re-running inside its own span with a tighter error budget until the
    /// fit stops improving.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The <c>same_match</c> check is deliberately not live code here, because it is not live
    /// upstream either.</b> Upstream computes it and then overrides it to <c>FALSE</c> on the very
    /// next line (<c>:17942-17944</c>), so the <c>same_span_of_group</c> loop beneath it never runs
    /// and the early exit at <c>:17966</c> reduces to <c>total_errors == 0</c>:
    /// </para>
    /// <code>
    /// same_match = state->match_pos == best_match_pos &amp;&amp;
    ///   state->text_pos == best_text_pos;
    /// same_match = FALSE;
    ///
    /// if (best_groups) {
    ///     size_t g;
    ///
    ///     /* Did we get the same match as the best so far? */
    ///     for (g = 0; same_match &amp;&amp; g &lt; pattern->public_group_count;
    ///       g++)
    ///         same_match = same_span_of_group(&amp;state->groups[g],
    ///           &amp;best_groups[g]);
    /// }
    /// </code>
    /// <para>
    /// It is deliberate, not a slip: the 2014.12.24 and 2015.09.28 releases had the check live
    /// (<c>if (same) break;</c>) in one combined best/enhanced loop, and 2015.11.5 - the issue 165
    /// "Performance / hung search" rework that split that loop into <c>do_simple</c>,
    /// <c>do_enhanced</c> and <c>do_best</c> - introduced <c>same_match</c> already overridden.
    /// DECISIONS 2026-09-12; <c>same_span_of_group</c> (<c>:11646</c>) is in PORTMAP's
    /// deliberately-not-ported table.
    /// </para>
    /// <para>
    /// <b>S41 measured it rather than reasoning about it, and the measurement is stronger than the
    /// argument was.</b> The check was ported behind a switch and three 2000-row <c>fuzzy</c> waves
    /// were replayed both ways. Honouring it saves 0.076, 0.075 and 0.063 <c>BasicMatch</c> runs per
    /// enhanced match - about 5% of 1.42 - which is well inside the "one extra run" the argument
    /// allowed. But it is <b>not</b> only an earlier exit: it changes the answer on 18, 15 and 14
    /// rows of 2000, and on every one of them the override's answer is upstream's, because the wave
    /// is green at all three seeds with the override in place. Some of the changed answers lose a
    /// perfect match outright - <c>(?e)(?:\d+b+?){2i+1d+1s&lt;=2}</c> over <c>1bb</c> is an exact
    /// match at (0, 2) with the override and a one-substitution match at (0, 3) without it. The
    /// figures are a lower bound on the divergence: the experiment guarded the check with
    /// "a previous best exists", which upstream's dead code does not, and that can only make it fire
    /// less often.
    /// </para>
    /// <para>
    /// NOT PORTED: <c>if (state->max_errors == PY_SSIZE_T_MAX) state->max_errors = 0;</c>
    /// (<c>:17985-17986</c>). It is unreachable. The only path that reaches it has just set
    /// <c>max_errors</c> from <c>total_errors</c> at <c>:17969</c>, which is an error count of a match
    /// that has been found and so is never <c>PY_SSIZE_T_MAX</c>.
    /// </para>
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="search">Whether to search rather than anchor at the start position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int DoEnhancedFuzzyMatch(MatchState state, bool search)
    {
        Span<long> bestFuzzyCounts = stackalloc long[FuzzyValue.Count];
        List<FuzzyChange> bestFuzzyChanges = [];

        // CHARACTERS, NOT CODE UNITS, for the reason 'DoExactMatch' spells out at length. Upstream
        // measures 'available' ONCE, against the slice the caller asked for, and never recomputes it
        // as the loop narrows the slice - so the early-out below, which goes live from the second run
        // whenever the first run found exactly one error, tests a stale width. That is upstream's
        // behaviour and it is safe in the direction that matters: a stale 'available' is never
        // smaller than the narrowed slice's, so the early-out can only fail to fire.
        long available = CountBetween(state, state.TextPos, state.Reverse ? state.SliceStart : state.SliceEnd);

        // The maximum permitted cost.
        state.MaxErrors = long.MaxValue;
        state.MaxCost = long.MaxValue;
        long fewestErrors = long.MaxValue;

        // Upstream has neither of these: its 'fewest_errors' does this job as well as its own, and
        // splitting the two is what this port changes. See the two tests inside the loop, and
        // 'IsBetterFuzzyMatch'.
        long lowestCost = long.MaxValue;
        long lowestCostErrors = long.MaxValue;

        GroupData[]? bestGroups = null;

        state.BestMatchPos = state.TextPos;
        state.BestTextPos = state.Reverse ? state.SliceStart : state.SliceEnd;

        int bestMatchPos = state.TextPos;
        int bestTextPos = 0;
        bool mustAdvance = state.MustAdvance;

        int sliceStart = state.SliceStart;
        int sliceEnd = state.SliceEnd;

        int status;

        while (true)
        {
            // If there's a better match, it won't start earlier in the string than the current best
            // match, so there's no need to start earlier than that match.
            state.MustAdvance = mustAdvance;

            // Initialise the state.
            state.InitMatch();

            status = MatchStatus.Success;
            if (
                state.MaxErrors == 0
                && state.PartialSide == MatchState.PartialNone
                && (available < state.MinWidth || (available == 0 && state.MustAdvance))
            )
            {
                // An exact match, and partial matches not permitted.
                status = MatchStatus.Failure;
            }

            if (status == MatchStatus.Success)
            {
                status = BasicMatch(state, search);
            }

            // Has an error occurred, or is it a partial match?
            if (status != MatchStatus.Success)
            {
                break;
            }

            // UPSTREAM'S TEST, UNCHANGED, AND IT IS THE LOOP'S TERMINATION RATHER THAN ITS RANKING.
            // 'better' at ':17930' does two jobs at once, and separating them is the whole of what
            // this port changes. It decides whether to keep the run - and it decides whether to go
            // round again, which is what 'else break' at ':17972' is.
            //
            // The second job is not optional and it is not a ranking question. 'max_errors' below
            // holds the next run to FEWER errors than this one, so a run that succeeds has always
            // improved on the error count and this test is all but always true - it is false only
            // where the count could not be tightened, at ':17970'. Replacing it with a cost
            // comparison therefore does not re-rank the chain, it CUTS the chain at the first run
            // that costs more, and the cheapest run can be further down.
            //
            // Measured, and it is why this is not one 'if'. S41's blind review found
            // 'fullmatch("(?e)(?:x|xyq){1i+9s+9d<=20}", "yzxyz")', where cutting the chain early
            // kept a run worse than upstream's answer on the cost this port claims to rank by AND
            // on the error count upstream ranks by. Merging the two tests again diverges on 54 rows
            // of the 2500 that 'tools/probes/enhancematch-cost-rows.py' writes at seed 777, against
            // 31 here; that probe's own header has the figures and how to re-run them.
            if (state.TotalErrors >= fewestErrors)
            {
                // The fit has stopped improving, so there is nothing further down the chain.
                break;
            }

            fewestErrors = state.TotalErrors;
            state.MaxErrors = fewestErrors;

            // AND THE RANKING, which is the other job, over the chain this port now walks to the
            // end. Upstream keeps the last run, because fewest errors is its definition of best;
            // this port keeps the cheapest, ties by fewer errors, then earliest. So this port's
            // answer is never dearer than upstream's and never uses fewer errors than it.
            if (IsBetterFuzzyMatch(state.TotalCost, state.TotalErrors, lowestCost, lowestCostErrors))
            {
                lowestCost = state.TotalCost;
                lowestCostErrors = state.TotalErrors;

                SaveFuzzyCounts(state, bestFuzzyCounts);
                SaveFuzzyChanges(state, bestFuzzyChanges);

                // Save the best result so far.
                bestGroups = SaveCaptures(state);

                bestMatchPos = state.MatchPos;
                bestTextPos = state.TextPos;
            }

            if (state.TotalErrors == 0)
            {
                break;
            }

            state.MaxErrors = state.TotalErrors;
            if (state.MaxErrors < FuzzyValue.MaxErrorsLimit)
            {
                --state.MaxErrors;
            }

            if (state.Reverse)
            {
                state.SliceStart = state.TextPos;
                state.SliceEnd = state.MatchPos;
            }
            else
            {
                state.SliceStart = state.MatchPos;
                state.SliceEnd = state.TextPos;
            }

            state.TextPos = state.MatchPos;
        }

        if (status is < 0 and not MatchStatus.Partial)
        {
            return status;
        }

        // The slice goes back to what the caller asked for, so that a following scan step is taken
        // against the subject rather than against the span this match narrowed to.
        state.SliceStart = sliceStart;
        state.SliceEnd = sliceEnd;

        if (bestGroups is not null)
        {
            // Upstream's true branch here is 'discard_groups' (:17500) alone: the last run WAS the
            // best one and is already in the state, so there is nothing to put back and only the
            // snapshot to free. Freeing it is going out of scope, so only the false branch is code.
            if (status != MatchStatus.Success || state.TotalErrors != 0)
            {
                // Restore the previous best match.
                status = MatchStatus.Success;

                state.MatchPos = bestMatchPos;
                state.TextPos = bestTextPos;

                RestoreGroups(state, bestGroups);
                RestoreFuzzyCounts(state, bestFuzzyCounts);
            }

            RestoreFuzzyChanges(state, bestFuzzyChanges);
        }

        return status;
    }

    /// <summary>
    /// Upstream <c>add_best_fuzzy_changes</c> (<c>upstream/src/_regex.c</c> line 9859): remembers the
    /// errors one equal-best candidate used, beside the candidate itself in the best list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two lists are added to together and cleared together, so entry <c>i</c> of one always
    /// describes entry <c>i</c> of the other; upstream relies on that when it copies
    /// <c>lists[0]</c> back over the widened-slice re-run's changes.
    /// </para>
    /// <para>
    /// NOT PORTED, because <see cref="List{T}"/> already is them: <c>init_best_changes_list</c>
    /// (<c>:9822</c>), <c>fini_best_changes_list</c> (<c>:9848</c>) and
    /// <c>clear_best_fuzzy_changes</c> (<c>:9831</c>), which are upstream's capacity bookkeeping and
    /// its nested <c>safe_dealloc</c> over a list of lists. A collection expression is the first,
    /// going out of scope is the second and <see cref="List{T}.Clear"/> is the third. The same
    /// reasoning already applies to <c>init_fuzzy_changes_list</c> and its <c>fini</c> - see PORTMAP
    /// row 417 - so this is the convention here rather than a new call.
    /// </para>
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="bestChangesList">The list of lists to add to.</param>
    private static void AddBestFuzzyChanges(MatchState state, List<List<FuzzyChange>> bestChangesList) =>
        bestChangesList.Add([.. state.FuzzyChanges]);

    /// <summary>
    /// Upstream <c>do_best_fuzzy_match</c> (<c>upstream/src/_regex.c</c> line 17584): search the whole
    /// slice for the match with the fewest errors rather than taking the first that fits, then
    /// re-examine the equal-best candidates for the earliest, lowest-cost fit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two passes. The first walks <c>start_pos</c> across the slice with a budget held one below the
    /// best seen so far, collecting every equal-best <c>(match_pos, text_pos)</c> pair into the best
    /// list and its changes into the best changes list, and stopping at a perfect match. The second,
    /// when the best is not perfect, revisits each entry at up to <c>min(fewest_errors,
    /// RE_MAX_ERRORS)</c> offsets from its <c>match_pos</c> with <see cref="MatchState.MaxErrors"/>
    /// climbing from 1, keeping the earliest lowest-error result; if nothing improves on the
    /// candidates, it re-runs entry 0 inside a slice widened by <c>fewest_errors</c> at each end and
    /// copies the recorded changes back over it.
    /// </para>
    /// <para>
    /// <b>THE BUDGET IS COST WHERE UPSTREAM'S IS ERROR COUNT, AND THAT IS THIS PORT'S ONE DELIBERATE
    /// DIVERGENCE HERE</b> (owner decision, DECISIONS 2026-09-12; upstream's open issue 470).
    /// Upstream's first pass is <c>state-&gt;max_errors = fewest_errors - 1</c> (<c>:17675</c>); this
    /// port's is <see cref="MatchState.MaxCost"/> <c>= lowest_cost - 1</c>, which is what releases up
    /// to 2015.09.28 had as <c>state-&gt;max_cost = state-&gt;total_cost - 1</c> (<c>_regex.c:16352</c>
    /// in that release) before the 2015.11.5 issue-165 hang fix replaced cost with error count
    /// throughout. With unit costs the two budgets are the same budget, so no ported test changes.
    /// </para>
    /// <para>
    /// <b>The bound has to be in the first pass, and the second pass then needs no ranking change at
    /// all.</b> Issue 470's own example is why the first half is true:
    /// <c>(?b)(voices){1i+1d+2s&lt;=2}</c> over <c>voixes voicees</c> must answer <c>voicees</c> - one
    /// insertion costing 1 against one substitution costing 2 - and with an error-count budget the
    /// second pass never sees it, because both candidates are one error and the search stops at
    /// <c>voixes</c>. The second half is the cheaper half of the change: the first pass now hands the
    /// second the CHEAPEST candidate, <see cref="MatchState.MaxCost"/> stays at that cost for the
    /// whole second pass so no refinement can be dearer, and inside that cap upstream's own
    /// fewest-errors-then-earliest rule IS the owner's rule of ties by fewer errors, then earliest. So the
    /// second pass is upstream's, line for line, with one assignment in front of it.
    /// </para>
    /// <para>
    /// <b>What was tried and rejected first, because the shape of it is not obvious.</b> S42's first
    /// sitting layered the cost rule into the second pass's equal-count tie-break and left the first
    /// pass alone. Measured 2026-09-13: the issue 470 example was unchanged - the decision still not
    /// honoured - and the <c>fuzzy</c> wave went from 0 divergences to 3, 2 and 3 of 2000 rows, every
    /// one a cheaper match at a different span. The whole change or none of it.
    /// </para>
    /// <para>
    /// <b>Three places here count CHARACTERS where the position is a UTF-16 index</b>, because
    /// upstream indexes the subject by codepoint and mixes these counts with error counts freely: the
    /// second pass's offset from a candidate (<c>:17715</c>), its <c>start_pos += step</c>
    /// (<c>:17776</c>), and the fallback's widening of the slice (<c>:17812-17821</c>). All three go
    /// through <see cref="CountBetween"/> or <see cref="StepBy"/>. The middle one is not theoretical -
    /// see the comment on it for the oracle row that split a surrogate pair in a <c>subf</c> result.
    /// </para>
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="search">Whether to search rather than anchor at the start position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int DoBestFuzzyMatch(MatchState state, bool search)
    {
        // CHARACTERS, NOT CODE UNITS, for the reason 'DoExactMatch' spells out at length.
        long available = CountBetween(state, state.TextPos, state.Reverse ? state.SliceStart : state.SliceEnd);
        int step = state.Reverse ? -1 : 1;

        // EVERY CANDIDATE IS TRIED AGAINST THE SLICE THE CALLER ASKED FOR. Added by S48, and it is a
        // deliberate departure from upstream, which restores the slice nowhere - so read this before
        // "restoring" the fidelity. It is LEDGER ENTRY 5's sixth door and its own proposed fix, one
        // level in from the door 'DoMatch' closed: there the stale slice crossed from one match to
        // the next, here it crosses from one CANDIDATE of a single '(?b)' match to the next.
        //
        // '(*SKIP)' assigns 'slice_start' mid-attempt (':14555', or 'slice_end' under '(?r)',
        // ':14553') and nothing puts it back. Both this function's attempt loops then read the moved
        // bound: the walk's guard holds 'start_pos' between the two (':17625') and 'start_pos' is the
        // match the candidate just found, so a verb that consumed anything leaves 'slice_start' ABOVE
        // it and the walk stops on its first successful candidate; the second pass reads it again in
        // its 'max_offset' (':17721') and in every anchored re-run.
        //
        // Measured 2026-09-14 on regex 2026.9.10, 'python
        // tools/probes/upstream-bestmatch-walk-truncated-by-a-skip.py', minimised to four characters:
        //
        //   (?b)(?:a(*SKIP)b){e<=1}   'axab'   search     (0, 2) one substitution
        //   ... the same compiled pattern ....  match(2)   (2, 4) NO errors
        //   (*PRUNE) in its place ............  search     (2, 4) NO errors
        //   the verb deleted .................  search     (2, 4) NO errors
        //
        // WHAT JUDGES IT IS THE VERB'S OWN DEFINITION. '(*SKIP)' sets a skip point and a later
        // attempt must not start BELOW it (PCRE2 pcre2pattern, "Verbs that act after backtracking");
        // the skip point here is 1 and the candidate the walk never reaches starts at 2, which the
        // verb permits outright. '(?b)' then promises the fewest errors among the matches that exist,
        // and this port's own anchored door finds a zero-error one - so it needs no second engine,
        // which is as well: PCRE2 has no fuzzy matching (tools/probes/pcre2-has-no-fuzzy-matching.py).
        // '(*PRUNE)' prunes backtracking identically and moves no bound, which is what makes the moved
        // bound the cause rather than the pattern's meaning.
        //
        // THE RESTORE IS ONCE PER CANDIDATE, NOT INSIDE THE ATTEMPT, and it restores the SLICE ONLY,
        // so a verb still cuts the backtracking of the attempt it fired in and still moves the slice
        // for the rest of that attempt - pinned by 'Gaps.Engine.FuzzyBestMatchTests.
        // Bestmatch_still_lets_a_skip_prune_a_candidates_own_alternatives', on a row where the
        // pruning decides the answer and the moved bound does not.
        // And it is NOT hoisted into 'InitMatch', where entry 5's note puts upstream's version of the
        // fix: 'DoEnhancedFuzzyMatch' (':17871') and this function's widened-slice fallback
        // (':17807') both narrow the slice DELIBERATELY and then call 'init_match', and a reset there
        // would throw their narrowing away.
        int callerSliceStart = state.SliceStart;
        int callerSliceEnd = state.SliceEnd;

        // WHICH BUDGET BOUNDS THE PASSES. Two conditions, one for soundness and one for waste.
        //
        // ONE FUZZY SECTION, because a cost is only a number the bound and the answer agree on while
        // ONE equation prices every error in the match, and upstream's own 'fuzzy_count' is what says
        // so. With a second FUZZY section an error made inside the inner one is priced at the inner
        // rates while it is being made - which is what the constraint predicates check the bound
        // against - and at the outer rates once 'END_FUZZY' merges the counts, which is what
        // 'state->total_cost' then reports. The two numbers differ, so the budget never bites, and
        // the walk does not merely mis-rank: 'start_pos' never advances, so IT HANGS. Measured
        // 2026-09-13 - '(?b)(?:(?:b\W){e:0}){1i+2d+1s<=4}' over 'B-\U0001D518' re-found the same
        // cost-4, two-error match under a cost-3 budget until it was killed.
        //
        // A WEIGHTED EQUATION, because otherwise the cost walk cannot change the answer and is pure
        // cost. Where the three error kinds are priced the same, a match's cost is a fixed multiple
        // of its error count and the two rankings agree row for row. Measured on the committed
        // seed-7 fuzzy wave, 665 `(?b)` rows: taking the walk on every row is 1964 runs of
        // 'BasicMatch' against upstream's 1600, +22.8%, for 462 matches either way; taking it only on
        // weighted rows leaves that wave at upstream's 1600 exactly, because no row in it is
        // weighted - `record-oracle.py` no longer pairs the two.
        //
        // Both conditions fall back to upstream's error-count budget, which is what those patterns
        // would have had anyway, so neither can lose a match this port would otherwise find.
        bool rankByCost = state.Pattern.FuzzyCount == 1 && state.Pattern.HasWeightedFuzzyCosts;

        long fewestErrors = long.MaxValue;
        long lowestCost = long.MaxValue;

        state.BestTextPos = state.Reverse ? state.SliceStart : state.SliceEnd;

        bool mustAdvance = state.MustAdvance;
        bool foundMatch = false;

        List<BestEntry> bestList = [];
        List<List<FuzzyChange>> bestChangesList = [];

        int status = MatchStatus.Failure;
        int firstStartPos = state.TextPos;
        int startPos;

        // UPSTREAM HAS ONE WALK OVER THE SLICE AND THIS PORT HAS TWO, and the reason is arithmetic
        // rather than taste. The owner's rule is lexicographic - cheapest, then fewest errors, then
        // earliest - and a single scalar budget cannot express a lexicographic order: a budget that
        // holds the next run to a strictly lower COST cannot also let an equal-cost run through to be
        // judged on its error count, and one that holds it to strictly fewer ERRORS prunes the
        // cheaper-but-equal-count match that is the whole of issue 470. So:
        //
        //   walk 0 - this port's - answers "what is the cheapest match in this slice?" and nothing
        //            else. It keeps no candidates.
        //   walk 1 - UPSTREAM'S WALK (':17612-17676'), unchanged, run with 'max_cost' pinned at that
        //            answer, so every run it sees costs exactly the minimum and its own
        //            fewest-errors rule is the owner's tie-break. It is the one that fills the best
        //            list.
        //
        // Both terminate for upstream's reason: 'start_pos' does not advance, so each holds the next
        // run to strictly better than the last on the quantity it bounds. For walk 0 that rests on
        // the whole-match cost test at 'END_FUZZY' - see the comment there - and NOT on the three
        // constraint predicates, which bound one section at a time and let a group call, a recursion
        // or a repeat spend the budget twice over. Walk 0 is skipped when the cost budget is unsound
        // (see 'rankByCost'), which leaves upstream's function exactly.
        for (int walk = rankByCost ? 0 : 1; walk <= 1; ++walk)
        {
            bool byCost = walk == 0;

            state.MaxErrors = long.MaxValue;
            state.MaxCost = byCost ? long.MaxValue : lowestCost;
            fewestErrors = long.MaxValue;

            bestList.Clear();
            bestChangesList.Clear();

            // Search the text for the best match.
            startPos = firstStartPos;
            while (callerSliceStart <= startPos && startPos <= callerSliceEnd)
            {
                // The candidate is tried against the caller's slice - see the note at the top.
                state.SliceStart = callerSliceStart;
                state.SliceEnd = callerSliceEnd;

                state.TextPos = startPos;
                state.MustAdvance = mustAdvance;

                // Initialise the state.
                state.InitMatch();

                status = MatchStatus.Success;
                if (
                    // Upstream tests 'max_errors == 0' because that is what it bounds the pass by;
                    // release 2015.09.28 spelled the same guard over its cost budget as
                    // 'state->max_cost == 0' (':16270'). It reads '<= 0' rather than '== 0' because a
                    // cost equation may price an error kind at zero, and then the budget below goes
                    // to -1 where upstream's 'size_t' could only wrap.
                    (byCost ? state.MaxCost <= 0 : state.MaxErrors == 0)
                    && state.PartialSide == MatchState.PartialNone
                    && (available < state.MinWidth || (available == 0 && state.MustAdvance))
                )
                {
                    // An exact match, and partial matches not permitted.
                    status = MatchStatus.Failure;
                }

                if (status == MatchStatus.Success)
                {
                    status = BasicMatch(state, search);
                }

                // Has an error occurred, or is it a partial match? Upstream's 'goto error' is a
                // return here: its label frees the two lists and returns the status, and the lists
                // are managed.
                if (status < 0)
                {
                    return status;
                }

                if (status == MatchStatus.Failure)
                {
                    break;
                }

                // It was a successful match.
                foundMatch = true;

                // WHAT THIS RUN COST AND HOW MANY ERRORS IT SPENT, taken from the LIVE counts rather
                // than from 'state.TotalCost' and 'state.TotalErrors' whenever this port is ranking.
                //
                // Those two fields are snapshots written at 'END_FUZZY', and a match can succeed on a
                // path whose last 'END_FUZZY' belongs to a branch that was backtracked out of - so
                // they can be STALE, while 'state.FuzzyCounts' is the live count 'FuzzyRegex' hands
                // the caller as 'Match.FuzzyCounts'. Ranking on a stale number does not merely
                // mis-rank: a run that really is cheaper is scored as equal, 'start_pos' never
                // advances, and walk 0 HANGS. Found by S42's blind review of the fix for the previous
                // hang, 2026-09-13: '(?b)((?:abc){e<=2,2i+1d+3s<=4}(?1)?)' over 'bb' reported cost 4
                // for a match whose live counts are one substitution and one deletion, costing 2.
                //
                // Walk 1 is upstream's walk and keeps upstream's fields, stale exactly where
                // upstream's are, because its job is to answer what upstream answers.
                long runCost = state.TotalCost;
                long runErrors = state.TotalErrors;

                if (rankByCost)
                {
                    // 'rankByCost' guarantees exactly one FUZZY section, so its node is the one that
                    // prices every error in the match and 'SingleFuzzyNode' is it.
                    runCost = TotalCost(state.FuzzyCounts, state.Pattern.SingleFuzzyNode!);
                    runErrors = TotalErrors(state.FuzzyCounts);
                }

                // The 'runErrors == 0' clause is this port's, and it is what keeps walk 0 finite.
                // A cost equation may price an error kind at zero, and then 'lowestCost' reaches 0
                // with errors still in the match; the budget goes to -1, which forbids every error,
                // and the run after it is a PERFECT match whose cost is not strictly lower than 0.
                // Without the clause that match falls into the branch below, 'start_pos' does not
                // advance, the budget does not change, and the walk re-finds it for ever. Upstream
                // cannot reach the case: its budget is 'fewest_errors - 1' and it breaks at 0 errors,
                // so it is never handed an equal-budget success. Held by
                // 'Gaps.Engine.FuzzyBestMatchTests.Bestmatch_terminates_when_an_error_kind_costs_nothing'.
                if ((byCost ? runCost < lowestCost : runErrors < fewestErrors) || runErrors == 0)
                {
                    // This match was better than any of the previous ones.
                    lowestCost = byCost ? runCost : Math.Min(lowestCost, runCost);
                    fewestErrors = runErrors;

                    if (runErrors == 0)
                    {
                        // It was a perfect match. Upstream's test is 'fewest_errors == 0' and it
                        // stays that: a perfect match is one with no errors, which costs nothing,
                        // whereas a cost of zero can still have been bought with errors a zero-price
                        // equation gave away, and the second pass must still run on those.
                        break;
                    }

                    // Forget all the previous worse matches and remember this one.
                    bestList.Clear();
                    bestList.Add(new BestEntry(state.MatchPos, state.TextPos));

                    bestChangesList.Clear();
                    AddBestFuzzyChanges(state, bestChangesList);
                }
                else if (byCost ? runCost == lowestCost : runErrors == fewestErrors)
                {
                    // This match was as good as the previous matches. Remember this one.
                    //
                    // UNREACHABLE under either budget, and it is upstream's line (':17664') left
                    // where upstream has it. The budget below holds the next run to STRICTLY better
                    // than this one, so a run that succeeds has always improved and the first branch
                    // always takes it; the best list therefore holds exactly one entry, upstream as
                    // well as here.
                    //
                    // It was NOT unreachable while the cost budget bounded one section rather than
                    // the whole match: that is the branch the group-call hang spun in, appending a
                    // run of unchanged cost for ever. The whole-match test at 'END_FUZZY' is what
                    // makes the sentence above true again, which is worth knowing before trusting
                    // any "unreachable" claim in this function.
                    bestList.Add(new BestEntry(state.MatchPos, state.TextPos));
                    AddBestFuzzyChanges(state, bestChangesList);
                }

                startPos = state.MatchPos;

                // The budget, and each walk's only guarantee of progress: 'start_pos' does not
                // advance, so a run that is not held to strictly better than the last re-finds it for
                // ever. Upstream: 'state->max_errors = fewest_errors - 1' (':17675'). Walk 0: release
                // 2015.09.28's 'state->max_cost = state->total_cost - 1' (':16352').
                if (byCost)
                {
                    state.MaxCost = lowestCost - 1;
                }
                else
                {
                    state.MaxErrors = fewestErrors - 1;
                }
            }

            if (!foundMatch || fewestErrors == 0)
            {
                // Nothing matched at all, or walk 0 found a perfect match - and a perfect match is
                // already the cheapest with the fewest errors, so there is nothing for walk 1 to
                // settle.
                break;
            }
        }

        if (!foundMatch)
        {
            return status;
        }

        // We found a match.
        if (fewestErrors == 0)
        {
            state.FuzzyChanges.Clear();
            return status;
        }

        // It doesn't look like a perfect match. Upstream saves the LIVE slice here (':17700'); this
        // port saved it at the top instead, because by this line the walk's last attempt may have
        // moved it - see the note there.
        int sliceStart = callerSliceStart;
        int sliceEnd = callerSliceEnd;

        state.SliceStart = callerSliceStart;
        state.SliceEnd = callerSliceEnd;

        long errorLimit = Math.Min(fewestErrors, FuzzyValue.MaxErrorsLimit);

        // THE WHOLE OF THE COST RULE IN THE SECOND PASS, and the reason the rest of it is upstream's
        // line for line. The first pass has just established that nothing in the slice matches for
        // less than 'lowestCost', so holding every run of this pass to that cost means a refinement
        // can never be dearer than the candidate it replaces - and inside the cap upstream's own
        // "fewest errors, then earliest" is exactly the owner's rule of ties by fewer errors, then earliest.
        // The climb below stays on 'max_errors', where 'RE_MAX_ERRORS' bounds it at ten; climbing a
        // cost instead would be unbounded, because a cost equation may price an error in the
        // thousands.
        if (rankByCost)
        {
            state.MaxCost = lowestCost;
        }

        Span<long> bestFuzzyCounts = stackalloc long[FuzzyValue.Count];
        List<FuzzyChange> bestFuzzyChanges = [];
        GroupData[]? bestGroups = null;
        int bestMatchPos = 0;
        int bestTextPos = 0;

        // Look again at the best of the matches that we've seen.
        for (int i = 0; i < bestList.Count; i++)
        {
            // Look for the best fit at this position.
            BestEntry entry = bestList[i];

            // 'maxOffset' below measures against the slice, and a previous entry's attempt may have
            // moved it - see the note at the top.
            state.SliceStart = callerSliceStart;
            state.SliceEnd = callerSliceEnd;

            long maxOffset;
            if (search)
            {
                // CHARACTERS, NOT CODE UNITS: upstream's subtraction is over codepoint indexes, and
                // this offset is compared against two error COUNTS on the next two lines.
                maxOffset = CountBetween(state, entry.MatchPos, state.Reverse ? state.SliceStart : state.SliceEnd);

                if (maxOffset > fewestErrors)
                {
                    maxOffset = fewestErrors;
                }

                if (maxOffset > errorLimit)
                {
                    maxOffset = errorLimit;
                }
            }
            else
            {
                maxOffset = 0;
            }

            startPos = entry.MatchPos;
            long offset = 0;

            while (offset <= maxOffset)
            {
                state.MaxErrors = 1;

                while (state.MaxErrors <= errorLimit)
                {
                    // The candidate is tried against the caller's slice - see the note at the top.
                    state.SliceStart = callerSliceStart;
                    state.SliceEnd = callerSliceEnd;

                    state.TextPos = startPos;
                    state.InitMatch();
                    status = BasicMatch(state, false);

                    if (status < 0)
                    {
                        return status;
                    }

                    if (status == MatchStatus.Success)
                    {
                        bool better;

                        if (state.TotalErrors < errorLimit || (i == 0 && offset == 0))
                        {
                            better = true;
                        }
                        else if (state.TotalErrors == errorLimit)
                        {
                            // The cost is as low as the current best, but is it earlier?
                            better = state.Reverse ? state.MatchPos > bestMatchPos : state.MatchPos < bestMatchPos;
                        }
                        else
                        {
                            better = false;
                        }

                        if (better)
                        {
                            SaveFuzzyCounts(state, bestFuzzyCounts);
                            SaveFuzzyChanges(state, bestFuzzyChanges);

                            bestGroups = SaveCaptures(state);

                            bestMatchPos = state.MatchPos;
                            bestTextPos = state.TextPos;
                            errorLimit = state.TotalErrors;
                        }

                        break;
                    }

                    ++state.MaxErrors;
                }

                // ONE CHARACTER, NOT ONE CODE UNIT, and this is the line the oracle caught. Upstream's
                // 'start_pos += step' moves one codepoint because its indexes are codepoints; +-1 here
                // lands INSIDE a surrogate pair, and the match that starts there splits the character.
                // Found 2026-09-13, seed 7 row 1773 of the default 2000-row 'fuzzy' wave:
                // 'subf' of '(?b)(?fi)(?:(?:b\W){e:0}){1i+2d+1s<=4}' over 'B-\U0001D518' with the
                // template '<>' gave '<>\uD835<>' here against upstream's '<><>\U0001D518' - a lone
                // high surrogate in the output. Pinned by Gaps.Engine.FuzzyBestMatchTests.
                // A_bestmatch_second_pass_steps_whole_characters_not_code_units, at the row's full
                // pattern - every smaller shape stops reaching the stepped position.
                startPos = StepBy(state, startPos, 1, step);
                ++offset;
            }

            if (status == MatchStatus.Success && state.TotalErrors == 0)
            {
                break;
            }
        }

        if (bestGroups is not null)
        {
            status = MatchStatus.Success;
            state.MatchPos = bestMatchPos;
            state.TextPos = bestTextPos;

            RestoreGroups(state, bestGroups);
            RestoreFuzzyCounts(state, bestFuzzyCounts);
            RestoreFuzzyChanges(state, bestFuzzyChanges);
        }
        else
        {
            // None of the "best" matches could be improved on, so pick the first. Look at only the
            // part of the string around the match.
            BestEntry entry = bestList[0];

            // We'll expand the part that we're looking at to compensate for any matching errors that
            // have occurred - CHARACTERS, NOT CODE UNITS, because 'fewest_errors' is a count of
            // errors and these are UTF-16 indexes.
            //
            // Upstream's two branches are "step back 'fewest_errors', or stop at the caller's slice
            // if there is not that much room" (':17812-17821'), and that is exactly what 'StepBy'
            // does against 'state.SliceStart'/'state.SliceEnd' - so both are computed HERE, while the
            // slice still holds what the caller asked for, and assigned afterwards.
            int widenedStart = StepBy(state, state.Reverse ? entry.TextPos : entry.MatchPos, fewestErrors, -1);
            int widenedEnd = StepBy(state, state.Reverse ? entry.MatchPos : entry.TextPos, fewestErrors, 1);

            state.SliceStart = widenedStart;
            state.SliceEnd = widenedEnd;

            // We've narrowed the slice. The required string position might now be outside it.
            //
            // Issue 612, commit 8244055, released 2026.8.30 and ported by S44 when it was INERT -
            // `ReqPos` was then only ever assigned -1, at state creation. **S60 made it live**:
            // `LocateRequiredString` now writes the position it found (`:4990`), so this reset is
            // doing the work it was ported for, on the one arm S60 landed. Upstream's own symptom
            // is a `count_one()` size underflow reading off the heap. It stays inert only for the
            // reverse and folded arms, which are still the Phase 7 deferral this file's header
            // records; deleting it as dead code was always wrong and is now visibly so.
            state.ReqPos = -1;

            state.MaxErrors = fewestErrors;
            state.TextPos = entry.MatchPos;
            state.InitMatch();
            status = BasicMatch(state, search);

            if (status < 0)
            {
                state.SliceStart = sliceStart;
                state.SliceEnd = sliceEnd;
                return status;
            }

            RestoreFuzzyChanges(state, bestChangesList[0]);
        }

        state.SliceStart = sliceStart;
        state.SliceEnd = sliceEnd;

        return status;
    }

    /// <summary>
    /// Upstream <c>do_simple_fuzzy_match</c> (<c>upstream/src/_regex.c</c> line 18027): plain fuzzy
    /// matching, which takes the first match it finds rather than the best one.
    /// </summary>
    /// <remarks>
    /// NOT PORTED: upstream's <c>available</c> and the <c>max_errors == 0 &amp;&amp; partial_side ==
    /// RE_PARTIAL_NONE</c> early-out below it (<c>:18028</c>, <c>:18048-18053</c>). The block is
    /// copied from <c>do_exact_match</c>, where <c>max_errors</c> is <c>0</c> and it is live; here
    /// the line above sets <c>max_errors</c> to <c>PY_SSIZE_T_MAX</c>, so the condition is false by
    /// construction and <c>available</c> is computed and never read.
    /// </remarks>
    /// <param name="state">The match state.</param>
    /// <param name="search">Whether to search rather than anchor at the start position.</param>
    /// <returns>A <see cref="MatchStatus"/>.</returns>
    private static int DoSimpleFuzzyMatch(MatchState state, bool search)
    {
        // The maximum permitted cost.
        state.MaxErrors = long.MaxValue;
        state.MaxCost = long.MaxValue;

        state.BestMatchPos = state.TextPos;
        state.BestTextPos = state.Reverse ? state.SliceStart : state.SliceEnd;

        // Initialise the state.
        state.InitMatch();

        return BasicMatch(state, search);
    }

    /// <summary>
    /// Upstream <c>do_match_2</c> (<c>upstream/src/_regex.c</c> line 18099): which of the four match
    /// strategies a pattern gets.
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
            return DoBestFuzzyMatch(state, search);
        }

        if ((pattern.Flags & RegexFlags.EnhanceMatch) != 0)
        {
            return DoEnhancedFuzzyMatch(state, search);
        }

        return DoSimpleFuzzyMatch(state, search);
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

        // THE SLICE GOES BACK TO WHAT THE CALLER ASKED FOR, ONCE PER MATCH. Added by S40a, and it is
        // a deliberate departure from upstream's code - upstream restores it nowhere - so read this
        // before "restoring" the fidelity.
        //
        // `(*SKIP)` moves `slice_start`/`slice_end` mid-attempt (`:14553`) and nothing puts them
        // back: `init_match` (`:3404`), `do_match` (`:18121`) and `scanner_search_or_match`
        // (`:20874`) all leave them alone, and one state serves a whole scan. So the slice a verb
        // moved in match *n* is still moved for match *n+1*, and the next match is answered against
        // a subject it was never asked about. That is LEDGER ENTRY 5, where it is upstream's bug
        // with this line as its proposed fix - "reset them in `init_match` alongside the guards".
        //
        // Why the port could not simply inherit it. With no `search_start` prefilter until Phase 7,
        // this port reaches the carried slice on shapes upstream's optimiser skips past, and there
        // it LOSES MATCHES rather than merely moving them. Measured on the S40a seed-7 wave, row
        // 117679 minimised to four characters:
        //
        //   finditer(r'\b(?:[^a](*SKIP))*', 'b\n\rS', overlapped=True)
        //     upstream     (0,4) (1,3) (3,1) (4,0)   - and its own fresh-state walk agrees
        //     before this  (0,4) (1,3)       (4,0)   - and this port's own Match() at 3 gives (3,1)
        //
        // The attempt at 2 failed with `slice_start` left at 4 by the previous match's verb, and the
        // failure arm below (`:15734`, and its port at the GreedyRepeatOne/search retry) then jumped
        // the next start position UP to `slice_start`, skipping 3 entirely. Hide the start test from
        // upstream's prefilter - `(?=\b)(?:[^a](*SKIP))*` - and upstream loses the same match, which
        // is what proves the mechanism is shared and the prefilter is what masks it.
        //
        // The measurements that say this is safe, all on the commit that introduced it: the whole
        // ported suite green (5825 tests), the 126,000-row seed-7 wave down from four divergences to
        // three, and all 37 `ExpectedDivergences` rows still firing - including every
        // `overlapped-skip-stale-slice` row, which is the family that would have gone quiet had this
        // changed what a carried slice does WITHIN one match. It does not: the reset is once per
        // match, so a verb still moves the slice for the rest of its own attempt, which is what
        // `BacktrackingVerbTests.Skip_moves_the_slice_start_and_a_later_match_in_the_same_scan_sees_it`
        // pins.
        state.SliceStart = state.InitialSliceStart;
        state.SliceEnd = state.InitialSliceEnd;

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
            int sliceStart = state.SliceStart;
            int sliceEnd = state.SliceEnd;

            // Try a normal match first.
            state.PartialSide = MatchState.PartialNone;

            status = DoMatch2(state, search);

            state.PartialSide = partialSide;

            if (status == MatchStatus.Failure)
            {
                // Fall back to the partial match as originally requested.
                //
                // THE SLICE GOES BACK WITH `text_pos`. Upstream restores `text_pos` alone (`:18160`)
                // and this port restored `text_pos` alone until S40b, so read this before
                // "restoring" the fidelity - the departure is deliberate and it is measured.
                //
                // These two `DoMatch2` calls are two attempts at ONE match. A `(*SKIP)` in the
                // non-partial pass moves `slice_start` (`:14553`, or `slice_end` when the node is
                // `RE_STATUS_REVERSE`, `:14551`) and nothing put it back, so the partial pass ran
                // with a slice the caller never asked for and its search retry jumped every start
                // position outside it. The answer then stopped being leftmost.
                //
                // What settles it is SELF-REFUTATION, not upstream: a search that reports a position
                // its own anchored matcher beats is wrong whatever upstream says. Both directions,
                // measured 2026-09-13 on regex 2026.7.19 by
                // tools/probes/upstream-partial-retry-slice-restore.py:
                //
                //   \b\D(*SKIP)z            over ' A'   search (2, 0); own MatchAtStart(1) (1, 1)
                //   (?r)\b(?:[^a-f](*SKIP)[\p{L}\p{N}]|[[:digit:]])(?P<g1>[A-Z]{0,})
                //                           over 'a\n'  search (0, 0); own MatchAtStart(0, 1) (0, 1)
                //
                // Upstream agrees with the fixed answer on the forward row, where its `search_start`
                // prefilter reaches the shape. ON THE REVERSED ROW UPSTREAM KEEPS THE DEFECT: its
                // own `match(endpos=1, partial=True)` and its own verb-free search both answer
                // (0, 1) and only its search with the verb answers (0, 0). `(*PRUNE)`, which prunes
                // backtracking identically and moves NO bound, answers (0, 1) - which is what makes
                // the bound move the cause rather than the pattern's meaning. So restoring BOTH ends
                // is the evidenced choice, not the forward end only; S40a's note that restoring
                // `slice_end` "introduces" a reversed row had the sign backwards, and that reading
                // is superseded.
                //
                // RESTORING THE FORWARD END ONLY WAS MEASURED, AND ON THE GATE'S OWN SEEDS THE ORACLE
                // CANNOT TELL IT FROM THIS. Over `partial,verbs` at 6000 rows it gives 1+2+2
                // diverging rows at seeds 7, 4242 and 20260913 - exactly what this code gives. What
                // moves there is the classification, not the count, because upstream shares the
                // reversed defect and a port that shares it too simply AGREES. So do not re-derive
                // this choice from the gate's divergence count; on those seeds it cannot answer.
                //
                // Two things do answer it. First, this port's own self-consistency, which is what
                // `A_reversed_skip_does_not_move_the_slice_end_the_partial_pass_searches` and the
                // `partial-retry-reversed-slice` staleness alarm pin. Second, A SEED THE GATE DOES
                // NOT USE: at seed 31 the forward-only variant leaves 4 diverging rows where this
                // leaves 3, and the row it fails to fix is
                // `(?r)^(\p{Lu}{2}?){2,4}\1([^\p{L}])*(?:\d?(*SKIP)[\p{L}\p{N}]|[[:digit:]])` -
                // reversed, so only the `slice_end` half reaches it, and restoring that half makes
                // this port AGREE WITH UPSTREAM there. Measured 2026-09-13 on the committed code.
                //
                // Pinned by `PartialMatchingTests.A_skip_in_the_non_partial_pass_does_not_move_the_
                // slice_the_partial_pass_searches` and `.A_reversed_skip_does_not_move_the_slice_
                // end_the_partial_pass_searches`.
                state.TextPos = textPos;
                state.SliceStart = sliceStart;
                state.SliceEnd = sliceEnd;
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
