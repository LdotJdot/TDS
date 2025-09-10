using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TDSAot.State;
using TDSNET.Engine.Actions.USN;
using TDSNET.Engine.Utils;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        private string keyword = string.Empty;
        readonly private RunningState runningState=new RunningState();

        int resultNumGlobal= 0;

        private async void SearchFilesThreadLoop(CancellationToken cancellationToken)
        {
            runningState.Threadrunning = true;

            while (runningState.Threadrunning == true && !cancellationToken.IsCancellationRequested)
            {
                string[] dwords = null;
                string[] words;
                int resultNum = 0;
                UInt64 unidwords = 0;
                UInt64 uniwords;
                bool DoDirectory = false;
                
                try
                {
                    await runningState.gOs.WaitAsync(cancellationToken);
                }
                catch
                {
                    break;
                }


                runningState.Threadrest = false;  //重启标签

                string threadKeyword = keyword;

                string[] driverNames = null;

                if (threadKeyword.Contains(":"))
                {
                    driverNames = (threadKeyword.Split(':'))[0].Split(',');
                    threadKeyword = (threadKeyword.Split(':'))[1];
                }

                threadKeyword = threadKeyword.ToUpperInvariant().Replace("  ", " ").Replace("  ", " ");
                runningState.isAll = false;

                if (threadKeyword.Contains(" /A"))
                {
                    threadKeyword = threadKeyword.Replace(" /A", "");
                    runningState.isAll = true;
                }

                if (threadKeyword.Contains("\\"))
                {
                    string[] tmp = threadKeyword.Split('\\');
                    string tmpdword = tmp[0].Replace(" ", " ");
                    string tmpword = tmp[1].Replace(" ", " ");

                    unidwords = FileSys.TBS(SpellCN.GetSpellCode(tmpdword));

                    uniwords = FileSys.TBS(SpellCN.GetSpellCode(tmpword));

                    if (tmp[0].Contains(" "))
                    {
                        dwords = tmp[0].Split(' ');
                    }
                    else
                    {
                        dwords = [tmp[0]];
                    }
                    if (tmp[1].Contains(" "))
                    {
                        words = tmp[1].Split(' ');
                    }
                    else
                    {
                        words = [tmp[1]];
                    }

                    DoDirectory = true;
                }
                else
                {
                    words = threadKeyword.Split(' ');
                    string tmpword = threadKeyword.Replace(" ", "");

                    uniwords = FileSys.TBS(SpellCN.GetSpellCode(tmpword));
                }

                try
                {
                    if (runningState.DoUSNupdate && !runningState.ForbidUSNupdate)
                    {
                        for (int i = 0; i < fileSysList.Count; i++)
                        {
                            try
                            {
                                fileSysList[i].DoWhileFileChanges();
                            }
                            catch
                            {
                                goto Restart;
                            }
                        }
                    }
                    runningState.DoUSNupdate = false;


                    Parallel.For(0, fileSysList.Count, d =>
                    {
                        if (runningState.Threadrest) { return; } //终止标签
                        var fs = fileSysList[d];


                        var l = fs.files;

                        if (l.Count == 0) return;


                        if (driverNames != null)
                        {
                            bool driverFound = false;
                            foreach (string driverName in driverNames)
                            {
                                if (string.Equals(driverName, fs.driveInfoData.Name[0].ToString(), StringComparison.OrdinalIgnoreCase))
                                {
                                    driverFound = true;
                                    break;
                                }
                            }

                            if (!driverFound) return;
                        }

                        var comparisondType = unidwords == 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                        var comparisonType = uniwords == 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                        bool finded = true;

                        foreach (var f in fs.files.Values)
                        {
                            if (runningState.Threadrest) { break; } //终止标签

                            finded = true;

                            if (DoDirectory)
                            {
                                if (f.parentFrn != null && l.TryGetValue(f.parentFrn.fileReferenceNumber, out FrnFileOrigin? dictmp))
                                {
                                    if((unidwords | dictmp.keyindex) != dictmp.keyindex)
                                    {
                                        finded = false;
                                        break;
                                    }

                                    foreach (string key in dwords)
                                    {
                                        if (dictmp.fileName.IndexOf(key, comparisondType) == -1)
                                        {
                                            finded = false;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    finded = false;
                                }
                            }

                            if (finded)
                            {
                                foreach (string key in words)
                                {
                                    if (((uniwords | f.keyindex) != f.keyindex) || (f.fileName.IndexOf(key, comparisonType) == -1))
                                    {
                                        finded = false;
                                        break;
                                    }
                                }
                            }

                            if (finded)
                            {
                                vlist[Interlocked.Increment(ref resultNum) - 1] = f;

                                if (resultNum == vlist.Length || (Option.Findmax > 0 && resultNum > Option.Findmax && runningState.isAll == false))
                                {
                                    break;
                                }

                                if (resultNum < 50)//提前显示
                                {
                                    resultNumGlobal = resultNum;
                                    UpdateList(false);  //必须异步BeginInvoke，不然不同步
                                }
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                }

                if (!runningState.Threadrest)
                {
                    if (resultNum > 0)
                    {
                        resultNumGlobal = resultNum;
                        UpdateList();  //必须异步BeginInvoke，不然不同步
                    }
                    else
                    {
                        resultNumGlobal = resultNum;
                        UpdateList();  //异步BeginInvoke
                    }
                }
Restart:;
            }
        }

        private void UpdateList(bool finished = true)
        {
            Items.Bind(vlist);
            Items.SetDisplayCount(resultNumGlobal);
            if (finished == false)
            {
                MessageData.Message = $"...";
            }
            else
            {
                if (Option.Findmax>0 && resultNumGlobal > Option.Findmax && runningState.isAll == false)
                {
                     MessageData.Message = $"{Option.Findmax} +item";
                }
                else
                {
                    if (resultNumGlobal <= 1)
                    {
                        MessageData.Message = $"{resultNumGlobal} item";
                    }
                    else
                    {
                        MessageData.Message = $"{resultNumGlobal} items";
                    }
                }
            }
        }

        IInputElement? lastFocused;
        private void RefreshFileData()
        {
            runningState.DoUSNupdate = true;
            TextChanged(null, null!);
            lastFocused?.Focus();
        }
    }
}