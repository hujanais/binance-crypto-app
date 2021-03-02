using GalaSoft.MvvmLight;
using Skender.Stock.Indicators;
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
    public class EMASummary : ViewModelBase
    {
        private IList<EmaResult> ema7;
        private IList<EmaResult> ema25;
        private IList<EmaResult> ema99;
        private decimal deltaEMA;
        private TrendEnum crossOverSignal;
        private TrendEnum trendSignal;

        public EMASummary()
        {
            this.ema7 = new List<EmaResult>();
            this.ema25 = new List<EmaResult>();
            this.ema99 = new List<EmaResult>();
        }

        public IList<EmaResult> EMA7
        {
            get => this.ema7;
            set
            {
                this.ema7 = value;
                this.RaisePropertyChanged(nameof(this.EMA7));
            }
        }
        public IList<EmaResult> EMA25
        {
            get => this.ema25;
            set
            {
                this.ema25 = value;
                this.RaisePropertyChanged(nameof(this.EMA25));
            }
        }
        public IList<EmaResult> EMA99
        {
            get => this.ema99;
            set
            {
                this.ema99 = value;
                this.RaisePropertyChanged(nameof(this.EMA99));
            }
        }

        public decimal DeltaEMA
        {
            get => this.deltaEMA;
            set
            {
                this.deltaEMA = value;
                this.RaisePropertyChanged(nameof(this.DeltaEMA));
            }
        }

        public TrendEnum CrossOverSignal
        {
            get => this.crossOverSignal;
            set
            {
                this.crossOverSignal = value;
                this.RaisePropertyChanged(nameof(this.CrossOverSignal));
            }
        }

        public TrendEnum TrendSignal
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
