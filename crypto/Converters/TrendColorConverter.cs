using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using static crypto.Models.MACDSummary;

namespace crypto.Converters
{
    /// <summary>
    /// Convert trends to color
    /// </summary>
    class TrendColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            MACDTrendEnum trend = (MACDTrendEnum)value;
            switch (trend)
            {
                case MACDTrendEnum.None:
                    return "White";
                case MACDTrendEnum.Up:
                    return "LimeGreen";
                case MACDTrendEnum.Down:
                    return "Salmon";
            }

            return "White";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
