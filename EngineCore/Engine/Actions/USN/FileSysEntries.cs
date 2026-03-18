using System.Collections;

namespace TDSNET.Engine.Actions.USN;

/// <summary>Dictionary-like view over CompactVolumeIndex for legacy call sites.</summary>
public sealed class FileSysEntries : IEnumerable<FrnFileOrigin>
{
    private readonly FileSys _fs;

    internal FileSysEntries(FileSys fs) => _fs = fs;

    public int Count
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _fs.Index.RowCount; i++)
            {
                if (_fs.Index.IsAlive(i)) n++;
            }
            return n;
        }
    }

    public bool ContainsKey(ulong frn) =>
        _fs.Index.TryGetRow(frn, out int r) && _fs.Index.IsAlive(r);

    public bool TryGetValue(ulong frn, out FrnFileOrigin f)
    {
        if (_fs.TryGetFrnOrigin(frn, out f!))
            return true;
        f = null!;
        return false;
    }

    public void Add(ulong frn, FrnFileOrigin _) =>
        throw new InvalidOperationException("Use Index.AppendRow / AppendRowWithTrigram");

    public void Remove(ulong frn) => _fs.Index.Remove(frn);

    public IEnumerator<FrnFileOrigin> GetEnumerator()
    {
        for (int i = 0; i < _fs.Index.RowCount; i++)
        {
            if (_fs.Index.IsAlive(i))
                yield return _fs.GetOrigin(i);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
