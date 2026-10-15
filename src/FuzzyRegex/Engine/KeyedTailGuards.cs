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
/// </remarks>
internal sealed class KeyedTailGuards
{
    private readonly Dictionary<TailKeyValues, GuardList> _lists = [];
    private readonly List<Dictionary<TailKeyValues, GuardList>?> _saved = [];
    private long[] _currentKey = [];
    private GuardList? _current;
    private bool _hasCurrent;

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

        list = new GuardList();
        var stored = new TailKeyValues([.. key]);
        _lists[stored] = list;
        Remember(key, list);
        return list;
    }

    /// <summary>Forgets every list, as <c>reset_guard_list</c> does for a plain one.</summary>
    /// <param name="newAttempt">
    /// Whether a new attempt begins, so no save made before can be restored; a group call's
    /// reset keeps the saves, since its return restores the one made just before it.
    /// </param>
    internal void Reset(bool newAttempt)
    {
        _changes++;
        _hasCurrent = false;
        _current = null;
        if (newAttempt)
        {
            _saved.Clear();
        }

        if (_lists.Count > 0)
        {
            _lists.Clear();
        }
    }

    /// <summary>Forgets everything, for a state reused by another call (<c>MatchState.Init</c>).</summary>
    internal void Clear()
    {
        Reset(newAttempt: true);
        _changes = 0;
        _currentKey = [];
    }

    /// <summary>
    /// Upstream saves a repeat's guard lists in full at a lookaround, a condition, an atomic group
    /// and a call (<c>push_repeat_data</c>, <c>_regex.c:2552</c>); this is the same for the keyed
    /// lists. Pushes the save's number and the change count; the copy itself is kept here.
    /// </summary>
    /// <param name="stack">The backtrack stack.</param>
    internal void PushTo(ByteStack stack)
    {
        Dictionary<TailKeyValues, GuardList>? copy = null;
        if (_lists.Count > 0)
        {
            // SHORTCUT: a full copy per save, as upstream copies its plain lists. Ceiling: a
            // lookaround inside a loop with many keys pays O(entries) per save; an undo journal
            // (the design note's option b) is the upgrade if a row shows it.
            copy = new Dictionary<TailKeyValues, GuardList>(_lists.Count);
            foreach (KeyValuePair<TailKeyValues, GuardList> pair in _lists)
            {
                copy[pair.Key] = pair.Value.Copy();
            }
        }

        _saved.Add(copy);
        stack.PushSize(_saved.Count - 1);
        stack.PushSize(_changes);
    }

    /// <summary>Undoes <see cref="PushTo"/>: the lists as they were at the save.</summary>
    /// <param name="stack">The backtrack stack.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    internal bool PopFrom(ByteStack stack)
    {
        if (!stack.PopSize(out long changes) || !stack.PopSize(out long slot))
        {
            return false;
        }

        // A save that was never restored (its entry dropped by an atomic success) leaves later
        // slots behind; the pop of an earlier save drops them too.
        Dictionary<TailKeyValues, GuardList>? copy = (int)slot < _saved.Count ? _saved[(int)slot] : null;
        if ((int)slot < _saved.Count)
        {
            _saved.RemoveRange((int)slot, _saved.Count - (int)slot);
        }

        if (changes == _changes)
        {
            return true;
        }

        _changes++;
        _hasCurrent = false;
        _current = null;
        _lists.Clear();
        if (copy is not null)
        {
            foreach (KeyValuePair<TailKeyValues, GuardList> pair in copy)
            {
                _lists[pair.Key] = pair.Value;
            }
        }

        return true;
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
    private readonly long[] _values;
    private readonly int _hash;

    /// <summary>Wraps <paramref name="values"/> without copying them.</summary>
    /// <param name="values">The values.</param>
    internal TailKeyValues(long[] values)
    {
        _values = values;
        var hash = new HashCode();
        foreach (long value in values)
        {
            hash.Add(value);
        }

        _hash = hash.ToHashCode();
    }

    /// <inheritdoc/>
    public bool Equals(TailKeyValues other) => _hash == other._hash && _values.AsSpan().SequenceEqual(other._values);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TailKeyValues other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _hash;
}
