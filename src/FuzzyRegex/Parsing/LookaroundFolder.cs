using System.Diagnostics;

namespace Fuzzy.Text.RegularExpressions.Parsing;

/// <summary>
/// NOT UPSTREAM (D101): folds each lookaround whose body succeeds at every position whatever the text:
/// a negative one becomes <c>(*FAIL)</c>, a positive one with no capture group becomes nothing.
/// </summary>
/// <remarks>
/// <para>
/// A body succeeds everywhere when it has a path from its start to its end through items that match
/// the empty string without reading anything: an empty sequence, a repeat that can take no
/// iteration, an alternative that can, a group around such a path. Backtracking reaches that path
/// whatever the text, since every item on it has an empty way through and nothing in the body can
/// cut (no verb, no <c>\K</c>; an atomic group or possessive repeat commits to its first success,
/// and the items after it, each with an empty way through, still succeed). An anchor, a word
/// boundary, a lookaround, a backreference, a condition, a call or a fuzzy section reads the text or
/// the state, so it is not on such a path. The body then needs no text at all, so no continuation of
/// a partial match can change the lookaround's outcome, and the partial second pass has nothing to
/// search there: `(?:x(?!(?!(?R)?))|z)+q` is `(?:x|z)+q`.
/// </para>
/// <para>
/// A positive lookaround's success leaves the groups its first successful path set, so it is
/// folded only when its body holds no capture group (witness: <c>(?=(a)?)\w</c> over 'a' sets group
/// 1). A verb anywhere in the body, or a call where the pattern holds a verb or <c>\K</c>, keeps it
/// (witness: <c>(?=(?:a(*PRUNE)b)?)a</c> over 'ac' has no match, ledger entry 47); an anchor or other
/// zero-width item is not on an empty path (witness: <c>a(?!\b)\w</c> over 'ab' matches). A
/// lookaround inside a fuzzy section is left alone, conservatively: no witness was found (six rows,
/// 2026-10-25), and the one fuzzy rule that treats a failed lookaround specially, the insertion in
/// front of it (ledger entry 50), applies to positive ones, which never fail here.
/// </para>
/// </remarks>
internal static class LookaroundFolder
{
    /// <summary>Folds the always-matching lookarounds of a tree, in place.</summary>
    /// <param name="node">The pattern, after the calls are written out.</param>
    /// <param name="info">The parse state: the calls a fold removed leave <see cref="Info.GroupCalls"/>.</param>
    /// <param name="upstreamCalls">Whether every call is kept, as upstream compiles it (see
    /// <see cref="PatternCompiler.Compile"/>): then nothing is folded.</param>
    /// <returns>The pattern with those lookarounds folded.</returns>
    internal static RegexBase Run(RegexBase node, Info info, bool upstreamCalls)
    {
        // The compile-parity seam compares code with upstream's, which has no such fold.
        // Assumption (rule 10): a lookaround node exists only if the parser built one
        // (ParseFunctions.ParseLookaround and ParseLookaroundConditional, which set HasLookaround);
        // every other constructor of LookAround and LookAroundConditional is the Optimise or Clone
        // of an existing one (Nodes.cs). The check walks the tree without allocating.
        Debug.Assert(info.HasLookaround || !ContainsLookaround(node), "D101: a lookaround the parser did not record");
        if (upstreamCalls || !info.HasLookaround)
        {
            return node;
        }

        var verbs = new PatternVerbs(node);
        RegexBase folded = Fold(node, inFuzzy: false, ref verbs);
        if (info.GroupCalls.Count != 0)
        {
            var left = new HashSet<RegexBase>(ReferenceEqualityComparer.Instance);
            CollectCalls(folded, left);
            info.GroupCalls.RemoveAll(entry => !left.Contains(entry.Call));
        }

        return folded;
    }

    private static void CollectCalls(RegexBase node, HashSet<RegexBase> calls)
    {
        switch (node)
        {
            case CallGroup call:
                calls.Add(call);
                break;
            case Sequence s:
                s.Items.ForEach(i => CollectCalls(i, calls));
                break;
            case StringSet:
                break;
            case Branch b:
                b.Branches.ForEach(i => CollectCalls(i, calls));
                break;
            case GreedyRepeat r:
                CollectCalls(r.Subpattern, calls);
                break;
            case Atomic a:
                CollectCalls(a.Subpattern, calls);
                break;
            case Group g:
                CollectCalls(g.Subpattern, calls);
                break;
            case LookAround l:
                CollectCalls(l.Subpattern, calls);
                break;
            case LookAroundConditional c:
                CollectCalls(c.Subpattern, calls);
                CollectCalls(c.YesItem, calls);
                CollectCalls(c.NoItem, calls);
                break;
            case Conditional c:
                CollectCalls(c.YesItem, calls);
                CollectCalls(c.NoItem, calls);
                break;
            case Fuzzy f:
                CollectCalls(f.Subpattern, calls);
                break;
            default:
                break;
        }
    }

    private static RegexBase Fold(RegexBase node, bool inFuzzy, ref PatternVerbs verbs)
    {
        Engine.WorkCounter.LookaroundFoldVisited();
        switch (node)
        {
            case LookAround look:
                look.Subpattern = Fold(look.Subpattern, inFuzzy, ref verbs);
                // A body that defines a group is kept whatever its sense: the group is numbered, may
                // be called or read elsewhere, and its definition must stay in the compiled pattern
                // (folded away, `a+(?<!(?1)(?P<g1>a*))` threw at match time).
                if (
                    !inFuzzy
                    && !look.Subpattern.ContainsGroup()
                    && MatchesEmptyWithoutReading(look.Subpattern)
                    && !HoldsAVerbOrKeep(look.Subpattern, verbs.Any)
                )
                {
                    return look.Positive ? new Sequence() : new Failure();
                }

                return look;
            case Sequence s:
                for (int i = 0; i < s.Items.Count; i++)
                {
                    // Written back only when it changed: a store into the list is a checked
                    // covariant array store, and most children never change.
                    RegexBase item = s.Items[i];
                    if (Fold(item, inFuzzy, ref verbs) is var folded && !ReferenceEquals(folded, item))
                    {
                        s.Items[i] = folded;
                    }
                }

                return s;
            case StringSet:
                return node;
            case Branch b:
                for (int i = 0; i < b.Branches.Count; i++)
                {
                    RegexBase branch = b.Branches[i];
                    if (Fold(branch, inFuzzy, ref verbs) is var folded && !ReferenceEquals(folded, branch))
                    {
                        b.Branches[i] = folded;
                    }
                }

                return b;
            case GreedyRepeat r:
                r.Subpattern = Fold(r.Subpattern, inFuzzy, ref verbs);
                return r;
            case Atomic a:
                a.Subpattern = Fold(a.Subpattern, inFuzzy, ref verbs);
                return a;
            case Group g:
                g.Subpattern = Fold(g.Subpattern, inFuzzy, ref verbs);
                return g;
            case LookAroundConditional c:
                c.Subpattern = Fold(c.Subpattern, inFuzzy, ref verbs);
                c.YesItem = Fold(c.YesItem, inFuzzy, ref verbs);
                c.NoItem = Fold(c.NoItem, inFuzzy, ref verbs);
                return c;
            case Conditional c:
                c.YesItem = Fold(c.YesItem, inFuzzy, ref verbs);
                c.NoItem = Fold(c.NoItem, inFuzzy, ref verbs);
                return c;
            case Fuzzy f:
                f.Subpattern = Fold(f.Subpattern, inFuzzy: true, ref verbs);
                return f;
            default:
                return node;
        }
    }

    /// <summary>Whether a tree holds a lookaround or a lookaround condition (Debug check only).</summary>
    /// <param name="node">The tree.</param>
    /// <returns><see langword="true"/> if it holds one.</returns>
    private static bool ContainsLookaround(RegexBase node)
    {
        switch (node)
        {
            case LookAround or LookAroundConditional:
                return true;
            case Sequence s:
                for (int i = 0; i < s.Items.Count; i++)
                {
                    if (ContainsLookaround(s.Items[i]))
                    {
                        return true;
                    }
                }

                return false;
            case Branch b:
                for (int i = 0; i < b.Branches.Count; i++)
                {
                    if (ContainsLookaround(b.Branches[i]))
                    {
                        return true;
                    }
                }

                return false;
            case GreedyRepeat r:
                return ContainsLookaround(r.Subpattern);
            case Atomic a:
                return ContainsLookaround(a.Subpattern);
            case Group g:
                return ContainsLookaround(g.Subpattern);
            case Conditional c:
                return ContainsLookaround(c.YesItem) || ContainsLookaround(c.NoItem);
            case Fuzzy f:
                return ContainsLookaround(f.Subpattern);
            default:
                return false;
        }
    }

    /// <summary>Whether a tree has a path that matches the empty string without reading anything.</summary>
    /// <param name="node">The tree.</param>
    /// <returns><see langword="true"/> if it has one.</returns>
    private static bool MatchesEmptyWithoutReading(RegexBase node) =>
        node switch
        {
            Sequence s => s.Items.TrueForAll(MatchesEmptyWithoutReading),
            StringSet => false,
            Branch b => b.Branches.Exists(MatchesEmptyWithoutReading),
            GreedyRepeat r => r.MinCount == 0 || MatchesEmptyWithoutReading(r.Subpattern),
            Atomic a => MatchesEmptyWithoutReading(a.Subpattern),
            Group g => MatchesEmptyWithoutReading(g.Subpattern),
            _ => false,
        };

    /// <summary>Whether a tree holds a verb or <c>\K</c> anywhere, lookarounds and sections included.</summary>
    /// <param name="node">The tree.</param>
    /// <param name="callsReachOne">Whether a call in it counts as one: true where the pattern holds a
    /// verb or <c>\K</c> somewhere a call could reach.</param>
    /// <returns><see langword="true"/> if it does.</returns>
    private static bool HoldsAVerbOrKeep(RegexBase node, bool callsReachOne)
    {
        // Loops rather than List.Exists: a lambda would capture callsReachOne and allocate a closure
        // and a delegate at every sequence and branch (most of the +8% allocation the BDN gate found
        // on CompileLargePattern, 2026-10-26).
        switch (node)
        {
            case Prune or Skip or Keep:
                return true;
            case CallGroup:
                return callsReachOne;
            case Sequence s:
                for (int i = 0; i < s.Items.Count; i++)
                {
                    if (HoldsAVerbOrKeep(s.Items[i], callsReachOne))
                    {
                        return true;
                    }
                }

                return false;
            case Branch b:
                for (int i = 0; i < b.Branches.Count; i++)
                {
                    if (HoldsAVerbOrKeep(b.Branches[i], callsReachOne))
                    {
                        return true;
                    }
                }

                return false;
            case GreedyRepeat r:
                return HoldsAVerbOrKeep(r.Subpattern, callsReachOne);
            case Atomic a:
                return HoldsAVerbOrKeep(a.Subpattern, callsReachOne);
            case Group g:
                return HoldsAVerbOrKeep(g.Subpattern, callsReachOne);
            case LookAround l:
                return HoldsAVerbOrKeep(l.Subpattern, callsReachOne);
            case LookAroundConditional c:
                return HoldsAVerbOrKeep(c.Subpattern, callsReachOne)
                    || HoldsAVerbOrKeep(c.YesItem, callsReachOne)
                    || HoldsAVerbOrKeep(c.NoItem, callsReachOne);
            case Conditional c:
                return HoldsAVerbOrKeep(c.YesItem, callsReachOne) || HoldsAVerbOrKeep(c.NoItem, callsReachOne);
            case Fuzzy f:
                return HoldsAVerbOrKeep(f.Subpattern, callsReachOne);
            default:
                return false;
        }
    }

    /// <summary>
    /// Whether the whole pattern holds a verb or <c>\K</c>, asked only when a lookaround could fold:
    /// most patterns have none that could, and the walk is the whole tree.
    /// </summary>
    /// <param name="root">The whole pattern.</param>
    private struct PatternVerbs(RegexBase root)
    {
        private bool? _any;

        /// <summary>Gets whether the pattern holds a verb or <c>\K</c> anywhere.</summary>
        internal bool Any => _any ??= HoldsAVerbOrKeep(root, callsReachOne: false);
    }
}
