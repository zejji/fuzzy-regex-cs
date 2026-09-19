using AwesomeAssertions;

namespace Fuzzy.Text.RegularExpressions.Tests.Gaps.Api;

/// <summary>
/// S59. The bounded pattern cache behind <see cref="FuzzyRegex.CacheSize"/>, and the key it files
/// entries under.
/// </summary>
/// <remarks>
/// <para>
/// The cache changes no answer, so nothing here can be tested through the answers: a test that
/// only checks <c>FuzzyRegex.IsMatch</c> still returns <see langword="true"/> passes with the cache
/// removed. Everything below therefore tests either <b>identity</b> - the same call twice gives the
/// same compiled object - or the cache's own <c>Count</c>/<c>Contains</c>, which exist for that
/// reason and are internal.
/// </para>
/// <para>
/// Most tests build their own <see cref="PatternCache"/> so they neither see nor disturb the one
/// <see cref="FuzzyRegex"/>'s static conveniences share; the handful that must use the shared one
/// are grouped under <c>[NotInParallel]</c> and restore <see cref="FuzzyRegex.CacheSize"/>.
/// </para>
/// </remarks>
public sealed class PatternCacheTests
{
    /// <summary>The version a static convenience compiles under, which is the cache's key too.</summary>
    private static int DefaultVersion => Fuzzy.Text.RegularExpressions.Parsing.PatternCompiler.DefaultVersion;

    private static FuzzyRegex Get(PatternCache cache, string pattern, FuzzyRegexOptions options) =>
        cache.GetOrAdd(pattern, options, FuzzyRegex.InfiniteMatchTimeout, DefaultVersion);

    [Test]
    public void The_same_pattern_and_options_are_compiled_once()
    {
        var cache = new PatternCache();

        FuzzyRegex first = Get(cache, "a(b)c", FuzzyRegexOptions.IgnoreCase);
        FuzzyRegex second = Get(cache, "a(b)c", FuzzyRegexOptions.IgnoreCase);

        second.Should().BeSameAs(first);
        cache.Count.Should().Be(1);
    }

    [Test]
    public void A_fresh_cache_holds_fifteen_patterns_as_Regex_CacheSize_does()
    {
        // Measured, not quoted: tools/probes/bcl-regex-cachesize.ps1 printed
        // "1. default CacheSize: 15" on .NET 10.0.10, 2026-09-19.
        new PatternCache()
            .Size.Should()
            .Be(15);
    }

    [Test]
    public void Different_options_are_different_entries()
    {
        var cache = new PatternCache();

        FuzzyRegex plain = Get(cache, "abc", FuzzyRegexOptions.None);
        FuzzyRegex folded = Get(cache, "abc", FuzzyRegexOptions.IgnoreCase);

        folded.Should().NotBeSameAs(plain);
        cache.Count.Should().Be(2);
    }

    [Test]
    public void The_key_is_the_raw_options_the_caller_passed_and_not_the_Options_property()
    {
        // The S53b item 2 trap, as a test. Options reports what the pattern's own inline prefix
        // resolved to, so these two calls agree on it exactly - a cache keyed on that property
        // would file them as one entry although the caller asked for two different compiles.
        var cache = new PatternCache();

        FuzzyRegex inlineOnly = Get(cache, "(?i)a", FuzzyRegexOptions.None);
        FuzzyRegex inlineAndOption = Get(cache, "(?i)a", FuzzyRegexOptions.IgnoreCase);

        inlineAndOption.Options.Should().Be(inlineOnly.Options, "this is what a key from Options would have merged");
        inlineAndOption.Should().NotBeSameAs(inlineOnly);
        cache.Count.Should().Be(2);
    }

    [Test]
    public void The_match_timeout_is_part_of_the_key()
    {
        var cache = new PatternCache();

        FuzzyRegex untimed = cache.GetOrAdd(
            "abc",
            FuzzyRegexOptions.None,
            FuzzyRegex.InfiniteMatchTimeout,
            DefaultVersion
        );
        FuzzyRegex timed = cache.GetOrAdd("abc", FuzzyRegexOptions.None, TimeSpan.FromSeconds(1), DefaultVersion);

        timed.Should().NotBeSameAs(untimed);
        timed.MatchTimeout.Should().Be(TimeSpan.FromSeconds(1));
        untimed.MatchTimeout.Should().Be(FuzzyRegex.InfiniteMatchTimeout);
    }

    [Test]
    public void The_default_version_is_part_of_the_key()
    {
        var cache = new PatternCache();

        FuzzyRegex version1 = cache.GetOrAdd(
            "abc",
            FuzzyRegexOptions.None,
            FuzzyRegex.InfiniteMatchTimeout,
            (int)FuzzyRegexOptions.Version1
        );
        FuzzyRegex version0 = cache.GetOrAdd(
            "abc",
            FuzzyRegexOptions.None,
            FuzzyRegex.InfiniteMatchTimeout,
            (int)FuzzyRegexOptions.Version0
        );

        version0.Should().NotBeSameAs(version1);
        version1.Options.Should().HaveFlag(FuzzyRegexOptions.Version1);
        version0.Options.Should().HaveFlag(FuzzyRegexOptions.Version0);
    }

    [Test]
    public void The_least_recently_used_entry_is_the_one_evicted()
    {
        var cache = new PatternCache { Size = 2 };

        FuzzyRegex first = Get(cache, "aaa", FuzzyRegexOptions.None);
        Get(cache, "bbb", FuzzyRegexOptions.None);

        // Using "aaa" again makes "bbb" the least recently used, so "ccc" evicts "bbb", not "aaa".
        Get(cache, "aaa", FuzzyRegexOptions.None).Should().BeSameAs(first);
        Get(cache, "ccc", FuzzyRegexOptions.None);

        cache.Count.Should().Be(2);
        cache
            .Contains("aaa", FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout, DefaultVersion)
            .Should()
            .BeTrue();
        cache
            .Contains("bbb", FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout, DefaultVersion)
            .Should()
            .BeFalse();
        Get(cache, "aaa", FuzzyRegexOptions.None).Should().BeSameAs(first);
    }

    [Test]
    public void An_evicted_pattern_is_compiled_again_rather_than_resurrected()
    {
        var cache = new PatternCache { Size = 1 };

        FuzzyRegex first = Get(cache, "aaa", FuzzyRegexOptions.None);
        Get(cache, "bbb", FuzzyRegexOptions.None);

        Get(cache, "aaa", FuzzyRegexOptions.None).Should().NotBeSameAs(first);
    }

    [Test]
    public void Reducing_the_size_evicts_down_to_the_new_bound_immediately()
    {
        // The measured behaviour of Regex.CacheSize: the probe's line 3 read
        // "immediately after CacheSize = 5 (no further calls): dict=5 list=5" from 20 entries.
        var cache = new PatternCache();

        foreach (int i in Enumerable.Range(0, 10))
        {
            Get(cache, "p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), FuzzyRegexOptions.None);
        }

        cache.Count.Should().Be(10);

        cache.Size = 3;

        cache.Count.Should().Be(3);
        cache.Contains("p9", FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout, DefaultVersion).Should().BeTrue();
        cache
            .Contains("p0", FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout, DefaultVersion)
            .Should()
            .BeFalse();
    }

    [Test]
    public void Size_zero_clears_the_cache_and_stops_it_storing_anything()
    {
        // The probe's lines 4 and 5: "dict=0" immediately on setting 0, and still 0 after a call.
        var cache = new PatternCache();
        FuzzyRegex before = Get(cache, "abc", FuzzyRegexOptions.None);

        cache.Size = 0;

        cache.Count.Should().Be(0);

        FuzzyRegex after = Get(cache, "abc", FuzzyRegexOptions.None);
        after.Should().NotBeSameAs(before);
        after.Pattern.Should().Be("abc");
        cache.Count.Should().Be(0);
        Get(cache, "abc", FuzzyRegexOptions.None).Should().NotBeSameAs(after);
    }

    [Test]
    public void Raising_the_size_from_zero_starts_caching_again()
    {
        // Nothing survives a bound of zero to be kept, so what this pins is the resumption: the
        // disabled state is the bound and not a latch.
        var cache = new PatternCache { Size = 0 };
        Get(cache, "abc", FuzzyRegexOptions.None);

        cache.Size = 4;

        FuzzyRegex first = Get(cache, "abc", FuzzyRegexOptions.None);
        Get(cache, "abc", FuzzyRegexOptions.None).Should().BeSameAs(first);
    }

    [Test]
    public void A_negative_size_is_rejected()
    {
        // Regex.CacheSize = -1 threw ArgumentOutOfRangeException with parameter name "value" on
        // .NET 10.0.10, and left the property unchanged (probe lines 6 and 7).
        var cache = new PatternCache();

        Action reduce = () => cache.Size = -1;

        reduce.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("value");
        cache.Size.Should().Be(PatternCache.DefaultSize);
    }

    [Test]
    public void A_null_pattern_is_rejected_with_the_constructor_s_parameter_name()
    {
        var cache = new PatternCache();

        Action get = () => Get(cache, null!, FuzzyRegexOptions.None);

        get.Should().Throw<ArgumentNullException>().WithParameterName("pattern");
    }

    [Test]
    public void A_pattern_that_does_not_compile_throws_and_leaves_nothing_behind()
    {
        var cache = new PatternCache();

        Action get = () => Get(cache, "(", FuzzyRegexOptions.None);

        get.Should().Throw<FuzzyRegexParseException>();
        cache.Count.Should().Be(0);
    }

    [Test]
    [MethodDataSource(nameof(EveryCacheableConvenience))]
    [NotInParallel(nameof(PatternCacheTests))]
    public void Every_cacheable_static_convenience_goes_through_the_cache(string name, Action<string> call)
    {
        // The slice's whole point: after one static call the key is present, so a second call
        // cannot be compiling again.
        string pattern = "s59-" + name + "-" + Guid.NewGuid().ToString("N");

        WithRoomInTheSharedCache(() =>
        {
            call(pattern);

            FuzzyRegex
                .Cache.Contains(pattern, FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout, DefaultVersion)
                .Should()
                .BeTrue($"{name} should compile through the pattern cache");
        });
    }

    /// <summary>
    /// Runs a body against the shared cache with room in it. The default bound is fifteen and the
    /// rest of this suite calls the static conveniences in parallel throughout, so a test asserting
    /// that its own key is still present has to be sure nothing evicted it in between - which is a
    /// property of the bound, not of luck. <c>[NotInParallel]</c> alone does not do it: it orders
    /// the tests in this class and says nothing about the other ninety files.
    /// </summary>
    /// <param name="body">The assertions to run.</param>
    private static void WithRoomInTheSharedCache(Action body)
    {
        int restore = FuzzyRegex.CacheSize;
        try
        {
            FuzzyRegex.CacheSize = 4096;
            body();
        }
        finally
        {
            FuzzyRegex.CacheSize = restore;
        }
    }

    /// <summary>
    /// Every static convenience that can be cached, one case each. The five that take a
    /// <c>namedLists</c> dictionary are here with it left null, which is the cacheable half of
    /// their contract; <see cref="A_caller_s_named_lists_dictionary_bypasses_the_cache"/> covers
    /// the other half.
    /// </summary>
    /// <returns>The name of each convenience and a call that applies it to a given pattern.</returns>
    public static IEnumerable<(string, Action<string>)> EveryCacheableConvenience()
    {
        yield return ("IsMatch", static pattern => FuzzyRegex.IsMatch("subject", pattern));
        yield return ("Match", static pattern => FuzzyRegex.Match("subject", pattern));
        yield return ("MatchAtStart", static pattern => FuzzyRegex.MatchAtStart("subject", pattern));
        yield return ("FullMatch", static pattern => FuzzyRegex.FullMatch("subject", pattern));
        yield return ("Matches", static pattern => _ = FuzzyRegex.Matches("subject", pattern).Count);
        yield return (
            "EnumerateMatches",
            static pattern => _ = FuzzyRegex.EnumerateMatches("subject", pattern).Count()
        );
        yield return ("EnumerateSplits", static pattern => _ = FuzzyRegex.EnumerateSplits("subject", pattern).Count());
        yield return ("Count", static pattern => FuzzyRegex.Count("subject", pattern));
        yield return ("Replace", static pattern => FuzzyRegex.Replace("subject", pattern, "x"));
        yield return (
            "ReplaceEvaluator",
            static pattern => FuzzyRegex.Replace("subject", pattern, static m => m.Value)
        );
        yield return ("ReplaceFormat", static pattern => FuzzyRegex.ReplaceFormat("subject", pattern, "{0}"));
        yield return ("Split", static pattern => _ = FuzzyRegex.Split("subject", pattern).Length);
    }

    [Test]
    public void A_caller_s_named_lists_dictionary_bypasses_the_cache()
    {
        // A caller-supplied dictionary is mutable, caller-owned and unbounded, so it cannot be part
        // of a key. The calls that take one therefore compile per call, and the proof is that
        // mutating the dictionary between two calls changes the answer.
        //
        // The two expected answers are upstream's, from regex 2026.9.10 on 2026-09-19: matching the
        // pattern below against "beta" with the words list holding only "alpha" gives None, and the
        // same call with "alpha" and "beta" in the list gives a match over span 0 to 4. Re-runnable
        // as tools/probes/s59-named-lists-rebind.py, which prints both.
        string pattern = @"\L<words>";
        var words = new Dictionary<string, IReadOnlyCollection<string>> { ["words"] = ["alpha"] };

        FuzzyRegex.Match("beta", pattern, namedLists: words).Success.Should().BeFalse();

        words["words"] = ["alpha", "beta"];

        FuzzyRegex.Match("beta", pattern, namedLists: words).Success.Should().BeTrue();
        FuzzyRegex
            .Cache.Contains(pattern, FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout, DefaultVersion)
            .Should()
            .BeFalse("a call carrying named lists must not file an entry under the bare pattern");
    }

    [Test]
    [NotInParallel(nameof(PatternCacheTests))]
    public void FuzzyRegex_CacheSize_is_the_shared_cache_s_bound()
    {
        int restore = FuzzyRegex.CacheSize;
        try
        {
            FuzzyRegex.CacheSize = 4;

            FuzzyRegex.Cache.Size.Should().Be(4);
            FuzzyRegex.CacheSize.Should().Be(4);
        }
        finally
        {
            FuzzyRegex.CacheSize = restore;
        }
    }

    [Test]
    [NotInParallel(nameof(PatternCacheTests))]
    public void Setting_FuzzyRegex_CacheSize_to_zero_is_this_port_s_regex_purge()
    {
        int restore = FuzzyRegex.CacheSize;
        try
        {
            string pattern = "s59-purge-" + Guid.NewGuid().ToString("N");
            FuzzyRegex.IsMatch("subject", pattern);

            FuzzyRegex.CacheSize = 0;

            // Asserted on this test's own key rather than on Count, which the rest of the suite is
            // free to move while this runs.
            FuzzyRegex
                .Cache.Contains(pattern, FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout, DefaultVersion)
                .Should()
                .BeFalse("setting the bound to zero empties the cache");

            // Still answers, just without caching.
            string second = "s59-purged-" + Guid.NewGuid().ToString("N");
            FuzzyRegex.IsMatch("subject", second).Should().BeFalse();
            FuzzyRegex
                .Cache.Contains(second, FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout, DefaultVersion)
                .Should()
                .BeFalse("a disabled cache stores nothing");
        }
        finally
        {
            FuzzyRegex.CacheSize = restore;
        }
    }

    [Test]
    [NotInParallel(nameof(PatternCacheTests))]
    public void A_negative_FuzzyRegex_CacheSize_is_rejected()
    {
        // Both halves are what the real Regex does, measured rather than assumed:
        // tools/probes/bcl-regex-cachesize.ps1 on .NET 10.0.10, 2026-09-19, printed
        // "System.ArgumentOutOfRangeException ... (Parameter 'value')" for Regex.CacheSize = -1
        // and then "CacheSize after the failed set: 15" - the property keeps its old value.
        int restore = FuzzyRegex.CacheSize;
        try
        {
            Action set = static () => FuzzyRegex.CacheSize = -1;

            set.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("value");
            FuzzyRegex.CacheSize.Should().Be(restore);
        }
        finally
        {
            FuzzyRegex.CacheSize = restore;
        }
    }

    [Test]
    public void The_instance_constructors_never_consult_the_cache()
    {
        // new Regex(...) does not read or write the static cache, and neither does this. A cache
        // that constructors wrote to would hold every pattern a program ever compiled.
        string pattern = "s59-ctor-" + Guid.NewGuid().ToString("N");

        var first = new FuzzyRegex(pattern);
        var second = new FuzzyRegex(pattern);

        second.Should().NotBeSameAs(first);
        FuzzyRegex
            .Cache.Contains(pattern, FuzzyRegexOptions.None, FuzzyRegex.InfiniteMatchTimeout, DefaultVersion)
            .Should()
            .BeFalse();
    }
}
