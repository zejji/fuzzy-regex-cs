namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// PROTOTYPE ONLY (D104-fast research, 2026-10-19): switches read from the environment variable
/// FUZZY_D104 (comma list) and counters read by reflection from the scratch probe. Never merged.
/// noguards: every repeat guard check answers "not guarded" (the reference with nobudget).
/// nobudget: the second pass's fork budget never fires.
/// keep: the cut-body store survives to the next start position of the same pass.
/// states: count arrivals at repeats that record after a blocked end, and distinct (key, position).
/// </summary>
internal static class D104Proto
{
    private static readonly string _value = "," + (Environment.GetEnvironmentVariable("FUZZY_D104") ?? "") + ",";

    internal static readonly bool NoGuards = Has("noguards");
    internal static readonly bool NoBudget = Has("nobudget");
    internal static readonly bool Keep = Has("keep");
    internal static readonly bool States = Has("states");
    internal static readonly bool Cont = Has("cont");
    internal static readonly bool NoG = Has("nog");

    internal static long Arrivals { get; private set; }

    internal static HashSet<string> Distinct { get; } = [];

    internal static long StoreKeys { get; private set; }

    internal static long StoreWrites { get; private set; }

    internal static long StoreHits { get; private set; }

    internal static void Write() => StoreWrites++;

    internal static void Hit(int keys)
    {
        StoreHits++;
        StoreKeys = Math.Max(StoreKeys, keys);
    }

    internal static void Arrive(string? key)
    {
        Arrivals++;
        if (key is not null)
        {
            Distinct.Add(key);
        }
    }

    /// <summary>Per (repeat, guard type): checks, checks at recording repeats, write calls, writes, hits.</summary>
    internal static SortedDictionary<string, long[]> PerRepeat { get; } = new(StringComparer.Ordinal);

    internal static void Tally(int index, uint guardType, int slot)
    {
        if (!States)
        {
            return;
        }

        string k = index + ((guardType & NodeStatus.Body) != 0 ? "B" : "T");
        if (!PerRepeat.TryGetValue(k, out long[]? c))
        {
            c = new long[5];
            PerRepeat[k] = c;
        }

        c[slot]++;
    }

    internal static SortedDictionary<int, string> Infos { get; } = [];

    internal static void Clear()
    {
        Infos.Clear();
        PerRepeat.Clear();
        Arrivals = 0;
        Distinct.Clear();
        StoreKeys = 0;
        StoreWrites = 0;
        StoreHits = 0;
    }

    private static bool Has(string name) => _value.Contains("," + name + ",", StringComparison.Ordinal);
}
