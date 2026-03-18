using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TDS.Globalization;
using TDS.Utils;
using TDSAot.State;
using TDSNET.Engine.Actions.USN;
using TDSNET.Engine.Utils;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        private string keyword = string.Empty;
        readonly RunningState runningState = new RunningState();
        private readonly Throttler _searchThrottler = new Throttler(100);

        static internal string[] words = [];

        /// <summary>Background USN journal polling only.</summary>
        private void SearchFilesThreadLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Thread.Sleep(4000);
                    if (!initialFinished) continue;
                    foreach (var fs in fileSysList)
                    {
                        try { fs.DoWhileFileChanges(); }
                        catch { /* ignore per-volume */ }
                    }
                }
                catch
                {
                    break;
                }
            }
        }

        private void TextChanged(object? sender, TextChangedEventArgs e)
        {
            keyword = inputBox?.Text ?? "";
            _searchThrottler.Throttle(() => _ = Task.Run(RunSearchFromInput));
        }

        private void RunSearchFromInput()
        {
            string threadKeyword = keyword?.Trim() ?? "";
            if (string.IsNullOrEmpty(threadKeyword))
            {
                Dispatcher.UIThread.Post(ChangeToRecord);
                return;
            }

            string[]? driverNames = null;
            if (threadKeyword.Contains(':'))
            {
                var parts = threadKeyword.Split(':', 2);
                driverNames = parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                threadKeyword = parts.Length > 1 ? parts[1] : "";
            }

            threadKeyword = threadKeyword.Replace("  ", " ").Trim();
            bool isAll = threadKeyword.Contains(" /A", StringComparison.OrdinalIgnoreCase)
                || threadKeyword.Contains("/a", StringComparison.OrdinalIgnoreCase);
            if (isAll)
            {
                threadKeyword = threadKeyword.Replace(" /A", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("/a", "", StringComparison.OrdinalIgnoreCase).Trim();
            }

            string[]? dwords = null;
            ulong unidwords = 0;
            ulong uniwords;
            bool doDirectory = false;
            string[] wordsLocal;

            if (threadKeyword.Contains('\\'))
            {
                var tmp = threadKeyword.Split('\\', 2);
                if (tmp.Length < 2) return;
                string tmpdword = tmp[0].Trim();
                string tmpword = tmp[1].Trim();
                unidwords = FileSys.TBS(SpellCN.GetSpellCode(tmpdword));
                uniwords = FileSys.TBS(SpellCN.GetSpellCode(tmpword));
                dwords = tmpdword.Contains(' ') ? tmpdword.Split(' ', StringSplitOptions.RemoveEmptyEntries) : [tmpdword];
                wordsLocal = tmpword.Contains(' ') ? tmpword.Split(' ', StringSplitOptions.RemoveEmptyEntries) : [tmpword];
                doDirectory = true;
            }
            else
            {
                wordsLocal = threadKeyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (wordsLocal.Length == 0)
                {
                    Dispatcher.UIThread.Post(ChangeToRecord);
                    return;
                }
                uniwords = FileSys.TBS(SpellCN.GetSpellCode(threadKeyword.Replace(" ", "")));
            }

            words = wordsLocal;
            int maxResults = isAll ? int.MaxValue : Math.Max(1, Option?.Findmax ?? 100);
            var results = new List<FrnFileOrigin>(Math.Min(maxResults, 512));
            object sync = new object();
            int ticket = 0;

            try
            {
                if (runningState.DoUSNupdate && !runningState.ForbidUSNupdate)
                {
                    foreach (var fs in fileSysList)
                    {
                        try { fs.DoWhileFileChanges(); }
                        catch { }
                    }
                }
                runningState.DoUSNupdate = false;

                Parallel.For(0, fileSysList.Count, d =>
                {
                    var fs = fileSysList[d];
                    if (fs.files.Count == 0) return;

                    if (driverNames != null)
                    {
                        bool driverFound = driverNames.Any(driverName =>
                            string.Equals(driverName, fs.driveInfoData.Name.TrimEnd('\\').TrimEnd(':'), StringComparison.OrdinalIgnoreCase)
                            || string.Equals(driverName, fs.driveInfoData.Name[0].ToString(), StringComparison.OrdinalIgnoreCase));
                        if (!driverFound) return;
                    }

                    // Linear scan over all alive rows (no trigram to save memory); filter with Index only, no alloc until we add.
                    for (int row = 0; row < fs.Index.RowCount; row++)
                    {
                        if (!fs.Index.IsAlive(row)) continue;

                        bool finded = true;

                        if (doDirectory)
                        {
                            ulong pfrn = fs.Index.GetParentFrn(row);
                            if (pfrn == ulong.MaxValue || !fs.Index.TryGetRow(pfrn, out int parentRow))
                            {
                                finded = false;
                            }
                            else
                            {
                                if ((unidwords | fs.Index.GetKeyIndex(parentRow)) != fs.Index.GetKeyIndex(parentRow))
                                    finded = false;
                                else
                                {
                                    foreach (string key in dwords!)
                                    {
                                        if (!fs.Index.ContainsSubstring(parentRow, key.AsSpan()))
                                        {
                                            finded = false;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (finded)
                        {
                            ulong kix = fs.Index.GetKeyIndex(row);
                            if ((uniwords | kix) != kix)
                                finded = false;
                            else
                            {
                                foreach (var w in wordsLocal)
                                {
                                    if (!fs.Index.ContainsSubstring(row, w.AsSpan()))
                                    {
                                        finded = false;
                                        break;
                                    }
                                }
                            }
                        }

                        if (!finded) continue;

                        var f = fs.GetOrigin(row);

                        if (isAll)
                        {
                            lock (sync)
                            {
                                results.Add(f);
                                if (results.Count == 1)
                                    Debug.WriteLine(f.fileReferenceNumber.ToString());
                            }
                        }
                        else
                        {
                            int t = Interlocked.Increment(ref ticket);
                            if (t <= maxResults)
                            {
                                lock (sync)
                                {
                                    results.Add(f);
                                    if (t == 1)
                                        Debug.WriteLine(f.fileReferenceNumber.ToString());
                                }
                            }
                        }
                    }
                });
            }
            catch
            {
                /* ignore */
            }

            int n = results.Count;
            Dispatcher.UIThread.Post(() =>
            {
                UpdateData(results, n);
                if (Option.Findmax > 0 && n > Option.Findmax && !isAll)
                    MessageData.Message = $"{Option.Findmax} +{LangManager.Instance.CurrentLang.Item}";
                else if (n <= 1)
                    MessageData.Message = $"{n} {LangManager.Instance.CurrentLang.Item}";
                else
                    MessageData.Message = $"{n} {LangManager.Instance.CurrentLang.Items}";
            });
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
