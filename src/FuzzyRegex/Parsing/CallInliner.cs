using System.Diagnostics;

namespace Fuzzy.Text.RegularExpressions.Parsing;

/// <summary>
/// NOT UPSTREAM (known defect D101): replaces each group call that nothing can tell from its group
/// written out by a copy of the group's body, before the pattern is optimised. The call then costs
/// what the written-out group costs, and a partial match cannot run out inside it.
/// </summary>
/// <remarks>
/// <para>
/// Why a copy answers as the call. D40: a call answers as its group written out at the call site,
/// in the call site's direction and fuzziness. D51: the return puts back every group and repeat,
/// and D62: so does a partial result that ends inside the call. So after a call nothing it did is
/// visible except the text it consumed, which the copy consumes the same way; inside it, a group
/// the caller set reads the same value in both forms, because the call starts from the caller's
/// groups (<c>Matcher.AdvanceGroupCall</c> saves them for the return and leaves them in place;
/// witness: <c>CallInliningTests.A_backreference_to_a_group_inside_the_copy_keeps_the_call</c>,
/// whose first row reads a caller's group from inside the copy). The copy is put where the call
/// was, so it is compiled with that place's direction and fuzziness, and inside a fuzzy section it
/// is not optimised, exactly as the written-out group would be.
/// </para>
/// <para>
/// A call is copied when all of these hold (each has a witness in <c>CallInliningTests</c>):
/// </para>
/// <list type="number">
/// <item>It calls a numbered or named group, not the pattern as a whole, and no call reachable from
/// the group's body leads back to a group already being copied: a recursion stays a call.</item>
/// <item>The copy's capture groups become plain groups, since a call discards their captures on
/// return; so nothing in the copy may read a group the copy itself sets (a backreference, or a
/// condition on it), because the read would see the caller's group instead. A read of a group set
/// outside the copy is fine.</item>
/// <item>Every call in the body can be copied too.</item>
/// <item>The pattern is not compiled to keep its calls (<see cref="Info.UpstreamCalls"/>, which
/// upstream's capture rule needs: there a call's capture stays on the list, so every call is
/// visible), nor under an ablation that gives calls upstream's features or copies
/// (<see cref="Info.UpstreamCallFeatures"/>, <see cref="Info.UpstreamCallCopies"/>).</item>
/// <item>The copies so far leave the pattern at most four times its size as written, or 1,024
/// nodes larger if that is more, and at most 32 levels deeper than its deepest point as written.
/// Copies multiply with nesting, <c>(?&lt;b&gt;(?&amp;a)(?&amp;a))(?&lt;c&gt;(?&amp;b)(?&amp;b))</c>,
/// and a chain of calls nests them; the calls left over stay calls. A repeat counts its body once per required repetition, as the
/// graph builder unrolls it.</item>
/// </list>
/// <para>
/// Why a body's capture groups are made plain rather than the call kept (D101 research's
/// <c>FR_STRIP</c>, 2026-10-21): kept, every call whose group holds a group of its own stays a call
/// and keeps B1's cost, about twice main's on D101's rows (<c>((x))(?(?!(?1)*y)a|b)\Z</c> over
/// 'x' * 100: 164,889 Debug steps, main 82,705, written out 4,089); made plain, 162 more of the
/// 2,660 call patterns of D98's corpora lose every call, and their 3,300 rows answer exactly as
/// before, captures included.
/// </para>
/// <para>
/// Nothing else in a body is a reason to keep the call: a verb, <c>\K</c>, a fuzzy section, a
/// lookaround or a condition on a lookaround in the copy is the same item written out, and D40
/// says the call answers as it.
/// </para>
/// <para>
/// Cost: one walk of the tree, plus one analysis of each called group's body, memoised, plus the
/// copies themselves, which the cap bounds.
/// </para>
/// </remarks>
internal static class CallInliner
{
    /// <summary>The fewest nodes the copies may add, whatever the pattern's size.</summary>
    private const long _minimumAllowance = 1024;

    /// <summary>How much deeper than the tree as parsed the copies may make it.</summary>
    /// <remarks>
    /// SHORTCUT: the parser's nesting limit (<see cref="ParseFunctions.MaxNestingDepth"/>) is what
    /// keeps every later pass over the tree inside the stack, and it was measured with about twice
    /// the headroom it needs on a 1 MB stack (D110: 1,100 to 1,450 levels compiled before the
    /// guard). Copies may add this many levels to the parsed tree's depth, which also bounds the
    /// inliner's own recursion along a chain of calls. Ceiling: a pattern at the nesting limit, on a
    /// thread with a much smaller stack than 1 MB, has less headroom than that. Upgrade path: a
    /// stack check in each container node's recursive passes, the one D110's own SHORTCUT names.
    /// </remarks>
    private const int _extraDepth = 32;

    /// <summary>
    /// Replaces the copyable calls in <paramref name="parsed"/> by copies, in place, and drops them
    /// from <see cref="Info.GroupCalls"/>.
    /// </summary>
    /// <param name="info">The parse state, after <see cref="RegexBase.FixGroups"/>.</param>
    /// <param name="parsed">The pattern as parsed, not yet optimised.</param>
    /// <returns>The pattern with the calls replaced.</returns>
    internal static RegexBase Run(Info info, RegexBase parsed)
    {
        if (info.GroupCalls.Count == 0 || info.UpstreamCalls || info.UpstreamCallFeatures || info.UpstreamCallCopies)
        {
            return parsed;
        }

        // The tree must be the one the parser built: D50's copy of it for the optimiser is made
        // after this, so the called groups' copies are prepared from a tree with these copies in.
        Debug.Assert(info.PatternAsWritten is null, "the calls are copied before the tree is copied for the optimiser");

        (long size, int depth) = SizeAndDepth(parsed);
        var walk = new Walk(info, Math.Max(3 * size, _minimumAllowance), depth + _extraDepth);
        RegexBase result = walk.Rewrite(parsed, 1);

        // Every call FixGroups found is in the tree, and the walk met each one (assumption: a call
        // joins GroupCalls in CallGroup.FixGroups and nowhere else before this point).
        Debug.Assert(walk.CallsMet == info.GroupCalls.Count, "the walk met every call FixGroups recorded");

        if (walk.Copied.Count != 0)
        {
            ForgetTheCopiedCalls(info, walk.Copied);
            info.InlinedCalls = walk.Copied.Count;
        }

        return result;
    }

    /// <summary>Drops the calls that were written out from <see cref="Info.GroupCalls"/>.</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="copied">The calls written out.</param>
    /// <remarks>
    /// Its own method: a lambda in <see cref="Run"/> capturing a local there made Run allocate the
    /// closure on entry, before its early return, on every compile (24 B).
    /// </remarks>
    private static void ForgetTheCopiedCalls(Info info, HashSet<CallGroup> copied) =>
        info.GroupCalls.RemoveAll(entry => copied.Contains((CallGroup)entry.Call));

    /// <summary>
    /// The node count of a tree, with a repeat's body counted once per required repetition and
    /// saturating rather than overflowing, and the tree's depth.
    /// </summary>
    /// <param name="node">The tree.</param>
    /// <returns>Its size and depth.</returns>
    private static (long Size, int Depth) SizeAndDepth(RegexBase node)
    {
        long total = 1;
        int depth = 0;
        foreach (RegexBase child in Children(node))
        {
            (long w, int d) = SizeAndDepth(child);
            if (node is GreedyRepeat repeat && repeat.MinCount > 1)
            {
                w = w > long.MaxValue / repeat.MinCount ? long.MaxValue / 2 : w * repeat.MinCount;
            }

            total = Math.Min(long.MaxValue / 2, total + w);
            depth = Math.Max(depth, d);
        }

        return (total, depth + 1);
    }

    /// <summary>The children of a node that can hold a call, a group or a read of one.</summary>
    /// <param name="node">The node.</param>
    /// <returns>Its children, none for a leaf.</returns>
    private static List<RegexBase> Children(RegexBase node) =>
        node switch
        {
            Sequence s => s.Items,
            StringSet => [],
            Branch b => b.Branches,
            GreedyRepeat r => [r.Subpattern],
            Atomic a => [a.Subpattern],
            Group g => [g.Subpattern],
            LookAround l => [l.Subpattern],
            LookAroundConditional c => [c.Subpattern, c.YesItem, c.NoItem],
            Conditional c => [c.YesItem, c.NoItem],
            Fuzzy f => [f.Subpattern],
            _ => [],
        };

    /// <summary>What copying one group's body involves.</summary>
    /// <param name="Copyable">Whether the body can be copied at all.</param>
    /// <param name="Size">The copy's size, called groups' copies included.</param>
    /// <param name="Depth">How deep the copy's tree is, called groups' copies included.</param>
    /// <param name="Sets">The groups the body sets, called groups' bodies included.</param>
    /// <param name="Reads">The groups the body reads, called groups' bodies included.</param>
    private sealed record Plan(bool Copyable, long Size, int Depth, HashSet<int> Sets, HashSet<int> Reads);

    /// <summary>What a survey of part of a body found.</summary>
    /// <param name="Copyable">Whether it can be copied.</param>
    /// <param name="Size">Its size.</param>
    /// <param name="Depth">Its depth.</param>
    /// <param name="Cut">Whether the survey stopped at the depth allowance, so the answer is not the body's own.</param>
    private readonly record struct Survey(bool Copyable, long Size, int Depth, bool Cut);

    /// <summary>The walk over one pattern.</summary>
    /// <param name="info">The parse state.</param>
    /// <param name="allowance">How many nodes the copies may add.</param>
    /// <param name="maxDepth">How deep the tree may become.</param>
    private sealed class Walk(Info info, long allowance, int maxDepth)
    {
        private readonly Dictionary<int, Plan> _plans = [];
        private readonly HashSet<int> _planning = [];
        private long _allowance = allowance;

        /// <summary>The calls replaced by copies.</summary>
        internal HashSet<CallGroup> Copied { get; } = new(ReferenceEqualityComparer.Instance);

        /// <summary>How many calls the walk of the pattern met.</summary>
        internal int CallsMet { get; private set; }

        /// <summary>Rewrites a node of the pattern in place, replacing the calls it can.</summary>
        /// <param name="node">The node.</param>
        /// <param name="depth">The node's depth in the tree, 1 for the root.</param>
        /// <returns>The node, or the copy that replaces it.</returns>
        internal RegexBase Rewrite(RegexBase node, int depth)
        {
            if (node is CallGroup call)
            {
                CallsMet++;
                if (
                    call.GroupNumber == 0
                    || PlanFor(call.GroupNumber, maxDepth - depth + 1) is not { Copyable: true } plan
                    || plan.Size > _allowance
                    || depth - 1 + plan.Depth > maxDepth
                )
                {
                    return call;
                }

                _allowance -= plan.Size;
                Copied.Add(call);
                return Copy(call.GroupNumber);
            }

            RewriteChildren(node, child => Rewrite(child, depth + 1));
            return node;
        }

        /// <summary>
        /// A copy of a group's body with its groups made plain and its calls replaced by copies.
        /// Only for a group whose plan says it can be copied.
        /// </summary>
        /// <param name="group">The group number.</param>
        /// <returns>The copy.</returns>
        private RegexBase Copy(int group)
        {
            Debug.Assert(_plans[group].Copyable, "only a copyable group is copied");
            return Plainly(info.DefinedGroups[group].Group.Subpattern.Clone());
        }

        /// <summary>Makes a cloned tree's groups plain and replaces its calls by copies.</summary>
        /// <param name="node">A node of the clone.</param>
        /// <returns>The node, or what replaces it.</returns>
        private RegexBase Plainly(RegexBase node)
        {
            switch (node)
            {
                case Group group:
                    return Plainly(group.Subpattern);
                case CallGroup call:
                    // The plan of the group that holds this call said every call in it can be
                    // copied; Clone shares the call node with the tree, so it is replaced, never
                    // changed.
                    Debug.Assert(_plans[call.GroupNumber].Copyable, "a copyable body holds only copyable calls");
                    return Copy(call.GroupNumber);
                default:
                    RewriteChildren(node, Plainly);
                    return node;
            }
        }

        /// <summary>
        /// Whether a group's body can be copied, how big and how deep the copy is, and what it sets
        /// and reads. A group met again while its own plan is being made is a recursion, and cannot
        /// be copied. A plan is kept for later only when the survey read the whole body: one cut at
        /// the depth allowance says only that this site cannot take the copy.
        /// </summary>
        /// <param name="group">The group number.</param>
        /// <param name="depthAllowance">How deep the copy may be at this site.</param>
        /// <returns>The plan, or null when the survey was cut.</returns>
        private Plan? PlanFor(int group, int depthAllowance)
        {
            if (_plans.TryGetValue(group, out Plan? known))
            {
                return known;
            }

            if (!_planning.Add(group))
            {
                return new Plan(false, 0, 0, [], []);
            }

            HashSet<int> sets = [];
            HashSet<int> reads = [];
            Survey survey = SurveyOf(info.DefinedGroups[group].Group.Subpattern, sets, reads, depthAllowance);
            _planning.Remove(group);
            if (survey.Cut)
            {
                return null;
            }

            var plan = new Plan(survey.Copyable && !sets.Overlaps(reads), survey.Size, survey.Depth, sets, reads);
            _plans[group] = plan;
            return plan;
        }

        /// <summary>
        /// Reads a body for <see cref="PlanFor"/>: whether everything in it can be copied, its size
        /// and depth, and the groups it sets and reads, the called groups' copies included. It stops
        /// at the depth allowance, which bounds its own recursion as well as the copy's depth.
        /// </summary>
        /// <param name="node">A node of the body.</param>
        /// <param name="sets">The groups the copy would set, added to.</param>
        /// <param name="reads">The groups the copy reads, added to.</param>
        /// <param name="depthAllowance">How deep this node's copy may be.</param>
        /// <returns>What it found.</returns>
        private Survey SurveyOf(RegexBase node, HashSet<int> sets, HashSet<int> reads, int depthAllowance)
        {
            if (depthAllowance <= 0)
            {
                return new Survey(false, 0, 0, Cut: true);
            }

            if (!IsKnown(node))
            {
                return new Survey(false, 0, 0, Cut: false);
            }

            switch (node)
            {
                case CallGroup call:
                    return SurveyOfCall(call, sets, reads, depthAllowance);
                case Group group:
                    sets.Add(group.GroupNumber);
                    break;
                case RefGroup reference:
                    reads.Add(reference.GroupNumber);
                    break;
                case Conditional { IsDefine: false, GroupNumber: int conditioned }:
                    reads.Add(conditioned);
                    break;
                default:
                    break;
            }

            bool copyable = true;
            long size = 1;
            int depth = 0;
            foreach (RegexBase child in Children(node))
            {
                Survey part = SurveyOf(child, sets, reads, depthAllowance - 1);
                if (part.Cut)
                {
                    return part;
                }

                copyable &= part.Copyable;
                long w = part.Size;
                if (node is GreedyRepeat repeat && repeat.MinCount > 1)
                {
                    w = w > long.MaxValue / repeat.MinCount ? long.MaxValue / 2 : w * repeat.MinCount;
                }

                size = Math.Min(long.MaxValue / 2, size + w);
                depth = Math.Max(depth, part.Depth);
            }

            return new Survey(copyable, size, depth + 1, Cut: false);
        }

        /// <summary><see cref="SurveyOf"/> for a call: the called group's plan stands in its place.</summary>
        /// <param name="call">The call.</param>
        /// <param name="sets">The groups the copy would set, added to.</param>
        /// <param name="reads">The groups the copy reads, added to.</param>
        /// <param name="depthAllowance">How deep the call's copy may be.</param>
        /// <returns>What it found.</returns>
        private Survey SurveyOfCall(CallGroup call, HashSet<int> sets, HashSet<int> reads, int depthAllowance)
        {
            if (call.GroupNumber == 0)
            {
                return new Survey(false, 0, 0, Cut: false);
            }

            if (PlanFor(call.GroupNumber, depthAllowance) is not { } inner)
            {
                return new Survey(false, 0, 0, Cut: true);
            }

            if (!inner.Copyable)
            {
                return new Survey(false, 0, 0, Cut: false);
            }

            // The called body's sets and reads count in this copy too: they are in it. A plan made
            // for another site can be deeper than this one allows.
            sets.UnionWith(inner.Sets);
            reads.UnionWith(inner.Reads);
            return inner.Depth > depthAllowance
                ? new Survey(false, 0, 0, Cut: true)
                : new Survey(true, inner.Size, inner.Depth, Cut: false);
        }

        /// <summary>Whether a node is one whose every child <see cref="Children"/> lists.</summary>
        /// <param name="node">The node.</param>
        /// <returns><see langword="true"/> for a known composite or a leaf that holds no node.</returns>
        private static bool IsKnown(RegexBase node) =>
            node
                is Sequence
                    or Branch
                    or GreedyRepeat
                    or Atomic
                    or Group
                    or LookAround
                    or LookAroundConditional
                    or Conditional
                    or Fuzzy
                    or CallGroup
                    or RefGroup
                    or ZeroWidthBase
                    or Character
                    or String
                    or SetBase
                    or Any
                    or Property
                    or Range
                    or Grapheme
                    or GraphemeBoundary;

        /// <summary>Replaces each child of a composite node by what <paramref name="rewrite"/> returns.</summary>
        /// <param name="node">The node.</param>
        /// <param name="rewrite">The rewrite for one child.</param>
        private static void RewriteChildren(RegexBase node, Func<RegexBase, RegexBase> rewrite)
        {
            switch (node)
            {
                case Sequence s:
                    for (int i = 0; i < s.Items.Count; i++)
                    {
                        s.Items[i] = rewrite(s.Items[i]);
                    }

                    break;
                case StringSet:
                    break;
                case Branch b:
                    for (int i = 0; i < b.Branches.Count; i++)
                    {
                        b.Branches[i] = rewrite(b.Branches[i]);
                    }

                    break;
                case GreedyRepeat r:
                    r.Subpattern = rewrite(r.Subpattern);
                    break;
                case Atomic a:
                    a.Subpattern = rewrite(a.Subpattern);
                    break;
                case Group g:
                    g.Subpattern = rewrite(g.Subpattern);
                    break;
                case LookAround l:
                    l.Subpattern = rewrite(l.Subpattern);
                    break;
                case LookAroundConditional c:
                    c.Subpattern = rewrite(c.Subpattern);
                    c.YesItem = rewrite(c.YesItem);
                    c.NoItem = rewrite(c.NoItem);
                    break;
                case Conditional c:
                    c.YesItem = rewrite(c.YesItem);
                    c.NoItem = rewrite(c.NoItem);
                    break;
                case Fuzzy f:
                    f.Subpattern = rewrite(f.Subpattern);
                    break;
                default:
                    break;
            }
        }
    }
}
