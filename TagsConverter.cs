using System;
using System.Globalization;
using System.Windows.Data;

namespace MyWPFApp
{
    public class TagsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string[] tags)
            {
                return string.Join(", ", tags);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}