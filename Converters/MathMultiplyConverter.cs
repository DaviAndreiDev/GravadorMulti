using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace GravadorMulti.Converters
{
    public class MathMultiplyConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values != null && values.Count == 2)
            {
                double v1 = 0, v2 = 0;
                
                if (values[0] is double d1) v1 = d1;
                else if (values[0] is float f1) v1 = (double)f1;
                else if (values[0] is int i1) v1 = (double)i1;
                else if (values[0] != null && double.TryParse(values[0]?.ToString(), out double parsed1)) v1 = parsed1;
                
                if (values[1] is double d2) v2 = d2;
                else if (values[1] is float f2) v2 = (double)f2;
                else if (values[1] is int i2) v2 = (double)i2;
                else if (values[1] != null && double.TryParse(values[1]?.ToString(), out double parsed2)) v2 = parsed2;

                return v1 * v2;
            }
            return 0.0;
        }
    }
}
