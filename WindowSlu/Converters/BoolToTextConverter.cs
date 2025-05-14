using System;
using System.Globalization;
using System.Windows.Data;

namespace WindowSlu.Converters
{
    /// <summary>
    /// bool値をテキストに変換するコンバーター
    /// </summary>
    public class BoolToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "有効" : "無効";
            }
            return "無効";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
