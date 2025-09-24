using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TDS.Utils
{
    internal class SpanCharUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NotContains(ReadOnlySpan<char> source, ReadOnlySpan<char> pattern)
        {
            return source.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) < 0;
        }

    }
}
