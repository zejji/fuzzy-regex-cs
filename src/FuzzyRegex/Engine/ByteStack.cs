using System.Buffers;
using System.Runtime.InteropServices;

namespace Fuzzy.Text.RegularExpressions.Engine;

/// <summary>
/// A stack of bytes. Port of <c>ByteStack</c> and its operations
/// (<c>upstream/src/_regex.c</c> lines 2285-2433), plus the typed push/pop helpers built on them
/// (<c>:2434-2815</c>).
/// </summary>
/// <remarks>
/// <para>
/// Upstream pushes whole structs onto one untyped byte array and pops them back, so the backtracking
/// stack holds a different layout per opcode. That is the design, not an accident of C: the matcher
/// is one function whose stack frames vary in size, and a per-opcode discriminated union here would
/// stop every future upstream diff mapping onto this file.
/// </para>
/// <para>
/// The backing array is rented from <see cref="ArrayPool{T}"/> rather than allocated, which is where
/// upstream's <c>stack_storage</c> cache on the pattern goes (design spec section 4). Upstream keeps
/// one cached buffer per pattern and caps it at 64KB; the pool does the same job across all patterns
/// and needs no lock of its own, so <c>state_fini</c>'s caching block has nothing to port.
/// </para>
/// <para>
/// Only the typed helpers the spine actually pushes are here - <see cref="PushUInt8"/> and
/// <see cref="PushSize"/> and their pops. The rest of upstream's set (<c>push_int8</c>,
/// <c>push_bool</c>, <c>push_code</c>, <c>push_int</c>, <c>push_pointer</c>, <c>push_groups</c>,
/// <c>push_captures</c>, <c>push_repeat_data</c>) arrives with its first caller in S18 and S19;
/// <c>push_pointer</c> in particular needs a decision this slice cannot make, because a node
/// reference cannot go in a byte array on a managed heap. <c>docs/PORTMAP.md</c> records them.
/// </para>
/// </remarks>
internal sealed class ByteStack : IDisposable
{
    /// <summary>Upstream <c>RE_MEMORY_LIMIT</c> (<c>upstream/src/_regex.c</c> line 40).</summary>
    private const int _memoryLimit = 0x40000000;

    private byte[] _storage = [];

    /// <summary>Upstream <c>count</c>: how many bytes are on the stack.</summary>
    internal int Count;

    /// <summary>
    /// Upstream <c>ByteStack_reset</c> (line 2300). Keeps the storage; only the count goes to zero.
    /// </summary>
    internal void Reset() => Count = 0;

    /// <summary>Upstream <c>ByteStack_fini</c> (line 2292).</summary>
    public void Dispose()
    {
        if (_storage.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_storage);
        }

        _storage = [];
        Count = 0;
    }

    /// <summary>Upstream <c>ByteStack_push</c> (line 2305).</summary>
    /// <param name="item">The byte to push.</param>
    internal void Push(byte item)
    {
        if (Count >= _storage.Length)
        {
            // Upstream doubles from 64 for a single byte and from 256 for a block. The pool rounds
            // a request up to a power of two anyway, so the two growth curves coincide here.
            int newCapacity = _storage.Length == 0 ? 64 : _storage.Length * 2;
            Grow(Count + 1, newCapacity);
        }

        _storage[Count++] = item;
    }

    /// <summary>Upstream <c>ByteStack_push_block</c> (line 2337).</summary>
    /// <param name="block">The bytes to push.</param>
    internal void PushBlock(ReadOnlySpan<byte> block)
    {
        int newCount = Count + block.Length;

        if (newCount > _storage.Length)
        {
            int newCapacity = _storage.Length == 0 ? 256 : _storage.Length;
            while (newCount > newCapacity)
            {
                newCapacity *= 2;
            }

            Grow(newCount, newCapacity);
        }

        block.CopyTo(_storage.AsSpan(Count));
        Count = newCount;
    }

    /// <summary>Upstream <c>ByteStack_pop</c> (line 2372).</summary>
    /// <param name="item">Receives the byte.</param>
    /// <returns><see langword="false"/> if the stack is empty.</returns>
    internal bool Pop(out byte item)
    {
        if (Count < 1)
        {
            item = 0;
            return false;
        }

        item = _storage[--Count];
        return true;
    }

    /// <summary>Upstream <c>ByteStack_pop_block</c> (line 2383).</summary>
    /// <param name="block">Receives the bytes.</param>
    /// <returns><see langword="false"/> if the stack holds fewer bytes than that.</returns>
    internal bool PopBlock(Span<byte> block)
    {
        if (block.Length > Count)
        {
            return false;
        }

        Count -= block.Length;
        _storage.AsSpan(Count, block.Length).CopyTo(block);
        return true;
    }

    /// <summary>Upstream <c>ByteStack_drop</c> (line 2395).</summary>
    /// <returns><see langword="false"/> if the stack is empty.</returns>
    internal bool Drop()
    {
        if (Count < 1)
        {
            return false;
        }

        --Count;
        return true;
    }

    /// <summary>Upstream <c>ByteStack_drop_block</c> (line 2405).</summary>
    /// <param name="count">How many bytes to drop.</param>
    /// <returns><see langword="false"/> if the stack holds fewer bytes than that.</returns>
    internal bool DropBlock(int count)
    {
        if (count > Count)
        {
            return false;
        }

        Count -= count;
        return true;
    }

    /// <summary>Upstream <c>ByteStack_top_block</c> (line 2415).</summary>
    /// <param name="block">Receives the bytes, which stay on the stack.</param>
    /// <returns><see langword="false"/> if the stack holds fewer bytes than that.</returns>
    internal bool TopBlock(Span<byte> block)
    {
        if (block.Length > Count)
        {
            return false;
        }

        _storage.AsSpan(Count - block.Length, block.Length).CopyTo(block);
        return true;
    }

    /// <summary>Upstream <c>push_uint8</c> (line 2440).</summary>
    /// <param name="item">The value to push.</param>
    internal void PushUInt8(byte item) => Push(item);

    /// <summary>Upstream <c>pop_uint8</c> (line 2611).</summary>
    /// <param name="item">Receives the value.</param>
    /// <returns><see langword="false"/> if the stack is empty.</returns>
    internal bool PopUInt8(out byte item) => Pop(out item);

    /// <summary>
    /// Upstream <c>push_size</c> (line 2456). <c>size_t</c> is 64-bit on the platforms this port
    /// targets, so the width on the stack matches upstream's byte for byte.
    /// </summary>
    /// <param name="item">The value to push.</param>
    internal void PushSize(long item) => PushBlock(MemoryMarshal.AsBytes(new ReadOnlySpan<long>(in item)));

    /// <summary>Upstream <c>pop_size</c>.</summary>
    /// <param name="item">Receives the value.</param>
    /// <returns><see langword="false"/> if the stack holds too few bytes.</returns>
    internal bool PopSize(out long item)
    {
        item = 0;
        return PopBlock(MemoryMarshal.AsBytes(new Span<long>(ref item)));
    }

    /// <summary>
    /// Grows the backing array to at least <paramref name="newCapacity"/>, keeping what is on the
    /// stack. Upstream's <c>safe_realloc</c> plus its <c>RE_MEMORY_LIMIT</c> check.
    /// </summary>
    /// <param name="required">The count the caller is about to reach.</param>
    /// <param name="newCapacity">Upstream's doubled capacity.</param>
    private void Grow(int required, int newCapacity)
    {
        if (newCapacity >= _memoryLimit || newCapacity < required)
        {
            // Upstream sets RE_ERROR_MEMORY, which surfaces as MemoryError. Neither
            // OutOfMemoryException nor InsufficientMemoryException can be used: the runtime reserves
            // both for a real allocation failure, and a library that throws one makes a genuine
            // failure indistinguishable from this limit. The limit itself stays - an unbounded
            // backtracking stack is a denial-of-service vector, and this is the guard against it.
            throw new InvalidOperationException(
                "the regular expression engine's backtracking stack exceeded its 1GB limit"
            );
        }

        byte[] grown = ArrayPool<byte>.Shared.Rent(newCapacity);
        _storage.AsSpan(0, Count).CopyTo(grown);

        if (_storage.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_storage);
        }

        _storage = grown;
    }
}
