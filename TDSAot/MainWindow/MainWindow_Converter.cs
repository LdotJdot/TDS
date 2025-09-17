using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
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
        IBrush highlightBrush = Brush.Parse("#00BFFF");
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var frn = value as FrnFileOrigin;
            
            if (frn == null) return null;

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
                var words = MainWindow.words.Select(o => o.Trim('|'));
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

                    if (result.IsMatch) run.Foreground =highlightBrush;

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

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}