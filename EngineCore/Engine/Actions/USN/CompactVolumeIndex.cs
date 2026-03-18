using System.Runtime.CompilerServices;
using System.Text;

namespace TDSNET.Engine.Actions.USN;

/// <summary>
/// Columnar storage per volume. Trigram index rebuilt after bulk load; incremental USN updates postings.
/// </summary>
public sealed class CompactVolumeIndex
{
    private readonly List<ulong> _fileFrn = new(1 << 16);
    private readonly List<ulong> _parentFrn = new(1 << 16);
    private readonly List<ulong> _keyIndex = new(1 << 16);
    private readonly List<byte> _alive = new(1 << 16);
    private readonly List<byte> _sortRank = new(1 << 16);
    private readonly List<int> _nameStart = new(1 << 16);
    private readonly List<int> _nameLen = new(1 << 16);

    private byte[] _nameBuf = new byte[8 * 1024 * 1024];
    private int _nameUsed;

    private readonly Dictionary<ulong, int> _frnToRow = new();

    public TrigramSearchIndex Trigrams { get; } = new();

    public int RowCount => _fileFrn.Count;

    public ulong GetFileFrn(int row) => _fileFrn[row];
    public ulong GetParentFrn(int row) => _parentFrn[row];
    public ulong GetKeyIndex(int row) => _keyIndex[row];
    public byte GetSortRank(int row) => _sortRank[row];

    public bool IsAlive(int row) => row >= 0 && row < _alive.Count && _alive[row] != 0;

    public bool TryGetRow(ulong frn, out int row) => _frnToRow.TryGetValue(frn, out row);

    public void Clear()
    {
        _fileFrn.Clear();
        _parentFrn.Clear();
        _keyIndex.Clear();
        _alive.Clear();
        _sortRank.Clear();
        _nameStart.Clear();
        _nameLen.Clear();
        _nameUsed = 0;
        _frnToRow.Clear();
        Trigrams.Clear();
    }

    /// <summary>Append during USN enum or bulk load. Skips duplicate FRN. No trigram until RebuildTrigramIndex.</summary>
    public int AppendRow(ulong frn, ulong parentFrn, string innerFileName, ulong keyIdx, byte sortRank = 0)
    {
        if (_frnToRow.ContainsKey(frn))
            return _frnToRow[frn];
        int row = _fileFrn.Count;
        _fileFrn.Add(frn);
        _parentFrn.Add(parentFrn);
        _keyIndex.Add(keyIdx);
        _alive.Add(1);
        _sortRank.Add(sortRank);
        _nameStart.Add(0);
        _nameLen.Add(0);
        WriteName(row, Encoding.UTF8.GetBytes(innerFileName));
        _frnToRow[frn] = row;
        return row;
    }

    public void UpdateRowNameAndParent(int row, ulong parentFrn, string innerFileName, ulong keyIdx)
    {
        _parentFrn[row] = parentFrn;
        _keyIndex[row] = keyIdx;
        WriteName(row, Encoding.UTF8.GetBytes(innerFileName));
    }

    public void SetSortRank(int row, byte rank) => _sortRank[row] = rank;

    public string GetInnerFileNameString(int row) =>
        Encoding.UTF8.GetString(_nameBuf, _nameStart[row], _nameLen[row]);

    /// <summary>Case-insensitive substring check. Uses thread-static buffer when name fits (no alloc).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsSubstring(int row, ReadOnlySpan<char> pattern)
    {
        if (pattern.Length == 0) return true;
        int start = _nameStart[row];
        int len = _nameLen[row];
        if (len == 0) return false;
        Span<char> buf = GetDecodeBuffer();
        if (len > buf.Length)
            return GetInnerFileNameString(row).AsSpan().Contains(pattern, StringComparison.OrdinalIgnoreCase);
        int decoded = Encoding.UTF8.GetChars(_nameBuf.AsSpan(start, len), buf);
        return MemoryExtensions.Contains(buf.Slice(0, decoded), pattern, StringComparison.OrdinalIgnoreCase);
    }

    [ThreadStatic] private static char[]? t_decodeBuffer;
    private static Span<char> GetDecodeBuffer()
    {
        if (t_decodeBuffer == null || t_decodeBuffer.Length < 4096)
            t_decodeBuffer = new char[4096];
        return t_decodeBuffer;
    }

    public void Remove(ulong frn)
    {
        if (!_frnToRow.TryGetValue(frn, out int row))
            return;
        _frnToRow.Remove(frn);
        _alive[row] = 0;
    }

    /// <summary>New file from USN after index was built. Idempotent if FRN already alive.</summary>
    public int AppendRowWithTrigram(ulong frn, ulong parentFrn, string innerFileName, ulong keyIdx, byte sortRank = 0)
    {
        if (_frnToRow.TryGetValue(frn, out int row) && IsAlive(row))
        {
            _parentFrn[row] = parentFrn;
            _keyIndex[row] = keyIdx;
            WriteName(row, Encoding.UTF8.GetBytes(innerFileName));
            return row;
        }
        return AppendRow(frn, parentFrn, innerFileName, keyIdx, sortRank);
    }

    public void RenameRow(int row, ulong parentFrn, string newInnerFileName, ulong keyIdx)
    {
        if (GetInnerFileNameString(row) == newInnerFileName && _parentFrn[row] == parentFrn) return;
        _parentFrn[row] = parentFrn;
        _keyIndex[row] = keyIdx;
        WriteName(row, Encoding.UTF8.GetBytes(newInnerFileName));
    }

    /// <summary>No-op: trigram index disabled to save memory; search uses linear scan + ContainsSubstring.</summary>
    public void RebuildTrigramIndex() { }

    private void WriteName(int row, byte[] utf8)
    {
        int need = _nameUsed + utf8.Length;
        if (need > _nameBuf.Length)
            Array.Resize(ref _nameBuf, Math.Max(need, _nameBuf.Length * 2));
        _nameStart[row] = _nameUsed;
        _nameLen[row] = utf8.Length;
        utf8.AsSpan().CopyTo(_nameBuf.AsSpan(_nameUsed));
        _nameUsed = need;
    }

    public int[] GetRowsDisplayOrder()
    {
        var list = new List<int>();
        for (int i = 0; i < _fileFrn.Count; i++)
        {
            if (_alive[i] != 0)
                list.Add(i);
        }
        list.Sort((a, b) =>
        {
            int c = _sortRank[b].CompareTo(_sortRank[a]);
            return c != 0 ? c : a.CompareTo(b);
        });
        return list.ToArray();
    }
}
