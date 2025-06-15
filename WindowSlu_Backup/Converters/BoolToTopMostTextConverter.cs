using System;
using System.Globalization;
using System.Windows.Data;

namespace WindowSlu.Converters
{
    public class BoolToTopMostTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isTopMost)
            {
                return isTopMost ? "\uE840" : "\uE718"; // Segoe MDL2 Assets: Pin, Unpin
            }
            return "\uE718";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 