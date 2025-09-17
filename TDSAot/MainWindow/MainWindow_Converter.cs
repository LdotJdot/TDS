using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using TDSAot.Utils;
using TDSNET.Engine.Actions.USN;

namespace TDSAot
{
    public partial class MainWindow : Window
    {

    }

    public class HighlightTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var frn = value as FrnFileOrigin;
            
            if (frn == null) return null;

            var inlines = new InlineCollection();

            //try
            //{

            //}
            //catch
            {
                inlines.Clear();
                inlines.Add(new Run { Text = frn.FileName });
                return inlines;
            }

            //foreach (var word in keyWords)
            //{

            //}

            //if (string.IsNullOrEmpty(highlightText))
            //    return new InlineCollection { new Run { Text = fullText } };

            //var index = fullText.IndexOf(highlightText, StringComparison.OrdinalIgnoreCase);

            //if (index < 0)
            //{
            //    inlines.Add(new Run { Text = fullText });
            //}
            //else
            //{
            //    // 添加高亮前的文本
            //    if (index > 0)
            //        inlines.Add(new Run { Text = fullText.Substring(0, index) });

            //    // 添加高亮文本
            //    inlines.Add(new Run
            //    {
            //        Text = fullText.Substring(index, highlightText.Length),
            //        Foreground = Brushes.Yellow,
            //    });

            //    // 添加高亮后的文本
            //    if (index + highlightText.Length < fullText.Length)
            //        inlines.Add(new Run
            //        {
            //            Text = fullText.Substring(index + highlightText.Length)
            //        });
            //}

            return inlines;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}