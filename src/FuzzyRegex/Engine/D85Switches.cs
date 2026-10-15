namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// EXPERIMENT ONLY (D85 C9 attribution, 2026-10-15 night): switches read once from the
/// <c>FUZZY_D85</c> environment variable, a comma list, so one build can run every variant.
/// Never merged; removed before any candidate is measured for the owner.
/// </summary>
internal static class D85Switches
{
    private static readonly string _value = "," + (Environment.GetEnvironmentVariable("FUZZY_D85") ?? "") + ",";

    /// <summary>Every body guard off.</summary>
    internal static readonly bool NoBody = Has("nobody");

    /// <summary>Every tail guard off.</summary>
    internal static readonly bool NoTail = Has("notail");

    /// <summary>The tail guards C9 leaves unkeyed off.</summary>
    internal static readonly bool NoUnkeyed = Has("nounk");

    /// <summary>The keyed tail guards off.</summary>
    internal static readonly bool NoKeyed = Has("nokeyed");

    /// <summary>Key part p (span compare) ignored.</summary>
    internal static readonly bool NoSpan = Has("nop");

    /// <summary>Key part i (inside the group) ignored.</summary>
    internal static readonly bool NoInside = Has("noi");

    /// <summary>Key part b (progress bits) held at zero.</summary>
    internal static readonly bool NoProgress = Has("nob");

    /// <summary>Every keyed group keyed on its exact span.</summary>
    internal static readonly bool Exact = Has("exact");

    /// <summary>C9's clip of the G2 span skip off.</summary>
    internal static readonly bool NoClip = Has("noclip");

    /// <summary>The G2 span skip off everywhere (every guarded span clipped to one position).</summary>
    internal static readonly bool NoG2 = Has("nog2");

    /// <summary>Body guard off for a repeat whose body holds a keyed tail.</summary>
    internal static readonly bool KBodyOff = Has("kbody");

    /// <summary>Key an inside group on its parked start as well.</summary>
    internal static readonly bool OpenStart = Has("open");

    /// <summary>Tail guard off for a repeat inside a keyed group's brackets.</summary>
    internal static readonly bool InsideOff = Has("ioff");

    /// <summary>Clip a guarded tail span only where the key moves, not to one position.</summary>
    internal static readonly bool Clip2 = Has("clip2");

    /// <summary>Mutation: the liveness pass never kills a group at its capture.</summary>
    internal static readonly bool MutateNoKill = Has("mut-nokill");

    /// <summary>Mutation: the liveness pass stops at a repeat's end node, as upstream's walk does.</summary>
    internal static readonly bool MutateStopAtEnd = Has("mut-stopend");

    private static bool Has(string name) => _value.Contains("," + name + ",", StringComparison.Ordinal);
}
