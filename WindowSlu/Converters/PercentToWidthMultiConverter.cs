using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Controls;

namespace WindowSlu.Converters
{
    public class PercentToWidthMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is double value && parameter is Slider slider)
            {
                double min = slider.Minimum;
                double max = slider.Maximum;
                double sliderWidth = slider.ActualWidth;

                if (max > min)
                {
                    double percent = (value - min) / (max - min);
                    return percent * sliderWidth;
                }
            }
            return 0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}