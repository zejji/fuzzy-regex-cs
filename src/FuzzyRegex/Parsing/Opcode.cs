namespace Fuzzy.Text.RegularExpressions.Parsing;

/// <summary>
/// The bytecode opcodes, port of the <c>OPCODES</c> table in
/// <c>upstream/regex/_regex_core.py</c> lines 212-296, followed by the engine-only operators from
/// <c>upstream/src/_regex.h</c> lines 100-117.
/// </summary>
/// <remarks>
/// <para>
/// Upstream builds a namespace by enumerating the names in that string
/// (<c>for i, op in enumerate(OPCODES.split()): setattr(OP, op, i)</c>, lines 300-302), so a
/// member's numeric value is its position in the table and the order is load-bearing: the C
/// engine's own <c>switch</c> is keyed on the same integers. Inserting a name anywhere but the
/// end renumbers everything after it.
/// </para>
/// <para>
/// The names are upstream's, in upstream's order, re-spelled in PascalCase. All 81 are here even
/// though the parser slices turn them on a few at a time, because the numbering only makes sense
/// as a whole.
/// </para>
/// <para>
/// <b>Values 81 to 97 never appear in a code list.</b> <c>_regex.h</c> continues the same numbering
/// past the parser's table with seventeen operators the C compiler invents while it builds the node
/// graph - <c>END_GROUP</c>, <c>END_FUZZY</c>, <c>GREEDY_REPEAT_ONE</c> and the rest. They are here,
/// in the same enum, because upstream keeps one numbering across both files and the engine's
/// <c>switch</c> is keyed on it; a second enum would only add a cast at every node it builds.
/// </para>
/// </remarks>
internal enum Opcode : uint
{
    /// <summary>Upstream <c>OP.FAILURE</c>.</summary>
    Failure = 0,

    /// <summary>Upstream <c>OP.SUCCESS</c>.</summary>
    Success = 1,

    /// <summary>Upstream <c>OP.ANY</c>.</summary>
    Any = 2,

    /// <summary>Upstream <c>OP.ANY_ALL</c>.</summary>
    AnyAll = 3,

    /// <summary>Upstream <c>OP.ANY_ALL_REV</c>.</summary>
    AnyAllRev = 4,

    /// <summary>Upstream <c>OP.ANY_REV</c>.</summary>
    AnyRev = 5,

    /// <summary>Upstream <c>OP.ANY_U</c>.</summary>
    AnyU = 6,

    /// <summary>Upstream <c>OP.ANY_U_REV</c>.</summary>
    AnyURev = 7,

    /// <summary>Upstream <c>OP.ATOMIC</c>.</summary>
    Atomic = 8,

    /// <summary>Upstream <c>OP.BOUNDARY</c>.</summary>
    Boundary = 9,

    /// <summary>Upstream <c>OP.BRANCH</c>.</summary>
    Branch = 10,

    /// <summary>Upstream <c>OP.CALL_REF</c>.</summary>
    CallRef = 11,

    /// <summary>Upstream <c>OP.CHARACTER</c>.</summary>
    Character = 12,

    /// <summary>Upstream <c>OP.CHARACTER_IGN</c>.</summary>
    CharacterIgn = 13,

    /// <summary>Upstream <c>OP.CHARACTER_IGN_REV</c>.</summary>
    CharacterIgnRev = 14,

    /// <summary>Upstream <c>OP.CHARACTER_REV</c>.</summary>
    CharacterRev = 15,

    /// <summary>Upstream <c>OP.CONDITIONAL</c>.</summary>
    Conditional = 16,

    /// <summary>Upstream <c>OP.DEFAULT_BOUNDARY</c>.</summary>
    DefaultBoundary = 17,

    /// <summary>Upstream <c>OP.DEFAULT_END_OF_WORD</c>.</summary>
    DefaultEndOfWord = 18,

    /// <summary>Upstream <c>OP.DEFAULT_START_OF_WORD</c>.</summary>
    DefaultStartOfWord = 19,

    /// <summary>Upstream <c>OP.END</c>.</summary>
    End = 20,

    /// <summary>Upstream <c>OP.END_OF_LINE</c>.</summary>
    EndOfLine = 21,

    /// <summary>Upstream <c>OP.END_OF_LINE_U</c>.</summary>
    EndOfLineU = 22,

    /// <summary>Upstream <c>OP.END_OF_STRING</c>.</summary>
    EndOfString = 23,

    /// <summary>Upstream <c>OP.END_OF_STRING_LINE</c>.</summary>
    EndOfStringLine = 24,

    /// <summary>Upstream <c>OP.END_OF_STRING_LINE_U</c>.</summary>
    EndOfStringLineU = 25,

    /// <summary>Upstream <c>OP.END_OF_WORD</c>.</summary>
    EndOfWord = 26,

    /// <summary>Upstream <c>OP.FUZZY</c>.</summary>
    Fuzzy = 27,

    /// <summary>Upstream <c>OP.GRAPHEME_BOUNDARY</c>.</summary>
    GraphemeBoundary = 28,

    /// <summary>Upstream <c>OP.GREEDY_REPEAT</c>.</summary>
    GreedyRepeat = 29,

    /// <summary>Upstream <c>OP.GROUP</c>.</summary>
    Group = 30,

    /// <summary>Upstream <c>OP.GROUP_CALL</c>.</summary>
    GroupCall = 31,

    /// <summary>Upstream <c>OP.GROUP_EXISTS</c>.</summary>
    GroupExists = 32,

    /// <summary>Upstream <c>OP.KEEP</c>.</summary>
    Keep = 33,

    /// <summary>Upstream <c>OP.LAZY_REPEAT</c>.</summary>
    LazyRepeat = 34,

    /// <summary>Upstream <c>OP.LOOKAROUND</c>.</summary>
    Lookaround = 35,

    /// <summary>Upstream <c>OP.NEXT</c>.</summary>
    Next = 36,

    /// <summary>Upstream <c>OP.PROPERTY</c>.</summary>
    Property = 37,

    /// <summary>Upstream <c>OP.PROPERTY_IGN</c>.</summary>
    PropertyIgn = 38,

    /// <summary>Upstream <c>OP.PROPERTY_IGN_REV</c>.</summary>
    PropertyIgnRev = 39,

    /// <summary>Upstream <c>OP.PROPERTY_REV</c>.</summary>
    PropertyRev = 40,

    /// <summary>Upstream <c>OP.PRUNE</c>.</summary>
    Prune = 41,

    /// <summary>Upstream <c>OP.RANGE</c>.</summary>
    Range = 42,

    /// <summary>Upstream <c>OP.RANGE_IGN</c>.</summary>
    RangeIgn = 43,

    /// <summary>Upstream <c>OP.RANGE_IGN_REV</c>.</summary>
    RangeIgnRev = 44,

    /// <summary>Upstream <c>OP.RANGE_REV</c>.</summary>
    RangeRev = 45,

    /// <summary>Upstream <c>OP.REF_GROUP</c>.</summary>
    RefGroup = 46,

    /// <summary>Upstream <c>OP.REF_GROUP_FLD</c>.</summary>
    RefGroupFld = 47,

    /// <summary>Upstream <c>OP.REF_GROUP_FLD_REV</c>.</summary>
    RefGroupFldRev = 48,

    /// <summary>Upstream <c>OP.REF_GROUP_IGN</c>.</summary>
    RefGroupIgn = 49,

    /// <summary>Upstream <c>OP.REF_GROUP_IGN_REV</c>.</summary>
    RefGroupIgnRev = 50,

    /// <summary>Upstream <c>OP.REF_GROUP_REV</c>.</summary>
    RefGroupRev = 51,

    /// <summary>Upstream <c>OP.SEARCH_ANCHOR</c>.</summary>
    SearchAnchor = 52,

    /// <summary>Upstream <c>OP.SET_DIFF</c>.</summary>
    SetDiff = 53,

    /// <summary>Upstream <c>OP.SET_DIFF_IGN</c>.</summary>
    SetDiffIgn = 54,

    /// <summary>Upstream <c>OP.SET_DIFF_IGN_REV</c>.</summary>
    SetDiffIgnRev = 55,

    /// <summary>Upstream <c>OP.SET_DIFF_REV</c>.</summary>
    SetDiffRev = 56,

    /// <summary>Upstream <c>OP.SET_INTER</c>.</summary>
    SetInter = 57,

    /// <summary>Upstream <c>OP.SET_INTER_IGN</c>.</summary>
    SetInterIgn = 58,

    /// <summary>Upstream <c>OP.SET_INTER_IGN_REV</c>.</summary>
    SetInterIgnRev = 59,

    /// <summary>Upstream <c>OP.SET_INTER_REV</c>.</summary>
    SetInterRev = 60,

    /// <summary>Upstream <c>OP.SET_SYM_DIFF</c>.</summary>
    SetSymDiff = 61,

    /// <summary>Upstream <c>OP.SET_SYM_DIFF_IGN</c>.</summary>
    SetSymDiffIgn = 62,

    /// <summary>Upstream <c>OP.SET_SYM_DIFF_IGN_REV</c>.</summary>
    SetSymDiffIgnRev = 63,

    /// <summary>Upstream <c>OP.SET_SYM_DIFF_REV</c>.</summary>
    SetSymDiffRev = 64,

    /// <summary>Upstream <c>OP.SET_UNION</c>.</summary>
    SetUnion = 65,

    /// <summary>Upstream <c>OP.SET_UNION_IGN</c>.</summary>
    SetUnionIgn = 66,

    /// <summary>Upstream <c>OP.SET_UNION_IGN_REV</c>.</summary>
    SetUnionIgnRev = 67,

    /// <summary>Upstream <c>OP.SET_UNION_REV</c>.</summary>
    SetUnionRev = 68,

    /// <summary>Upstream <c>OP.SKIP</c>.</summary>
    Skip = 69,

    /// <summary>Upstream <c>OP.START_OF_LINE</c>.</summary>
    StartOfLine = 70,

    /// <summary>Upstream <c>OP.START_OF_LINE_U</c>.</summary>
    StartOfLineU = 71,

    /// <summary>Upstream <c>OP.START_OF_STRING</c>.</summary>
    StartOfString = 72,

    /// <summary>Upstream <c>OP.START_OF_WORD</c>.</summary>
    StartOfWord = 73,

    /// <summary>Upstream <c>OP.STRING</c>.</summary>
    String = 74,

    /// <summary>Upstream <c>OP.STRING_FLD</c>.</summary>
    StringFld = 75,

    /// <summary>Upstream <c>OP.STRING_FLD_REV</c>.</summary>
    StringFldRev = 76,

    /// <summary>Upstream <c>OP.STRING_IGN</c>.</summary>
    StringIgn = 77,

    /// <summary>Upstream <c>OP.STRING_IGN_REV</c>.</summary>
    StringIgnRev = 78,

    /// <summary>Upstream <c>OP.STRING_REV</c>.</summary>
    StringRev = 79,

    /// <summary>Upstream <c>OP.FUZZY_EXT</c>.</summary>
    FuzzyExt = 80,

    /// <summary>Engine-only. Upstream <c>RE_OP_BODY_END</c>.</summary>
    BodyEnd = 81,

    /// <summary>Engine-only. Upstream <c>RE_OP_BODY_START</c>.</summary>
    BodyStart = 82,

    /// <summary>Engine-only. Upstream <c>RE_OP_END_ATOMIC</c>.</summary>
    EndAtomic = 83,

    /// <summary>Engine-only. Upstream <c>RE_OP_END_CONDITIONAL</c>.</summary>
    EndConditional = 84,

    /// <summary>Engine-only. Upstream <c>RE_OP_END_FUZZY</c>.</summary>
    EndFuzzy = 85,

    /// <summary>Engine-only. Upstream <c>RE_OP_END_GREEDY_REPEAT</c>.</summary>
    EndGreedyRepeat = 86,

    /// <summary>Engine-only. Upstream <c>RE_OP_END_GROUP</c>.</summary>
    EndGroup = 87,

    /// <summary>Engine-only. Upstream <c>RE_OP_END_LAZY_REPEAT</c>.</summary>
    EndLazyRepeat = 88,

    /// <summary>Engine-only. Upstream <c>RE_OP_END_LOOKAROUND</c>.</summary>
    EndLookaround = 89,

    /// <summary>Engine-only. Upstream <c>RE_OP_FUZZY_INSERT</c>.</summary>
    FuzzyInsert = 90,

    /// <summary>Engine-only. Upstream <c>RE_OP_GREEDY_REPEAT_ONE</c>.</summary>
    GreedyRepeatOne = 91,

    /// <summary>Engine-only. Upstream <c>RE_OP_GROUP_RETURN</c>.</summary>
    GroupReturn = 92,

    /// <summary>Engine-only. Upstream <c>RE_OP_LAZY_REPEAT_ONE</c>.</summary>
    LazyRepeatOne = 93,

    /// <summary>Engine-only. Upstream <c>RE_OP_MATCH_BODY</c>.</summary>
    MatchBody = 94,

    /// <summary>Engine-only. Upstream <c>RE_OP_MATCH_TAIL</c>.</summary>
    MatchTail = 95,

    /// <summary>Engine-only. Upstream <c>RE_OP_START_GROUP</c>.</summary>
    StartGroup = 96,

    /// <summary>Engine-only. Upstream <c>RE_OP_TAIL_START</c>.</summary>
    TailStart = 97,
}
