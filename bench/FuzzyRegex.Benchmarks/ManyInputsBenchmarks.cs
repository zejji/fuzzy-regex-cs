using BenchmarkDotNet.Attributes;

namespace Fuzzy.Text.RegularExpressions.Benchmarks;

/// <summary>
/// One compiled pattern over 100,000 short inputs: the shape of most real use, which
/// <see cref="WorkloadBenchmarks"/> does not cover because every one of its workloads is a single
/// large subject.
/// </summary>
/// <remarks>
/// <para>
/// Over a short input the scan loop is a small part of the cost. What dominates is what every call
/// pays before it scans - state creation, buffer rental, the start-up analysis - so these workloads
/// measure what the single-subject suite cannot see, and are the ones a per-call optimisation moves.
/// </para>
/// <para>
/// The <c>FuzzyPhrase</c> rows are a fuzzy search at volume: a case-insensitive <c>{e&lt;=2}</c>
/// search for a 19-20 character phrase through 100,000 records of 40 to 50 characters, where almost every
/// record fails, with a target of well under a second for the whole set. More than one phrase is
/// measured three ways - an alternation, a named list and one pass per phrase - so the numbers say
/// which a user should write.
/// </para>
/// <para>
/// Each benchmark is one full pass over its 100,000 inputs and returns a count, so the reported time
/// IS the time for the whole list and the result cannot be optimised away.
/// </para>
/// </remarks>
// S3267 ("use Where") is disapplied here and nowhere else: LINQ would put a delegate call per
// input inside the timed body, and that cost would be reported as the library's. The loops are
// the measurement, not a style choice.
#pragma warning disable S3267
[MemoryDiagnoser]
public class ManyInputsBenchmarks
{
    /// <summary>One phrase, two errors allowed, case-insensitive.</summary>
    private static readonly FuzzyRegex _onePhrase = new(
        $"(?:{UsageCorpus.Phrases[0]}){{e<=2}}",
        FuzzyRegexOptions.IgnoreCase
    );

    /// <summary>The same under <c>ENHANCEMATCH</c>, which a caller wants for the closest alignment.</summary>
    private static readonly FuzzyRegex _onePhraseEnhanced = new(
        $"(?:{UsageCorpus.Phrases[0]}){{e<=2}}",
        FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.EnhanceMatch
    );

    /// <summary>All three phrases as one alternation inside a single fuzzy group.</summary>
    private static readonly FuzzyRegex _phrasesAlternation = new(
        $"(?:{string.Join('|', UsageCorpus.Phrases)}){{e<=2}}",
        FuzzyRegexOptions.IgnoreCase
    );

    /// <summary>All three phrases as a named list, the form the library documents for a vocabulary.</summary>
    private static readonly FuzzyRegex _phrasesNamedList = new(
        @"(?:\L<phrases>){e<=2}",
        FuzzyRegexOptions.IgnoreCase,
        new Dictionary<string, IReadOnlyCollection<string>> { ["phrases"] = UsageCorpus.Phrases }
    );

    /// <summary>One pattern per phrase, for the one-pass-each alternative.</summary>
    private static readonly FuzzyRegex[] _phrasesSeparate =
    [
        .. UsageCorpus.Phrases.Select(static e => new FuzzyRegex($"(?:{e}){{e<=2}}", FuzzyRegexOptions.IgnoreCase)),
    ];

    /// <summary>A typical address check, anchored at both ends: the validation shape.</summary>
    private static readonly FuzzyRegex _email = new(@"^[\w.+'-]+@[\w-]+(?:\.[\w-]+)+$");

    /// <summary>Named groups pulled out of a fixed log layout.</summary>
    private static readonly FuzzyRegex _logLine = new(@"^(?<date>\d{4}-\d\d-\d\d) (?<level>[A-Z]+) (?<msg>.*)$");

    /// <summary>Digits replaced, as a redaction pass over each line.</summary>
    private static readonly FuzzyRegex _digits = new(@"\d+");

    /// <summary>
    /// Case-insensitive over non-ASCII text, with full case folding, so <c>STRASSE</c> matches
    /// <c>straße</c>: the fold-table path a Latin-1-only benchmark never reaches.
    /// </summary>
    private static readonly FuzzyRegex _accented = new(
        "straße|café",
        FuzzyRegexOptions.IgnoreCase | FuzzyRegexOptions.FullCase
    );

    /// <summary>
    /// A case-insensitive alternation of plain words (D84): the compiler puts an ignore-case
    /// first-set check in front of the branch, which the leading-units screen must see through.
    /// </summary>
    private static readonly FuzzyRegex _caseBlind = new("(?i)(?:amber|copper|violet)");

    /// <summary>
    /// <see cref="_caseBlind"/> with <c>kestrel</c> and <c>thistle</c> for <c>copper</c> and
    /// <c>violet</c> (C6): <c>k</c> also matches U+212A KELVIN SIGN and <c>s</c> U+017F LATIN SMALL
    /// LETTER LONG S, which .NET's ignore-case rule does not know, so a screen built on that rule
    /// has to allow for them. <c>THISTLE</c> is one of the records' words, so this row matches far
    /// more often than its neighbour.
    /// </summary>
    private static readonly FuzzyRegex _caseBlindKs = new("(?i)(?:amber|kestrel|thistle)");

    /// <summary>
    /// A whole-word search for ten words (C6), upper case as the records are: six are noise
    /// words, <c>AMBER</c> opens a phrase, and <c>HARBOR</c>, <c>PARCELS</c> and <c>TIMBERLAND</c>
    /// never occur whole.
    /// </summary>
    private static readonly FuzzyRegex _wholeWords = new(
        @"\b\L<words>\b",
        FuzzyRegexOptions.None,
        new Dictionary<string, IReadOnlyCollection<string>>
        {
            ["words"] =
            [
                "ORBIT",
                "MEADOW",
                "GRANITE",
                "FALCON",
                "BEACON",
                "THISTLE",
                "AMBER",
                "HARBOR",
                "PARCELS",
                "TIMBERLAND",
            ],
        }
    );

    /// <summary>Which records carry the one phrase.</summary>
    /// <returns>How many records match.</returns>
    [Benchmark]
    public int FuzzyPhraseOneIsMatch()
    {
        int hits = 0;
        foreach (string record in UsageCorpus.Records)
        {
            if (_onePhrase.IsMatch(record))
            {
                hits++;
            }
        }
        return hits;
    }

    /// <summary>The same, keeping the matched text, as a caller that reports what was found does.</summary>
    /// <returns>The total length of the matched text.</returns>
    [Benchmark]
    public int FuzzyPhraseOneMatch()
    {
        int length = 0;
        foreach (string record in UsageCorpus.Records)
        {
            length += _onePhrase.Match(record).Length;
        }
        return length;
    }

    /// <summary>The one phrase under <c>ENHANCEMATCH</c>.</summary>
    /// <returns>The total length of the matched text.</returns>
    [Benchmark]
    public int FuzzyPhraseOneEnhanced()
    {
        int length = 0;
        foreach (string record in UsageCorpus.Records)
        {
            length += _onePhraseEnhanced.Match(record).Length;
        }
        return length;
    }

    /// <summary>Three phrases, one alternation.</summary>
    /// <returns>How many records match any phrase.</returns>
    [Benchmark]
    public int FuzzyPhraseThreeAlternation()
    {
        int hits = 0;
        foreach (string record in UsageCorpus.Records)
        {
            if (_phrasesAlternation.IsMatch(record))
            {
                hits++;
            }
        }
        return hits;
    }

    /// <summary>Three phrases, one named list.</summary>
    /// <returns>How many records match any phrase.</returns>
    [Benchmark]
    public int FuzzyPhraseThreeNamedList()
    {
        int hits = 0;
        foreach (string record in UsageCorpus.Records)
        {
            if (_phrasesNamedList.IsMatch(record))
            {
                hits++;
            }
        }
        return hits;
    }

    /// <summary>Three phrases, one full pass each.</summary>
    /// <returns>How many (record, phrase) pairs match.</returns>
    [Benchmark]
    public int FuzzyPhraseThreeSeparatePasses()
    {
        int hits = 0;
        foreach (FuzzyRegex phrase in _phrasesSeparate)
        {
            foreach (string record in UsageCorpus.Records)
            {
                if (phrase.IsMatch(record))
                {
                    hits++;
                }
            }
        }
        return hits;
    }

    /// <summary>A validation pass over 100,000 short strings.</summary>
    /// <returns>How many are valid.</returns>
    [Benchmark]
    public int ValidateEmails()
    {
        int valid = 0;
        foreach (string email in UsageCorpus.Emails)
        {
            if (_email.IsMatch(email))
            {
                valid++;
            }
        }
        return valid;
    }

    /// <summary>Group extraction by name from 100,000 log lines.</summary>
    /// <returns>The total length of the extracted level names.</returns>
    [Benchmark]
    public int ParseLogLines()
    {
        int length = 0;
        foreach (string line in UsageCorpus.LogLines)
        {
            Match m = _logLine.Match(line);
            if (m.Success)
            {
                length += m.Groups["level"].Length;
            }
        }
        return length;
    }

    /// <summary>A substitution on every one of 100,000 lines.</summary>
    /// <returns>The total length of the results.</returns>
    [Benchmark]
    public int RedactDigits()
    {
        int length = 0;
        foreach (string record in UsageCorpus.Records)
        {
            length += _digits.Replace(record, "#").Length;
        }
        return length;
    }

    /// <summary>Full case folding over 100,000 lines of non-ASCII text.</summary>
    /// <returns>How many lines match.</returns>
    [Benchmark]
    public int CaseFoldAccented()
    {
        int hits = 0;
        foreach (string line in UsageCorpus.Accented)
        {
            if (_accented.IsMatch(line))
            {
                hits++;
            }
        }
        return hits;
    }

    /// <summary>A case-insensitive alternation of three words over 100,000 records.</summary>
    /// <returns>How many matches there are in all.</returns>
    [Benchmark]
    public int CaseBlindAlternation()
    {
        int hits = 0;
        foreach (string record in UsageCorpus.Records)
        {
            hits += _caseBlind.Count(record);
        }
        return hits;
    }

    /// <summary>The same with words holding <c>k</c> and <c>s</c>, over the same records.</summary>
    /// <returns>How many matches there are in all.</returns>
    [Benchmark]
    public int CaseBlindAlternationKs()
    {
        int hits = 0;
        foreach (string record in UsageCorpus.Records)
        {
            hits += _caseBlindKs.Count(record);
        }
        return hits;
    }

    /// <summary>A whole-word search for ten words over 100,000 records.</summary>
    /// <returns>How many matches there are in all.</returns>
    [Benchmark]
    public int WholeWordRecords()
    {
        int hits = 0;
        foreach (string record in UsageCorpus.Records)
        {
            hits += _wholeWords.Count(record);
        }
        return hits;
    }

    /// <summary>
    /// The full answer behind each row's returned number, per input, for the same-answer check
    /// against Python (<see cref="AnswerText"/>): which inputs matched, where, and what a
    /// substitution produced.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, Func<string>> AnswerDetails = new Dictionary<
        string,
        Func<string>
    >
    {
        [nameof(FuzzyPhraseOneIsMatch)] = static () =>
            AnswerText.Bitmap(UsageCorpus.Records.Select(s => _onePhrase.IsMatch(s))),
        [nameof(FuzzyPhraseOneMatch)] = static () =>
            string.Join('\n', UsageCorpus.Records.Select(r => AnswerText.Span(_onePhrase.Match(r)))),
        [nameof(FuzzyPhraseOneEnhanced)] = static () =>
            string.Join('\n', UsageCorpus.Records.Select(r => AnswerText.Span(_onePhraseEnhanced.Match(r)))),
        [nameof(FuzzyPhraseThreeAlternation)] = static () =>
            AnswerText.Bitmap(UsageCorpus.Records.Select(s => _phrasesAlternation.IsMatch(s))),
        [nameof(FuzzyPhraseThreeNamedList)] = static () =>
            AnswerText.Bitmap(UsageCorpus.Records.Select(s => _phrasesNamedList.IsMatch(s))),
        [nameof(FuzzyPhraseThreeSeparatePasses)] = static () =>
            string.Join(
                '\n',
                _phrasesSeparate.Select(p => AnswerText.Bitmap(UsageCorpus.Records.Select(s => p.IsMatch(s))))
            ),
        [nameof(ValidateEmails)] = static () => AnswerText.Bitmap(UsageCorpus.Emails.Select(s => _email.IsMatch(s))),
        [nameof(ParseLogLines)] = static () =>
            string.Join(
                '\n',
                UsageCorpus.LogLines.Select(l =>
                    _logLine.Match(l) is { Success: true } m ? m.Groups["level"].Value : "-"
                )
            ),
        [nameof(RedactDigits)] = static () =>
            string.Join('\n', UsageCorpus.Records.Select(r => _digits.Replace(r, "#"))),
        [nameof(CaseFoldAccented)] = static () =>
            AnswerText.Bitmap(UsageCorpus.Accented.Select(s => _accented.IsMatch(s))),
        [nameof(CaseBlindAlternation)] = static () =>
            string.Join('\n', UsageCorpus.Records.Select(r => AnswerText.Spans(_caseBlind.Matches(r)))),
        [nameof(CaseBlindAlternationKs)] = static () =>
            string.Join('\n', UsageCorpus.Records.Select(r => AnswerText.Spans(_caseBlindKs.Matches(r)))),
        [nameof(WholeWordRecords)] = static () =>
            string.Join('\n', UsageCorpus.Records.Select(r => AnswerText.Spans(_wholeWords.Matches(r)))),
    };
}
#pragma warning restore S3267
