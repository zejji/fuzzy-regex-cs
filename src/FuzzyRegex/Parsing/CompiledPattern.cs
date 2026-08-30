namespace Fuzzy.Text.RegularExpressions.Parsing;

/// <summary>
/// Everything upstream's compiler produces for one pattern: the tuple <c>_main._compile</c> hands
/// to <c>_regex.compile</c> (<c>upstream/regex/_main.py</c> lines 660-663).
/// </summary>
/// <remarks>
/// <para>
/// This is the port's compile-time seam. It exists as a type of its own, rather than as state on
/// <see cref="FuzzyRegex"/>, because the compile-parity corpus compares against it directly: the
/// parser can be verified bit for bit against upstream long before any opcode can match a string
/// (slice S06).
/// </para>
/// <para>
/// <c>index_group</c>, upstream's fifth argument, is not here: it is
/// <see cref="GroupIndex"/> inverted and carries no information of its own.
/// </para>
/// </remarks>
/// <param name="Flags">
/// The resolved flags, <c>info.flags | version</c> - the input flags plus whatever the pattern's
/// own inline flags and the default version added. Not the caller's flags.
/// </param>
/// <param name="Code">
/// The flattened bytecode. Unsigned because upstream's <c>UNLIMITED</c> sentinel is
/// <c>0xFFFFFFFF</c>.
/// </param>
/// <param name="GroupIndex">Capture group name to group number.</param>
/// <param name="NamedLists">
/// The named lists by name, deduplicated. Upstream keeps these so a <c>\L&lt;name&gt;</c> pattern
/// can be recompiled from a pickle. Sets, because upstream stores each as a <c>frozenset</c>
/// (<c>upstream/regex/_main.py</c> line 613) - as does <c>FuzzyRegex.NamedLists</c>, decided in
/// S04.
/// </param>
/// <param name="NamedListIndexes">
/// One entry per distinct (name, case flags) pair used in the pattern, indexed by the number the
/// parser assigned it, holding the values after any case folding. The list position is the index
/// and is baked into the bytecode; each entry is a <c>frozenset</c> upstream, so its own member
/// order means nothing.
/// </param>
/// <param name="ReqOffset">
/// Where the required string starts, or -1 when the offset is unbounded, or 0 when there is none.
/// A <c>long</c> because the offset is a repeat's maximum width and a repeat count runs to
/// <c>UNLIMITED - 1</c> = 4294967294, which does not fit in an <see cref="int"/>: upstream hands
/// the C compiler a Python <c>int</c> that it reads as a <c>Py_ssize_t</c>
/// (<c>upstream/src/_regex.c</c>). Pinned by <c>Gaps/Parsing/RepeatWidthOverflowTests.cs</c>.
/// </param>
/// <param name="ReqChars">The required string, as codepoints, after case folding.</param>
/// <param name="ReqFlags">The case flags the required string must be compared under.</param>
/// <param name="GroupCount">The number of capture groups.</param>
internal sealed record CompiledPattern(
    int Flags,
    IReadOnlyList<uint> Code,
    IReadOnlyDictionary<string, int> GroupIndex,
    IReadOnlyDictionary<string, IReadOnlySet<string>> NamedLists,
    IReadOnlyList<IReadOnlySet<string>> NamedListIndexes,
    long ReqOffset,
    IReadOnlyList<int> ReqChars,
    int ReqFlags,
    int GroupCount
);
