using System.Globalization;
using System.Text;

namespace TDSNET.Engine.Actions.USN;

/// <summary>
/// Rune trigram inverted index for substring search on normalized (lower) text.
/// </summary>
public sealed class TrigramSearchIndex
{
    private readonly Dictionary<ulong, List<int>> _postings = new();

    private static ReadOnlySpan<Rune> PadSearchable(string s)
    {
        // Use low control chars as boundaries so short queries still get 3 runes
        const char L = '\u0002';
        const char R = '\u0003';
        return string.Create(s.Length + 2, s, static (span, state) =>
        {
            span[0] = L;
            int o = 1;
            foreach (var r in state.EnumerateRunes())
            {
                var lr = Rune.ToLowerInvariant(r);
                if (o < span.Length - 1)
                    lr.EncodeToUtf16(span.Slice(o));
                o += lr.Utf16SequenceLength;
            }
            span[^1] = R;
        }).EnumerateRunes().ToArray().AsSpan(); // small alloc; query path can optimize later
    }

    private static void CollectRunes(string s, List<Rune> runes)
    {
        runes.Clear();
        runes.Add(new Rune('\u0002'));
        foreach (var r in s.EnumerateRunes())
            runes.Add(Rune.ToLowerInvariant(r));
        runes.Add(new Rune('\u0003'));
    }

    internal static ulong PackTrigram(Rune a, Rune b, Rune c) =>
        ((ulong)(uint)a.Value << 42) | ((ulong)(uint)b.Value << 21) | (ulong)(uint)c.Value;

    public void Clear() => _postings.Clear();

    /// <summary>Batch build: O(total trigrams), then sort each posting list.</summary>
    public void FullRebuild(CompactVolumeIndex index)
    {
        Clear();
        var acc = new Dictionary<ulong, List<int>>();
        var runes = new List<Rune>(256);
        for (int row = 0; row < index.RowCount; row++)
        {
            if (!index.IsAlive(row))
                continue;
            var name = index.GetInnerFileNameString(row);
            CollectRunes(name, runes);
            for (int i = 0; i <= runes.Count - 3; i++)
            {
                var key = PackTrigram(runes[i], runes[i + 1], runes[i + 2]);
                if (!acc.TryGetValue(key, out var list))
                {
                    list = new List<int>(64);
                    acc[key] = list;
                }
                list.Add(row);
            }
        }
        foreach (var kv in acc)
        {
            kv.Value.Sort();
            DedupeSorted(kv.Value);
            _postings[kv.Key] = kv.Value;
        }
    }

    private static void DedupeSorted(List<int> list)
    {
        if (list.Count <= 1) return;
        int w = 0;
        for (int r = 1; r < list.Count; r++)
        {
            if (list[r] != list[w])
                list[++w] = list[r];
        }
        list.RemoveRange(w + 1, list.Count - w - 1);
    }

    /// <summary>Incremental add (USN create) — sorted insert per posting.</summary>
    public void AddFileIncremental(int fileId, string innerFileName)
    {
        var runes = new List<Rune>(128);
        CollectRunes(innerFileName, runes);
        for (int i = 0; i <= runes.Count - 3; i++)
        {
            var key = PackTrigram(runes[i], runes[i + 1], runes[i + 2]);
            if (!_postings.TryGetValue(key, out var list))
            {
                list = new List<int>(8);
                _postings[key] = list;
            }
            InsertSortedUnique(list, fileId);
        }
    }

    /// <summary>Bulk load path: append without sorting (caller runs FullRebuild).</summary>
    public void AddFileBulk(int fileId, string innerFileName)
    {
        var runes = new List<Rune>(128);
        CollectRunes(innerFileName, runes);
        for (int i = 0; i <= runes.Count - 3; i++)
        {
            var key = PackTrigram(runes[i], runes[i + 1], runes[i + 2]);
            if (!_postings.TryGetValue(key, out var list))
            {
                list = new List<int>(64);
                _postings[key] = list;
            }
            list.Add(fileId);
        }
    }

    public void RemoveFile(int fileId, string innerFileName)
    {
        var runes = new List<Rune>(128);
        CollectRunes(innerFileName, runes);
        for (int i = 0; i <= runes.Count - 3; i++)
        {
            var key = PackTrigram(runes[i], runes[i + 1], runes[i + 2]);
            if (!_postings.TryGetValue(key, out var list))
                continue;
            RemoveSorted(list, fileId);
        }
    }

    private static void InsertSortedUnique(List<int> list, int id)
    {
        int lo = 0, hi = list.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            if (list[mid] < id) lo = mid + 1;
            else hi = mid;
        }
        if (lo < list.Count && list[lo] == id) return;
        list.Insert(lo, id);
    }

    private static void RemoveSorted(List<int> list, int id)
    {
        int i = list.BinarySearch(id);
        if (i >= 0)
            list.RemoveAt(i);
    }

    /// <summary>
    /// Returns row ids matching keyword (substring, case-insensitive) on inner file name.
    /// </summary>
    public List<int> Search(string keyword, CompactVolumeIndex index)
    {
        var results = new List<int>();
        if (string.IsNullOrEmpty(keyword))
            return results;

        var runes = new List<Rune>(64);
        CollectRunes(keyword, runes);
        if (runes.Count < 3)
        {
            // Degenerate: scan all alive rows (rare ultra-short after padding — padded has >= 4 runes for ""? keyword "a" -> ^ a ^ = 3 runes exactly)
            var k = keyword.AsSpan();
            for (int row = 0; row < index.RowCount; row++)
            {
                if (!index.IsAlive(row)) continue;
                if (index.ContainsSubstring(row, k))
                    results.Add(row);
            }
            return results;
        }

        List<List<int>> lists = new();
        for (int i = 0; i <= runes.Count - 3; i++)
        {
            var key = PackTrigram(runes[i], runes[i + 1], runes[i + 2]);
            if (_postings.TryGetValue(key, out var list) && list.Count > 0)
                lists.Add(list);
        }
        if (lists.Count == 0)
            return results;

        lists.Sort((a, b) => a.Count.CompareTo(b.Count));
        var smallest = lists[0];
        var pattern = keyword.AsSpan();
        foreach (var row in smallest)
        {
            if (!index.IsAlive(row)) continue;
            bool ok = true;
            for (int j = 1; j < lists.Count; j++)
            {
                if (lists[j].BinarySearch(row) < 0)
                {
                    ok = false;
                    break;
                }
            }
            if (!ok) continue;
            if (index.ContainsSubstring(row, pattern))
                results.Add(row);
        }
        return results;
    }
}
