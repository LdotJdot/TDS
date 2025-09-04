using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using TDSNET.Engine.Actions.USN;

namespace TDSAot.ViewModels
{
    public class RecordFrn : ReactiveObject
    {
        private const int maxFreq = 100;
        private const int initFreq = 50;
        private const int increFreq = 15;
        public FrnFileOrigin file;
        public int freq = initFreq;

        public RecordFrn(FrnFileOrigin f)
        {
            this.file = f;
        }

        public RecordFrn(FrnFileOrigin f, int freq)
        {
            this.file = f;
            this.freq = freq;
        }

        public override string ToString()
        {
            if (file != null)
            {
                return $"{file.fileReferenceNumber}@{file.VolumeName}@{freq}";
            }
            else
            {
                return string.Empty;
            }
        }

        public static void Update(ref List<RecordFrn> records, FrnFileOrigin? newRecord = null)
        {
            if (newRecord != null)
            {
                var oldrec = records.FirstOrDefault(o => o.file == newRecord);
                if (oldrec != null)
                {
                    if (oldrec.freq < initFreq) oldrec.freq = initFreq;

                    oldrec.freq += increFreq;
                }
                else
                {
                    records.Add(new RecordFrn(newRecord));
                }

                records = records.OrderByDescending(o => o.freq).ToList();

                for (int i = 0; i < records.Count; i++)
                {
                    records[i].freq -= i;
                }
            }
            records = records.OrderByDescending(o => o.freq).ToList();
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].freq > maxFreq) records[i].freq = maxFreq;
            }
        }
    }
}