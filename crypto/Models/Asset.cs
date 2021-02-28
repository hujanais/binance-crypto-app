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
    /// Describes an asset
    /// </summary>
    public class Asset : ViewModelBase
    {
        private IList<IQuote> candles;
        private IList<MacdResult> macdChart;
        private decimal price;
        private MACDSummary macdSummary;
        private Trade CurrentTrade;
        private IList<Trade> TradeHistory;

        public Asset(string ticker)
        {
            this.Ticker = ticker;
            this.MacdSummary = new MACDSummary();
            this.TradeHistory = new List<Trade>();
        }

        public string Ticker { get; private set; }        
        public decimal Price
        {
            get { return this.price; }
            set
            {
                this.price = value;
                this.RaisePropertyChanged(nameof(this.Price));
            }
        }

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

            // Get the last price.
            // this.Price = quotes.Last().Close; // Last price is not updated from the websocket.

            // calculate MACD
            this.macdChart = new List<MacdResult>(Indicator.GetMacd(quotes));

            // Get the last 2 values of the macd.
            var last2 = this.macdChart.OrderByDescending(p => p.Date).Take(2).ToArray();
            var currentMACD = last2[0];
            var previousMACD = last2[1];

            this.MacdSummary.Macd = currentMACD.Macd.Value;
            this.MacdSummary.Signal = currentMACD.Signal.Value;
            this.MacdSummary.Histogram = currentMACD.Histogram.Value;
            (this.MacdSummary.CrossOverSignal, this.MacdSummary.TrendSignal) = this.getTrends(previousMACD, currentMACD);
        }

        #endregion

        #region Private functions
        private (MACDSummary.MACDTrendEnum crossSignal, MACDSummary.MACDTrendEnum trendSignal) getTrends(MacdResult previousMACD, MacdResult currentMACD)
        {
            const double epsilon = 0.02;    // 2 percent

            MACDSummary.MACDTrendEnum crossSignal = MACDSummary.MACDTrendEnum.None;
            MACDSummary.MACDTrendEnum trendSignal = MACDSummary.MACDTrendEnum.None;

            var deltaHistogram = Convert.ToDouble((currentMACD.Histogram - previousMACD.Histogram)/ Math.Abs(previousMACD.Histogram.Value));
            if (Math.Abs(deltaHistogram) <= epsilon)
            {
                trendSignal = MACDSummary.MACDTrendEnum.None;
            } else if (deltaHistogram > 0)
            {
                trendSignal = MACDSummary.MACDTrendEnum.Up;
            } else
            {
                trendSignal = MACDSummary.MACDTrendEnum.Down;
            }

            if (currentMACD.Histogram > 0 && previousMACD.Histogram < 0)
            {
                crossSignal = MACDSummary.MACDTrendEnum.Up;
            } else if (currentMACD.Histogram < 0 && previousMACD.Histogram > 0)
            {
                crossSignal = MACDSummary.MACDTrendEnum.Down;
            } else
            {
                crossSignal = MACDSummary.MACDTrendEnum.None;
            }


            return (crossSignal, trendSignal);
        }

        #endregion
    }
}
