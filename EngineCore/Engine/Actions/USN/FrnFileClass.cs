using Avalonia.Media.Imaging;
using EngineCore.Engine.Actions.USN;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using TDSNET.Engine.Utils;
using TDSNET.Utils;

namespace TDSNET.Engine.Actions.USN
{
   

    public class FrnFileOrigin
    {
        public ulong keyindex;
        public ulong fileReferenceNumber;
        public ulong parentFileReferenceNumber;


        public Bitmap? icon => FileIconService.GetIcon(FilePath);
        public string innerFileName = "";
        public FrnFileOrigin parentFrn = null;

        public string FileName => PathHelper.getfileName(innerFileName).ToString();
        public string FilePath => PathHelper.GetPath(this).ToString();

        public string FileInfo=> PathHelper.getFileInfoStr(this);





        public static FrnFileOrigin Create(string filename, ulong fileRefNum, ulong parentFileRefNum)
        {
            FrnFileOrigin f = new FrnFileOrigin(filename, fileRefNum);

            f.parentFileReferenceNumber = parentFileRefNum;

            return f;
        }

        private FrnFileOrigin(string filename, ulong fileRefNum)
        {
            innerFileName = filename;
            fileReferenceNumber = fileRefNum;
        }

    }

    public class FileSys
    {
        public DriveInfoData driveInfoData;
        public NtfsUsnJournal ntfsUsnJournal;
        public Dictionary<ulong, FrnFileOrigin> files = new Dictionary<ulong, FrnFileOrigin>(100_0000);
        public Win32Api.USN_JOURNAL_DATA usnStates;

        public FileSys(DriveInfoData disk)
        {
            this.driveInfoData=disk;
        }

        public void Compress()
        {           
            files.TrimExcess();
        }

        /// <summary>
        /// 查询并跟踪USN状态，更新后保存当前状态再继续跟踪
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public bool SaveJournalState()        //保存USN状态
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

        /// <summary>
        /// 掩码
        /// </summary>
        private uint reasonMask = Win32Api.USN_REASON_FILE_CREATE | Win32Api.USN_REASON_FILE_DELETE | Win32Api.USN_REASON_RENAME_NEW_NAME | Win32Api.USN_REASON_OBJECT_ID_CHANGE;

        public void DoWhileFileChanges()  //筛选USN状态改变
        {
            if (usnStates.UsnJournalID != 0)
            {
                _ = ntfsUsnJournal.GetUsnJournalEntries(usnStates, reasonMask, out List<Win32Api.UsnEntry> usnEntries, out Win32Api.USN_JOURNAL_DATA newUsnState);

                for (int i = 0; i < usnEntries.Count; i++)
                {
                    var f = usnEntries[i];
         
                    if (f.Reason== Win32Api.USN_REASON_OBJECT_ID_CHANGE)
                    {
                        // means referenceNumber changed.
                        if (files.ContainsKey(f.FileReferenceNumber))
                        {
                            files.Remove(f.FileReferenceNumber);
                        }
                        continue;
                    }
                    
                    uint value = f.Reason & Win32Api.USN_REASON_RENAME_NEW_NAME;

                    if (0 != value && files.Count > 0)
                    {
                   
                        if (files.ContainsKey(f.ParentFileReferenceNumber))
                        {
                            if (files.TryGetValue(f.FileReferenceNumber, out var frn))
                            {
                                GetNACNNameAndIndex(f.Name, out var nacnName, out var index);
                                frn.innerFileName = nacnName;
                                frn.parentFileReferenceNumber = f.ParentFileReferenceNumber;
                                frn.keyindex = index;
                            }
                            else
                            {
                                GetNACNNameAndIndex(f.Name, out var nacnName, out var index);
                                var frnNew = FrnFileOrigin.Create(nacnName, f.FileReferenceNumber, f.ParentFileReferenceNumber);
                                frnNew.innerFileName = nacnName;
                                frnNew.keyindex = index;
                                frnNew.parentFrn = files[f.ParentFileReferenceNumber];
                                files[frnNew.fileReferenceNumber] = frnNew;
                            }
                        }
                    }

                    value = f.Reason & Win32Api.USN_REASON_FILE_CREATE;
                    if (0 != value)
                    {
                        if (!files.ContainsKey(f.FileReferenceNumber) && !string.IsNullOrWhiteSpace(f.Name) && files.ContainsKey(f.ParentFileReferenceNumber))
                        {
                            GetNACNNameAndIndex(f.Name,out var name, out var index);

                            FrnFileOrigin frn = FrnFileOrigin.Create(name, f.FileReferenceNumber, f.ParentFileReferenceNumber);
                            frn.keyindex = index;
                            frn.parentFrn = files[f.ParentFileReferenceNumber];
                            files.Add(frn.fileReferenceNumber, frn);
                        }
                    }

                    value = f.Reason & Win32Api.USN_REASON_FILE_DELETE;
                    if (0 != value && files.Count > 0)
                    {
                        if (files.ContainsKey(f.FileReferenceNumber))
                        {
                            files.Remove(f.FileReferenceNumber);
                        }
                    }
                }
                usnStates = newUsnState;   //更新状态
            }
        }

        public void CreateFiles()
        {
            ntfsUsnJournal.GetNtfsVolumeAllentries(driveInfoData.Name[0], out NtfsUsnJournal.UsnJournalReturnCode rtnCode, this);
        }

        private const int SCREENCHARNUM = 45;

        private static readonly char[] alphbet = { '@', '.', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '-', '_', '[', ']', '(', ')', '/' };

        public static void GetNACNNameAndIndex(string name,out string nacnName, out ulong nacnIndex, ConcurrentDictionary<char, char> cache)
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

        public static void GetNACNNameAndIndex(string name,out string nacnName, out ulong nacnIndex)
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
            ulong indexValue=0;

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