﻿using BymlLibrary.Extensions;
using BymlLibrary.Structures;
using BymlLibrary.Yaml;
using Revrs;
using Revrs.Extensions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BymlLibrary.Nodes.Immutable.Containers.RelocatedHashMap;

public readonly ref struct ImmutableBymlHashMap32(Span<byte> data, int offset, int count, BymlNodeType type)
{
    /// <summary>
    /// Span of the BYMl data
    /// </summary>
    private readonly Span<byte> _data = data;

    /// <summary>
    /// The container offset (start of header)
    /// </summary>
    private readonly int _offset = offset;

    /// <summary>
    /// The container item count
    /// </summary>
    private readonly int Count = count;

    /// <summary>
    /// The container item count
    /// </summary>
    private readonly BymlNodeType Type = type;

    /// <summary>
    /// Container offset entries
    /// </summary>
    private readonly Span<Entry> _entries = count == 0 ? []
        : data[(offset + BymlContainerNodeHeader.SIZE)..]
            .ReadSpan<Entry>(count);

    /// <summary>
    /// Container offset entries
    /// </summary>
    private readonly Span<BymlNodeType> _types = count == 0 ? []
        : data[(offset + BymlContainerNodeHeader.SIZE + Entry.SIZE * count)..]
            .ReadSpan<BymlNodeType>(count + 1);

    public readonly ImmutableBymlHashMap32Entry this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            Entry entry = _entries[index];
            return new(entry.Hash, _data, entry.Value, _types[index]);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = SIZE)]
    private readonly struct Entry
    {
        public const int SIZE = 8;

        public readonly uint Hash;
        public readonly int Value;

        public class Reverser : IStructReverser
        {
            public static void Reverse(in Span<byte> slice)
            {
                slice[0..4].Reverse();
                slice[4..8].Reverse();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Enumerator GetEnumerator()
        => new(this);

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref struct Enumerator(ImmutableBymlHashMap32 container)
    {
        private readonly ImmutableBymlHashMap32 _container = container;
        private int _index = -1;

        public readonly ImmutableBymlHashMap32Entry Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _container[_index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (++_index >= _container.Count)
            {
                return false;
            }

            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Reverse(ref RevrsReader reader, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Entry entry = reader.Read<Entry, Entry.Reverser>(
                offset + BymlContainerNodeHeader.SIZE + Entry.SIZE * i
            );

            ImmutableByml.ReverseNode(ref reader, entry.Value,
                reader.Read<BymlNodeType>(offset + BymlContainerNodeHeader.SIZE + Entry.SIZE * count + i)
            );
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void EmitYaml(YamlEmitter emitter, in ImmutableByml root)
    {
        emitter.Builder.Append($"!h");
        emitter.NewLine();

        if (Count <= 5 && !emitter.IsIndented && !HasContainerNodes())
        {
            emitter.Builder.Append('{');
            for (int i = 0; i < Count;)
            {
                var (hash, node) = this[i];
                emitter.Builder.Append($"0x{hash:x2}: ");
                emitter.EmitNode(node, root);
                if (++i < Count)
                {
                    emitter.Builder.Append(", ");
                }
            }

            emitter.Builder.Append('}');
            return;
        }

        foreach ((var hash, var node) in this)
        {
            if (!emitter.IsIndented)
            {
                emitter.NewLine();
            }

            emitter.IndentLine();
            emitter.Builder.Append($"0x{hash:x2}");
            emitter.Builder.Append(": ");
            emitter.IsInline = true;
            emitter.IsIndented = false;
            emitter.Level++;
            emitter.EmitNode(node, root);
            emitter.Level--;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasContainerNodes()
    {
        foreach ((_, var node) in this)
        {
            if (node.Type.IsContainerType())
            {
                return true;
            }
        }

        return false;
    }
}
