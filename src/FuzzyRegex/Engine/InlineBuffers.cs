using System.Runtime.CompilerServices;
using Fuzzy.Text.RegularExpressions.Unicode;

namespace Fuzzy.Text.RegularExpressions.Engine;

// Fixed-size scratch buffers that live in a method's own frame, used in place of stackalloc. A
// method that contains a stackalloc (IL localloc) cannot be given patchpoints for on-stack
// replacement, so when it also has a loop the JIT compiles it once, fully optimised but with no
// profile data, and never recompiles it; and the JIT never inlines a method with a localloc. Each
// length is the constant the stackalloc it replaced was sized by, so the two cannot drift apart.
// See JitTieringTests.
#pragma warning disable S1144 // The runtime reads each element through the InlineArray layout, not by name.

/// <summary>Room for every case of one codepoint (<see cref="UnicodeTables.MaxCases"/>).</summary>
[InlineArray(UnicodeTables.MaxCases)]
internal struct CaseBuffer
{
    private uint _element;
}

/// <summary>Room for the full case folding of one codepoint (<see cref="UnicodeTables.MaxFolded"/>).</summary>
[InlineArray(UnicodeTables.MaxFolded)]
internal struct FoldBuffer
{
    private uint _element;
}

/// <summary>Room for the full case folding of every case of one codepoint.</summary>
[InlineArray(UnicodeTables.MaxCases * UnicodeTables.MaxFolded)]
internal struct CaseFoldBuffer
{
    private uint _element;
}

/// <summary>One length per case of one codepoint (<see cref="UnicodeTables.MaxCases"/>).</summary>
[InlineArray(UnicodeTables.MaxCases)]
internal struct CaseLengthBuffer
{
    private int _element;
}

/// <summary>One count per kind of fuzzy error (<see cref="FuzzyValue.Count"/>).</summary>
[InlineArray(FuzzyValue.Count)]
internal struct FuzzyCountBuffer
{
    private long _element;
}

/// <summary>Room for every script extension of one codepoint (<see cref="UnicodeTables.MaxScx"/>).</summary>
[InlineArray(UnicodeTables.MaxScx)]
internal struct ScriptBuffer
{
    private byte _element;
}

#pragma warning restore S1144
