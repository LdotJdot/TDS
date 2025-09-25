using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using TDSAot.Utils;
using TDSNET.Engine.Actions.USN;
using TDSNET.Engine.Utils;
using TDSNET.Utils;

namespace TDSAot
{
    public partial class MainWindow : Window
    {

    }

    public class HighlightTextConverter : IValueConverter
    {
        static readonly IBrush highlightBrush = Brush.Parse("#00BFFF");
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }
            var frn = value as FrnFileOrigin;


            var inlines = new InlineCollection();


            var nameOrigin = frn.FileName;
            var nameOriginUpper = nameOrigin.ToUpper();
            if (MainWindow.words.Length == 0)
            {
                 inlines.Add(new Run { Text = frn.FileName });
                return inlines;
            }
            try
            {
                var words = new string[MainWindow.words.Length];
                for (int i=0;i< MainWindow.words.Length;i++)
                {
                    words[i]= MainWindow.words[i].Trim('|');
                }

                TextMatch[] results;

                var nameNorm = PathHelper.getfileNameNormalize(frn.innerFileName);

                if (nameNorm.Length == 0)
                {
                    results = StringSplitAndMerge.GetTextMatches(
                        nameOriginUpper,
                        words.ToArray()
                        );
                }
                else
                {
                    results = StringSplitAndMerge.GetTextMatches(
                        nameOriginUpper,
                        words.ToArray(),
                        nameNorm,
                        words.Select(o => SpellCN.GetSpellCode(o)).ToArray()
                        );
                }

                foreach (var result in results)
                {
                    var run = new Run(nameOrigin.Substring(result.Start, result.Length));

                    if (result.IsMatch) run.Foreground = highlightBrush;

                    inlines.Add(run);
                }
            }
            catch
            {
                inlines.Clear();
                inlines.Add(new Run { Text = frn.FileName });
            }
            
            return inlines;
        }



        // 在StringSplitAndMerge类中添加优化版本
        public static TextMatch[] GetTextMatchesOptimized(string text, string[] words)
        {
            var matches = new List<TextMatch>();
            var usedPositions = new HashSet<int>();

            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(word)) continue;

                int index = -1;
                while ((index = text.IndexOf(word, index + 1, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    // 避免重叠匹配
                    if (!IsPositionUsed(usedPositions, index, word.Length))
                    {
                        MarkPositionUsed(usedPositions, index, word.Length);
                        matches.Add(new TextMatch(index, word.Length, true));
                    }
                }
            }

            return matches.OrderBy(m => m.Start).ToArray();
        }

        private static bool IsPositionUsed(HashSet<int> usedPositions, int start, int length)
        {
            for (int i = start; i < start + length; i++)
            {
                if (usedPositions.Contains(i)) return true;
            }
            return false;
        }

        private static void MarkPositionUsed(HashSet<int> usedPositions, int start, int length)
        {
            for (int i = start; i < start + length; i++)
            {
                usedPositions.Add(i);
            }
        }


        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}