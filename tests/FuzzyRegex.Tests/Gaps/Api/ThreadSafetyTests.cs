using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// S52b. The design spec's "runtime discipline" promises what upstream and the built-in
/// <see cref="System.Text.RegularExpressions.Regex"/> promise - a compiled pattern is immutable and
/// safe to share between threads, and everything mutable lives in per-call state. These tests are
/// the proof, and they are PERMANENT.
/// </summary>
/// <remarks>
/// <para>
/// <b>They constrain Phase 7 directly.</b> Optimisation is where caches appear - start
/// optimisations, required-string prefilters, interpreter state - and it is exactly where .NET's
/// own <c>Regex</c> had to make thread safety deliberate. A Phase 7 slice that turns one of these
/// red has introduced shared mutable state reachable from a compiled pattern; the fix is to remove
/// it or to publish it atomically, never to widen <see cref="_patternGraphAllowlist"/>.
/// </para>
/// <para>
/// <b>Two tests, two different questions.</b>
/// <see cref="Every_field_reachable_from_a_compiled_pattern_is_write_once_or_allowlisted"/> asks
/// what <i>could</i> be written after construction, and is the ratchet: a new mutable field fails
/// it on the day it appears.
/// <see cref="Matching_writes_nothing_reachable_from_a_compiled_pattern"/> asks whether anything
/// <i>was</i>, by comparing a full value snapshot of the graph before and after the whole workload,
/// and it is what justifies every allowlist entry at once - each is written by the compiler before
/// the pattern is published and by nothing afterwards.
/// </para>
/// <para>
/// The .NET contract these mirror, quoted from
/// <c>learn.microsoft.com/dotnet/standard/base-types/best-practices-regex</c> (fetched 2026-09-16):
/// "The Regex class itself is thread safe and immutable (read-only). That is, Regex objects can be
/// created on any thread and shared between threads; matching methods can be called from any thread
/// and never alter any global state." And the caveat this port does not inherit: "However, result
/// objects (Match and MatchCollection) returned by Regex should be used on a single thread.
/// Although many of these objects are logically immutable, their implementations could delay
/// computation of some results to improve performance, and as a result, callers must serialize
/// access to them."
/// </para>
/// </remarks>
public sealed class ThreadSafetyTests
{
    /// <summary>The library assembly, which is what every rule below is scoped to.</summary>
    private static readonly Assembly _library = typeof(FuzzyRegex).Assembly;

    /// <summary>
    /// Fields reachable from a compiled pattern that are not <c>readonly</c>, each written by the
    /// compiler before the pattern is published and by nothing afterwards.
    /// </summary>
    /// <remarks>
    /// Every entry's justification is proved en masse, not asserted, by
    /// <see cref="Matching_writes_nothing_reachable_from_a_compiled_pattern"/>: it snapshots the
    /// whole graph, runs every family of the workload, and compares. An entry whose field the
    /// engine did write would show up there as a changed path.
    /// </remarks>
    private static readonly IReadOnlySet<string> _patternGraphAllowlist = BuildPatternGraphAllowlist();

    /// <summary>
    /// Every library-declared field reachable from a compiled pattern that is neither
    /// <c>readonly</c> nor <c>init</c>-only, which is the set both rules below are about.
    /// </summary>
    /// <remarks>
    /// S53 pulled this out of the two tests that had a copy each, so that the non-vacuity guard
    /// lives in one place. Both rules are subset assertions and BOTH of them pass if this comes
    /// back empty - one because an empty set is a subset of the allowlist, the other because the
    /// allowlist would then be entirely stale, which is at least loud. The guard is not
    /// theoretical: in the published Native AOT binary this returned NOTHING, because the trimmer
    /// does not keep the property-accessor metadata
    /// <c>ObjectGraph.IsWriteOnceAfterConstruction</c> reads, so every field looked init-only.
    /// </remarks>
    /// <summary>
    /// Skips the caller when <see cref="ObjectGraph.Walk"/> cannot run, which is any Native AOT
    /// binary. S53 measured what happens there, and it is not a fixable hazard in this port:
    /// </summary>
    /// <remarks>
    /// <para>
    /// The walk descends into BCL objects on purpose - a library field typed
    /// <c>IReadOnlyList&lt;uint&gt;</c> says nothing about whether a builder still holds the list
    /// behind it - and it does that by reading private fields such as
    /// <c>List&lt;FuzzyRegex&gt;._items</c>. Native AOT generates no field accessor for a
    /// framework generic reached only by reflection. Published and run 2026-09-16, both ways
    /// round: by default the walk returned ONE slot and the three subset assertions passed over
    /// nothing, and with <c>IlcGenerateCompleteTypeMetadata</c> it threw
    /// <c>NotSupportedException: This object cannot be invoked because no code was generated for
    /// it: 'System.Collections.Generic.List`1[Fuzzy.Text.RegularExpressions.FuzzyRegex]._items'</c>
    /// and cost 5.3 MB of binary for the privilege.
    /// </para>
    /// <para>
    /// So these three rules are JIT-only, and they are skipped rather than weakened. They still
    /// run on every commit, on all three operating systems, in the <c>build-and-ratchet</c> job -
    /// what they audit is the port's own structure, which does not depend on the runtime that
    /// asks. Rewriting the walk to enumerate collections through <c>IEnumerable</c> instead would
    /// make it run here, and would also stop it seeing a list's <c>_size</c> and <c>_version</c>
    /// move; that is a weaker thread-safety audit bought with a runtime the audit does not need,
    /// and S52b's rules are permanent. The library's OWN reflection audits are unaffected and do
    /// run natively: they read rooted library types, not BCL internals.
    /// </para>
    /// </remarks>
    private static void SkipWhereTheObjectGraphWalkCannotRun()
    {
        // The documented proxy for "this is a Native AOT binary": no JIT, hence no generated
        // accessor for a field nothing statically references.
        if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
        {
            TUnit.Core.Skip.Test(
                "the object-graph walk reads BCL private fields, for which native AOT generates no "
                    + "accessor; this rule runs under the JIT in the build-and-ratchet job"
            );
        }
    }

    /// <returns>The field names, as <c>Type.Field</c>, sorted and deduplicated.</returns>
    private static IReadOnlyList<string> MutableFieldsInThePatternGraph()
    {
        SkipWhereTheObjectGraphWalkCannotRun();

        IReadOnlyList<ObjectGraph.Slot> slots = ObjectGraph.Walk(CompileEveryFamily());

        // Measured 2026-09-16 under the JIT: the walk visits 27,000-odd slots for the family set.
        // The floor is a "the walk found the graph" alarm, deliberately far below it.
        slots.Should().HaveCountGreaterThan(1000, "the walk must reach the pattern graph to judge it");

        IReadOnlyList<string> mutable =
        [
            .. slots
                .Where(static slot =>
                    slot.Field is not null
                    && slot.Field.DeclaringType?.Assembly == _library
                    && !ObjectGraph.IsWriteOnceAfterConstruction(slot.Field, slot.Owner!)
                )
                .Select(static slot => $"{slot.Field!.DeclaringType!.Name}.{slot.Field.Name}")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static name => name, StringComparer.Ordinal),
        ];

        // The allowlist names forty, and under the JIT this set is exactly those forty. A floor of
        // thirty catches a reflection surface that has stopped reporting writability without
        // pinning the count, which the two subset rules already do between them.
        mutable
            .Should()
            .HaveCountGreaterThan(
                30,
                "the allowlist names forty writable fields, so a near-empty answer means the "
                    + "reflection surface stopped reporting writability - not that the engine "
                    + "became immutable"
            );

        return mutable;
    }

    [Test]
    public void Every_field_reachable_from_a_compiled_pattern_is_write_once_or_allowlisted() =>
        MutableFieldsInThePatternGraph()
            .Should()
            .BeSubsetOf(
                _patternGraphAllowlist,
                "a field reachable from a compiled pattern that is neither readonly nor init-only "
                    + "is shared mutable state the moment two threads match against one pattern; if "
                    + "it is genuinely written only by the compiler, add it to the allowlist with "
                    + "the writer named, and the snapshot test will prove the claim"
            );

    [Test]
    public void The_pattern_graph_allowlist_has_no_entry_that_has_stopped_existing() =>
        // Strict, the way tests/parity-baseline.json is: an allowlist that keeps an entry for a
        // field somebody has since made readonly is an allowlist nobody is reading.
        _patternGraphAllowlist
            .Should()
            .BeSubsetOf(
                MutableFieldsInThePatternGraph(),
                "an allowlist entry for a field that is now readonly, or has been deleted, is stale "
                    + "and should be removed"
            );

    [Test]
    public void Matching_writes_nothing_reachable_from_a_compiled_pattern()
    {
        SkipWhereTheObjectGraphWalkCannotRun();

        IReadOnlyList<FuzzyRegex> patterns = CompileEveryFamily();

        // THE FIRST SNAPSHOT IS TAKEN BEFORE THESE PATTERNS HAVE MATCHED ANYTHING, and that is
        // load-bearing. An earlier draft warmed up first, on the reasoning that a one-time
        // initialisation is a legitimate write; the effect was to hide every first-match-only write
        // behind the warm-up, which is precisely the shape a lazily computed cache takes. S52b's
        // blind review demonstrated it: `pattern.ReqString ??= new Node(0)` on the match path left
        // this test green. Nothing is warmed here, so a write on the first match is a difference.
        //
        // There is no Lazy anywhere in the instance graph to make that awkward - the Unicode tables
        // that do use one are static, and are covered separately below.
        IReadOnlyDictionary<string, string> before = Snapshot(patterns);

        RunWholeWorkload(patterns);

        IReadOnlyDictionary<string, string> afterOnce = Snapshot(patterns);

        RunWholeWorkload(patterns);

        IReadOnlyDictionary<string, string> afterTwice = Snapshot(patterns);

        // Rendered to sorted lines rather than compared with BeEquivalentTo, which cannot run
        // under Native AOT (see Equivalence). Equal on two line lists reports the first path whose
        // value moved, which is the same diagnosis BeEquivalentTo gave.
        IReadOnlyList<string> baseline = Equivalence.Lines(before);

        Equivalence
            .Lines(afterOnce)
            .Should()
            .Equal(
                baseline,
                "matching must not write anything reachable from a compiled pattern - that is the "
                    + "whole of the shareable-between-threads promise, and it is what justifies "
                    + "every entry in the pattern-graph allowlist"
            );

        // The second reading catches a write that accumulates rather than one that settles on the
        // first match; between them the two cover both shapes.
        Equivalence.Lines(afterTwice).Should().Equal(baseline, "and it must not drift on later matches either");
    }

    /// <summary>
    /// S53. Every rule in this file is a reflection scan that reports what it found, so each one
    /// passes vacuously if the scan finds nothing - and a trimmed publish is exactly how a scan
    /// comes back empty. The suite is published as a Native AOT binary and run as the AOT gate, so
    /// that is not hypothetical: without <c>TrimmerRootAssembly</c> in the test project, the
    /// trimmer keeps only the members the tests happen to call and every audit below becomes a
    /// tick for nothing.
    /// </summary>
    /// <remarks>
    /// The floors are well under the real sizes, measured 2026-09-16 on
    /// <c>src/FuzzyRegex/bin/Release/net10.0/FuzzyRegex.dll</c>: 281 types and 465 declared static
    /// fields. They are a trimming alarm, not a size ratchet, so they are deliberately loose.
    /// </remarks>
    [Test]
    public void The_library_assembly_this_file_scans_has_not_been_trimmed_away()
    {
        _library
            .GetTypes()
            .Should()
            .HaveCountGreaterThan(200, "281 types were measured; far fewer means the trimmer took them");

        LibraryStaticFields()
            .Should()
            .HaveCountGreaterThan(300, "465 declared static fields were measured, and every rule below scans them");
    }

    [Test]
    public void Every_static_field_in_the_library_is_readonly_or_const()
    {
        IReadOnlyList<string> writable =
        [
            .. LibraryStaticFields()
                .Where(static field => !field.IsInitOnly && !field.IsLiteral)
                .Select(static field => $"{field.DeclaringType!.FullName}.{field.Name}")
                .OrderBy(static name => name, StringComparer.Ordinal),
        ];

        writable
            .Should()
            .BeEmpty(
                "a writable static is global mutable state, which no amount of per-call discipline "
                    + "can make safe to share"
            );
    }

    [Test]
    public void The_compilers_own_static_caches_are_single_reference_publications()
    {
        // Compiler-generated closure types hold a static cache field per lambda, and those are not
        // emitted readonly because they are filled on first use. They are excluded from the rule
        // above, so the exclusion is bounded here: every one of them must be a delegate, whose
        // publication is a single atomic reference write of an idempotent value.
        IReadOnlyList<string> notDelegates =
        [
            .. _library
                .GetTypes()
                .Where(static type => type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                .SelectMany(static type =>
                    type.GetFields(
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
                    )
                )
                .Where(static field => !field.IsInitOnly && !field.IsLiteral)
                .Where(static field => !typeof(Delegate).IsAssignableFrom(field.FieldType))
                .Select(static field => $"{field.DeclaringType!.FullName}.{field.Name}")
                .OrderBy(static name => name, StringComparer.Ordinal),
        ];

        notDelegates.Should().BeEmpty("the only writable statics the C# compiler emits here are its lambda caches");
    }

    [Test]
    public void Every_static_field_in_the_library_holds_an_immutable_or_thread_safe_type()
    {
        IReadOnlyList<string> unsafeTypes =
        [
            .. LibraryStaticFields()
                .Where(static field => !IsThreadSafeStaticType(field.FieldType))
                .Select(static field => $"{field.DeclaringType!.FullName}.{field.Name} : {field.FieldType.Name}")
                .OrderBy(static name => name, StringComparer.Ordinal),
        ];

        unsafeTypes
            .Should()
            .BeEmpty(
                "a readonly static still shares whatever it points at; an array or a dictionary "
                    + "behind one is safe only because nothing writes it, which "
                    + "Matching_writes_nothing_in_the_librarys_static_tables measures"
            );
    }

    [Test]
    public void Matching_writes_nothing_in_the_librarys_static_tables()
    {
        IReadOnlyList<FuzzyRegex> patterns = CompileEveryFamily();

        // Statics are process-wide, so unlike the instance graph above this test cannot get a
        // reading from before anything has ever matched - another test may have run first. Two
        // defences instead, and between them they do not depend on test order:
        //
        //  - the first reading is still taken before THIS workload, which catches a first-write in
        //    a run where nothing came earlier;
        //  - the two workloads use DIFFERENT subjects, so a static that memoises per subject keeps
        //    growing instead of saturating after one pass. S52b's blind review put an unsynchronised
        //    static Dictionary on the match path and the saturating version of this test missed it.
        //
        // A Lazy is left out of the snapshot rather than being compared: going from not-created
        // to created is the one legitimate write a static here may make, and it may happen at any
        // point depending on which test ran first.
        IReadOnlyDictionary<string, string> before = StaticTableSnapshot();

        RunWholeWorkload(patterns, salt: "s52b-first");

        IReadOnlyDictionary<string, string> afterOnce = StaticTableSnapshot();

        RunWholeWorkload(patterns, salt: "s52b-second");

        IReadOnlyDictionary<string, string> afterTwice = StaticTableSnapshot();

        IReadOnlyList<string> baseline = Equivalence.Lines(before);

        Equivalence
            .Lines(afterOnce)
            .Should()
            .Equal(
                baseline,
                "the generated Unicode tables are readonly references to mutable arrays, so "
                    + "'nothing writes them' is a claim that has to be measured rather than "
                    + "inferred from the field modifier"
            );

        Equivalence
            .Lines(afterTwice)
            .Should()
            .Equal(
                baseline,
                "and a static that grew with each distinct subject would be shared mutable state "
                    + "however readonly the field holding it is"
            );
    }

    [Test]
    public void Every_field_of_a_match_is_write_once()
    {
        FuzzyRegex pattern = new("(?:kitten){e<=2}");
        Match match = pattern.Match("sitting");

        match.Success.Should().BeTrue("the rest of the test is vacuous without a real match");

        IReadOnlyList<string> mutable =
        [
            .. MatchFieldsOf(match)
                .Where(static entry => !ObjectGraph.IsWriteOnceAfterConstruction(entry.Field, entry.Owner))
                .Select(static entry => $"{entry.Field.DeclaringType!.Name}.{entry.Field.Name}")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static name => name, StringComparer.Ordinal),
        ];

        Equivalence
            .Sorted(mutable)
            .Should()
            .Equal(
                Equivalence.Sorted(_matchAllowlist),
                "this port documents a Match as readable from any thread, which is stronger than "
                    + "the built-in Regex's contract, so any field a read can write must be a "
                    + "single reference - anything wider cannot be published atomically"
            );
    }

    [Test]
    public void Every_field_a_match_writes_after_construction_is_a_single_reference()
    {
        // THIS is the deterministic half of the Match rule, and the one that would have caught the
        // defect S52b found. A write to a reference-typed field is atomic on every architecture
        // .NET supports, so a reader sees either the old value or the new one; a write to anything
        // wider is not, so a reader can see a half-built value. Match._splitChanges was a
        // 'FuzzyChanges?' - a flag plus three list references - and readers did see it torn.
        //
        // ThreadSafetyStressTests.One_match_can_be_read_from_many_threads_at_once reproduces the
        // tear itself, on three runs out of three. The two are kept because they fail for different
        // reasons: this one fails on the shape the moment the field is declared, that one fails on
        // the behaviour, and a future race that is not "a value-typed field" will only trip the
        // second.
        IReadOnlyList<string> notReferences =
        [
            .. MatchFieldsOf(new FuzzyRegex("(?:kitten){e<=2}").Match("sitting"))
                .Where(static entry => !ObjectGraph.IsWriteOnceAfterConstruction(entry.Field, entry.Owner))
                .Where(static entry => entry.Field.FieldType.IsValueType)
                .Select(static entry =>
                    $"{entry.Field.DeclaringType!.Name}.{entry.Field.Name} : {entry.Field.FieldType.Name}"
                )
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static name => name, StringComparer.Ordinal),
        ];

        notReferences
            .Should()
            .BeEmpty(
                "a value-typed field wider than a machine word cannot be published atomically, so "
                    + "a second thread reading it can see a value that was never written"
            );
    }

    /// <summary>
    /// The one field on a match that a read writes, and why that is safe. Checked exactly rather
    /// than as a subset, so a second lazily computed field cannot be added without this test
    /// noticing and without someone justifying it here.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Match._splitChanges</c> caches <see cref="Match.FuzzyChanges"/>, which upstream rebuilds
    /// on every attribute read. It is a <c>StrongBox</c>, so publishing it is one reference write -
    /// atomic on every architecture .NET supports - and its contents are fixed before the reference
    /// is stored. Two threads may race to fill it and one wins; the value is a pure function of the
    /// match's readonly change array, so the loser computed the same answer.
    /// </para>
    /// <para>
    /// <b>It was a <c>FuzzyChanges?</c> before S52b, and that genuinely tore</b>: four fields wide,
    /// so a reader could see the flag set and a list still null.
    /// <see cref="ThreadSafetyStressTests.One_match_can_be_read_from_many_threads_at_once"/> is the
    /// reproduction, and is what keeps the fix honest.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyList<string> _matchAllowlist = ["Match._splitChanges"];

    /// <summary>The fields of a match and of its group and capture types.</summary>
    /// <param name="match">The match to read.</param>
    /// <returns>Each field with the type it was found on.</returns>
    private static IEnumerable<(FieldInfo Field, Type Owner)> MatchFieldsOf(Match match)
    {
        Type[] types =
        [
            typeof(Match),
            typeof(Group),
            typeof(Capture),
            typeof(GroupCollection),
            typeof(CaptureCollection),
            typeof(MatchCollection),
            .. match.Groups.Select(static group => group.GetType()),
        ];

        return types.Distinct().SelectMany(type => ObjectGraph.InstanceFields(type).Select(field => (field, type)));
    }

    /// <summary>One compiled pattern per family of the S52b workload.</summary>
    /// <returns>The patterns, in the workload's order.</returns>
    private static IReadOnlyList<FuzzyRegex> CompileEveryFamily() =>
        [.. ThreadSafetyWorkload.Families.Select(static family => family.Compile())];

    /// <summary>Runs every family against every one of its subjects, discarding the answers.</summary>
    /// <param name="patterns">The compiled patterns, in the workload's order.</param>
    /// <param name="salt">
    /// Appended to every subject, so that two runs ask about different text. A static that memoised
    /// anything per subject would keep growing across salted runs where it would saturate across
    /// identical ones, which is the difference between noticing one and not.
    /// </param>
    private static void RunWholeWorkload(IReadOnlyList<FuzzyRegex> patterns, string salt = "")
    {
        for (int i = 0; i < patterns.Count; i++)
        {
            ThreadSafetyWorkload.Family family = ThreadSafetyWorkload.Families[i];

            foreach (string subject in family.Subjects)
            {
                family.Run(patterns[i], subject + salt);
            }
        }
    }

    /// <summary>
    /// Every leaf value reachable from the given patterns, keyed by its path, so a difference
    /// between two snapshots names the field that moved.
    /// </summary>
    /// <param name="patterns">The patterns to walk.</param>
    /// <returns>The snapshot.</returns>
    private static Dictionary<string, string> Snapshot(IReadOnlyList<FuzzyRegex> patterns)
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (ObjectGraph.Slot slot in ObjectGraph.Walk(patterns))
        {
            string? rendered = ObjectGraph.RenderLeaf(slot.Value);

            if (rendered is not null)
            {
                // A path can repeat only if the walk reached the same array element twice, which it
                // does not; indexing rather than adding keeps a duplicate from throwing over what
                // would be an equal value anyway.
                snapshot[slot.Path] = rendered;
            }
        }

        return snapshot;
    }

    /// <summary>
    /// A content hash of every static array the library holds, which is where the generated Unicode
    /// tables live. Hashed rather than listed because the tables run to millions of entries.
    /// </summary>
    /// <returns>One hash per static array field.</returns>
    private static Dictionary<string, string> StaticTableSnapshot()
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (FieldInfo field in LibraryStaticFields())
        {
            object? value = field.GetValue(null);
            string name = $"{field.DeclaringType!.FullName}.{field.Name}";

            // A Lazy is left out: reading its contents would force it, which is the very write this
            // test is looking for, and going from not-created to created is the one legitimate
            // write a static here may make - it happens on whichever test runs first. Every caller
            // dropped these entries anyway, so leaving them out is what the comparison already did.
            //
            // S53: the earlier version recorded `lazy-created:{IsValueCreated}`, read through
            // `field.FieldType.GetProperty("IsValueCreated")`. That returns NULL in a Native AOT
            // binary - the trimmer keeps no property metadata for a framework generic reached only
            // by name - so the `!` threw a NullReferenceException and this test was the last red
            // one in the published suite. The value was discarded by the caller either way.
            if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Lazy<>))
            {
                continue;
            }

            snapshot[name] = value switch
            {
                null => "null",
                string text => text,
                // Arrays, dictionaries and sets are all hashed the same way, by walking their
                // elements in enumeration order: for a Dictionary that order is itself evidence,
                // because any write to one moves it.
                IEnumerable sequence => HashElements(sequence),
                _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "null",
            };
        }

        return snapshot;
    }

    /// <summary>A content hash of a sequence, in enumeration order.</summary>
    /// <param name="sequence">The sequence to hash.</param>
    /// <returns>The hash, rendered.</returns>
    private static string HashElements(IEnumerable sequence)
    {
        var hash = new HashCode();
        int count = 0;

        foreach (object? element in sequence)
        {
            hash.Add(
                element switch
                {
                    null => 0,
                    string text => text.GetHashCode(StringComparison.Ordinal),
                    _ => element.GetHashCode(),
                }
            );
            count++;
        }

        return $"{count}:{hash.ToHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Every static field the library declares in its own source, which is every static field on a
    /// type the C# compiler did not generate.
    /// </summary>
    /// <returns>The fields.</returns>
    private static IEnumerable<FieldInfo> LibraryStaticFields() =>
        _library
            .GetTypes()
            .Where(static type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .SelectMany(static type =>
                type.GetFields(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
                )
            );

    /// <summary>
    /// Whether a static field's type is one that is safe to reach from several threads: immutable
    /// by construction, frozen, or a type whose own contract is thread-safe.
    /// </summary>
    /// <remarks>
    /// An array and a <c>Dictionary</c> are on this list, and that is a deliberate judgement rather
    /// than an oversight: neither is thread-safe to write, and the reason they are safe here is
    /// that nothing writes them after their static constructor. That claim is measured by
    /// <see cref="Matching_writes_nothing_in_the_librarys_static_tables"/> rather than assumed.
    /// <c>Lazy&lt;T&gt;</c> is on it because all three uses in the library take the default
    /// thread-safety mode, which is <c>ExecutionAndPublication</c>.
    /// </remarks>
    /// <param name="type">The field type to judge.</param>
    /// <returns>Whether it may be held in a static.</returns>
    private static bool IsThreadSafeStaticType(Type type)
    {
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(TimeSpan))
        {
            return true;
        }

        // A table nothing writes. The element type is not recursed into: every array in the library
        // holds primitives, strings or value tuples of them, which the static-table snapshot
        // hashes in full.
        if (type.IsArray)
        {
            return true;
        }

        Type definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;

        return definition == typeof(Lazy<>)
            || definition == typeof(IReadOnlyDictionary<,>)
            || definition == typeof(IReadOnlyList<>)
            || definition == typeof(IReadOnlySet<>)
            || definition == typeof(Dictionary<,>)
            || definition == typeof(System.Collections.Frozen.FrozenSet<>)
            || definition == typeof(System.Collections.Frozen.FrozenDictionary<,>);
    }

    /// <summary>
    /// The allowlist itself. Each entry names the field and the writer that fills it; see the
    /// remarks on <see cref="_patternGraphAllowlist"/> for how the claims are proved.
    /// </summary>
    /// <returns>The allowlisted <c>Type.Field</c> names.</returns>
    private static HashSet<string> BuildPatternGraphAllowlist() =>
        new(StringComparer.Ordinal)
        {
            // ONE writer for all forty, and it is the reason they are not readonly: the engine's
            // graph is built by mutation, exactly as upstream's C builds RE_PatternObject and
            // RE_Node in place. Every write happens inside Engine.PatternObject.Compile
            // (src/FuzzyRegex/Engine/PatternObject.cs:217) and the NodeCompiler.CompileToNodes and
            // Optimiser passes it calls, and Compile is invoked from one place only -
            // the FuzzyRegex constructor, src/FuzzyRegex/FuzzyRegex.cs:170 - so the whole graph is
            // finished before the instance the caller holds exists. Making them readonly would
            // mean restructuring the port away from upstream's shape, which costs more at every
            // future sync than it buys.
            //
            // That "and by nothing afterwards" half is measured, not asserted: see
            // Matching_writes_nothing_reachable_from_a_compiled_pattern, which snapshots all forty
            // (and everything they point at) and runs the whole workload between two readings.

            // PatternObject: the compiled pattern itself. Object-initialiser and Compile's later
            // passes (the required-string node, the start optimisations, the fuzzy survey).
            "PatternObject.DoSearchStart",
            "PatternObject.Flags",
            "PatternObject.FuzzyCount",
            "PatternObject.GroupEndIndex",
            "PatternObject.GroupIndex",
            "PatternObject.HasWeightedFuzzyCosts",
            "PatternObject.IsFuzzy",
            "PatternObject.MinWidth",
            "PatternObject.NamedListIndexes",
            "PatternObject.NamedLists",
            "PatternObject.PatternCallRef",
            "PatternObject.PublicGroupCount",
            "PatternObject.RepeatCount",
            "PatternObject.ReqFlags",
            "PatternObject.ReqOffset",
            "PatternObject.ReqString",
            "PatternObject.RequiredChars",
            "PatternObject.SingleFuzzyNode",
            "PatternObject.StartNode",
            "PatternObject.StartTest",
            "PatternObject.TrueGroupCount",
            "PatternObject.VisibleCaptureCount",
            // Node and NextNode: the opcode graph. Nodes are emitted before their successors exist,
            // so the links are patched up afterwards - upstream's own two-pass shape.
            "Node.Index",
            "Node.Match",
            "Node.Op",
            "Node.Status",
            "Node.Step",
            "Node.TrueNode",
            "NextNode.MatchNext",
            "NextNode.MatchStep",
            "NextNode.Node",
            "NextNode.Test",
            // The per-group, per-call and per-repeat side tables, filled as the compiler meets each
            // construct and revisited when a later one refers back to it.
            "GroupInfo.EndIndex",
            "GroupInfo.HasName",
            "GroupInfo.Node",
            "GroupInfo.Referenced",
            "CallRefInfo.Defined",
            "CallRefInfo.Node",
            "CallRefInfo.Used",
            "RepeatInfo.Status",
        };
}
