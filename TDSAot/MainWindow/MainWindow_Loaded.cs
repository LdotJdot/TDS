using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TDSAot.State;
using TDSAot.Utils;
using TDSNET.Engine.Actions.USN;
using TDSNET.Engine.Utils;
using TDSNET.Utils;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        private static ActionState? state;

        private List<FileSys> fileSysList = new List<FileSys>();
        private FrnFileOrigin[] vlist;

        private bool initialFinished = false;

        private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {

            Reset();
        }

        private void Reset()
        {
            Option = new AppOption();
            RegisterHotKeys();
            state?.Dispose();
            state = new ActionState();
            Task.Run(ListFilesThreadStart);
        }

 
        private void ListFilesThreadStart()
        {
            DisableController();


            int dri_nums = -1;  //盘数

            fileSysList.Clear();
            foreach (DriveInfo driInfo in DriverUtils.GetAllFixedNtfsDrives())
            {
                fileSysList.Add(new FileSys(driInfo));
            }

            dri_nums = fileSysList.Count();

            if (dri_nums > 0)
            {
                int totalcount = 0;

                ConcurrentDictionary<char, char> SpellDict = new ConcurrentDictionary<char, char>();

                //DateTime startTime= DateTime.Now;

                Task[] tasks = new Task[fileSysList.Count];
#if DEBUG
                var st=Stopwatch.StartNew();
#endif

                MessageData.Message = ("Indexing files..(" + string.Join(",",fileSysList.Select(o=>o.driveInfo.Name))+")");

                for (int i=0;i<fileSysList.Count;i++)
                {
                   FileSys fs = fileSysList[i];
                   tasks[i]=Task.Run(() =>{

                        fs.ntfsUsnJournal = new NtfsUsnJournal(fs.driveInfo);

                        fs.usnStates = new Win32Api.USN_JOURNAL_DATA();
                        if (!fs.SaveJournalState())
                        {
                            fs.ntfsUsnJournal.CreateUsnJournal(1000 * 1024, 16 * 1024);  //尝试重建USN
                            if (!fs.SaveJournalState())
                            {
                                MessageData.Message = "File read failed";
                            }
                        }
                        fs.CreateFiles();

                        //重整parent索引
                        foreach (FrnFileOrigin ffull in fs.files.Values)
                        {
                            FrnFileOrigin f = ffull as FrnFileOrigin;
                            if (f.parentFileReferenceNumber!=ulong.MaxValue && fs.files.ContainsKey(f.parentFileReferenceNumber))
                            {
                                if (f.parentFrn == null)
                                {
                                    f.parentFrn = fs.files[f.parentFileReferenceNumber];
                                }
                            }
                        }

                        foreach(var f in fs.files.Values)
                        {  
                            FileSys.GetNACNNameAndIndex(f.fileName, out var nacnName, out var index, SpellDict);
                            
                            f.keyindex = index;
                            f.fileName=nacnName;
                        }

                       foreach (var f in fs.files.Values)
                       {
                           string ext = StringUtils.GetExtension(PathHelper.getfilePath(f.fileName)).ToString();

                           if (string.Equals(ext, ".LNK", StringComparison.OrdinalIgnoreCase))
                           {
                               var path = PathHelper.GetPath(f);
                               if (path.IndexOf(PathHelper.USER_PROGRAM_PATH, StringComparison.OrdinalIgnoreCase) != -1 || path.IndexOf(PathHelper.ALLUSER_PROGRAM_PATH, StringComparison.OrdinalIgnoreCase) != -1)
                               {
                                   f.orderFirst = true;
                               }
                           }
                       }

                       fs.files = fs.files.OrderByDescending(o => o.Value.orderFirst).ToDictionary(p => p.Key, o => o.Value);
                       fs.Compress();
                        totalcount += fs.files.Count;
                   });
                }

               Task.WaitAll(tasks);
#if DEBUG
                Debug.WriteLine(st.Elapsed.ToString());
#endif
                vlist = new FrnFileOrigin[totalcount];
            }

            ReadRecords();  //记录相关* //
            UpdateRecord();

            StringBuilder drinfo = new StringBuilder();
            foreach (FileSys fs in fileSysList)
            {
                drinfo.Append(fs.driveInfo.Name + ",");
            }

            initialFinished = true;

            cts = new CancellationTokenSource();
            state?.Start(new Task(() => SearchFilesThreadLoop(cts.Token)), cts);            

            EnableController();
            ChangeToRecord();
        }

        CancellationTokenSource cts;

        private void EnableController()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                if (_trayIcon.Menu?.Items[1] is NativeMenuItem nmi)
                {
                    nmi.IsEnabled = true;
                }
                inputBox.Watermark = "Please input keywords";
                inputBox.IsEnabled = true;
                fileListBox.IsEnabled = true;
                inputBox.Focus();
                lastFocused = inputBox;
            });
        }

        private void DisableController()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                if (_trayIcon.Menu?.Items[1] is NativeMenuItem nmi)
                {
                    nmi.IsEnabled = false;
                }
                inputBox.Watermark = "Initialization pending...";
                inputBox.IsEnabled = false;
                fileListBox.IsEnabled = false;
            });
        }
    }
}