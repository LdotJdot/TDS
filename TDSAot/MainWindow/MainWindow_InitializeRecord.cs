using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.IO;
using System.Linq;
using TDSAot.State;
using TDSAot.Utils;
using TDSAot.ViewModels;
using TDSNET.Engine.Actions.USN;
using TDSNET.Utils;

namespace TDSAot
{
    public partial class MainWindow : Window
    {
        private RecordManager recordsManager = new RecordManager();

        private void buffercoolies()
        {
            //Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using (StreamWriter fs = new StreamWriter(AppOption.CurrentRecordPath, false, System.Text.Encoding.GetEncoding("gb2312")))
            {
                foreach (var f in recordsManager.Records)
                {
                    string fp = PathHelper.GetPath(f.file).ToString();
                    if (File.Exists(fp) || Directory.Exists(fp))
                    {
                        fs.WriteLine(f.ToString());
                    }
                }
                fs.Close();
            }
        }

        private void UpdateRecord(FrnFileOrigin? targ = null)
        {

            RecordFrn.Update(ref recordsManager.Records, targ);
            buffercoolies();
        }

        private void ChangeToRecord()
        {
            UpdateData(recordsManager.Records.Select(o => o.file).ToList(),recordsManager.Records.Count());

            if (recordsManager.Records.Count <= 1)
            {
                MessageData.Message = $"{this.Items.DisplayCount} item";
            }
            else
            {
                MessageData.Message = $"{this.Items.DisplayCount} items";
            }
            Dispatcher.UIThread.Invoke(scrollView.ScrollToHome);

        }

        private void ReadRecords()
        {
            if (File.Exists(AppOption.CurrentRecordPath))
            {
                //try
                {
                    // Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    using (StreamReader fs = new StreamReader(AppOption.CurrentRecordPath, System.Text.Encoding.GetEncoding("gb2312")))
                    {
                        recordsManager.Clear();
                        while (!fs.EndOfStream)
                        {
                            try
                            {
                                string[] KeyValue = fs.ReadLine().Split('@');

                                if (KeyValue != null && KeyValue.Length == 3)
                                {
                                    foreach (var filesys in fileSysList)
                                    {
                                        if (filesys.driveInfoData.Name.TrimEnd('\\').TrimEnd(':') == KeyValue[1])
                                        {
                                            if (filesys.files.TryGetValue(UInt64.Parse(KeyValue[0]), out FrnFileOrigin f))
                                            {
                                                recordsManager.Add(new RecordFrn(f, int.Parse(KeyValue[2])));
                                            }
                                        }
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }
                        fs.Close();
                    }
                }
                //  catch { }
            }
        }
    }
}