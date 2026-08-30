namespace Fuzzy.Text.RegularExpressions.Parsing;

/// <summary>
/// Info about the regular expression being parsed: the flags in force, the group bookkeeping and
/// the named lists. Port of <c>Info</c> (<c>upstream/regex/_regex_core.py</c> lines 4356-4419).
/// </summary>
/// <remarks>
/// <c>char_type</c> is not ported; this port is <c>char</c>-based. Everything else is upstream's,
/// including the mutability: the parse functions read and write these fields as they descend,
/// which is why a <c>Group</c> node holds a reference back to its <c>Info</c>.
/// </remarks>
internal sealed class Info
{
    /// <summary>Initializes the parse state for one attempt at one pattern.</summary>
    /// <param name="flags">The flags to start from, already including any global flags a previous attempt discovered.</param>
    /// <param name="kwargs">The caller's named lists, keyed by name. Upstream's keyword arguments.</param>
    /// <param name="defaultVersion">
    /// The version to assume when the flags name none. Upstream reads the module global
    /// <c>DEFAULT_VERSION</c> here.
    /// </param>
    internal Info(int flags, IReadOnlyDictionary<string, IReadOnlyList<string>> kwargs, int defaultVersion)
    {
        DefaultVersion = defaultVersion;
        flags |= RegexFlags.DefaultFlags(
            (flags & RegexFlags.AllVersions) != 0 ? flags & RegexFlags.AllVersions : defaultVersion
        );
        Flags = flags;
        GlobalFlags = flags;
        InlineLocale = false;
        Kwargs = kwargs;
    }

    /// <summary>The flags in force at the current point in the parse. Upstream <c>info.flags</c>.</summary>
    internal int Flags { get; set; }

    /// <summary>The global flags accumulated so far. Upstream <c>info.global_flags</c>.</summary>
    internal int GlobalFlags { get; set; }

    /// <summary>Whether the pattern set the <c>LOCALE</c> flag inline. Upstream <c>info.inline_locale</c>.</summary>
    internal bool InlineLocale { get; set; }

    /// <summary>The caller's named lists. Upstream <c>info.kwargs</c>.</summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<string>> Kwargs { get; }

    /// <summary>
    /// The encoding guessed from the pattern's type, always <see cref="RegexFlags.Unicode"/> here
    /// because this port has no <c>bytes</c> patterns. Upstream <c>info.guess_encoding</c>, set by
    /// <c>_main._compile</c> (<c>upstream/regex/_main.py</c> lines 520-523).
    /// </summary>
    internal int GuessEncoding { get; set; }

    /// <summary>The version this pattern gets when its flags name none.</summary>
    internal int DefaultVersion { get; }

    /// <summary>How many capture groups the pattern has. Upstream <c>info.group_count</c>.</summary>
    internal int GroupCount { get; set; }

    /// <summary>Group name to group number. Upstream <c>info.group_index</c>.</summary>
    internal Dictionary<string, int> GroupIndex { get; } = new(StringComparer.Ordinal);

    /// <summary>Group number to group name. Upstream <c>info.group_name</c>.</summary>
    internal Dictionary<int, string> GroupName { get; } = [];

    /// <summary>
    /// Each distinct (named list name, case flags) pair used by the pattern, mapped to the index
    /// baked into the bytecode. Upstream <c>info.named_lists_used</c>.
    /// </summary>
    internal Dictionary<(string Name, int CaseFlags), int> NamedListsUsed { get; } = [];

    /// <summary>The groups whose closing parenthesis has not been read yet. Upstream <c>info.open_groups</c>.</summary>
    internal List<int> OpenGroups { get; } = [];

    /// <summary>How many times each group is currently open. Upstream <c>info.open_group_count</c>.</summary>
    internal Dictionary<int, int> OpenGroupCount { get; } = [];

    /// <summary>
    /// Each group's defining node, with the reverse and fuzzy flags it was defined under. Upstream
    /// <c>info.defined_groups</c>.
    /// </summary>
    internal Dictionary<int, (Group Group, bool Reverse, bool Fuzzy)> DefinedGroups { get; } = [];

    /// <summary>The group calls found in the pattern. Upstream <c>info.group_calls</c>.</summary>
    internal List<(RegexBase Call, bool Reverse, bool Fuzzy)> GroupCalls { get; } = [];

    /// <summary>
    /// Negative alias to real group number, for a named group nested inside itself. Upstream
    /// <c>info.private_groups</c>.
    /// </summary>
    internal Dictionary<int, int> PrivateGroups { get; } = [];

    /// <summary>
    /// The reference number each distinct group call resolves to. Upstream <c>info.call_refs</c>,
    /// set by <c>_check_group_features</c>.
    /// </summary>
    internal Dictionary<(int Group, bool Reverse, bool Fuzzy), int> CallRefs { get; set; } = [];

    /// <summary>
    /// Extra copies of groups that a call needs under different reverse/fuzzy settings. Upstream
    /// <c>info.additional_groups</c>.
    /// </summary>
    internal List<(RegexBase Group, bool Reverse, bool Fuzzy)> AdditionalGroups { get; set; } = [];

    /// <summary>Opens a capture group, assigning its number. Upstream <c>open_group</c>.</summary>
    /// <param name="name">The group's name, or <see langword="null"/> for an unnamed group.</param>
    /// <returns>The group number, negative for a nested named group's private alias.</returns>
    internal int OpenGroup(string? name = null)
    {
        if (name is null || !GroupIndex.TryGetValue(name, out int group))
        {
            while (true)
            {
                GroupCount++;
                if (name is null || !GroupName.ContainsKey(GroupCount))
                {
                    break;
                }
            }

            group = GroupCount;

            // Upstream's `if name:` - the empty string is falsy in Python, so a group named "" is
            // not recorded. parse_name rejects it before we get here, but the shape is upstream's.
            if (!string.IsNullOrEmpty(name))
            {
                GroupIndex[name] = group;
                GroupName[group] = name;
            }
        }

        if (OpenGroups.Contains(group))
        {
            // A nested named group. Give it a private, initially negative, number until a proper
            // one can be assigned.
            int groupAlias = -(PrivateGroups.Count + 1);
            PrivateGroups[groupAlias] = group;
            group = groupAlias;
        }

        OpenGroups.Add(group);
        OpenGroupCount[group] = OpenGroupCount.GetValueOrDefault(group) + 1;

        return group;
    }

    /// <summary>Closes the innermost open group. Upstream <c>close_group</c>.</summary>
    internal void CloseGroup() => OpenGroups.RemoveAt(OpenGroups.Count - 1);

    /// <summary>
    /// Whether a reference names a group that is still open. Upstream <c>is_open_group</c>: in
    /// version 1 a reference may name an open group, so this always reports false there.
    /// </summary>
    /// <param name="name">The group name or number, as written in the pattern.</param>
    /// <returns><see langword="true"/> if the reference must be rejected as circular.</returns>
    internal bool IsOpenGroup(string name)
    {
        int version = (Flags & RegexFlags.AllVersions) != 0 ? Flags & RegexFlags.AllVersions : DefaultVersion;
        if (version == RegexFlags.Version1)
        {
            return false;
        }

        if (ParseFunctions.IsDigitName(name))
        {
            // The same Python int() as ParseName and the three group-resolving nodes: unbounded,
            // and accepting any Unicode decimal digit. int.Parse threw OverflowException out
            // through parse_escape's catch, which is neither upstream's error nor the backreference
            // the delimited path is supposed to reach, and BigInteger.Parse threw FormatException
            // on `\g<١>`, which upstream resolves to group 1. TryParseGroupNumber saturates at
            // int.MaxValue, and a number that large is never an open group either way.
            //
            // A false return here also covers the `²` case - a str.isdigit digit with no decimal
            // value - where upstream would raise ValueError. Unreachable: parse_name converts the
            // same name first and rejects it before this is called.
            return ParseFunctions.TryParseGroupNumber(name, out int number) && OpenGroups.Contains(number);
        }

        // Upstream's dict.get returns None when the name is unknown, and None is never in
        // open_groups.
        return GroupIndex.TryGetValue(name, out int group) && OpenGroups.Contains(group);
    }
}
