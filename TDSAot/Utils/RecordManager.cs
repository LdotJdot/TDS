using System.Collections.Generic;
using TDSAot.ViewModels;

namespace TDSAot.Utils
{
    internal class RecordManager
    {
        public List<RecordFrn> Records = new List<RecordFrn>();

        internal void Clear()
        {
            Records.Clear();
        }

        internal void Add(RecordFrn record)
        {
            Records.Add(record);
        }
    }
}