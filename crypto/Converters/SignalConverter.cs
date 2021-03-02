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
            TrendEnum trend = (TrendEnum)value;
            switch (trend)
            {
                case TrendEnum.None:
                    return "--";
                case TrendEnum.Up:
                    return "BUY";
                case TrendEnum.Down:
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
