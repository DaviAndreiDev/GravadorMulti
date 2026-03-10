using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace GravadorMulti.Converters
{
    public class NullToEmptyPointsConverter : IValueConverter
    {
        private static readonly List<Point> EmptyPoints = new List<Point> { new Point(0, 0), new Point(1, 0) };

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is IEnumerable<Point> validPoints)
            {
                return validPoints;
            }
            return EmptyPoints;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
