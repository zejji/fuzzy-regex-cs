namespace Fuzzy.Text.RegularExpressions.Parsing;

/// <summary>
/// The parse-tree node types. Port of the node classes in <c>upstream/regex/_regex_core.py</c>
/// lines 1940-4108, of which this slice carries the ones a pattern of literals, dots and groups
/// needs: <see cref="RegexBase"/>, <see cref="Any"/>, <see cref="AnyAll"/>, <see cref="AnyU"/>,
/// <see cref="Character"/>, <see cref="String"/>, <see cref="Literal"/>,
/// <see cref="PrecompiledCode"/>, <see cref="Sequence"/> and <see cref="Group"/>.
/// </summary>
/// <remarks>
/// Common base class for all nodes. Upstream <c>RegexBase</c> (lines 1941-2008).
/// <para>
/// Upstream identifies a node by <c>self._key</c>, a tuple beginning with the class, and derives
/// <c>__eq__</c> and <c>__hash__</c> from it. Here each subclass overrides
/// <see cref="object.Equals(object)"/> and <see cref="object.GetHashCode"/> over the same fields.
/// Structural equality is not decoration: <c>Branch</c>'s optimiser (S08) deduplicates alternatives
/// with it, and <c>_check_firstset</c> builds a set of nodes.
/// </para>
/// <para>
/// <c>positive</c>, <c>case_flags</c> and <c>zerowidth</c> are attributes only some upstream nodes
/// have; <c>with_flags</c> reads them off <c>self</c> regardless and fails on a node that lacks
/// them. They are virtual properties here with the values a node without them behaves as having.
/// </para>
/// </remarks>
internal abstract class RegexBase
{
    /// <summary>Whether the node matches its content or its complement. Upstream <c>positive</c>.</summary>
    internal virtual bool Positive => true;

    /// <summary>The node's case flags. Upstream <c>case_flags</c>.</summary>
    internal virtual int CaseFlags => RegexFlags.NoCase;

    /// <summary>Whether the node consumes nothing. Upstream <c>zerowidth</c>.</summary>
    internal virtual bool Zerowidth => false;

    /// <summary>Returns this node with the given flags changed. Upstream <c>with_flags</c>.</summary>
    /// <param name="positive">The new sense, or <see langword="null"/> to keep this node's.</param>
    /// <param name="caseFlags">The new case flags, or <see langword="null"/> to keep this node's.</param>
    /// <param name="zerowidth">The new zero-width setting, or <see langword="null"/> to keep this node's.</param>
    /// <returns>This node if nothing changed, otherwise a rebuilt one.</returns>
    internal RegexBase WithFlags(bool? positive = null, int? caseFlags = null, bool? zerowidth = null)
    {
        bool newPositive = positive ?? Positive;
        int newCaseFlags = caseFlags is null
            ? CaseFlags
            : RegexFlags.CaseFlagsCombination(caseFlags.Value & RegexFlags.CaseFlags);
        bool newZerowidth = zerowidth ?? Zerowidth;

        if (newPositive == Positive && newCaseFlags == CaseFlags && newZerowidth == Zerowidth)
        {
            return this;
        }

        return Rebuild(newPositive, newCaseFlags, newZerowidth);
    }

    /// <summary>Resolves group references once the whole pattern is known. Upstream <c>fix_groups</c>.</summary>
    /// <param name="pattern">The pattern text, for error messages.</param>
    /// <param name="reverse">Whether the pattern is matched right to left.</param>
    /// <param name="fuzzy">Whether this node sits inside a fuzzy section.</param>
    internal virtual void FixGroups(string pattern, bool reverse, bool fuzzy) { }

    /// <summary>Rewrites the node into a cheaper equivalent. Upstream <c>optimise</c>.</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="reverse">Whether the pattern is matched right to left.</param>
    /// <returns>The optimised node.</returns>
    internal virtual RegexBase Optimise(Info info, bool reverse) => this;

    /// <summary>Packs runs of characters into strings. Upstream <c>pack_characters</c>.</summary>
    /// <param name="info">The parse state.</param>
    /// <returns>The packed node.</returns>
    internal virtual RegexBase PackCharacters(Info info) => this;

    /// <summary>Strips capture groups from the node. Upstream <c>remove_captures</c>.</summary>
    /// <returns>The node without captures.</returns>
    internal virtual RegexBase RemoveCaptures() => this;

    /// <summary>Whether the node needs no backtracking. Upstream <c>is_atomic</c>.</summary>
    /// <returns><see langword="true"/> if it is atomic.</returns>
    internal virtual bool IsAtomic() => true;

    /// <summary>Whether the node may be hoisted out of a branch. Upstream <c>can_be_affix</c>.</summary>
    /// <returns><see langword="true"/> if it may.</returns>
    internal virtual bool CanBeAffix() => true;

    /// <summary>Whether the node contains a capture group. Upstream <c>contains_group</c>.</summary>
    /// <returns><see langword="true"/> if it does.</returns>
    internal virtual bool ContainsGroup() => false;

    /// <summary>
    /// The set of nodes that can start a match here, with <see langword="null"/> standing for
    /// "this node can match nothing, so look at what follows". Upstream <c>get_firstset</c>.
    /// </summary>
    /// <param name="reverse">Whether the pattern is matched right to left.</param>
    /// <returns>The first set.</returns>
    /// <exception cref="FirstSetErrorException">This node cannot contribute one.</exception>
    internal virtual HashSet<RegexBase?> GetFirstset(bool reverse) => throw new FirstSetErrorException();

    /// <summary>Whether a match here always begins with a single known character. Upstream <c>has_simple_start</c>.</summary>
    /// <returns><see langword="true"/> if it does.</returns>
    internal virtual bool HasSimpleStart() => false;

    /// <summary>Emits the node's bytecode, one <see cref="uint"/> array per upstream tuple. Upstream <c>compile</c>.</summary>
    /// <param name="reverse">Whether the pattern is matched right to left.</param>
    /// <param name="fuzzy">Whether this node sits inside a fuzzy section.</param>
    /// <returns>The bytecode, still in tuples.</returns>
    internal List<uint[]> Compile(bool reverse = false, bool fuzzy = false) => CompileCore(reverse, fuzzy);

    /// <summary>Whether the node matches the empty string and nothing else. Upstream <c>is_empty</c>.</summary>
    /// <returns><see langword="true"/> if it is empty.</returns>
    internal virtual bool IsEmpty() => false;

    /// <summary>
    /// The literal run a search can scan for, and how far into the match it starts. Upstream
    /// <c>get_required_string</c>.
    /// </summary>
    /// <param name="reverse">Whether the pattern is matched right to left.</param>
    /// <returns>The offset, and the required node or <see langword="null"/>.</returns>
    internal virtual (long Offset, RegexBase? Required) GetRequiredString(bool reverse) => (MaxWidth(), null);

    /// <summary>The most characters this node can match. Upstream <c>max_width</c>.</summary>
    /// <returns>The width, in codepoints.</returns>
    internal abstract long MaxWidth();

    /// <summary>
    /// Structural equality over upstream's <c>_key</c>. Abstract so that every node has to answer:
    /// nodes go into sets and are compared for equality by the optimiser, and a node that
    /// inherited reference equality by accident would break that silently.
    /// </summary>
    /// <param name="obj">The other node.</param>
    /// <returns><see langword="true"/> if they are the same node structurally.</returns>
    public abstract override bool Equals(object? obj);

    /// <summary>The hash of upstream's <c>_key</c>.</summary>
    /// <returns>The hash code.</returns>
    public abstract override int GetHashCode();

    /// <summary>Builds a copy of the node with different flags. Upstream <c>rebuild</c>.</summary>
    /// <param name="positive">The new sense.</param>
    /// <param name="caseFlags">The new case flags.</param>
    /// <param name="zerowidth">The new zero-width setting.</param>
    /// <returns>The rebuilt node.</returns>
    protected virtual RegexBase Rebuild(bool positive, int caseFlags, bool zerowidth) =>
        throw new NotSupportedException($"{GetType().Name} has no rebuild; upstream would raise AttributeError");

    /// <summary>Emits the node's own bytecode. Upstream <c>_compile</c>.</summary>
    /// <param name="reverse">Whether the pattern is matched right to left.</param>
    /// <param name="fuzzy">Whether this node sits inside a fuzzy section.</param>
    /// <returns>The bytecode, still in tuples.</returns>
    protected abstract List<uint[]> CompileCore(bool reverse, bool fuzzy);
}

/// <summary>
/// Base class for the zero-width nodes - the anchors and word boundaries. Upstream
/// <c>ZeroWidthBase</c> (<c>upstream/regex/_regex_core.py</c> lines 2011-2038).
/// </summary>
/// <remarks>
/// Upstream's <c>_key</c> is <c>(self.__class__, self.positive)</c>, so <c>encoding</c> is
/// deliberately <b>not</b> part of equality: <c>Boundary()</c> and
/// <c>Boundary(encoding=ASCII_ENCODING)</c> compare equal even though they compile to different
/// flag words. <c>Branch</c>'s prefix and suffix splitting hoists on that equality, so the
/// divergence would be observable in the bytecode.
/// </remarks>
/// <param name="positive">Whether the node asserts the position or its complement.</param>
/// <param name="encoding">The encoding tag, one of <see cref="RegexFlags.AsciiEncoding"/> and friends.</param>
internal abstract class ZeroWidthBase(bool positive = true, int encoding = 0) : RegexBase
{
    /// <inheritdoc />
    internal override bool Positive { get; } = positive;

    /// <summary>The encoding this node's word rules come from. Upstream <c>encoding</c>.</summary>
    internal int Encoding { get; } = encoding;

    /// <summary>The opcode this node compiles to. Upstream <c>_opcode</c>.</summary>
    protected abstract Opcode ZeroWidthOpcode { get; }

    /// <inheritdoc />
    internal override HashSet<RegexBase?> GetFirstset(bool reverse) => [null];

    /// <inheritdoc />
    internal override long MaxWidth() => 0;

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is ZeroWidthBase other && GetType() == other.GetType() && Positive == other.Positive;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Positive);

    /// <inheritdoc />
    protected override List<uint[]> CompileCore(bool reverse, bool fuzzy)
    {
        uint flags = 0;
        if (Positive)
        {
            flags |= NodeFlags.Positive;
        }

        if (fuzzy)
        {
            flags |= NodeFlags.Fuzzy;
        }

        if (reverse)
        {
            flags |= NodeFlags.Reverse;
        }

        flags |= (uint)Encoding << NodeFlags.EncodingShift;

        return
        [
            [(uint)ZeroWidthOpcode, flags],
        ];
    }
}

/// <summary><c>\b</c> and <c>\B</c>. Upstream <c>Boundary</c> (lines 2130-2132).</summary>
internal sealed class Boundary : ZeroWidthBase
{
    /// <summary>Initializes a word-boundary assertion.</summary>
    /// <param name="positive">Whether this is <c>\b</c> or <c>\B</c>.</param>
    /// <param name="encoding">The encoding whose word rules apply.</param>
    internal Boundary(bool positive = true, int encoding = 0)
        : base(positive, encoding) { }

    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.Boundary;
}

/// <summary><c>\b</c> under the <c>WORD</c> flag. Upstream <c>DefaultBoundary</c> (lines 2744-2746).</summary>
internal sealed class DefaultBoundary : ZeroWidthBase
{
    /// <summary>Initializes a default word-boundary assertion.</summary>
    /// <param name="positive">Whether this is <c>\b</c> or <c>\B</c>.</param>
    internal DefaultBoundary(bool positive = true)
        : base(positive) { }

    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.DefaultBoundary;
}

/// <summary><c>\M</c> under the <c>WORD</c> flag. Upstream <c>DefaultEndOfWord</c> (lines 2748-2750).</summary>
internal sealed class DefaultEndOfWord : ZeroWidthBase
{
    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.DefaultEndOfWord;
}

/// <summary><c>\m</c> under the <c>WORD</c> flag. Upstream <c>DefaultStartOfWord</c> (lines 2752-2754).</summary>
internal sealed class DefaultStartOfWord : ZeroWidthBase
{
    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.DefaultStartOfWord;
}

/// <summary><c>$</c> under <c>MULTILINE</c>. Upstream <c>EndOfLine</c> (lines 2756-2758).</summary>
internal class EndOfLine : ZeroWidthBase
{
    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.EndOfLine;
}

/// <summary><c>$</c> under <c>MULTILINE</c> and <c>WORD</c>. Upstream <c>EndOfLineU</c> (lines 2760-2762).</summary>
internal sealed class EndOfLineU : EndOfLine
{
    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.EndOfLineU;
}

/// <summary><c>\Z</c> and <c>\z</c>. Upstream <c>EndOfString</c> (lines 2764-2766).</summary>
internal sealed class EndOfString : ZeroWidthBase
{
    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.EndOfString;
}

/// <summary><c>$</c> outside <c>MULTILINE</c>. Upstream <c>EndOfStringLine</c> (lines 2768-2770).</summary>
internal class EndOfStringLine : ZeroWidthBase
{
    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.EndOfStringLine;
}

/// <summary><c>$</c> under <c>WORD</c>. Upstream <c>EndOfStringLineU</c> (lines 2772-2774).</summary>
internal sealed class EndOfStringLineU : EndOfStringLine
{
    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.EndOfStringLineU;
}

/// <summary><c>\M</c>. Upstream <c>EndOfWord</c> (lines 2776-2778).</summary>
internal sealed class EndOfWord : ZeroWidthBase
{
    /// <summary>Initializes an end-of-word assertion.</summary>
    /// <param name="encoding">The encoding whose word rules apply.</param>
    internal EndOfWord(int encoding = 0)
        : base(true, encoding) { }

    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.EndOfWord;
}

/// <summary><c>\K</c>. Upstream <c>Keep</c> (lines 3142-3144).</summary>
internal sealed class Keep : ZeroWidthBase
{
    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.Keep;
}

/// <summary><c>\G</c>. Upstream <c>SearchAnchor</c> (lines 3498-3500).</summary>
internal sealed class SearchAnchor : ZeroWidthBase
{
    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.SearchAnchor;
}

/// <summary><c>^</c> under <c>MULTILINE</c>. Upstream <c>StartOfLine</c> (lines 3991-3993).</summary>
internal class StartOfLine : ZeroWidthBase
{
    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.StartOfLine;
}

/// <summary><c>^</c> under <c>MULTILINE</c> and <c>WORD</c>. Upstream <c>StartOfLineU</c> (lines 3995-3997).</summary>
internal sealed class StartOfLineU : StartOfLine
{
    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.StartOfLineU;
}

/// <summary><c>^</c> outside <c>MULTILINE</c>, and <c>\A</c>. Upstream <c>StartOfString</c> (lines 3999-4001).</summary>
internal sealed class StartOfString : ZeroWidthBase
{
    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.StartOfString;
}

/// <summary><c>\m</c>. Upstream <c>StartOfWord</c> (lines 4003-4005).</summary>
internal sealed class StartOfWord : ZeroWidthBase
{
    /// <summary>Initializes a start-of-word assertion.</summary>
    /// <param name="encoding">The encoding whose word rules apply.</param>
    internal StartOfWord(int encoding = 0)
        : base(true, encoding) { }

    /// <inheritdoc />
    protected override Opcode ZeroWidthOpcode => Opcode.StartOfWord;
}

/// <summary>
/// Bytecode written straight out, with no node of its own. Upstream <c>PrecompiledCode</c>
/// (<c>upstream/regex/_regex_core.py</c> lines 3303-3308).
/// </summary>
internal sealed class PrecompiledCode : RegexBase
{
    private readonly uint[] _code;

    /// <summary>Initializes a node that emits exactly the given words.</summary>
    /// <param name="code">The bytecode words.</param>
    internal PrecompiledCode(uint[] code)
    {
        _code = code;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    /// <inheritdoc />
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

    /// <inheritdoc />
    internal override long MaxWidth() =>
        throw new NotSupportedException("upstream's PrecompiledCode has no max_width either");

    /// <inheritdoc />
    protected override List<uint[]> CompileCore(bool reverse, bool fuzzy) => [_code];
}

/// <summary>
/// <c>.</c> outside <see cref="FuzzyRegexOptions.Singleline"/>: any character but a newline.
/// Upstream <c>Any</c> (<c>upstream/regex/_regex_core.py</c> lines 2040-2057).
/// </summary>
internal class Any : RegexBase
{
    /// <summary>The opcode pair, forward and reverse. Upstream <c>_opcode</c>.</summary>
    protected virtual (Opcode Forward, Opcode Reverse) Opcodes => (Opcode.Any, Opcode.AnyRev);

    /// <inheritdoc />
    internal override bool HasSimpleStart() => true;

    /// <inheritdoc />
    internal override long MaxWidth() => 1;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is not null && GetType() == obj.GetType();

    /// <inheritdoc />
    public override int GetHashCode() => GetType().GetHashCode();

    /// <inheritdoc />
    protected override List<uint[]> CompileCore(bool reverse, bool fuzzy)
    {
        uint flags = 0;
        if (fuzzy)
        {
            flags |= NodeFlags.Fuzzy;
        }

        return
        [
            [(uint)(reverse ? Opcodes.Reverse : Opcodes.Forward), flags],
        ];
    }
}

/// <summary>
/// <c>.</c> under <see cref="FuzzyRegexOptions.Singleline"/>: any character at all. Upstream
/// <c>AnyAll</c> (<c>upstream/regex/_regex_core.py</c> lines 2059-2068).
/// </summary>
internal sealed class AnyAll : Any
{
    /// <inheritdoc />
    protected override (Opcode Forward, Opcode Reverse) Opcodes => (Opcode.AnyAll, Opcode.AnyAllRev);
}

/// <summary>
/// <c>.</c> under the <c>WORD</c> flag: any character but a Unicode line break. Upstream
/// <c>AnyU</c> (<c>upstream/regex/_regex_core.py</c> lines 2070-2072).
/// </summary>
internal sealed class AnyU : Any
{
    /// <inheritdoc />
    protected override (Opcode Forward, Opcode Reverse) Opcodes => (Opcode.AnyU, Opcode.AnyURev);
}

/// <summary>
/// Alternatives, tried left to right. Upstream <c>Branch</c>
/// (<c>upstream/regex/_regex_core.py</c> lines 2134-2524).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Optimise"/> is the densest routine in the parser: it flattens nested branches, hoists
/// a common prefix (or, matching right to left, a common suffix) out of the alternatives, reduces
/// runs of single-character alternatives to a set, and prepends a cheap first-set precheck.
/// </para>
/// <para>
/// <b>Not ported: <c>_merge_common_prefixes</c>, <c>_is_simple_character</c> and
/// <c>_flush_char_prefix</c></b> (lines 2386-2415, 2445-2469). Nothing in the upstream package
/// calls <c>_merge_common_prefixes</c>, and it could not work if anything did: line 2409 calls the
/// five-parameter <c>_flush_char_prefix</c> with four arguments, which is a <c>TypeError</c>. Its
/// two helpers have no other caller. See PORTMAP's "Where we diverge".
/// </para>
/// </remarks>
internal sealed class Branch : RegexBase
{
    /// <summary>Initializes an alternation.</summary>
    /// <param name="branches">The alternatives, in pattern order.</param>
    internal Branch(List<RegexBase> branches)
    {
        Branches = branches;
    }

    /// <summary>The alternatives. Upstream <c>branches</c>.</summary>
    internal List<RegexBase> Branches { get; private set; }

    /// <inheritdoc />
    internal override void FixGroups(string pattern, bool reverse, bool fuzzy)
    {
        foreach (RegexBase b in Branches)
        {
            b.FixGroups(pattern, reverse, fuzzy);
        }
    }

    /// <inheritdoc />
    internal override RegexBase Optimise(Info info, bool reverse)
    {
        if (Branches.Count == 0)
        {
            return new Sequence();
        }

        // Flatten branches within branches.
        List<RegexBase> branches = FlattenBranches(info, reverse, Branches);

        // Move any common prefix or suffix out of the branches.
        List<RegexBase> prefix;
        List<RegexBase> suffix;
        if (reverse)
        {
            (suffix, branches) = SplitCommonSuffix(info, branches);
            prefix = [];
        }
        else
        {
            (prefix, branches) = SplitCommonPrefix(info, branches);
            suffix = [];
        }

        // Try to reduce adjacent single-character branches to sets.
        branches = ReduceToSet(info, reverse, branches);

        List<RegexBase> sequence;
        if (branches.Count > 1)
        {
            sequence = [new Branch(branches)];

            if (prefix.Count == 0 || suffix.Count == 0)
            {
                // We might be able to add a quick precheck before the branches.
                RegexBase? firstset = AddPrecheck(info, reverse, branches);

                if (firstset is not null)
                {
                    if (reverse)
                    {
                        sequence.Add(firstset);
                    }
                    else
                    {
                        sequence.Insert(0, firstset);
                    }
                }
            }
        }
        else
        {
            sequence = branches;
        }

        return Sequence.MakeSequence([.. prefix, .. sequence, .. suffix]);
    }

    /// <inheritdoc />
    internal override RegexBase PackCharacters(Info info)
    {
        Branches = [.. Branches.Select(b => b.PackCharacters(info))];
        return this;
    }

    /// <inheritdoc />
    internal override RegexBase RemoveCaptures()
    {
        Branches = [.. Branches.Select(b => b.RemoveCaptures())];
        return this;
    }

    /// <inheritdoc />
    internal override bool IsAtomic() => Branches.TrueForAll(b => b.IsAtomic());

    /// <inheritdoc />
    internal override bool CanBeAffix() => Branches.TrueForAll(b => b.CanBeAffix());

    /// <inheritdoc />
    internal override bool ContainsGroup() => Branches.Exists(b => b.ContainsGroup());

    /// <inheritdoc />
    internal override HashSet<RegexBase?> GetFirstset(bool reverse)
    {
        HashSet<RegexBase?> fs = [];
        foreach (RegexBase b in Branches)
        {
            fs.UnionWith(b.GetFirstset(reverse));
        }

        // Upstream's `return fs or set([None])`.
        return fs.Count > 0 ? fs : [null];
    }

    /// <inheritdoc />
    internal override bool IsEmpty() => Branches.TrueForAll(b => b.IsEmpty());

    /// <inheritdoc />
    internal override long MaxWidth() => Branches.Max(b => b.MaxWidth());

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Branch other && Branches.SequenceEqual(other.Branches);

    /// <inheritdoc />
    /// <remarks>
    /// Upstream defines <c>__eq__</c> without <c>__hash__</c>, which makes <c>Branch</c> unhashable
    /// in Python; nothing ever puts one in a set. <see cref="Branches"/> is reassigned by
    /// <c>pack_characters</c> and <c>remove_captures</c>, so hashing it would break the "equal
    /// objects hash equal, and a hash does not change" contract. A constant hash keeps both, at the
    /// cost of collisions in a set nothing builds.
    /// </remarks>
    public override int GetHashCode() => typeof(Branch).GetHashCode();

    /// <inheritdoc />
    protected override List<uint[]> CompileCore(bool reverse, bool fuzzy)
    {
        if (Branches.Count == 0)
        {
            return [];
        }

        List<uint[]> code =
        [
            [(uint)Opcode.Branch],
        ];
        foreach (RegexBase b in Branches)
        {
            code.AddRange(b.Compile(reverse, fuzzy));
            code.Add([(uint)Opcode.Next]);
        }

        code[^1] = [(uint)Opcode.End];

        return code;
    }

    /// <summary>Upstream <c>Branch._flatten_branches</c> (lines 2237-2248).</summary>
    private static List<RegexBase> FlattenBranches(Info info, bool reverse, List<RegexBase> branches)
    {
        // Flatten the branches so that there aren't branches of branches.
        List<RegexBase> newBranches = [];
        foreach (RegexBase branch in branches)
        {
            RegexBase b = branch.Optimise(info, reverse);
            if (b is Branch nested)
            {
                newBranches.AddRange(nested.Branches);
            }
            else
            {
                newBranches.Add(b);
            }
        }

        return newBranches;
    }

    /// <summary>Upstream <c>Branch._split_common_prefix</c> (lines 2250-2290).</summary>
    private static (List<RegexBase> Prefix, List<RegexBase> Branches) SplitCommonPrefix(
        Info info,
        List<RegexBase> branches
    )
    {
        List<List<RegexBase>> alternatives = Alternatives(branches);

        // What is the maximum possible length of the prefix?
        int maxCount = alternatives.Min(a => a.Count);

        // What is the longest common prefix?
        List<RegexBase> prefix = alternatives[0];
        int pos = 0;
        while (pos < maxCount && prefix[pos].CanBeAffix() && alternatives.TrueForAll(a => a[pos].Equals(prefix[pos])))
        {
            pos++;
        }

        int count = pos;

        if ((info.Flags & RegexFlags.Unicode) != 0)
        {
            // We need to check that we're not splitting a sequence of characters which could form
            // part of full case-folding.
            while (count > 0 && !alternatives.TrueForAll(a => CanSplit(a, count)))
            {
                count--;
            }
        }

        // No common prefix is possible.
        if (count == 0)
        {
            return ([], branches);
        }

        // Rebuild the branches.
        List<RegexBase> newBranches = [];
        foreach (List<RegexBase> a in alternatives)
        {
            newBranches.Add(Sequence.MakeSequence([.. a.Skip(count)]));
        }

        return ([.. prefix.Take(count)], newBranches);
    }

    /// <summary>Upstream <c>Branch._split_common_suffix</c> (lines 2292-2331).</summary>
    private static (List<RegexBase> Suffix, List<RegexBase> Branches) SplitCommonSuffix(
        Info info,
        List<RegexBase> branches
    )
    {
        List<List<RegexBase>> alternatives = Alternatives(branches);

        // What is the maximum possible length of the suffix?
        int maxCount = alternatives.Min(a => a.Count);

        // What is the longest common suffix? Upstream indexes from the end with a negative
        // subscript; `back` counts the same positions forwards.
        List<RegexBase> suffix = alternatives[0];
        int back = 0;
        while (
            back < maxCount
            && suffix[suffix.Count - 1 - back].CanBeAffix()
            && alternatives.TrueForAll(a => a[a.Count - 1 - back].Equals(suffix[suffix.Count - 1 - back]))
        )
        {
            back++;
        }

        int count = back;

        if ((info.Flags & RegexFlags.Unicode) != 0)
        {
            while (count > 0 && !alternatives.TrueForAll(a => CanSplitRev(a, count)))
            {
                count--;
            }
        }

        // No common suffix is possible.
        if (count == 0)
        {
            return ([], branches);
        }

        // Rebuild the branches.
        List<RegexBase> newBranches = [];
        foreach (List<RegexBase> a in alternatives)
        {
            newBranches.Add(Sequence.MakeSequence([.. a.Take(a.Count - count)]));
        }

        return ([.. suffix.Skip(suffix.Count - count)], newBranches);
    }

    /// <summary>
    /// Upstream's "get the items in the branches" preamble, written out identically in
    /// <c>_split_common_prefix</c> and <c>_split_common_suffix</c> (lines 2253-2259, 2295-2301).
    /// </summary>
    /// <remarks>
    /// The lists a <see cref="Sequence"/> contributes are its own <see cref="Sequence.Items"/>, not
    /// copies, exactly as upstream. Neither caller mutates them.
    /// </remarks>
    private static List<List<RegexBase>> Alternatives(List<RegexBase> branches)
    {
        List<List<RegexBase>> alternatives = [];
        foreach (RegexBase b in branches)
        {
            alternatives.Add(b is Sequence sequence ? sequence.Items : [b]);
        }

        return alternatives;
    }

    /// <summary>Upstream <c>Branch._can_split</c> (lines 2333-2356).</summary>
    private static bool CanSplit(List<RegexBase> items, int count)
    {
        // Check the characters either side of the proposed split.
        if (!IsFullCase(items, count - 1))
        {
            return true;
        }

        if (!IsFullCase(items, count))
        {
            return true;
        }

        // Check whether a 1-1 split would be OK.
        if (IsFolded(PySlice(items, count - 1, count + 1)))
        {
            return false;
        }

        // Check whether a 1-2 split would be OK.
        if (IsFullCase(items, count + 2) && IsFolded(PySlice(items, count - 1, count + 2)))
        {
            return false;
        }

        // Check whether a 2-1 split would be OK.
        if (IsFullCase(items, count - 2) && IsFolded(PySlice(items, count - 2, count + 1)))
        {
            return false;
        }

        return true;
    }

    /// <summary>Upstream <c>Branch._can_split_rev</c> (lines 2358-2383).</summary>
    private static bool CanSplitRev(List<RegexBase> items, int count)
    {
        int end = items.Count;

        // Check the characters either side of the proposed split.
        if (!IsFullCase(items, end - count))
        {
            return true;
        }

        if (!IsFullCase(items, end - count - 1))
        {
            return true;
        }

        // Check whether a 1-1 split would be OK.
        if (IsFolded(PySlice(items, end - count - 1, end - count + 1)))
        {
            return false;
        }

        // Check whether a 1-2 split would be OK.
        if (IsFullCase(items, end - count + 2) && IsFolded(PySlice(items, end - count - 1, end - count + 2)))
        {
            return false;
        }

        // Check whether a 2-1 split would be OK.
        if (IsFullCase(items, end - count - 2) && IsFolded(PySlice(items, end - count - 2, end - count + 1)))
        {
            return false;
        }

        return true;
    }

    /// <summary>Upstream <c>Branch._reduce_to_set</c> (lines 2417-2443).</summary>
    private static List<RegexBase> ReduceToSet(Info info, bool reverse, List<RegexBase> branches)
    {
        // Can the branches be reduced to a set?
        List<RegexBase> newBranches = [];
        HashSet<RegexBase> items = [];
        int caseFlags = RegexFlags.NoCase;
        foreach (RegexBase b in branches)
        {
            // Upstream's isinstance check also names Property and SetBase, which arrive in S10.
            if (b is Character)
            {
                // Branch starts with a single character.
                if (b.CaseFlags != caseFlags)
                {
                    // Different case sensitivity, so flush.
                    FlushSetMembers(info, reverse, items, caseFlags, newBranches);

                    caseFlags = b.CaseFlags;
                }

                items.Add(b.WithFlags(caseFlags: RegexFlags.NoCase));
            }
            else
            {
                FlushSetMembers(info, reverse, items, caseFlags, newBranches);

                newBranches.Add(b);
            }
        }

        FlushSetMembers(info, reverse, items, caseFlags, newBranches);

        return newBranches;
    }

    /// <summary>Upstream <c>Branch._flush_set_members</c> (lines 2471-2484).</summary>
    private static void FlushSetMembers(
        Info info,
        bool reverse,
        HashSet<RegexBase> items,
        int caseFlags,
        List<RegexBase> newBranches
    )
    {
        // Flush the set members.
        if (items.Count == 0)
        {
            return;
        }

        if (items.Count > 1)
        {
            // Upstream: SetUnion(info, list(items)).optimise(info, reverse). This is also one of the
            // two points PORTMAP's "Where we diverge" requires the members to be sorted at, because
            // a Python set of nodes has no stable order; the sort lands with the set node.
            _ = (info, reverse);
            throw new NotImplementedException(
                "needs:character-classes - reducing alternatives to a set needs the SetUnion node (S10)"
            );
        }

        RegexBase item = items.First();

        newBranches.Add(item.WithFlags(caseFlags: caseFlags));

        items.Clear();
    }

    /// <summary>Upstream <c>Branch._is_full_case</c> (lines 2486-2493).</summary>
    private static bool IsFullCase(List<RegexBase> items, int i)
    {
        if (i < 0 || i >= items.Count)
        {
            return false;
        }

        return items[i] is Character { Positive: true } item
            && (item.CaseFlags & RegexFlags.FullIgnoreCase) == RegexFlags.FullIgnoreCase;
    }

    /// <summary>Upstream <c>Branch._is_folded</c> (lines 2495-2515).</summary>
    private static bool IsFolded(List<RegexBase> items)
    {
        if (items.Count < 2)
        {
            return false;
        }

        foreach (RegexBase i in items)
        {
            if (i is not Character { Positive: true } character || character.CaseFlags == RegexFlags.NoCase)
            {
                return false;
            }
        }

        // Upstream folds the run with _regex.fold_case and compares it against every character in
        // _regex.get_expand_on_folding(), both of which are the generated Unicode tables.
        throw new NotImplementedException(
            "needs:case-folding - deciding whether a run folds together needs the Unicode tables (S09)"
        );
    }

    /// <summary>Upstream <c>Branch._add_precheck</c> (lines 2178-2191).</summary>
    /// <remarks>
    /// <c>type(branch) is Literal</c> is an exact type test, and the only place a
    /// <see cref="Literal"/> is built is <c>Sequence._fix_full_casefold</c>, so this returns
    /// <see langword="null"/> for everything a pattern without full case folding can produce.
    /// </remarks>
    private static RegexBase? AddPrecheck(Info info, bool reverse, List<RegexBase> branches)
    {
        HashSet<int> charset = [];
        foreach (RegexBase branch in branches)
        {
            // Upstream's `type(branch) is Literal` is an exact type test; Literal is sealed, so
            // the pattern match is the same test.
            if (branch is Literal literal && literal.CaseFlags == RegexFlags.NoCase)
            {
                charset.Add(literal.Characters[reverse ? ^1 : 0]);
            }
            else
            {
                return null;
            }
        }

        if (charset.Count == 0)
        {
            return null;
        }

        return ParseFunctions.CheckFirstset(info, reverse, [.. charset.Select(c => (RegexBase?)new Character(c))]);
    }

    /// <summary>Python's <c>items[start:end]</c>, which clamps rather than throwing.</summary>
    private static List<RegexBase> PySlice(List<RegexBase> items, int start, int end)
    {
        int from = Math.Clamp(start, 0, items.Count);
        int to = Math.Clamp(end, from, items.Count);
        return items.GetRange(from, to - from);
    }
}

/// <summary>
/// One literal character. Upstream <c>Character</c> (<c>upstream/regex/_regex_core.py</c> lines
/// 2581-2653).
/// </summary>
internal sealed class Character : RegexBase
{
    private static readonly Dictionary<(int CaseFlags, bool Reverse), Opcode> _opcodes = new()
    {
        [(RegexFlags.NoCase, false)] = Opcode.Character,
        [(RegexFlags.IgnoreCase, false)] = Opcode.CharacterIgn,
        [(RegexFlags.FullCase, false)] = Opcode.Character,
        [(RegexFlags.FullIgnoreCase, false)] = Opcode.CharacterIgn,
        [(RegexFlags.NoCase, true)] = Opcode.CharacterRev,
        [(RegexFlags.IgnoreCase, true)] = Opcode.CharacterIgnRev,
        [(RegexFlags.FullCase, true)] = Opcode.CharacterRev,
        [(RegexFlags.FullIgnoreCase, true)] = Opcode.CharacterIgnRev,
    };

    /// <summary>Initializes a literal character node.</summary>
    /// <param name="value">The codepoint.</param>
    /// <param name="positive">Whether the character matches itself or anything else.</param>
    /// <param name="caseFlags">The case flags in force.</param>
    /// <param name="zerowidth">Whether the node consumes nothing.</param>
    internal Character(int value, bool positive = true, int caseFlags = RegexFlags.NoCase, bool zerowidth = false)
    {
        int normalisedCaseFlags = RegexFlags.CaseFlagsCombination(caseFlags);
        Value = value;
        Positive = positive;
        CaseFlags = normalisedCaseFlags;
        Zerowidth = zerowidth;

        if (positive && (normalisedCaseFlags & RegexFlags.FullIgnoreCase) == RegexFlags.FullIgnoreCase)
        {
            // Upstream: self.folded = _regex.fold_case(FULL_CASE_FOLDING, chr(self.value)), a
            // lookup into the generated Unicode case-folding tables.
            throw new NotImplementedException("needs:case-folding - full case folding needs the Unicode tables (S09)");
        }

        Folded = [value];
    }

    /// <summary>The codepoint. Upstream <c>value</c>.</summary>
    internal int Value { get; }

    /// <inheritdoc />
    internal override bool Positive { get; }

    /// <inheritdoc />
    internal override int CaseFlags { get; }

    /// <inheritdoc />
    internal override bool Zerowidth { get; }

    /// <summary>The character after full case folding, as codepoints. Upstream <c>folded</c>.</summary>
    internal int[] Folded { get; }

    /// <summary>
    /// The folded form as the compiler wants it. Upstream sets <c>folded_characters</c> lazily, in
    /// <c>get_required_string</c>; here it is simply <see cref="Folded"/>.
    /// </summary>
    internal int[] FoldedCharacters => Folded;

    /// <inheritdoc />
    internal override HashSet<RegexBase?> GetFirstset(bool reverse) => [this];

    /// <inheritdoc />
    internal override bool HasSimpleStart() => true;

    /// <inheritdoc />
    internal override long MaxWidth() => Folded.Length;

    /// <inheritdoc />
    internal override (long Offset, RegexBase? Required) GetRequiredString(bool reverse) =>
        Positive ? (0, this) : (1, null);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is Character other
        && Value == other.Value
        && Positive == other.Positive
        && CaseFlags == other.CaseFlags
        && Zerowidth == other.Zerowidth;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(typeof(Character), Value, Positive, CaseFlags, Zerowidth);

    /// <inheritdoc />
    protected override RegexBase Rebuild(bool positive, int caseFlags, bool zerowidth) =>
        new Character(Value, positive, caseFlags, zerowidth);

    /// <inheritdoc />
    protected override List<uint[]> CompileCore(bool reverse, bool fuzzy)
    {
        uint flags = 0;
        if (Positive)
        {
            flags |= NodeFlags.Positive;
        }

        if (Zerowidth)
        {
            flags |= NodeFlags.Zerowidth;
        }

        if (fuzzy)
        {
            flags |= NodeFlags.Fuzzy;
        }

        RegexBase code = new PrecompiledCode([(uint)_opcodes[(CaseFlags, reverse)], flags, (uint)Value]);

        // Upstream wraps the character in a Branch with its expanded form when full case-folding
        // makes it longer than one character; the constructor above cannot build such a node yet.
        return code.Compile(reverse, fuzzy);
    }
}

/// <summary>
/// A run of literal characters. Upstream <c>String</c> (<c>upstream/regex/_regex_core.py</c> lines
/// 4007-4060).
/// </summary>
internal class String : RegexBase
{
    private static readonly Dictionary<(int CaseFlags, bool Reverse), Opcode> _opcodes = new()
    {
        [(RegexFlags.NoCase, false)] = Opcode.String,
        [(RegexFlags.IgnoreCase, false)] = Opcode.StringIgn,
        [(RegexFlags.FullCase, false)] = Opcode.String,
        [(RegexFlags.FullIgnoreCase, false)] = Opcode.StringFld,
        [(RegexFlags.NoCase, true)] = Opcode.StringRev,
        [(RegexFlags.IgnoreCase, true)] = Opcode.StringIgnRev,
        [(RegexFlags.FullCase, true)] = Opcode.StringRev,
        [(RegexFlags.FullIgnoreCase, true)] = Opcode.StringFldRev,
    };

    /// <summary>Initializes a literal run.</summary>
    /// <param name="characters">The codepoints.</param>
    /// <param name="caseFlags">The case flags in force.</param>
    internal String(IReadOnlyList<int> characters, int caseFlags = RegexFlags.NoCase)
    {
        int normalisedCaseFlags = RegexFlags.CaseFlagsCombination(caseFlags);
        Characters = [.. characters];
        CaseFlags = normalisedCaseFlags;

        if ((normalisedCaseFlags & RegexFlags.FullIgnoreCase) == RegexFlags.FullIgnoreCase)
        {
            // Upstream folds every character here with _regex.fold_case, which is a lookup into
            // the generated Unicode case-folding tables.
            throw new NotImplementedException("needs:case-folding - full case folding needs the Unicode tables (S09)");
        }

        FoldedCharacters = Characters;
    }

    /// <summary>The literal codepoints. Upstream <c>characters</c>.</summary>
    internal int[] Characters { get; }

    /// <inheritdoc />
    internal override int CaseFlags { get; }

    /// <summary>The codepoints after full case folding. Upstream <c>folded_characters</c>.</summary>
    internal int[] FoldedCharacters { get; }

    /// <summary>
    /// Whether this run is the pattern's required string, which the compiler marks so the engine
    /// can scan for it. Upstream <c>required</c>.
    /// </summary>
    internal bool Required { get; set; }

    /// <inheritdoc />
    internal override HashSet<RegexBase?> GetFirstset(bool reverse) =>
        [new Character(Characters[reverse ? Characters.Length - 1 : 0], caseFlags: CaseFlags)];

    /// <inheritdoc />
    internal override bool HasSimpleStart() => true;

    /// <inheritdoc />
    internal override long MaxWidth() => FoldedCharacters.Length;

    /// <inheritdoc />
    internal override (long Offset, RegexBase? Required) GetRequiredString(bool reverse) => (0, this);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is String other
        && GetType() == other.GetType()
        && CaseFlags == other.CaseFlags
        && Characters.AsSpan().SequenceEqual(other.Characters);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(GetType());
        hash.Add(CaseFlags);
        foreach (int c in Characters)
        {
            hash.Add(c);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    protected override List<uint[]> CompileCore(bool reverse, bool fuzzy)
    {
        uint flags = 0;
        if (fuzzy)
        {
            flags |= NodeFlags.Fuzzy;
        }

        if (Required)
        {
            flags |= NodeFlags.Required;
        }

        uint[] code = new uint[3 + FoldedCharacters.Length];
        code[0] = (uint)_opcodes[(CaseFlags, reverse)];
        code[1] = flags;
        code[2] = (uint)FoldedCharacters.Length;
        for (int i = 0; i < FoldedCharacters.Length; i++)
        {
            code[3 + i] = (uint)FoldedCharacters[i];
        }

        return [code];
    }
}

/// <summary>
/// A literal run that differs from <see cref="String"/> only in how upstream's <c>dump</c> prints
/// it. Upstream <c>Literal</c> (<c>upstream/regex/_regex_core.py</c> lines 4062-4067).
/// </summary>
internal sealed class Literal : String
{
    /// <summary>Initializes a literal run.</summary>
    /// <param name="characters">The codepoints.</param>
    /// <param name="caseFlags">The case flags in force.</param>
    internal Literal(IReadOnlyList<int> characters, int caseFlags = RegexFlags.NoCase)
        : base(characters, caseFlags) { }
}

/// <summary>
/// Items matched one after another. Upstream <c>Sequence</c>
/// (<c>upstream/regex/_regex_core.py</c> lines 3502-3713).
/// </summary>
internal sealed class Sequence : RegexBase
{
    /// <summary>Initializes a sequence.</summary>
    /// <param name="items">The items, in pattern order.</param>
    internal Sequence(List<RegexBase>? items = null)
    {
        Items = items ?? [];
    }

    /// <summary>The items. Upstream <c>items</c>.</summary>
    internal List<RegexBase> Items { get; }

    /// <summary>Upstream <c>make_sequence</c> (lines 1935-1938).</summary>
    /// <param name="items">The items.</param>
    /// <returns>The single item if there is one, otherwise a sequence of them.</returns>
    internal static RegexBase MakeSequence(List<RegexBase> items) => items.Count == 1 ? items[0] : new Sequence(items);

    /// <inheritdoc />
    internal override void FixGroups(string pattern, bool reverse, bool fuzzy)
    {
        foreach (RegexBase s in Items)
        {
            s.FixGroups(pattern, reverse, fuzzy);
        }
    }

    /// <inheritdoc />
    internal override RegexBase Optimise(Info info, bool reverse)
    {
        // Flatten the sequences.
        List<RegexBase> items = [];
        foreach (RegexBase item in Items)
        {
            RegexBase s = item.Optimise(info, reverse);
            if (s is Sequence sequence)
            {
                items.AddRange(sequence.Items);
            }
            else
            {
                items.Add(s);
            }
        }

        return MakeSequence(items);
    }

    /// <inheritdoc />
    internal override RegexBase PackCharacters(Info info)
    {
        List<RegexBase> items = [];
        List<int> characters = [];
        int caseFlags = RegexFlags.NoCase;

        foreach (RegexBase s in Items)
        {
            if (s is Character character && character.Positive && !character.Zerowidth)
            {
                // Different case sensitivity, so flush, unless neither the previous nor the new
                // character are cased. Upstream nests these two conditions (lines 3533-3540); the
                // inner one has no else, so the conjunction is the same test.
                if (
                    character.CaseFlags != caseFlags
                    && (character.CaseFlags != 0 || ParseFunctions.IsCasedI(info, character.Value))
                )
                {
                    FlushCharacters(info, characters, caseFlags, items);
                    caseFlags = character.CaseFlags;
                }

                characters.Add(character.Value);
            }
            else if (s is String literal)
            {
                // As above, for a string rather than a character (lines 3543-3552).
                if (
                    literal.CaseFlags != caseFlags
                    && (literal.CaseFlags != 0 || characters.Exists(c => ParseFunctions.IsCasedI(info, c)))
                )
                {
                    FlushCharacters(info, characters, caseFlags, items);
                    caseFlags = literal.CaseFlags;
                }

                characters.AddRange(literal.Characters);
            }
            else
            {
                FlushCharacters(info, characters, caseFlags, items);
                items.Add(s.PackCharacters(info));
            }
        }

        FlushCharacters(info, characters, caseFlags, items);

        return MakeSequence(items);
    }

    /// <inheritdoc />
    internal override RegexBase RemoveCaptures()
    {
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i] = Items[i].RemoveCaptures();
        }

        return this;
    }

    /// <inheritdoc />
    internal override bool IsAtomic() => Items.TrueForAll(s => s.IsAtomic());

    /// <inheritdoc />
    internal override bool CanBeAffix() => false;

    /// <inheritdoc />
    internal override bool ContainsGroup() => Items.Exists(s => s.ContainsGroup());

    /// <inheritdoc />
    internal override HashSet<RegexBase?> GetFirstset(bool reverse)
    {
        HashSet<RegexBase?> fs = [];

        // Upstream reverses self.items in place here, not a copy (line 3581). Kept, because a
        // divergence would be invisible until some later slice depended on the order.
        if (reverse)
        {
            Items.Reverse();
        }

        foreach (RegexBase s in Items)
        {
            fs.UnionWith(s.GetFirstset(reverse));
            if (!fs.Contains(null))
            {
                return fs;
            }

            fs.Remove(null);
        }

        fs.Add(null);
        return fs;
    }

    /// <inheritdoc />
    internal override bool HasSimpleStart() => Items.Count > 0 && Items[0].HasSimpleStart();

    /// <inheritdoc />
    internal override bool IsEmpty() => Items.TrueForAll(i => i.IsEmpty());

    /// <inheritdoc />
    internal override long MaxWidth()
    {
        long total = 0;
        foreach (RegexBase s in Items)
        {
            total = Widths.Add(total, s.MaxWidth());
        }

        return total;
    }

    /// <inheritdoc />
    internal override (long Offset, RegexBase? Required) GetRequiredString(bool reverse)
    {
        IEnumerable<RegexBase> seq = reverse ? Enumerable.Reverse(Items) : Items;
        long offset = 0;

        foreach (RegexBase s in seq)
        {
            (long ofs, RegexBase? req) = s.GetRequiredString(reverse);
            offset = Widths.Add(offset, ofs);
            if (req is not null)
            {
                return (offset, req);
            }
        }

        return (offset, null);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Sequence other && Items.SequenceEqual(other.Items);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(typeof(Sequence));
        foreach (RegexBase item in Items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    protected override List<uint[]> CompileCore(bool reverse, bool fuzzy)
    {
        IEnumerable<RegexBase> seq = reverse ? Enumerable.Reverse(Items) : Items;

        List<uint[]> code = [];
        foreach (RegexBase s in seq)
        {
            code.AddRange(s.Compile(reverse, fuzzy));
        }

        return code;
    }

    /// <summary>Upstream <c>Sequence._flush_characters</c> (lines 3608-3634).</summary>
    private static void FlushCharacters(Info info, List<int> characters, int caseFlags, List<RegexBase> items)
    {
        if (characters.Count == 0)
        {
            return;
        }

        // Disregard case_flags if all of the characters are case-less.
        if ((caseFlags & RegexFlags.IgnoreCase) != 0 && !characters.Exists(c => ParseFunctions.IsCasedI(info, c)))
        {
            caseFlags = RegexFlags.NoCase;
        }

        if ((caseFlags & RegexFlags.FullIgnoreCase) == RegexFlags.FullIgnoreCase)
        {
            // Upstream splits the run into full-folding and simple-folding chunks with
            // _fix_full_casefold, which reads _regex.get_expand_on_folding().
            throw new NotImplementedException(
                "needs:case-folding - splitting a full-case-folded literal needs the Unicode tables (S09)"
            );
        }

        items.Add(
            characters.Count == 1
                ? new Character(characters[0], caseFlags: caseFlags)
                : new String(characters, caseFlags)
        );

        characters.Clear();
    }
}

/// <summary>
/// A capture group. Upstream <c>Group</c> (<c>upstream/regex/_regex_core.py</c> lines 3060-3140).
/// </summary>
internal sealed class Group : RegexBase
{
    private readonly Info _info;

    /// <summary>Initializes a capture group.</summary>
    /// <param name="info">The parse state, which the group reads its numbering from at compile time.</param>
    /// <param name="group">The group number, negative for a nested named group's private alias.</param>
    /// <param name="subpattern">What the group matches.</param>
    internal Group(Info info, int group, RegexBase subpattern)
    {
        _info = info;
        GroupNumber = group;
        Subpattern = subpattern;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Upstream's <c>_key</c> for a group is (class, group, subpattern), but <c>Subpattern</c> is
    /// reassigned by <c>pack_characters</c>, so only the immutable part is hashed. Equal groups
    /// still hash equal, which is all a hash has to promise.
    /// </remarks>
    public override int GetHashCode() => HashCode.Combine(typeof(Group), GroupNumber);

    /// <summary>The group number. Upstream <c>group</c>.</summary>
    internal int GroupNumber { get; }

    /// <summary>What the group matches. Upstream <c>subpattern</c>.</summary>
    internal RegexBase Subpattern { get; set; }

    /// <inheritdoc />
    internal override void FixGroups(string pattern, bool reverse, bool fuzzy)
    {
        _info.DefinedGroups[GroupNumber] = (this, reverse, fuzzy);
        Subpattern.FixGroups(pattern, reverse, fuzzy);
    }

    /// <inheritdoc />
    internal override RegexBase Optimise(Info info, bool reverse) =>
        new Group(_info, GroupNumber, Subpattern.Optimise(info, reverse));

    /// <inheritdoc />
    internal override RegexBase PackCharacters(Info info)
    {
        Subpattern = Subpattern.PackCharacters(info);
        return this;
    }

    /// <inheritdoc />
    internal override RegexBase RemoveCaptures() => Subpattern.RemoveCaptures();

    /// <inheritdoc />
    internal override bool IsAtomic() => Subpattern.IsAtomic();

    /// <inheritdoc />
    internal override bool CanBeAffix() => false;

    /// <inheritdoc />
    internal override bool ContainsGroup() => true;

    /// <inheritdoc />
    internal override HashSet<RegexBase?> GetFirstset(bool reverse) => Subpattern.GetFirstset(reverse);

    /// <inheritdoc />
    internal override bool HasSimpleStart() => Subpattern.HasSimpleStart();

    /// <inheritdoc />
    internal override long MaxWidth() => Subpattern.MaxWidth();

    /// <inheritdoc />
    internal override (long Offset, RegexBase? Required) GetRequiredString(bool reverse) =>
        Subpattern.GetRequiredString(reverse);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is Group other && GroupNumber == other.GroupNumber && Subpattern.Equals(other.Subpattern);

    /// <inheritdoc />
    protected override List<uint[]> CompileCore(bool reverse, bool fuzzy)
    {
        List<uint[]> code = [];

        int publicGroup = GroupNumber;
        int privateGroup = GroupNumber;
        if (privateGroup < 0)
        {
            publicGroup = _info.PrivateGroups[privateGroup];
            privateGroup = _info.GroupCount - privateGroup;
        }

        (int, bool, bool) key = (GroupNumber, reverse, fuzzy);
        bool hasRef = _info.CallRefs.TryGetValue(key, out int reference);
        if (hasRef)
        {
            code.Add([(uint)Opcode.CallRef, (uint)reference]);
        }

        code.Add([(uint)Opcode.Group, reverse ? 0u : 1u, (uint)privateGroup, (uint)publicGroup]);
        code.AddRange(Subpattern.Compile(reverse, fuzzy));
        code.Add([(uint)Opcode.End]);

        if (hasRef)
        {
            code.Add([(uint)Opcode.End]);
        }

        return code;
    }
}

/// <summary>
/// A greedy repeat, <c>a*</c> or <c>a{2,3}</c>. Upstream <c>GreedyRepeat</c>
/// (<c>upstream/regex/_regex_core.py</c> lines 2938-3028).
/// </summary>
internal class GreedyRepeat : RegexBase
{
    /// <summary>Initializes a repeat.</summary>
    /// <param name="subpattern">What is repeated.</param>
    /// <param name="minCount">The minimum number of repeats.</param>
    /// <param name="maxCount">The maximum, or <see langword="null"/> for unlimited.</param>
    internal GreedyRepeat(RegexBase subpattern, long minCount, long? maxCount)
    {
        Subpattern = subpattern;
        MinCount = minCount;
        MaxCount = maxCount;
    }

    /// <summary>What is repeated. Upstream <c>subpattern</c>.</summary>
    internal RegexBase Subpattern { get; set; }

    /// <summary>The minimum number of repeats. Upstream <c>min_count</c>.</summary>
    internal long MinCount { get; }

    /// <summary>
    /// The maximum number of repeats, or <see langword="null"/> for unlimited. Upstream
    /// <c>max_count</c>, whose <c>None</c> this keeps as <see langword="null"/> rather than
    /// collapsing to <see cref="RegexFlags.Unlimited"/>: <c>max_width</c> and
    /// <c>get_required_string</c> both branch on which of the two it is.
    /// </summary>
    internal long? MaxCount { get; }

    /// <summary>The opcode this repeat compiles to. Upstream <c>_opcode</c>.</summary>
    protected virtual Opcode RepeatOpcode => Opcode.GreedyRepeat;

    /// <inheritdoc />
    internal override void FixGroups(string pattern, bool reverse, bool fuzzy) =>
        Subpattern.FixGroups(pattern, reverse, fuzzy);

    /// <inheritdoc />
    internal override RegexBase Optimise(Info info, bool reverse) =>
        // Upstream's `type(self)(...)`, so a lazy or possessive repeat stays what it was.
        Recreate(Subpattern.Optimise(info, reverse));

    /// <inheritdoc />
    internal override RegexBase PackCharacters(Info info)
    {
        Subpattern = Subpattern.PackCharacters(info);
        return this;
    }

    /// <inheritdoc />
    internal override RegexBase RemoveCaptures()
    {
        Subpattern = Subpattern.RemoveCaptures();
        return this;
    }

    /// <inheritdoc />
    internal override bool IsAtomic() => MinCount == MaxCount && Subpattern.IsAtomic();

    /// <inheritdoc />
    internal override bool CanBeAffix() => false;

    /// <inheritdoc />
    internal override bool ContainsGroup() => Subpattern.ContainsGroup();

    /// <inheritdoc />
    internal override HashSet<RegexBase?> GetFirstset(bool reverse)
    {
        HashSet<RegexBase?> fs = Subpattern.GetFirstset(reverse);
        if (MinCount == 0)
        {
            fs.Add(null);
        }

        return fs;
    }

    /// <inheritdoc />
    internal override bool IsEmpty() => Subpattern.IsEmpty();

    /// <inheritdoc />
    internal override long MaxWidth() =>
        MaxCount is null ? RegexFlags.Unlimited : Widths.Multiply(Subpattern.MaxWidth(), MaxCount.Value);

    /// <inheritdoc />
    internal override (long Offset, RegexBase? Required) GetRequiredString(bool reverse)
    {
        long maxCount = MaxCount ?? RegexFlags.Unlimited;
        if (MinCount == 0)
        {
            return (Math.Min(Widths.Multiply(Subpattern.MaxWidth(), maxCount), RegexFlags.Unlimited), null);
        }

        (long ofs, RegexBase? req) = Subpattern.GetRequiredString(reverse);
        if (req is not null)
        {
            return (ofs, req);
        }

        return (Math.Min(Widths.Multiply(Subpattern.MaxWidth(), maxCount), RegexFlags.Unlimited), null);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is GreedyRepeat other
        && GetType() == other.GetType()
        && Subpattern.Equals(other.Subpattern)
        && MinCount == other.MinCount
        && MaxCount == other.MaxCount;

    /// <inheritdoc />
    /// <remarks>
    /// Upstream defines <c>__eq__</c> without <c>__hash__</c>, so a repeat is unhashable in Python;
    /// see <see cref="Branch.GetHashCode"/> for why this port supplies one anyway.
    /// <see cref="Subpattern"/> is reassigned by <c>pack_characters</c>, so only the immutable part
    /// is hashed - equal repeats still hash equal, which is all a hash has to promise.
    /// </remarks>
    public override int GetHashCode() => HashCode.Combine(GetType(), MinCount, MaxCount);

    /// <summary>Upstream's <c>type(self)(subpattern, self.min_count, self.max_count)</c>.</summary>
    /// <param name="subpattern">The new subpattern.</param>
    /// <returns>A repeat of the same kind over the new subpattern.</returns>
    protected virtual GreedyRepeat Recreate(RegexBase subpattern) => new(subpattern, MinCount, MaxCount);

    /// <summary>The repeat's own three words. Upstream's <c>repeat</c> local (lines 2981-2985).</summary>
    /// <returns>The opcode, the minimum and the maximum.</returns>
    protected uint[] RepeatCode() => [(uint)RepeatOpcode, (uint)MinCount, (uint)(MaxCount ?? RegexFlags.Unlimited)];

    /// <inheritdoc />
    protected override List<uint[]> CompileCore(bool reverse, bool fuzzy)
    {
        uint[] repeat = RepeatCode();

        List<uint[]> subpattern = Subpattern.Compile(reverse, fuzzy);
        if (subpattern.Count == 0)
        {
            return [];
        }

        return [repeat, .. subpattern, [(uint)Opcode.End]];
    }
}

/// <summary>
/// A lazy repeat, <c>a*?</c>. Upstream <c>LazyRepeat</c> (<c>upstream/regex/_regex_core.py</c>
/// lines 3146-3148).
/// </summary>
internal sealed class LazyRepeat : GreedyRepeat
{
    /// <summary>Initializes a lazy repeat.</summary>
    /// <param name="subpattern">What is repeated.</param>
    /// <param name="minCount">The minimum number of repeats.</param>
    /// <param name="maxCount">The maximum, or <see langword="null"/> for unlimited.</param>
    internal LazyRepeat(RegexBase subpattern, long minCount, long? maxCount)
        : base(subpattern, minCount, maxCount) { }

    /// <inheritdoc />
    protected override Opcode RepeatOpcode => Opcode.LazyRepeat;

    /// <inheritdoc />
    protected override GreedyRepeat Recreate(RegexBase subpattern) => new LazyRepeat(subpattern, MinCount, MaxCount);
}

/// <summary>
/// A possessive repeat, <c>a*+</c>. Upstream <c>PossessiveRepeat</c>
/// (<c>upstream/regex/_regex_core.py</c> lines 3030-3058).
/// </summary>
/// <remarks>
/// It keeps <c>GreedyRepeat</c>'s opcode and wraps the repeat in an <c>ATOMIC</c> instead.
/// </remarks>
internal sealed class PossessiveRepeat : GreedyRepeat
{
    /// <summary>Initializes a possessive repeat.</summary>
    /// <param name="subpattern">What is repeated.</param>
    /// <param name="minCount">The minimum number of repeats.</param>
    /// <param name="maxCount">The maximum, or <see langword="null"/> for unlimited.</param>
    internal PossessiveRepeat(RegexBase subpattern, long minCount, long? maxCount)
        : base(subpattern, minCount, maxCount) { }

    /// <inheritdoc />
    internal override bool IsAtomic() => true;

    /// <inheritdoc />
    protected override GreedyRepeat Recreate(RegexBase subpattern) =>
        new PossessiveRepeat(subpattern, MinCount, MaxCount);

    /// <inheritdoc />
    protected override List<uint[]> CompileCore(bool reverse, bool fuzzy)
    {
        List<uint[]> subpattern = Subpattern.Compile(reverse, fuzzy);
        if (subpattern.Count == 0)
        {
            return [];
        }

        return
        [
            [(uint)Opcode.Atomic],
            RepeatCode(),
            .. subpattern,
            [(uint)Opcode.End],
            [(uint)Opcode.End],
        ];
    }
}

/// <summary>
/// Arithmetic on the widths <c>max_width</c> reports.
/// </summary>
/// <remarks>
/// Upstream's widths are Python <c>int</c>s, which never overflow;
/// <c>(?:a{4294967294}){4294967294}</c> is a legal pattern whose product does not fit in
/// <see cref="long"/>. Every consumer either compares the result against
/// <see cref="RegexFlags.Unlimited"/> or takes the minimum of the two, so saturating at
/// <see cref="long.MaxValue"/> is indistinguishable from unbounded arithmetic, and far cheaper than
/// making every width a <c>BigInteger</c>.
/// </remarks>
internal static class Widths
{
    /// <summary>Multiplies two widths, saturating instead of overflowing.</summary>
    /// <param name="left">The first width.</param>
    /// <param name="right">The second width.</param>
    /// <returns>The product, or <see cref="long.MaxValue"/> if it would overflow.</returns>
    internal static long Multiply(long left, long right)
    {
        try
        {
            return checked(left * right);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    /// <summary>Adds two widths, saturating instead of overflowing.</summary>
    /// <param name="left">The first width.</param>
    /// <param name="right">The second width.</param>
    /// <returns>The sum, or <see cref="long.MaxValue"/> if it would overflow.</returns>
    internal static long Add(long left, long right)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }
}

/// <summary>
/// The bit flags a node writes into its opcode's flags word. Upstream
/// <c>upstream/regex/_regex_core.py</c> lines 1924-1929.
/// </summary>
internal static class NodeFlags
{
    /// <summary>Upstream <c>POSITIVE_OP</c>.</summary>
    internal const uint Positive = 0x1;

    /// <summary>Upstream <c>ZEROWIDTH_OP</c>.</summary>
    internal const uint Zerowidth = 0x2;

    /// <summary>Upstream <c>FUZZY_OP</c>.</summary>
    internal const uint Fuzzy = 0x4;

    /// <summary>Upstream <c>REVERSE_OP</c>.</summary>
    internal const uint Reverse = 0x8;

    /// <summary>Upstream <c>REQUIRED_OP</c>.</summary>
    internal const uint Required = 0x10;

    /// <summary>Upstream <c>ENCODING_OP_SHIFT</c>.</summary>
    internal const int EncodingShift = 5;
}
