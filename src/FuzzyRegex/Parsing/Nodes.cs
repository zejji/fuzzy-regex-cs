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

    /// <summary>
    /// Rewrites a <b>character-set member</b> into a cheaper equivalent. Upstream's <c>optimise</c>
    /// again, called with its third argument.
    /// </summary>
    /// <param name="info">The parse state.</param>
    /// <param name="reverse">Whether the pattern is matched right to left.</param>
    /// <param name="inSet">Whether the node is being optimised as a member of another set.</param>
    /// <returns>The optimised node.</returns>
    /// <exception cref="NotSupportedException">
    /// This node is not one a character set can hold.
    /// </exception>
    /// <remarks>
    /// Upstream declares <c>in_set</c> on six classes only - <c>Character</c>, <c>Property</c>,
    /// <c>Range</c> and the four set types (lines 2608, 3333, 3394, 3828-3917) - so calling any
    /// other node's <c>optimise</c> with a third argument is a <c>TypeError</c>, whatever its
    /// value. That is reachable, not theoretical: <c>(?V1)[[\s\S]--a]</c> reduces its first
    /// operand to an <c>AnyAll</c>, and <c>SetDiff.optimise</c> then calls
    /// <c>items[0].with_flags(...).optimise(info, reverse, in_set)</c> on it, which upstream
    /// answers with "RegexBase.optimise() got an unexpected keyword argument 'in_set'" (measured
    /// 2026-08-30). Two separate methods reproduce that; one method with an ignored parameter
    /// would compile the pattern upstream rejects.
    /// </remarks>
    internal virtual RegexBase Optimise(Info info, bool reverse, bool inSet) =>
        throw new NotSupportedException(
            $"{GetType().Name}.optimise() got an unexpected keyword argument 'in_set'; "
                + "upstream would raise TypeError"
        );

    /// <summary>Whether the node matches a codepoint. Upstream <c>matches</c>.</summary>
    /// <param name="ch">The codepoint.</param>
    /// <returns><see langword="true"/> if it matches.</returns>
    /// <remarks>
    /// Upstream defines <c>matches</c> only on the nodes a character set can hold, so calling it on
    /// anything else is an <c>AttributeError</c>. <c>SetBase._handle_case_folding</c> and
    /// <c>SetBase.max_width</c> are its only callers.
    /// </remarks>
    internal virtual bool Matches(int ch) =>
        throw new NotSupportedException($"{GetType().Name} has no matches; upstream would raise AttributeError");

    /// <summary>
    /// Upstream's <c>_key</c> rendered as a string, with the class <i>name</i> in place of the
    /// class <i>object</i>, so that the two places a set of nodes becomes an ordered list can be
    /// sorted identically here and in the corpus recorder.
    /// </summary>
    /// <returns>The rendered key.</returns>
    /// <remarks>
    /// See PORTMAP's "Where we diverge": <c>RegexBase.__hash__</c> hashes a tuple beginning with
    /// the class object, whose hash is its address, so upstream's own order varies from one Python
    /// process to the next. <c>tools/record-compile-corpus.py</c>'s <c>_render_key</c> is this
    /// function; every rendered key is ASCII, so an ordinal sort is Python's <c>sorted</c>.
    /// <para>
    /// The default is upstream's default <c>_key</c>, the class alone (line 1943). Only a node that
    /// overrides <c>_key</c> overrides this.
    /// </para>
    /// </remarks>
    internal virtual string RenderKey() => GetType().Name;

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
    internal override string RenderKey() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"({GetType().Name},{Positive})");

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
/// A Unicode property, as <c>\p{...}</c>, <c>\P{...}</c>, <c>[[:alpha:]]</c> and the shorthand
/// classes <c>\d \D \h \s \S \w \W</c> all compile to. Upstream <c>Property</c>
/// (<c>upstream/regex/_regex_core.py</c> lines 3310-3364).
/// </summary>
/// <remarks>
/// <c>encoding</c> is deliberately not part of <c>_key</c> upstream, so a property is equal to the
/// same property with a different encoding tag even though the two compile to different flag
/// words. Kept, because <c>Branch</c>'s prefix splitting hoists on that equality.
/// </remarks>
internal sealed class Property : RegexBase
{
    private static readonly Dictionary<(int CaseFlags, bool Reverse), Opcode> _opcodes = new()
    {
        [(RegexFlags.NoCase, false)] = Opcode.Property,
        [(RegexFlags.IgnoreCase, false)] = Opcode.PropertyIgn,
        [(RegexFlags.FullCase, false)] = Opcode.Property,
        [(RegexFlags.FullIgnoreCase, false)] = Opcode.PropertyIgn,
        [(RegexFlags.NoCase, true)] = Opcode.PropertyRev,
        [(RegexFlags.IgnoreCase, true)] = Opcode.PropertyIgnRev,
        [(RegexFlags.FullCase, true)] = Opcode.PropertyRev,
        [(RegexFlags.FullIgnoreCase, true)] = Opcode.PropertyIgnRev,
    };

    /// <summary>Initializes a property node.</summary>
    /// <param name="value">The packed property code: the property id in the high 16 bits, the value id in the low.</param>
    /// <param name="positive">Whether the codepoint must have that value or must not.</param>
    /// <param name="caseFlags">The case flags in force.</param>
    /// <param name="zerowidth">Whether the node consumes nothing.</param>
    /// <param name="encoding">The encoding tag, one of <see cref="RegexFlags.AsciiEncoding"/> and friends.</param>
    internal Property(
        uint value,
        bool positive = true,
        int caseFlags = RegexFlags.NoCase,
        bool zerowidth = false,
        int encoding = 0
    )
    {
        Value = value;
        Positive = positive;
        CaseFlags = RegexFlags.CaseFlagsCombination(caseFlags);
        Zerowidth = zerowidth;
        Encoding = encoding;
    }

    /// <summary>The packed property code. Upstream <c>value</c>.</summary>
    internal uint Value { get; }

    /// <inheritdoc />
    internal override bool Positive { get; }

    /// <inheritdoc />
    internal override int CaseFlags { get; }

    /// <inheritdoc />
    internal override bool Zerowidth { get; }

    /// <summary>The encoding whose answer to the property this node wants. Upstream <c>encoding</c>.</summary>
    internal int Encoding { get; }

    /// <inheritdoc />
    internal override HashSet<RegexBase?> GetFirstset(bool reverse) => [this];

    /// <inheritdoc />
    internal override bool HasSimpleStart() => true;

    /// <inheritdoc />
    /// <remarks>Upstream <c>Property.optimise</c> (line 3333) takes <c>in_set</c> and ignores it.</remarks>
    internal override RegexBase Optimise(Info info, bool reverse, bool inSet) => this;

    /// <inheritdoc />
    internal override bool Matches(int ch) => Unicode.RegexModule.HasPropertyValue(Value, (uint)ch) == Positive;

    /// <inheritdoc />
    internal override long MaxWidth() => 1;

    /// <inheritdoc />
    internal override string RenderKey() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"({nameof(Property)},{Value},{Positive},{CaseFlags},{Zerowidth})"
        );

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is Property other
        && Value == other.Value
        && Positive == other.Positive
        && CaseFlags == other.CaseFlags
        && Zerowidth == other.Zerowidth;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(typeof(Property), Value, Positive, CaseFlags, Zerowidth);

    /// <inheritdoc />
    protected override RegexBase Rebuild(bool positive, int caseFlags, bool zerowidth) =>
        new Property(Value, positive, caseFlags, zerowidth, Encoding);

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

        flags |= (uint)Encoding << NodeFlags.EncodingShift;

        return
        [
            [(uint)_opcodes[(CaseFlags, reverse)], flags, Value],
        ];
    }
}

/// <summary>
/// A range of codepoints inside a character set, as <c>[a-z]</c>. Upstream <c>Range</c>
/// (<c>upstream/regex/_regex_core.py</c> lines 3372-3447).
/// </summary>
internal sealed class Range : RegexBase
{
    private static readonly Dictionary<(int CaseFlags, bool Reverse), Opcode> _opcodes = new()
    {
        [(RegexFlags.NoCase, false)] = Opcode.Range,
        [(RegexFlags.IgnoreCase, false)] = Opcode.RangeIgn,
        [(RegexFlags.FullCase, false)] = Opcode.Range,
        [(RegexFlags.FullIgnoreCase, false)] = Opcode.RangeIgn,
        [(RegexFlags.NoCase, true)] = Opcode.RangeRev,
        [(RegexFlags.IgnoreCase, true)] = Opcode.RangeIgnRev,
        [(RegexFlags.FullCase, true)] = Opcode.RangeRev,
        [(RegexFlags.FullIgnoreCase, true)] = Opcode.RangeIgnRev,
    };

    /// <summary>Initializes a codepoint range.</summary>
    /// <param name="lower">The first codepoint, inclusive.</param>
    /// <param name="upper">The last codepoint, inclusive.</param>
    /// <param name="positive">Whether the range matches its members or everything else.</param>
    /// <param name="caseFlags">The case flags in force.</param>
    /// <param name="zerowidth">Whether the node consumes nothing.</param>
    internal Range(
        int lower,
        int upper,
        bool positive = true,
        int caseFlags = RegexFlags.NoCase,
        bool zerowidth = false
    )
    {
        Lower = lower;
        Upper = upper;
        Positive = positive;
        CaseFlags = RegexFlags.CaseFlagsCombination(caseFlags);
        Zerowidth = zerowidth;
    }

    /// <summary>The first codepoint. Upstream <c>lower</c>.</summary>
    internal int Lower { get; }

    /// <summary>The last codepoint. Upstream <c>upper</c>.</summary>
    internal int Upper { get; }

    /// <inheritdoc />
    internal override bool Positive { get; }

    /// <inheritdoc />
    internal override int CaseFlags { get; }

    /// <inheritdoc />
    internal override bool Zerowidth { get; }

    /// <inheritdoc />
    /// <remarks>Python's default argument: <c>optimise(info, reverse)</c> is <c>in_set=False</c>.</remarks>
    internal override RegexBase Optimise(Info info, bool reverse) => Optimise(info, reverse, inSet: false);

    /// <inheritdoc />
    internal override RegexBase Optimise(Info info, bool reverse, bool inSet)
    {
        ArgumentNullException.ThrowIfNull(info);

        // Is the range case-sensitive?
        if (!Positive || (CaseFlags & RegexFlags.IgnoreCase) == 0 || inSet)
        {
            return this;
        }

        // Is full case-folding possible?
        if (
            (info.Flags & RegexFlags.Unicode) == 0
            || (CaseFlags & RegexFlags.FullIgnoreCase) != RegexFlags.FullIgnoreCase
        )
        {
            return this;
        }

        // Get the characters which expand to multiple codepoints on folding, and fold the ones in
        // the range. The order is upstream's table order, not set order, so it must reproduce
        // exactly.
        List<RegexBase> items = [];
        foreach (int ch in Unicode.RegexModule.GetExpandOnFolding().Where(ch => Lower <= ch && ch <= Upper))
        {
            items.Add(new String(Unicode.RegexModule.FoldCase(RegexFlags.FullCaseFolding, [ch]), CaseFlags));
        }

        if (items.Count == 0)
        {
            // We can fall back to simple case-folding.
            return this;
        }

        if (items.Count < Upper - Lower + 1)
        {
            // Not all the characters are covered by the full case-folding.
            items.Insert(0, this);
        }

        return new Branch(items);
    }

    /// <inheritdoc />
    internal override bool Matches(int ch) => (Lower <= ch && ch <= Upper) == Positive;

    /// <inheritdoc />
    internal override long MaxWidth() => 1;

    /// <inheritdoc />
    internal override string RenderKey() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"({nameof(Range)},{Lower},{Upper},{Positive},{CaseFlags},{Zerowidth})"
        );

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is Range other
        && Lower == other.Lower
        && Upper == other.Upper
        && Positive == other.Positive
        && CaseFlags == other.CaseFlags
        && Zerowidth == other.Zerowidth;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(typeof(Range), Lower, Upper, Positive, CaseFlags, Zerowidth);

    /// <inheritdoc />
    protected override RegexBase Rebuild(bool positive, int caseFlags, bool zerowidth) =>
        new Range(Lower, Upper, positive, caseFlags, zerowidth);

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

        return
        [
            [(uint)_opcodes[(CaseFlags, reverse)], flags, (uint)Lower, (uint)Upper],
        ];
    }
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

    /// <inheritdoc />
    /// <remarks>
    /// <c>AnyAll</c> is the one <c>Any</c> that overrides <c>_key</c>, to <c>(class, positive)</c>
    /// (line 2068); <see cref="Any"/> and <see cref="AnyU"/> keep the class alone.
    /// </remarks>
    internal override string RenderKey() => $"({nameof(AnyAll)},{true})";
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
            if (b is Character or Property or SetBase)
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

        // One of the two points PORTMAP's "Where we diverge" requires the members to be sorted at,
        // because a Python set of nodes has no stable order. The corpus recorder sorts by the same
        // rendered key.
        List<RegexBase> ordered = [.. items.OrderBy(i => i.RenderKey(), StringComparer.Ordinal)];

        RegexBase item = ordered.Count == 1 ? ordered[0] : new SetUnion(info, ordered).Optimise(info, reverse);

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

        int[] folded = Unicode.RegexModule.FoldCase(
            RegexFlags.FullCaseFolding,
            [.. items.Select(i => ((Character)i).Value)]
        );

        // Get the characters which expand to multiple codepoints on folding.
        return Unicode
            .RegexModule.GetExpandOnFolding()
            .Any(c => folded.SequenceEqual(Unicode.RegexModule.FoldCase(RegexFlags.FullCaseFolding, [c])));
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

        Folded =
            positive && (normalisedCaseFlags & RegexFlags.FullIgnoreCase) == RegexFlags.FullIgnoreCase
                ? Unicode.RegexModule.FoldCase(RegexFlags.FullCaseFolding, [value])
                : [value];
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
    /// <remarks>Upstream <c>Character.optimise</c> (line 2608) takes <c>in_set</c> and ignores it.</remarks>
    internal override RegexBase Optimise(Info info, bool reverse, bool inSet) => this;

    /// <inheritdoc />
    internal override bool Matches(int ch) => ch == Value == Positive;

    /// <inheritdoc />
    internal override long MaxWidth() => Folded.Length;

    /// <inheritdoc />
    internal override string RenderKey() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"({nameof(Character)},{Value},{Positive},{CaseFlags},{Zerowidth})"
        );

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

        if (Folded.Length > 1)
        {
            // The character expands on full case-folding.
            code = new Branch([code, new String(Folded, CaseFlags)]);
        }

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

        // Upstream folds one character at a time and concatenates, which is what FoldCase over the
        // whole run does: full folding is per-codepoint, with no context.
        FoldedCharacters =
            (normalisedCaseFlags & RegexFlags.FullIgnoreCase) == RegexFlags.FullIgnoreCase
                ? Unicode.RegexModule.FoldCase(RegexFlags.FullCaseFolding, Characters)
                : Characters;
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
    /// <remarks><c>GetType().Name</c>, not <c>String</c>: <see cref="Literal"/> inherits this key.</remarks>
    internal override string RenderKey() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"({GetType().Name},({string.Join(',', Characters)}),{CaseFlags})"
        );

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
            foreach (Literal literal in FixFullCasefold(characters))
            {
                int[] chars = literal.Characters;

                items.Add(
                    chars.Length == 1
                        ? new Character(chars[0], caseFlags: literal.CaseFlags)
                        : new String(chars, literal.CaseFlags)
                );
            }
        }
        else
        {
            items.Add(
                characters.Count == 1
                    ? new Character(characters[0], caseFlags: caseFlags)
                    : new String(characters, caseFlags)
            );
        }

        characters.Clear();
    }

    /// <summary>Upstream <c>Sequence._fix_full_casefold</c> (lines 3636-3668).</summary>
    /// <remarks>
    /// Splits a literal needing full case-folding into chunks that need it and chunks that can use
    /// simple case-folding, which is faster.
    /// <para>
    /// The chunk offsets are found in the <b>folded</b> text and then used to slice the
    /// <b>unfolded</b> characters, which are not the same length when a character expands. That is
    /// upstream's own arithmetic, and it works out because an expansion begins where its character
    /// does: <c>aß</c> folds to <c>ass</c>, <c>ss</c> is found at 1, and <c>characters[1:3]</c>
    /// clamps to just the <c>ß</c>. Python's slicing clamps, so this one does too.
    /// </para>
    /// </remarks>
    private static List<Literal> FixFullCasefold(List<int> characters)
    {
        // Get the characters which expand to multiple codepoints on folding, folded.
        List<int[]> expanded =
        [
            .. Unicode
                .RegexModule.GetExpandOnFolding()
                .Select(c => Unicode.RegexModule.FoldCase(RegexFlags.FullCaseFolding, [c])),
        ];

        int[] text = Unicode.PythonStr.Lower(Unicode.RegexModule.FoldCase(RegexFlags.FullCaseFolding, [.. characters]));

        List<(int Start, int End)> chunks = [];
        foreach (int[] e in expanded)
        {
            int found = Find(text, e, 0);

            while (found >= 0)
            {
                chunks.Add((found, found + e.Length));
                found = Find(text, e, found + 1);
            }
        }

        int pos = 0;
        List<Literal> literals = [];

        foreach ((int start, int end) in MergeChunks(chunks))
        {
            if (pos < start)
            {
                literals.Add(new Literal(PySlice(characters, pos, start), RegexFlags.IgnoreCase));
            }

            literals.Add(new Literal(PySlice(characters, start, end), RegexFlags.FullIgnoreCase));
            pos = end;
        }

        if (pos < characters.Count)
        {
            literals.Add(new Literal(PySlice(characters, pos, characters.Count), RegexFlags.IgnoreCase));
        }

        return literals;
    }

    /// <summary>Upstream <c>Sequence._merge_chunks</c> (lines 3670-3689).</summary>
    private static List<(int Start, int End)> MergeChunks(List<(int Start, int End)> chunks)
    {
        if (chunks.Count < 2)
        {
            return chunks;
        }

        // Python sorts a list of tuples lexicographically.
        chunks.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));

        (int start, int end) = chunks[0];
        List<(int Start, int End)> newChunks = [];

        foreach ((int s, int e) in chunks.Skip(1))
        {
            if (s <= end)
            {
                end = Math.Max(end, e);
            }
            else
            {
                newChunks.Add((start, end));
                (start, end) = (s, e);
            }
        }

        newChunks.Add((start, end));

        return newChunks;
    }

    /// <summary>Python's <c>str.find(sub, start)</c> over codepoints.</summary>
    private static int Find(int[] text, int[] sub, int start)
    {
        for (int i = Math.Max(start, 0); i + sub.Length <= text.Length; i++)
        {
            if (text.AsSpan(i, sub.Length).SequenceEqual(sub))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Python's <c>characters[start:end]</c>, which clamps rather than throwing.</summary>
    private static List<int> PySlice(List<int> characters, int start, int end)
    {
        int from = Math.Clamp(start, 0, characters.Count);
        int to = Math.Clamp(end, from, characters.Count);
        return characters.GetRange(from, to - from);
    }
}

/// <summary>
/// Base class for the four character-set nodes. Upstream <c>SetBase</c>
/// (<c>upstream/regex/_regex_core.py</c> lines 3715-3818).
/// </summary>
/// <remarks>
/// Upstream's <c>char_width</c> is not ported: it is set here and read only by the engine.
/// <c>__del__</c>, which drops the <c>info</c> reference to break a cycle for Python's collector,
/// has no counterpart either.
/// </remarks>
/// <param name="info">The parse state; the set reads the encoding flags off it when folding.</param>
/// <param name="items">The members.</param>
/// <param name="positive">Whether the set matches its members or everything else.</param>
/// <param name="caseFlags">The case flags in force.</param>
/// <param name="zerowidth">Whether the node consumes nothing.</param>
internal abstract class SetBase(
    Info info,
    IReadOnlyList<RegexBase> items,
    bool positive = true,
    int caseFlags = RegexFlags.NoCase,
    bool zerowidth = false
) : RegexBase
{
    /// <summary>The parse state. Upstream <c>info</c>.</summary>
    internal Info Info { get; } = info;

    /// <summary>The members. Upstream <c>items</c>, which <c>optimise</c> reassigns.</summary>
    internal List<RegexBase> Items { get; private protected set; } = [.. items];

    /// <inheritdoc />
    internal override bool Positive { get; } = positive;

    /// <inheritdoc />
    internal override int CaseFlags { get; } = RegexFlags.CaseFlagsCombination(caseFlags);

    /// <inheritdoc />
    internal override bool Zerowidth { get; } = zerowidth;

    /// <summary>The opcode table for this set operator. Upstream <c>_opcode</c>.</summary>
    protected abstract IReadOnlyDictionary<(int CaseFlags, bool Reverse), Opcode> Opcodes { get; }

    /// <inheritdoc />
    /// <remarks>Python's default argument: <c>optimise(info, reverse)</c> is <c>in_set=False</c>.</remarks>
    internal override RegexBase Optimise(Info info, bool reverse) => Optimise(info, reverse, inSet: false);

    /// <inheritdoc />
    internal override HashSet<RegexBase?> GetFirstset(bool reverse) => [this];

    /// <inheritdoc />
    internal override bool HasSimpleStart() => true;

    /// <inheritdoc />
    internal override long MaxWidth()
    {
        // Is the set case-sensitive?
        if (!Positive || (CaseFlags & RegexFlags.IgnoreCase) == 0)
        {
            return 1;
        }

        // Is full case-folding possible?
        if (
            (Info.Flags & RegexFlags.Unicode) == 0
            || (CaseFlags & RegexFlags.FullIgnoreCase) != RegexFlags.FullIgnoreCase
        )
        {
            return 1;
        }

        // Get the folded characters in the set.
        HashSet<string> seen = [];
        long widest = 0;
        foreach (int ch in Unicode.RegexModule.GetExpandOnFolding().Where(Matches))
        {
            int[] folded = Unicode.RegexModule.FoldCase(RegexFlags.FullCaseFolding, [ch]);
            if (seen.Add(string.Join(',', folded)))
            {
                widest = Math.Max(widest, folded.Length);
            }
        }

        return seen.Count == 0 ? 1 : widest;
    }

    /// <inheritdoc />
    internal override string RenderKey() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"({GetType().Name},({string.Join(',', Items.Select(i => i.RenderKey()))}),{Positive},{CaseFlags},{Zerowidth})"
        );

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is SetBase other
        && GetType() == other.GetType()
        && Positive == other.Positive
        && CaseFlags == other.CaseFlags
        && Zerowidth == other.Zerowidth
        && Items.SequenceEqual(other.Items);

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="Items"/> is reassigned by <c>optimise</c>, so hashing it would break the "a hash
    /// does not change" contract for a node already sitting in a set - which is exactly where set
    /// nodes go (<c>_check_firstset</c>, <c>Branch._reduce_to_set</c>). The immutable part is
    /// hashed instead, as <see cref="Branch"/> does; <see cref="Equals(object)"/> still decides.
    /// </remarks>
    public override int GetHashCode() => HashCode.Combine(GetType(), Positive, CaseFlags, Zerowidth);

    /// <summary>Upstream's <c>type(self)(...)</c>, which rebuilds the same set operator.</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="items">The members.</param>
    /// <param name="positive">The new sense.</param>
    /// <param name="caseFlags">The new case flags.</param>
    /// <param name="zerowidth">The new zero-width setting.</param>
    /// <returns>The new set.</returns>
    private protected abstract SetBase Recreate(
        Info info,
        IReadOnlyList<RegexBase> items,
        bool positive,
        int caseFlags,
        bool zerowidth
    );

    /// <inheritdoc />
    protected override RegexBase Rebuild(bool positive, int caseFlags, bool zerowidth) =>
        Recreate(Info, Items, positive, caseFlags, zerowidth).Optimise(Info, reverse: false);

    /// <summary>Upstream <c>SetBase._handle_case_folding</c> (lines 3762-3790).</summary>
    /// <param name="info">The parse state; upstream takes it and reads <c>self.info</c> instead.</param>
    /// <param name="inSet">Whether this set is itself a member of another set.</param>
    /// <returns>This set, or a branch of it and its folded expansions.</returns>
    protected RegexBase HandleCaseFolding(Info info, bool inSet)
    {
        _ = info;

        // Is the set case-sensitive?
        if (!Positive || (CaseFlags & RegexFlags.IgnoreCase) == 0 || inSet)
        {
            return this;
        }

        // Is full case-folding possible?
        if (
            (Info.Flags & RegexFlags.Unicode) == 0
            || (CaseFlags & RegexFlags.FullIgnoreCase) != RegexFlags.FullIgnoreCase
        )
        {
            return this;
        }

        // Get the folded characters in the set. Upstream's table order, not set order. Upstream
        // calls this list `items`, which the primary constructor's parameter now owns.
        List<RegexBase> expansions = [];
        HashSet<string> seen = [];
        foreach (int ch in Unicode.RegexModule.GetExpandOnFolding().Where(Matches))
        {
            int[] folded = Unicode.RegexModule.FoldCase(RegexFlags.FullCaseFolding, [ch]);
            if (seen.Add(string.Join(',', folded)))
            {
                expansions.Add(new String(folded, CaseFlags));
            }
        }

        if (expansions.Count == 0)
        {
            // We can fall back to simple case-folding.
            return this;
        }

        return new Branch([this, .. expansions]);
    }

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

        List<uint[]> code =
        [
            [(uint)Opcodes[(CaseFlags, reverse)], flags],
        ];
        foreach (RegexBase m in Items)
        {
            code.AddRange(m.Compile());
        }

        code.Add([(uint)Opcode.End]);

        return code;
    }
}

/// <summary>
/// <c>[x--y]</c>, the version 1 set difference. Upstream <c>SetDiff</c>
/// (<c>upstream/regex/_regex_core.py</c> lines 3820-3844).
/// </summary>
internal sealed class SetDiff : SetBase
{
    private static readonly Dictionary<(int CaseFlags, bool Reverse), Opcode> _opcodes = new()
    {
        [(RegexFlags.NoCase, false)] = Opcode.SetDiff,
        [(RegexFlags.IgnoreCase, false)] = Opcode.SetDiffIgn,
        [(RegexFlags.FullCase, false)] = Opcode.SetDiff,
        [(RegexFlags.FullIgnoreCase, false)] = Opcode.SetDiffIgn,
        [(RegexFlags.NoCase, true)] = Opcode.SetDiffRev,
        [(RegexFlags.IgnoreCase, true)] = Opcode.SetDiffIgnRev,
        [(RegexFlags.FullCase, true)] = Opcode.SetDiffRev,
        [(RegexFlags.FullIgnoreCase, true)] = Opcode.SetDiffIgnRev,
    };

    /// <summary>Initializes a set difference.</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="items">The members.</param>
    /// <param name="positive">Whether the set matches its members or everything else.</param>
    /// <param name="caseFlags">The case flags in force.</param>
    /// <param name="zerowidth">Whether the node consumes nothing.</param>
    internal SetDiff(
        Info info,
        IReadOnlyList<RegexBase> items,
        bool positive = true,
        int caseFlags = RegexFlags.NoCase,
        bool zerowidth = false
    )
        : base(info, items, positive, caseFlags, zerowidth) { }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<(int CaseFlags, bool Reverse), Opcode> Opcodes => _opcodes;

    /// <inheritdoc />
    internal override RegexBase Optimise(Info info, bool reverse, bool inSet)
    {
        List<RegexBase> items = Items;
        if (items.Count > 2)
        {
            items = [items[0], new SetUnion(info, items.GetRange(1, items.Count - 1))];
        }

        if (items.Count == 1)
        {
            return items[0].WithFlags(caseFlags: CaseFlags, zerowidth: Zerowidth).Optimise(info, reverse, inSet);
        }

        Items = [.. items.Select(m => m.Optimise(info, reverse, inSet: true))];

        return HandleCaseFolding(info, inSet);
    }

    /// <inheritdoc />
    internal override bool Matches(int ch) => (Items[0].Matches(ch) && !Items[1].Matches(ch)) == Positive;

    /// <inheritdoc />
    private protected override SetBase Recreate(
        Info info,
        IReadOnlyList<RegexBase> items,
        bool positive,
        int caseFlags,
        bool zerowidth
    ) => new SetDiff(info, items, positive, caseFlags, zerowidth);
}

/// <summary>
/// <c>[x&amp;&amp;y]</c>, the version 1 set intersection. Upstream <c>SetInter</c>
/// (<c>upstream/regex/_regex_core.py</c> lines 3846-3874).
/// </summary>
internal sealed class SetInter : SetBase
{
    private static readonly Dictionary<(int CaseFlags, bool Reverse), Opcode> _opcodes = new()
    {
        [(RegexFlags.NoCase, false)] = Opcode.SetInter,
        [(RegexFlags.IgnoreCase, false)] = Opcode.SetInterIgn,
        [(RegexFlags.FullCase, false)] = Opcode.SetInter,
        [(RegexFlags.FullIgnoreCase, false)] = Opcode.SetInterIgn,
        [(RegexFlags.NoCase, true)] = Opcode.SetInterRev,
        [(RegexFlags.IgnoreCase, true)] = Opcode.SetInterIgnRev,
        [(RegexFlags.FullCase, true)] = Opcode.SetInterRev,
        [(RegexFlags.FullIgnoreCase, true)] = Opcode.SetInterIgnRev,
    };

    /// <summary>Initializes a set intersection.</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="items">The members.</param>
    /// <param name="positive">Whether the set matches its members or everything else.</param>
    /// <param name="caseFlags">The case flags in force.</param>
    /// <param name="zerowidth">Whether the node consumes nothing.</param>
    internal SetInter(
        Info info,
        IReadOnlyList<RegexBase> items,
        bool positive = true,
        int caseFlags = RegexFlags.NoCase,
        bool zerowidth = false
    )
        : base(info, items, positive, caseFlags, zerowidth) { }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<(int CaseFlags, bool Reverse), Opcode> Opcodes => _opcodes;

    /// <inheritdoc />
    internal override RegexBase Optimise(Info info, bool reverse, bool inSet)
    {
        List<RegexBase> items = [];
        foreach (RegexBase item in Items)
        {
            RegexBase m = item.Optimise(info, reverse, inSet: true);
            if (m is SetInter nested && nested.Positive)
            {
                // Intersection in intersection.
                items.AddRange(nested.Items);
            }
            else
            {
                items.Add(m);
            }
        }

        if (items.Count == 1)
        {
            return items[0].WithFlags(caseFlags: CaseFlags, zerowidth: Zerowidth).Optimise(info, reverse, inSet);
        }

        Items = items;

        return HandleCaseFolding(info, inSet);
    }

    /// <inheritdoc />
    internal override bool Matches(int ch) => Items.TrueForAll(i => i.Matches(ch)) == Positive;

    /// <inheritdoc />
    private protected override SetBase Recreate(
        Info info,
        IReadOnlyList<RegexBase> items,
        bool positive,
        int caseFlags,
        bool zerowidth
    ) => new SetInter(info, items, positive, caseFlags, zerowidth);
}

/// <summary>
/// <c>[x~~y]</c>, the version 1 symmetric difference. Upstream <c>SetSymDiff</c>
/// (<c>upstream/regex/_regex_core.py</c> lines 3876-3907).
/// </summary>
internal sealed class SetSymDiff : SetBase
{
    private static readonly Dictionary<(int CaseFlags, bool Reverse), Opcode> _opcodes = new()
    {
        [(RegexFlags.NoCase, false)] = Opcode.SetSymDiff,
        [(RegexFlags.IgnoreCase, false)] = Opcode.SetSymDiffIgn,
        [(RegexFlags.FullCase, false)] = Opcode.SetSymDiff,
        [(RegexFlags.FullIgnoreCase, false)] = Opcode.SetSymDiffIgn,
        [(RegexFlags.NoCase, true)] = Opcode.SetSymDiffRev,
        [(RegexFlags.IgnoreCase, true)] = Opcode.SetSymDiffIgnRev,
        [(RegexFlags.FullCase, true)] = Opcode.SetSymDiffRev,
        [(RegexFlags.FullIgnoreCase, true)] = Opcode.SetSymDiffIgnRev,
    };

    /// <summary>Initializes a symmetric difference.</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="items">The members.</param>
    /// <param name="positive">Whether the set matches its members or everything else.</param>
    /// <param name="caseFlags">The case flags in force.</param>
    /// <param name="zerowidth">Whether the node consumes nothing.</param>
    internal SetSymDiff(
        Info info,
        IReadOnlyList<RegexBase> items,
        bool positive = true,
        int caseFlags = RegexFlags.NoCase,
        bool zerowidth = false
    )
        : base(info, items, positive, caseFlags, zerowidth) { }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<(int CaseFlags, bool Reverse), Opcode> Opcodes => _opcodes;

    /// <inheritdoc />
    internal override RegexBase Optimise(Info info, bool reverse, bool inSet)
    {
        List<RegexBase> items = [];
        foreach (RegexBase item in Items)
        {
            RegexBase m = item.Optimise(info, reverse, inSet: true);
            if (m is SetSymDiff nested && nested.Positive)
            {
                // Symmetric difference in symmetric difference.
                items.AddRange(nested.Items);
            }
            else
            {
                items.Add(m);
            }
        }

        if (items.Count == 1)
        {
            return items[0].WithFlags(caseFlags: CaseFlags, zerowidth: Zerowidth).Optimise(info, reverse, inSet);
        }

        Items = items;

        return HandleCaseFolding(info, inSet);
    }

    /// <inheritdoc />
    internal override bool Matches(int ch)
    {
        bool m = false;
        foreach (RegexBase i in Items)
        {
            m = m != i.Matches(ch);
        }

        return m == Positive;
    }

    /// <inheritdoc />
    private protected override SetBase Recreate(
        Info info,
        IReadOnlyList<RegexBase> items,
        bool positive,
        int caseFlags,
        bool zerowidth
    ) => new SetSymDiff(info, items, positive, caseFlags, zerowidth);
}

/// <summary>
/// An ordinary character set, <c>[abc]</c>, and the explicit union <c>[x||y]</c>. Upstream
/// <c>SetUnion</c> (<c>upstream/regex/_regex_core.py</c> lines 3909-3985).
/// </summary>
internal sealed class SetUnion : SetBase
{
    private static readonly Dictionary<(int CaseFlags, bool Reverse), Opcode> _opcodes = new()
    {
        [(RegexFlags.NoCase, false)] = Opcode.SetUnion,
        [(RegexFlags.IgnoreCase, false)] = Opcode.SetUnionIgn,
        [(RegexFlags.FullCase, false)] = Opcode.SetUnion,
        [(RegexFlags.FullIgnoreCase, false)] = Opcode.SetUnionIgn,
        [(RegexFlags.NoCase, true)] = Opcode.SetUnionRev,
        [(RegexFlags.IgnoreCase, true)] = Opcode.SetUnionIgnRev,
        [(RegexFlags.FullCase, true)] = Opcode.SetUnionRev,
        [(RegexFlags.FullIgnoreCase, true)] = Opcode.SetUnionIgnRev,
    };

    /// <summary>Initializes a set union.</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="items">The members.</param>
    /// <param name="positive">Whether the set matches its members or everything else.</param>
    /// <param name="caseFlags">The case flags in force.</param>
    /// <param name="zerowidth">Whether the node consumes nothing.</param>
    internal SetUnion(
        Info info,
        IReadOnlyList<RegexBase> items,
        bool positive = true,
        int caseFlags = RegexFlags.NoCase,
        bool zerowidth = false
    )
        : base(info, items, positive, caseFlags, zerowidth) { }

    /// <inheritdoc />
    protected override IReadOnlyDictionary<(int CaseFlags, bool Reverse), Opcode> Opcodes => _opcodes;

    /// <inheritdoc />
    internal override RegexBase Optimise(Info info, bool reverse, bool inSet)
    {
        List<RegexBase> items = [];
        foreach (RegexBase item in Items)
        {
            RegexBase m = item.Optimise(info, reverse, inSet: true);
            if (m is SetUnion nested && nested.Positive)
            {
                // Union in union.
                items.AddRange(nested.Items);
            }
            else if (m is AnyAll)
            {
                return new AnyAll();
            }
            else
            {
                items.Add(m);
            }
        }

        // Are there complementary properties?
        HashSet<(uint Value, int CaseFlags, bool Zerowidth)> negative = [];
        HashSet<(uint Value, int CaseFlags, bool Zerowidth)> positive = [];

        foreach (RegexBase m in items)
        {
            if (m is Property property)
            {
                (property.Positive ? positive : negative).Add((property.Value, property.CaseFlags, property.Zerowidth));
            }
        }

        if (negative.Overlaps(positive))
        {
            return new AnyAll();
        }

        if (items.Count == 1)
        {
            RegexBase i = items[0];
            return i.WithFlags(positive: i.Positive == Positive, caseFlags: CaseFlags, zerowidth: Zerowidth)
                .Optimise(info, reverse, inSet);
        }

        Items = items;

        return HandleCaseFolding(info, inSet);
    }

    /// <inheritdoc />
    internal override bool Matches(int ch) => Items.Exists(i => i.Matches(ch)) == Positive;

    /// <inheritdoc />
    private protected override SetBase Recreate(
        Info info,
        IReadOnlyList<RegexBase> items,
        bool positive,
        int caseFlags,
        bool zerowidth
    ) => new SetUnion(info, items, positive, caseFlags, zerowidth);

    /// <inheritdoc />
    /// <remarks>
    /// Upstream buckets the members' characters by sense into a <c>defaultdict(list)</c> and then
    /// iterates it, so the two buckets come out in the order they were first created, not
    /// positive-then-negative - and a set of one negated member followed by positive ones compiles
    /// differently from the other way round. A list of buckets keeps that order by construction;
    /// <see cref="Dictionary{TKey, TValue}"/>'s enumeration order is not part of its contract.
    /// </remarks>
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

        List<(bool Positive, List<int> Values)> characters = [];
        List<RegexBase> others = [];
        foreach (RegexBase m in Items)
        {
            if (m is Character character)
            {
                int bucket = characters.FindIndex(b => b.Positive == character.Positive);
                if (bucket < 0)
                {
                    characters.Add((character.Positive, []));
                    bucket = characters.Count - 1;
                }

                characters[bucket].Values.Add(character.Value);
            }
            else
            {
                others.Add(m);
            }
        }

        List<uint[]> code =
        [
            [(uint)Opcodes[(CaseFlags, reverse)], flags],
        ];

        foreach ((bool positive, List<int> values) in characters)
        {
            uint memberFlags = positive ? NodeFlags.Positive : 0;
            if (values.Count == 1)
            {
                code.Add([(uint)Opcode.Character, memberFlags, (uint)values[0]]);
            }
            else
            {
                uint[] word = new uint[3 + values.Count];
                word[0] = (uint)Opcode.String;
                word[1] = memberFlags;
                word[2] = (uint)values.Count;
                for (int i = 0; i < values.Count; i++)
                {
                    word[3 + i] = (uint)values[i];
                }

                code.Add(word);
            }
        }

        foreach (RegexBase m in others)
        {
            code.AddRange(m.Compile());
        }

        code.Add([(uint)Opcode.End]);

        return code;
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
