using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace GravadorMulti.Converters
{
    public class InverseProgressToPixelConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Count == 2)
            {
                if (values[0] is double progress && values[1] is double width)
                {
                    progress = Math.Clamp(progress, 0, 1);
                    return (1.0 - progress) * width;
                }
            }
            return 0.0; // Padrão
        }
    }
}
