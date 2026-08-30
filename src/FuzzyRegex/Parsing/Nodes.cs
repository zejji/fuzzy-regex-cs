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
            total += s.MaxWidth();
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
            offset += ofs;
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
