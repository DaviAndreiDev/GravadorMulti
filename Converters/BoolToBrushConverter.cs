using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace GravadorMulti.Converters
{
    public class BoolToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isTrue && parameter is string colors)
            {
                var parts = colors.Split(':');
                var trueColor = parts.Length > 0 ? parts[0] : "#FF4444";
                var falseColor = parts.Length > 1 ? parts[1] : "Transparent";
                
                return new SolidColorBrush(Color.Parse(isTrue ? trueColor : falseColor));
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
