using Avalonia.Media.Imaging;
using EngineCore.Engine.Actions.USN;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using TDSNET.Engine.Utils;
using TDSNET.Utils;

namespace TDSNET.Engine.Actions.USN
{
    public sealed class FrnFileOrigin
    {
        public ulong keyindex => Volume.Index.GetKeyIndex(RowId);
        public ulong fileReferenceNumber => Volume.Index.GetFileFrn(RowId);
        public ulong parentFileReferenceNumber => Volume.Index.GetParentFrn(RowId);

        public FrnFileOrigin? parentFrn
        {
            get
            {
                var p = parentFileReferenceNumber;
                if (p == ulong.MaxValue) return null;
                return Volume.TryGetFrnOrigin(p, out var x) ? x : null;
            }
        }

        public Bitmap? icon => FileIconService.GetIcon(FilePath);

        public string innerFileName => Volume.Index.GetInnerFileNameString(RowId);

        public string FileName => PathHelper.getfileName(innerFileName).ToString();

        public string FilePath => PathHelper.GetPathFromRow(Volume, RowId);

        public string? FileInfo => PathHelper.getFileInfoStr(this);

        public FileSys Volume { get; }
        public int RowId { get; }

        internal FrnFileOrigin(FileSys volume, int rowId)
        {
            Volume = volume;
            RowId = rowId;
        }

        [Obsolete("Use FileSys index only")]
        public static FrnFileOrigin Create(string filename, ulong fileRefNum, ulong parentFileRefNum) =>
            throw new InvalidOperationException("Legacy Create disabled; use FileSys.Index.AppendRow");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInnerFileName(string filename) =>
            Volume.Index.UpdateRowNameAndParent(RowId, parentFileReferenceNumber, filename, keyindex);

        public void ApplyNacnName(string nacnName, ulong idx) =>
            Volume.Index.UpdateRowNameAndParent(RowId, parentFileReferenceNumber, nacnName, idx);
    }

    public class FileSys
    {
        public DriveInfoData driveInfoData;
        public NtfsUsnJournal ntfsUsnJournal;
        public CompactVolumeIndex Index { get; } = new();
        public FileSysEntries files { get; }
        public Win32Api.USN_JOURNAL_DATA usnStates;

        public FileSys(DriveInfoData disk)
        {
            driveInfoData = disk;
            files = new FileSysEntries(this);
        }

        public FrnFileOrigin GetOrigin(int row) => new FrnFileOrigin(this, row);

        public bool TryGetFrnOrigin(ulong frn, out FrnFileOrigin f)
        {
            if (Index.TryGetRow(frn, out int r) && Index.IsAlive(r))
            {
                f = new FrnFileOrigin(this, r);
                return true;
            }
            f = null!;
            return false;
        }

        public void Compress()
        {
        }

        public bool SaveJournalState()
        {
            Win32Api.USN_JOURNAL_DATA journalState = new Win32Api.USN_JOURNAL_DATA();
            NtfsUsnJournal.UsnJournalReturnCode rtn = ntfsUsnJournal.GetUsnJournalState(ref journalState);
            if (rtn == NtfsUsnJournal.UsnJournalReturnCode.USN_JOURNAL_SUCCESS)
            {
                usnStates = journalState;
                return true;
            }
            return false;
        }

        private uint reasonMask = Win32Api.USN_REASON_FILE_CREATE | Win32Api.USN_REASON_FILE_DELETE | Win32Api.USN_REASON_RENAME_NEW_NAME | Win32Api.USN_REASON_OBJECT_ID_CHANGE;

        public void DoWhileFileChanges()
        {
            if (usnStates.UsnJournalID != 0)
            {
                _ = ntfsUsnJournal.GetUsnJournalEntries(usnStates, reasonMask, out List<Win32Api.UsnEntry> usnEntries, out Win32Api.USN_JOURNAL_DATA newUsnState);

                for (int i = 0; i < usnEntries.Count; i++)
                {
                    var f = usnEntries[i];

                    if (f.Reason == Win32Api.USN_REASON_OBJECT_ID_CHANGE)
                    {
                        Index.Remove(f.FileReferenceNumber);
                        continue;
                    }

                    uint value = f.Reason & Win32Api.USN_REASON_RENAME_NEW_NAME;

                    if (0 != value && Index.RowCount > 0)
                    {
                        if (files.ContainsKey(f.ParentFileReferenceNumber))
                        {
                            if (Index.TryGetRow(f.FileReferenceNumber, out int row) && Index.IsAlive(row))
                            {
                                GetNACNNameAndIndex(f.Name, out var nacnName, out var index);
                                Index.RenameRow(row, f.ParentFileReferenceNumber, nacnName, index);
                            }
                            else
                            {
                                GetNACNNameAndIndex(f.Name, out var nacnName, out var index);
                                if (!Index.TryGetRow(f.FileReferenceNumber, out _))
                                    Index.AppendRowWithTrigram(f.FileReferenceNumber, f.ParentFileReferenceNumber, nacnName, index);
                            }
                        }
                    }

                    value = f.Reason & Win32Api.USN_REASON_FILE_CREATE;
                    if (0 != value)
                    {
                        if (!files.ContainsKey(f.FileReferenceNumber) && !string.IsNullOrWhiteSpace(f.Name) && files.ContainsKey(f.ParentFileReferenceNumber))
                        {
                            GetNACNNameAndIndex(f.Name, out var name, out var index);
                            Index.AppendRowWithTrigram(f.FileReferenceNumber, f.ParentFileReferenceNumber, name, index);
                        }
                    }

                    value = f.Reason & Win32Api.USN_REASON_FILE_DELETE;
                    if (0 != value && Index.RowCount > 0)
                    {
                        Index.Remove(f.FileReferenceNumber);
                    }
                }
                usnStates = newUsnState;
            }
        }

        public void CreateFiles()
        {
            ntfsUsnJournal.GetNtfsVolumeAllentries(driveInfoData.Name[0], out NtfsUsnJournal.UsnJournalReturnCode rtnCode, this);
        }

        private const int SCREENCHARNUM = 45;

        private static readonly char[] alphbet = { '@', '.', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '-', '_', '[', ']', '(', ')', '/' };

        public static void GetNACNNameAndIndex(string name, out string nacnName, out ulong nacnIndex, ConcurrentDictionary<char, char> cache)
        {
            if (string.IsNullOrEmpty(name))
            {
                nacnName = name;
                nacnIndex = 0;
                return;
            }
            else
            {
                string nacn = SpellCN.GetSpellCode(name, cache);

                if (!string.Equals(nacn, name, StringComparison.OrdinalIgnoreCase))
                {
                    nacnName = $"|{name}|{nacn}|";
                }
                else
                {
                    nacnName = $"|{name}|";
                }
                nacnIndex = TBS(nacnName);
            }
        }

        public static void GetNACNNameAndIndex(string name, out string nacnName, out ulong nacnIndex)
        {
            if (string.IsNullOrEmpty(name))
            {
                nacnName = name;
                nacnIndex = 0;
                return;
            }
            else
            {
                string nacn = SpellCN.GetSpellCode(name);

                if (!string.Equals(nacn, name, StringComparison.OrdinalIgnoreCase))
                {
                    nacnName = $"|{name}|{nacn}|";
                }
                else
                {
                    nacnName = $"|{name}|";
                }
                nacnIndex = TBS(nacnName);
            }
        }

        public static ulong TBS(string txt)
        {
            ulong indexValue = 0;

            for (int i = 0; i < SCREENCHARNUM; i++)
            {
                if (txt.Contains(alphbet[i], StringComparison.OrdinalIgnoreCase))
                {
                    SetBit(ref indexValue, i);
                }
            }
            return indexValue;
        }

        static void SetBit(ref ulong value, int position)
        {
            value = value | ((ulong)1 << position);
        }
    }
}
