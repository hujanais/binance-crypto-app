using GalaSoft.MvvmLight;
using LiveCharts.Defaults;
using Skender.Stock.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace crypto.Models
{
    /// <summary>
    /// Describes an asset
    /// </summary>
    public class Asset : ViewModelBase
    {
        private IList<IQuote> candles;
        private IList<MacdResult> macdChart;

        private decimal price;
        private MACDSummary macdSummary;
        private EMASummary emaSummary;
        private Trade CurrentTrade;
        private IList<Trade> TradeHistory;
        private bool hasTrade = false;
        private decimal buyPrice = 0;

        public Asset(string ticker)
        {
            this.Ticker = ticker;
            this.MacdSummary = new MACDSummary();
            this.EmaSummary = new EMASummary();
            this.TradeHistory = new List<Trade>();
            this.OHLCPoints = new List<OhlcPoint>();
        }

        public string Ticker { get; private set; }        
        
        public decimal Price
        {
            get { return this.price; }
            set
            {
                this.price = value;
                this.RaisePropertyChanged(nameof(this.Price));
                this.RaisePropertyChanged(nameof(this.UnrealizedPLPercentage));
            }
        }

        public bool HasTrade
        {
            get => this.hasTrade;
            set
            {
                this.hasTrade = value;
                this.RaisePropertyChanged(nameof(this.HasTrade));
            }
        }

        public decimal BuyPrice
        {
            get { return this.buyPrice; }
            set
            {
                this.buyPrice = value;
                this.RaisePropertyChanged(nameof(this.BuyPrice));
            }
        }

        public decimal UnrealizedPLPercentage
        {
            get
            {
                if (this.hasTrade && this.buyPrice > 0)
                {
                    return (this.price - this.buyPrice) / this.buyPrice * 100;
                }
                return 0;
            }
        }

        public IList<OhlcPoint> OHLCPoints { get; private set; }

        public IList<IQuote> Candles
        {
            get => this.candles;
        }

        public IList<MacdResult> MacdChart
        {
            get => this.macdChart;
        }

        public MACDSummary MacdSummary
        {
            get { return this.macdSummary; }
            set
            {
                this.macdSummary = value;
                this.RaisePropertyChanged(nameof(this.MacdSummary));
            }
        }

        public EMASummary EmaSummary
        {
            get { return this.emaSummary; }
            set
            {
                this.emaSummary = value;
                this.RaisePropertyChanged(nameof(this.EmaSummary));
            }
        }

        #region Methods

        public void executeBuy(decimal price)
        {
            this.CurrentTrade = new Trade();
            this.CurrentTrade.BuySide.AvgPrice = price;
        }

        public void executeSell(decimal price)
        {
            this.CurrentTrade.SellSide.AvgPrice = price;
            this.TradeHistory.Add(this.CurrentTrade);
            this.CurrentTrade = null;
        }

        public bool canBuy
        {
            get => CurrentTrade == null;
        }

        public bool canSell
        {
            get => CurrentTrade != null && CurrentTrade.BuySide.HasPosition && !CurrentTrade.SellSide.HasPosition;
        }

        public void updateQuotes(IList<IQuote> quotes)
        {
            this.candles = quotes;

            this.OHLCPoints.Clear();
            // transfer IQuote to OHLCPoint for charting.
            foreach (var candle in quotes)
            {
                this.OHLCPoints.Add(new OhlcPoint((double)candle.Open, (double)candle.High, (double)candle.Low, (double)candle.Close));
            }

            // Get the last price.
            // this.Price = quotes.Last().Close; // Last price is not updated from the websocket.

            // calculate EMA
            this.EmaSummary.EMA7 = new List<EmaResult>(Indicator.GetEma(quotes, 7));
            this.EmaSummary.EMA25 = new List<EmaResult>(Indicator.GetEma(quotes, 25));
            this.EmaSummary.EMA99 = new List<EmaResult>(Indicator.GetEma(quotes, 99));
            // Get the last 2 values of the macd.
            var last2EMA7 = this.EmaSummary.EMA7.OrderByDescending(p => p.Date).Take(2).ToArray();
            var currentEMA7 = last2EMA7[0];
            var previousEMA7 = last2EMA7[1];
            var currentEMA25 = this.EmaSummary.EMA25.OrderByDescending(p => p.Date).First();
            (this.EmaSummary.CrossOverSignal, this.EmaSummary.TrendSignal, this.EmaSummary.DeltaEMA) = this.getEMATrends(previousEMA7, currentEMA7, currentEMA25);

            // calculate MACD
            this.macdChart = new List<MacdResult>(Indicator.GetMacd(quotes));

            // Get the last 2 values of the macd.
            var last2 = this.macdChart.OrderByDescending(p => p.Date).Take(2).ToArray();
            var currentMACD = last2[0];
            var previousMACD = last2[1];

            this.MacdSummary.Macd = currentMACD.Macd.Value;
            this.MacdSummary.Signal = currentMACD.Signal.Value;
            this.MacdSummary.Histogram = currentMACD.Histogram.Value;
            (this.MacdSummary.CrossOverSignal, this.MacdSummary.TrendSignal) = this.getMACDTrends(previousMACD, currentMACD);
        }

        #endregion

        private (TrendEnum crossSignal, TrendEnum trendSignal, decimal deltaEMA) getEMATrends(EmaResult previousEMA7, EmaResult currentEMA7, EmaResult currentEMA25)
        {
            const double epsilon = 0.02;    // 2 percent

            TrendEnum crossSignal = TrendEnum.None;
            TrendEnum trendSignal = TrendEnum.None;
            decimal deltaEMA = 0;

            var deltaValue = Convert.ToDouble((currentEMA7.Ema - previousEMA7.Ema) / Math.Abs(previousEMA7.Ema.Value));
            
            if (Math.Abs(deltaValue) <= epsilon)
            {
                trendSignal = TrendEnum.None;
            }
            else if (deltaValue > 0)
            {
                trendSignal = TrendEnum.Up;
            }
            else
            {
                trendSignal = TrendEnum.Down;
            }

            if (currentEMA7.Ema > currentEMA25.Ema && previousEMA7.Ema <= currentEMA25.Ema)
            {
                crossSignal = TrendEnum.Up;
            } else if (currentEMA7.Ema < currentEMA25.Ema && previousEMA7.Ema >= currentEMA25.Ema)
            {
                crossSignal = TrendEnum.Down;
            } else
            {
                crossSignal = TrendEnum.None;
            }

            deltaEMA = currentEMA7.Ema.Value - currentEMA25.Ema.Value;

            return (crossSignal, trendSignal, deltaEMA);
        }

        #region Private functions
        private (TrendEnum crossSignal, TrendEnum trendSignal) getMACDTrends(MacdResult previousMACD, MacdResult currentMACD)
        {
            const double epsilon = 0.02;    // 2 percent

            TrendEnum crossSignal = TrendEnum.None;
            TrendEnum trendSignal = TrendEnum.None;

            var deltaHistogram = Convert.ToDouble((currentMACD.Histogram - previousMACD.Histogram)/ Math.Abs(previousMACD.Histogram.Value));
            if (Math.Abs(deltaHistogram) <= epsilon)
            {
                trendSignal = TrendEnum.None;
            } else if (deltaHistogram > 0)
            {
                trendSignal = TrendEnum.Up;
            } else
            {
                trendSignal = TrendEnum.Down;
            }

            if (currentMACD.Histogram > 0 && previousMACD.Histogram < 0)
            {
                crossSignal = TrendEnum.Up;
            } else if (currentMACD.Histogram < 0 && previousMACD.Histogram > 0)
            {
                crossSignal = TrendEnum.Down;
            } else
            {
                crossSignal = TrendEnum.None;
            }


            return (crossSignal, trendSignal);
        }

        #endregion
    }
}
