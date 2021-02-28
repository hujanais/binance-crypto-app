using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace crypto.Models
{
    /// <summary>
    /// Describes the MACD trading signal summary
    /// </summary>
    public class MACDSummary : ViewModelBase
    {
        private decimal macd;
        private decimal signal;
        private decimal histogram;
        private MACDTrendEnum crossOverSignal;
        private MACDTrendEnum trendSignal;

        public enum MACDTrendEnum
        {
            None,
            Down,
            Up,
        }

        public enum MACDHistogramRegion
        {
            None,
            Negative,
            Positive,
        }

        public decimal Macd
        {
            get { return this.macd; }
            set
            {
                this.macd = value;
                this.RaisePropertyChanged(nameof(this.Macd));
            }
        }

        public decimal Signal
        {
            get { return this.signal; }
            set
            {
                this.signal = value;
                this.RaisePropertyChanged(nameof(this.Signal));
            }
        }

        public decimal Histogram
        {
            get { return this.histogram; }
            set
            {
                this.histogram = value;
                this.RaisePropertyChanged(nameof(this.Histogram));
            }
        }

        public MACDTrendEnum CrossOverSignal
        {
            get => this.crossOverSignal;
            set
            {
                this.crossOverSignal = value;
                this.RaisePropertyChanged(nameof(this.CrossOverSignal));
            }
        }

        public MACDTrendEnum TrendSignal
        {
            get => this.trendSignal;
            set
            {
                this.trendSignal = value;
                this.RaisePropertyChanged(nameof(this.TrendSignal));
            }
        }
    }
}
