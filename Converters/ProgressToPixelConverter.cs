using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace GravadorMulti.Converters
{
    public class ProgressToPixelConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values != null && values.Count == 2 && 
                values[0] is double progress && 
                values[1] is double width)
            {
                return progress * width;
            }
            return 0.0;
        }
    }
}