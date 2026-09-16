namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>
/// The subjects every benchmark in this assembly measures against, built once and shared.
/// </summary>
/// <remarks>
/// <para>
/// One corpus rather than one per class, so a ratio between two benchmarks means something. The
/// long subjects are a megabyte because that is the size <c>S54</c>'s edge pins use and the size
/// at which per-call overhead has stopped being what is measured; the filler carries no
/// <c>needle</c>, so a literal search for one is a full scan rather than an early hit.
/// </para>
/// <para>
/// Everything here is deterministic. A baseline compares runs on different days, so a corpus with
/// a random element would make every comparison a guess.
/// </para>
/// </remarks>
internal static class Corpus
{
    /// <summary>The filler sentence. Forty-four characters, no <c>needle</c>, no digits.</summary>
    private const string _sentence = "the quick brown fox jumps over the lazy dog ";

    /// <summary>What the long subjects are padded to, before their distinguishing tail.</summary>
    private const int _megabyte = 1024 * 1024;

    /// <summary>A megabyte of filler ending in a single findable <c>needle</c>.</summary>
    public static readonly string Long = Pad("a needle in a haystack.");

    /// <summary>
    /// A megabyte of filler ending in a strict prefix of <c>a needle in a haystack.</c>, so a
    /// partial match for that pattern is found at the very end and nowhere earlier.
    /// </summary>
    public static readonly string LongPartial = Pad("a needle in a hay");

    /// <summary>A short subject, for the workloads that measure per-call cost rather than scanning.</summary>
    public static readonly string Short = _sentence + "and finds a needle.";

    /// <summary>
    /// A hundred kilobytes of the same filler. The lazy walk's per-step state makes a full walk
    /// quadratic in the subject (<c>OPTIMISATION-NOTES.md</c>), so the workload that drains one to
    /// the end uses this rather than <see cref="Long"/>: at a megabyte it costs tens of seconds,
    /// which is a measurement nobody would re-run and so a baseline nobody would compare against.
    /// The `sizing` mode prints both numbers.
    /// </summary>
    public static readonly string Dense = Pad("a needle in a haystack.", 100 * 1024);

    /// <summary>
    /// A large pattern, for the compile-time workload: a 300-way literal alternation followed by a
    /// quantified class and an optional group, which is the shape a generated pattern tends to have.
    /// </summary>
    public static readonly string LargePattern = BuildLargePattern();

    /// <summary>Builds a subject of at least <paramref name="size"/> filler characters plus a tail.</summary>
    /// <param name="tail">The distinguishing tail appended once, at the end.</param>
    /// <param name="size">How much filler to lay down first.</param>
    /// <returns>The subject.</returns>
    private static string Pad(string tail, int size = _megabyte)
    {
        System.Text.StringBuilder builder = new(size + tail.Length);
        while (builder.Length < size)
        {
            builder.Append(_sentence);
        }

        return builder.Append(tail).ToString();
    }

    /// <summary>Builds <see cref="LargePattern"/>.</summary>
    /// <returns>The pattern source.</returns>
    private static string BuildLargePattern()
    {
        System.Text.StringBuilder builder = new("(?:");
        for (int i = 0; i < 300; i++)
        {
            if (i > 0)
            {
                builder.Append('|');
            }

            builder.Append("word").Append(i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.Append(@")[0-9]{2,4}(?:[A-Za-z_]\w*)?\b").ToString();
    }
}
