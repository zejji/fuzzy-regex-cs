using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// NOT UPSTREAM (D85): one repeat's tail guards when the rest of the match reads group state, one
/// <see cref="GuardList"/> per distinct key (<see cref="TailGuardKeySpec"/>), with the list for the
/// key last asked about cached so that the common case, the same key as last time, costs one
/// comparison of a few numbers. See <c>MatchState.KeyedTailGuardList</c>.
/// </summary>
/// <remarks>
/// A key's list holds only failures recorded while the read state equalled that key, so a read
/// under another key sees none of them, which is what makes the guard sound (ledger entry 84).
/// Dropping a list or skipping a write only loses pruning, never an answer.
/// <para>
/// sync-divergence: upstream keeps one tail_guard_list per repeat (_regex.c :339) / this keeps one
/// per key / D85, ledger 84. Re-aligning: see MatchState.ActiveGuardList.
/// </para>
/// </remarks>
internal sealed class KeyedTailGuards
{
    private readonly Dictionary<TailKeyValues, GuardList> _lists = [];
    private long[] _currentKey = [];

    /// <summary>Where <see cref="CheckUnchanged"/> reads a saved key, in Debug only.</summary>
    private long[] _checkKey = [];
    private GuardList? _current;
    private bool _hasCurrent;

    /// <summary>
    /// Lists and key arrays forgotten by <see cref="Reset"/> or replaced by a restore
    /// (<see cref="PopFrom"/>), kept for reuse so that a warm call on a compiled pattern allocates
    /// nothing once its keys have been seen.
    /// </summary>
    private readonly Stack<(long[] Key, GuardList List)> _spare = [];

    /// <summary>
    /// How many times a list was written, reset or restored since the match began, so a restore
    /// can tell that nothing changed since the matching save.
    /// </summary>
    private long _changes;

    /// <summary>How many keys have a list.</summary>
    internal int Count => _lists.Count;

    /// <summary>The list for <paramref name="key"/>, or <see langword="null"/> when there is none.</summary>
    /// <param name="key">The key, in a scratch array the caller keeps.</param>
    /// <returns>The list.</returns>
    internal GuardList? Find(long[] key)
    {
        if (_hasCurrent && key.AsSpan().SequenceEqual(_currentKey))
        {
            return _current;
        }

        _ = _lists.TryGetValue(new TailKeyValues(key), out GuardList? list);
        Remember(key, list);
        return list;
    }

    /// <summary>
    /// The list to write under <paramref name="key"/>, made if there is none yet; or
    /// <see langword="null"/> when <paramref name="mayAdd"/> is false and there is none.
    /// </summary>
    /// <param name="key">The key, in a scratch array the caller keeps.</param>
    /// <param name="mayAdd">Whether a new list may be made (the memory cap).</param>
    /// <returns>The list.</returns>
    internal GuardList? ForWrite(long[] key, bool mayAdd)
    {
        _changes++;
        GuardList? list = Find(key);
        if (list is not null || !mayAdd)
        {
            return list;
        }

        long[] copy;
        if (_spare.Count > 0 && _spare.Peek().Key.Length == key.Length)
        {
            (copy, list) = _spare.Pop();
            key.AsSpan().CopyTo(copy);
            list.Reset();
        }
        else
        {
            copy = [.. key];
            list = new GuardList();
        }

        _lists[new TailKeyValues(copy)] = list;
        Remember(key, list);
        return list;
    }

    /// <summary>Forgets every list, as <c>reset_guard_list</c> does for a plain one.</summary>
    internal void Reset()
    {
        _changes++;
        _hasCurrent = false;
        _current = null;
        ReturnListsToSpare();
    }

    /// <summary>Forgets everything, for a state reused by another call (<c>MatchState.Init</c>).</summary>
    internal void Clear()
    {
        Reset();
        _changes = 0;
    }

    /// <summary>
    /// Upstream saves a repeat's guard lists in full at a lookaround, a condition, an atomic group
    /// and a call (<c>push_repeat_data</c>, <c>_regex.c:2552</c>); this is the same for the keyed
    /// lists, in the same form: each list's spans and count (<see cref="GuardList.PushTo"/>) and
    /// its key, then the key length, the bytes all that took, how many lists and the change count.
    /// </summary>
    /// <remarks>
    /// D103: this used to save a new dictionary of copied lists, about 440 bytes of heap a save,
    /// and a conditional inside a loop saves on every entry. The byte stack allocates nothing once
    /// it has grown. The cost per save is O(keys + spans), the order upstream pays for a plain list.
    /// </remarks>
    /// <param name="stack">The backtrack stack.</param>
    internal void PushTo(ByteStack stack)
    {
        int start = stack.Count;
        int keyLength = 0;
        foreach (KeyValuePair<TailKeyValues, GuardList> pair in _lists)
        {
            long[] key = pair.Key.Values;
            Debug.Assert(keyLength == 0 || key.Length == keyLength, "a store's keys share one length");
            keyLength = key.Length;
            pair.Value.PushTo(stack);
            stack.PushBlock(MemoryMarshal.AsBytes(key.AsSpan()));
        }

        int bytes = stack.Count - start;
        stack.PushSize(keyLength);
        stack.PushSize(bytes);
        stack.PushSize(_lists.Count);
        stack.PushSize(_changes);
    }

    /// <summary>Undoes <see cref="PushTo"/>: the lists as they were at the save.</summary>
    /// <remarks>
    /// When nothing changed since the save the lists are already right and the saved bytes are
    /// dropped. Otherwise the current lists go back to the spare pool and the saved ones are rebuilt
    /// from it, so a warm call allocates nothing. A save that was never restored (its entry dropped
    /// by an atomic success) leaves nothing behind but stack bytes, which the drop took.
    /// </remarks>
    /// <param name="stack">The backtrack stack.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    internal bool PopFrom(ByteStack stack)
    {
        if (
            !stack.PopSize(out long changes)
            || !stack.PopSize(out long lists)
            || !stack.PopSize(out long bytes)
            || !stack.PopSize(out long keyLength)
        )
        {
            return false;
        }

        if (changes == _changes)
        {
            CheckUnchanged(stack, lists, keyLength);
            return stack.DropBlock((int)bytes);
        }

        _changes++;
        _hasCurrent = false;
        _current = null;
        ReturnListsToSpare();

        int end = stack.Count - (int)bytes;
        for (long i = 0; i < lists; i++)
        {
            long[] key;
            GuardList list;
            if (_spare.Count > 0 && _spare.Peek().Key.Length == keyLength)
            {
                (key, list) = _spare.Pop();
            }
            else
            {
                key = new long[keyLength];
                list = new GuardList();
            }

            if (!stack.PopBlock(MemoryMarshal.AsBytes(key.AsSpan())) || !list.RestoreFrom(stack))
            {
                return false;
            }

            bool added = _lists.TryAdd(new TailKeyValues(key), list);
            Debug.Assert(added, "a save holds each key once");
        }

        Debug.Assert(stack.Count == end, "the restore read back exactly the bytes the save pushed");
        return true;
    }

    /// <summary>Moves every list to the spare pool, keeping its key array with it.</summary>
    private void ReturnListsToSpare()
    {
        if (_lists.Count > 0)
        {
            foreach (KeyValuePair<TailKeyValues, GuardList> pair in _lists)
            {
                _spare.Push((pair.Key.Values, pair.Value));
            }

            _lists.Clear();
        }
    }

    /// <summary>
    /// Debug only: asserts that a save whose change count still matches holds exactly the lists
    /// there are now, which is what lets <see cref="PopFrom"/> drop it unread. It rests on every
    /// edit of a keyed list going through <see cref="ForWrite"/> or <see cref="Reset"/>, both of
    /// which count a change (<c>MatchState.KeyedTailGuardList</c> hands out a list to write only
    /// from <see cref="ForWrite"/>).
    /// </summary>
    /// <param name="stack">The backtrack stack, with the save's lists on top.</param>
    /// <param name="lists">How many lists the save holds.</param>
    /// <param name="keyLength">Their key length.</param>
    [Conditional("DEBUG")]
    private void CheckUnchanged(ByteStack stack, long lists, long keyLength)
    {
        Debug.Assert(lists == _lists.Count, "an unchanged store has as many lists as its save");
        if (lists == 0)
        {
            return;
        }

        if (_checkKey.Length != keyLength)
        {
            _checkKey = new long[keyLength];
        }

        int top = stack.Count;
        for (long i = 0; i < lists; i++)
        {
            bool read = stack.ReadBlockBelow(top, MemoryMarshal.AsBytes(_checkKey.AsSpan()));
            top -= (int)keyLength * sizeof(long);
            Debug.Assert(
                read
                    && _lists.TryGetValue(new TailKeyValues(_checkKey), out GuardList? list)
                    && list.MatchesSaved(stack, ref top),
                "an unchanged store's lists equal its save"
            );
        }
    }

    /// <summary>Caches <paramref name="list"/> as the answer for <paramref name="key"/>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="list">Its list, or <see langword="null"/> for none yet.</param>
    private void Remember(long[] key, GuardList? list)
    {
        if (_currentKey.Length != key.Length)
        {
            _currentKey = new long[key.Length];
        }

        key.AsSpan().CopyTo(_currentKey);
        _current = list;
        _hasCurrent = true;
    }
}

/// <summary>NOT UPSTREAM (D85): a tail guard key's values with their hash, for a dictionary.</summary>
internal readonly struct TailKeyValues : IEquatable<TailKeyValues>
{
    private readonly int _hash;

    /// <summary>The key's values (the array the key was made from).</summary>
    internal long[] Values { get; }

    /// <summary>Wraps <paramref name="values"/> without copying them.</summary>
    /// <param name="values">The values.</param>
    internal TailKeyValues(long[] values)
    {
        Values = values;
        var hash = new HashCode();
        foreach (long value in values)
        {
            hash.Add(value);
        }

        _hash = hash.ToHashCode();
    }

    /// <inheritdoc/>
    public bool Equals(TailKeyValues other) => _hash == other._hash && Values.AsSpan().SequenceEqual(other.Values);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TailKeyValues other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _hash;
}
