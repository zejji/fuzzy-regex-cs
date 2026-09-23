using System.Collections;
using System.Reflection;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// A reflection walk over every object reachable from a root, used by the S52b thread-safety tests
/// to ask two questions of the same graph: which fields <i>could</i> be written after construction,
/// and whether any of them <i>was</i>.
/// </summary>
/// <remarks>
/// <para>
/// It walks instances rather than declared types on purpose. The declared type of the compiled
/// pattern's <c>Code</c> is <c>IReadOnlyList&lt;uint&gt;</c>, which says nothing about whether the
/// object behind it is a frozen array or a <c>List</c> its builder still holds a reference to.
/// Walking the real references answers what a second thread can actually see.
/// </para>
/// <para>
/// The walk descends into BCL objects (a <c>List</c>'s backing array, a <c>Dictionary</c>'s
/// entries) because that is where a library type's contents live, but only fields declared by the
/// library's own types are subject to the readonly rule - the BCL's internals are not ours to fix,
/// and a change to them is caught by value instead.
/// </para>
/// </remarks>
internal static class ObjectGraph
{
    /// <summary>
    /// The most objects a single walk may visit. A cap is needed because a walk that ran away into
    /// the Unicode tables would take minutes; a walk that silently stopped early would report a
    /// clean graph it never finished reading, so <see cref="Walk"/> throws rather than truncating.
    /// </summary>
    private const int _visitLimit = 500_000;

    /// <summary>Types the walk treats as leaves: descending into them reaches the whole runtime.</summary>
    private static readonly Type[] _opaque =
    [
        typeof(Type),
        typeof(MemberInfo),
        typeof(Assembly),
        typeof(Module),
        typeof(Delegate),
        typeof(Thread),
        typeof(CancellationTokenSource),
    ];

    /// <summary>
    /// Library types the walk records but does not open, because they are mutable on purpose and
    /// safe by a different argument than immutability. Each entry needs its own stress test, and this
    /// list must stay as short as the argument is rare.
    /// </summary>
    /// <remarks>
    /// <see cref="RegularExpressions.Engine.MatchStateCache"/> (S61) is a single slot that every call takes with
    /// <see cref="Interlocked.Exchange{T}(ref T, T)"/> and puts back, the shape of the built-in
    /// <c>Regex._runner</c>, so no two threads ever hold the state inside it. Its proof is
    /// <c>ThreadSafetyStressTests.Every_family_answers_the_same_under_real_parallelism</c>, which fails
    /// when the slot is read without the exchange,
    /// and <c>MatchStateCacheTests</c> proves that a reused state matches exactly as a new one does.
    /// The field that holds it on <see cref="FuzzyRegex"/> is readonly, so the walk still checks
    /// that nothing replaces the cache itself.
    /// </remarks>
    private static readonly Type[] _atomicallyShared = [typeof(RegularExpressions.Engine.MatchStateCache)];

    /// <summary>One field of one reachable object, or one element of one reachable array.</summary>
    /// <param name="Path">Where in the graph it sits, for a failure message.</param>
    /// <param name="Field">The field itself, or null for an array element.</param>
    /// <param name="Owner">The type the walk found it on, which may derive from the declarer.</param>
    /// <param name="Value">What it held at the moment of the walk.</param>
    internal sealed record Slot(string Path, FieldInfo? Field, Type? Owner, object? Value);

    /// <summary>
    /// Walks everything reachable from <paramref name="root"/>, in a deterministic order, so that
    /// two walks of an unchanged graph produce the same paths in the same sequence.
    /// </summary>
    /// <param name="root">Where to start.</param>
    /// <returns>Every field of every reachable object.</returns>
    /// <exception cref="InvalidOperationException">The graph exceeded <see cref="_visitLimit"/>.</exception>
    internal static IReadOnlyList<Slot> Walk(object root)
    {
        var slots = new List<Slot>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<(object Instance, string Path)>();

        queue.Enqueue((root, root.GetType().Name));
        seen.Add(root);

        while (queue.Count > 0)
        {
            (object instance, string path) = queue.Dequeue();

            if (seen.Count > _visitLimit)
            {
                throw new InvalidOperationException(
                    $"the object graph walk passed {_visitLimit} objects at '{path}'; either the "
                        + "graph grew a reference into the Unicode tables or the walk has a cycle "
                        + "the reference-identity set is not catching"
                );
            }

            if (instance is Array array)
            {
                WalkArray(array, path, slots, seen, queue);
                continue;
            }

            foreach (FieldInfo field in InstanceFields(instance.GetType()))
            {
                object? value = field.GetValue(instance);
                string slotPath = $"{path}.{field.Name}";

                slots.Add(new Slot(slotPath, field, instance.GetType(), value));
                Enqueue(value, slotPath, seen, queue);
            }
        }

        return slots;
    }

    /// <summary>
    /// Whether a field cannot be written after its declaring object's constructor returns - either
    /// because it is <c>readonly</c>, or because it is the compiler-generated backing field of a
    /// property whose only setter is an <c>init</c> accessor.
    /// </summary>
    /// <remarks>
    /// The second case matters because a positional record's properties are <c>init</c>-only and
    /// their backing fields are <i>not</i> emitted as <c>initonly</c> - the init accessor writes
    /// them - so a rule that looked only at <see cref="FieldInfo.IsInitOnly"/> would report every
    /// record in the port as mutable.
    /// </remarks>
    /// <param name="field">The field to judge.</param>
    /// <param name="owner">The type the field was found on.</param>
    /// <returns>Whether nothing can write it after construction.</returns>
    internal static bool IsWriteOnceAfterConstruction(FieldInfo field, Type owner)
    {
        if (field.IsInitOnly || field.IsLiteral)
        {
            return true;
        }

        PropertyInfo? property = BackedProperty(field, owner);

        if (property is null)
        {
            return false;
        }

        MethodInfo? setter = property.GetSetMethod(nonPublic: true);

        return setter is null || IsInitOnlySetter(setter);
    }

    /// <summary>The property a compiler-generated backing field belongs to, if it is one.</summary>
    /// <param name="field">The field to look up.</param>
    /// <param name="owner">The type the field was found on.</param>
    /// <returns>The property, or null when the field is an ordinary one.</returns>
    internal static PropertyInfo? BackedProperty(FieldInfo field, Type owner)
    {
        if (!field.Name.StartsWith('<') || !field.Name.EndsWith(">k__BackingField", StringComparison.Ordinal))
        {
            return null;
        }

        string name = field.Name[1..field.Name.IndexOf('>', StringComparison.Ordinal)];

        for (Type? type = owner; type is not null; type = type.BaseType)
        {
            PropertyInfo? property = type.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
            );

            if (property is not null)
            {
                return property;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a setter is an <c>init</c> accessor, which the compiler marks by a required custom
    /// modifier of <c>IsExternalInit</c> on its return type.
    /// </summary>
    /// <param name="setter">The setter to inspect.</param>
    /// <returns>Whether it is init-only.</returns>
    internal static bool IsInitOnlySetter(MethodInfo setter) =>
        setter
            .ReturnParameter.GetRequiredCustomModifiers()
            .Any(static modifier =>
                string.Equals(
                    modifier.FullName,
                    "System.Runtime.CompilerServices.IsExternalInit",
                    StringComparison.Ordinal
                )
            );

    /// <summary>Every instance field of a type and its bases, in a stable order.</summary>
    /// <param name="type">The type to read.</param>
    /// <returns>The fields, base-most first and alphabetical within a level.</returns>
    internal static IEnumerable<FieldInfo> InstanceFields(Type type)
    {
        var levels = new List<Type>();

        for (Type? level = type; level is not null && level != typeof(object); level = level.BaseType)
        {
            levels.Insert(0, level);
        }

        return levels.SelectMany(static level =>
            level
                .GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
                )
                .OrderBy(static field => field.Name, StringComparer.Ordinal)
        );
    }

    /// <summary>
    /// Whether a value is a leaf: something the walk records but does not descend into, because it
    /// has no references of its own that a race could change.
    /// </summary>
    /// <param name="type">The type to judge.</param>
    /// <returns>Whether to stop here.</returns>
    internal static bool IsLeaf(Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type.IsPointer
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(TimeSpan)
        || type == typeof(Guid)
        || type == typeof(IntPtr)
        || type == typeof(UIntPtr)
        || _opaque.Any(opaque => opaque.IsAssignableFrom(type))
        || Array.IndexOf(_atomicallyShared, type) >= 0;

    /// <summary>Renders a leaf value so two walks can be compared as text.</summary>
    /// <param name="value">The value.</param>
    /// <returns>Its rendering, or null when the value is not a leaf.</returns>
    internal static string? RenderLeaf(object? value)
    {
        if (value is null)
        {
            return "null";
        }

        Type type = value.GetType();

        return IsLeaf(type)
            ? $"{type.Name}:{Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)}"
            : null;
    }

    /// <summary>Walks an array's elements, which are slots in every sense except having a field.</summary>
    /// <param name="array">The array.</param>
    /// <param name="path">Where it sits.</param>
    /// <param name="slots">Where to record what is found.</param>
    /// <param name="seen">The objects already queued.</param>
    /// <param name="queue">The walk's queue.</param>
    private static void WalkArray(
        Array array,
        string path,
        List<Slot> slots,
        HashSet<object> seen,
        Queue<(object Instance, string Path)> queue
    )
    {
        // Multi-dimensional arrays do not appear in this port; IEnumerable covers the rank-one case
        // and would silently flatten a higher rank, so refuse one rather than mis-report it.
        if (array.Rank != 1)
        {
            throw new NotSupportedException($"'{path}' is a rank-{array.Rank} array");
        }

        int index = 0;

        foreach (object? element in (IEnumerable)array)
        {
            string elementPath = $"{path}[{index}]";

            // Elements are recorded as slots in their own right: the bytecode a pattern compiled to
            // is a uint[], so a snapshot that walked past the elements would not notice the one
            // change that matters most.
            slots.Add(new Slot(elementPath, Field: null, Owner: null, element));
            Enqueue(element, elementPath, seen, queue);
            index++;
        }
    }

    /// <summary>Queues a value for walking if it is a reference the walk has not reached yet.</summary>
    /// <param name="value">The value.</param>
    /// <param name="path">Where it sits.</param>
    /// <param name="seen">The objects already queued.</param>
    /// <param name="queue">The walk's queue.</param>
    private static void Enqueue(
        object? value,
        string path,
        HashSet<object> seen,
        Queue<(object Instance, string Path)> queue
    )
    {
        if (value is null || IsLeaf(value.GetType()))
        {
            return;
        }

        // A boxed struct is a fresh box every time it is read out of a field, so reference identity
        // cannot dedupe it; it is walked on each encounter, which terminates because a struct
        // cannot contain itself.
        if (!value.GetType().IsValueType && !seen.Add(value))
        {
            return;
        }

        queue.Enqueue((value, path));
    }
}
