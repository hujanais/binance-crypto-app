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
    /// Convert Signal to Text.
    /// </summary>
    class SignalConverter : IValueConverter
    {
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            MACDTrendEnum trend = (MACDTrendEnum)value;
            switch (trend)
            {
                case MACDTrendEnum.None:
                    return "--";
                case MACDTrendEnum.Up:
                    return "BUY";
                case MACDTrendEnum.Down:
                    return "SELL";
            }

            return "--";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
