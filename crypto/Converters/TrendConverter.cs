using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using static crypto.Models.MACDSummary;

namespace crypto.Converters
{
    /// <summary>
    /// Convert trend into icon.
    /// </summary>
    class TrendConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            MACDTrendEnum trend = (MACDTrendEnum)value;
            switch (trend)
            {
                case MACDTrendEnum.None:
                    return "ArrowLeftRight";
                case MACDTrendEnum.Up:
                    return "ArrowTopRight";
                case MACDTrendEnum.Down:
                    return "ArrowBottomRight";
            }

            return "Help";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
