using crypto.Models;
using crypto.Utilities;
using ExchangeSharp;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Skender.Stock.Indicators;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace crypto.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        #region Fields

        const int ONEHOUR_MS = 60 * 60 * 1000;
        private ExchangeSharp.ExchangeBinanceUSAPI api;
        private IWebSocket socket;

        private int progressPercentage = 0;
        private DateTime lastUpdated;
        private DateTime nextUpdate;
        private Timer stateTimer;
        private bool isReady = true;
        private string progressBarMessage = "...";
        private bool isLiveTrading = false;

        private Asset selectedAsset;
        private double selectedAssetTickTime;
        private double selectedTickTime;

        private ChartValues<OhlcPoint> ohlcChartValues = new ChartValues<OhlcPoint>();
        private ChartValues<double> ema7ChartValues = new ChartValues<double>();
        private ChartValues<double> ema25ChartValues = new ChartValues<double>();
        private ChartValues<double> ema99ChartValues = new ChartValues<double>();

        private ChartValues<double> macdChartValues = new ChartValues<double>();
        private ChartValues<double> macdSignalChartValues = new ChartValues<double>();
        private ChartValues<double> macdHistogramChartValues = new ChartValues<double>();

        NLog.Logger logger = NLog.LogManager.GetLogger("crypto-app");

        #endregion

        #region ICommands

        public ICommand EnumeratePairsCommand { get; private set; }

        #endregion

        #region Properties

        /// <summary>
        /// The candle resolution in hours.
        /// </summary>
        public IList<double> TickTimes { get; private set; }

        public double SelectedTickTime
        {
            get => this.selectedTickTime;
            set
            {
                this.selectedTickTime = value;
                this.RaisePropertyChanged(nameof(this.SelectedTickTime));
            }
        }

        public bool IsLiveTrading
        {
            get => this.isLiveTrading;
            set
            {
                this.isLiveTrading = value;
                this.RaisePropertyChanged(nameof(this.IsLiveTrading));
            }
        }
        public double SelectedAssetTickTime
        {
            get => this.selectedAssetTickTime;
            set
            {
                this.selectedAssetTickTime = value;
                this.RaisePropertyChanged(nameof(this.SelectedAssetTickTime));

                this.doUpdateAssetTickTime();
            }
        }

        public string ProgressBarMessage
        {
            get => this.progressBarMessage;
            set
            {
                this.progressBarMessage = value;
                this.RaisePropertyChanged(nameof(this.ProgressBarMessage));
            }
        }

        public Asset SelectedAsset
        {
            get => this.selectedAsset;
            set
            {
                this.selectedAsset = value;
                this.displayChart(value);
                this.RaisePropertyChanged(nameof(SelectedAsset));
            }
        }

        public DateTime LastUpdated
        {
            get => lastUpdated;
            set
            {
                lastUpdated = value;
                this.RaisePropertyChanged(nameof(LastUpdated));
            }
        }

        public DateTime NextUpdate
        {
            get => nextUpdate;
            set
            {
                nextUpdate = value;
                this.RaisePropertyChanged(nameof(NextUpdate));
            }
        }


        public int ProgressPercentage
        {
            get => this.progressPercentage;
            set
            {
                this.progressPercentage = value;
                this.RaisePropertyChanged(nameof(this.ProgressPercentage));
            }
        }

        public IList<Asset> Assets { get; private set; }

        public SeriesCollection SeriesCollection { get; set; }
        public SeriesCollection MacdCollection { get; private set; }
        public bool IsReady
        {
            get => isReady;
            set
            {
                isReady = value;
                this.RaisePropertyChanged(nameof(this.IsReady));
            }
        }

        #endregion

        public MainViewModel()
        {
            logger.Info($"App started");

            // Initialize the api.
            api = new ExchangeSharp.ExchangeBinanceUSAPI();

            // load in the api keys.
            api.LoadAPIKeysUnsecure(ConfigurationManager.AppSettings.Get("PublicKey"), ConfigurationManager.AppSettings.Get("SecretKey"));

            // ICommand binding
            this.EnumeratePairsCommand = new RelayCommand(this.executeEnumerate);

            // Pre-allocate memory
            this.Assets = new ObservableCollection<Asset>();
            this.SeriesCollection = new SeriesCollection();
            this.MacdCollection = new SeriesCollection();

            this.SeriesCollection.Add(new OhlcSeries() { Values = ohlcChartValues, ScalesYAt = 0, Fill = Brushes.Transparent });
            this.SeriesCollection.Add(new LineSeries() { Values = ema7ChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });
            this.SeriesCollection.Add(new LineSeries() { Values = ema25ChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });
            this.SeriesCollection.Add(new LineSeries() { Values = ema99ChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });

            this.MacdCollection.Add(new LineSeries() { Values = macdChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });
            this.MacdCollection.Add(new LineSeries() { Values = macdSignalChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });
            this.MacdCollection.Add(new ColumnSeries() { Values = macdHistogramChartValues, ScalesYAt = 0 });

            this.TickTimes = new ObservableCollection<double> { 0.25, 0.5, 1, 2, 4, 8, 12, 24 };
            this.SelectedTickTime = 8;
        }

        /// <summary>
        /// You can call this method multiple times with no issues.
        /// </summary>
        private async void executeEnumerate()
        {
            // Get a list of markets.
            var pairs = (await api.GetTickersAsync()).Where(p => p.Value.MarketSymbol.Contains("USDT") && !p.Value.MarketSymbol.Contains("USDTUSD")).Take(20).ToList();
            pairs.ForEach(pair =>
            {
                if (this.Assets.FirstOrDefault(a => a.Ticker == pair.Value.MarketSymbol) == null)
                {
                    this.Assets.Add(new Asset(pair.Value.MarketSymbol));
                }
            });

            // Start websocket.
            // the web socket will handle disconnects and attempt to re-connect automatically.
            string[] tickers = this.Assets.Select(a => a.Ticker).ToArray();
            int numOfTickers = this.Assets.Count();
            if (socket == null)
            {
                socket = await api.GetTickersWebSocketAsync(items =>
                {
                    for (int i = 0; i < numOfTickers; i++)
                    {
                        var tkr = this.Assets[i].Ticker;
                        var foundTkr = items.FirstOrDefault(p => p.Key == tkr);
                        if (foundTkr.Value != null)
                        {
                            this.Assets[i].Price = foundTkr.Value.Last;
                        }

                    }
                }, tickers);
            }

            // start the timer event.
            if (stateTimer != null)
            {
                stateTimer.Dispose();
            }
            stateTimer = new Timer((stateObj) => executeStart(stateObj));
            stateTimer.Change(500, ONEHOUR_MS);
        }

        private async void executeStart(Object stateInfo)
        {
            this.LastUpdated = DateTime.Now;
            this.NextUpdate = this.LastUpdated.AddMilliseconds(ONEHOUR_MS);

            // some cleanup.
            ProgressPercentage = 0;
            int numOfTickers = 0;
            this.IsReady = false;

            var tickers = this.Assets.Select(p => p.Ticker).ToArray();
            numOfTickers = tickers.Count();

            await Task.Factory.StartNew(async () =>
            {
                for (int idx = 0; idx < numOfTickers; idx++)
                {
                    var ticker = tickers[idx];
                    int periodSeconds = Convert.ToInt32(selectedTickTime * 60 * 60);
                    var candles = await api.GetCandlesAsync(ticker, periodSeconds, null, DateTime.Now, 500);
                    IList<IQuote> quotes = new List<IQuote>();
                    candles.ToList().ForEach(data =>
                    {
                        quotes.Add(new Quote() { Open = data.OpenPrice, Close = data.ClosePrice, Low = data.LowPrice, High = data.HighPrice, Volume = Convert.ToDecimal(data.BaseCurrencyVolume), Date = data.Timestamp });
                    });

                    var asset = this.Assets.First(a => a.Ticker == ticker);
                    asset.updateQuotes(quotes);

                    this.ProgressPercentage = Convert.ToInt32(((double)(idx + 1) / (double)numOfTickers) * 100.0);
                    this.ProgressBarMessage = $"updating: {progressPercentage}%";
                }

                this.IsReady = true;

                // update wallet if live trading.
                double usdtAvail = 0.0;
                Dictionary<string, decimal> wallet = null;
                if (isLiveTrading)
                {
                    wallet = await api.GetAmountsAvailableToTradeAsync();
                    if (wallet.ContainsKey("USDT"))
                    {
                        usdtAvail = (double)wallet["USDT"];
                    }
                }

                // Check for buy signal.
                var balanceLowWaterMark = 350;
                var stakeSize = 300;
                // prevent BTC from being traded.
                var assetsToBuy = this.Assets.Where(a => !a.Ticker.Contains("BTC") && (a.MacdSummary.CrossOverSignal == TrendEnum.Up && !a.HasTrade && (double)a.Price > 0.1));
                foreach (var asset in assetsToBuy)
                {
                    // get the number of shares to buy.
                    var shares = RoundShares.GetRoundedShares(stakeSize, asset.Price);

                    if (isLiveTrading)
                    {
                        // live trading
                        if (usdtAvail >= balanceLowWaterMark)
                        {
                            // place limit order for 0.01 bitcoin at ticker.Ask USD
                            var result = await api.PlaceOrderAsync(new ExchangeOrderRequest
                            {
                                Amount = shares,
                                IsBuy = true,
                                Price = asset.Price,
                                MarketSymbol = asset.Ticker
                            });
                            logger.Info($"BUY: PlaceOrderAsync. {result.MarketSymbol} ${result.Price}.  {result.Result}. {result.OrderId}");

                            // reduce banksize
                            usdtAvail -= stakeSize;

                            asset.HasTrade = true;
                            asset.BuyPrice = asset.Price;

                        }
                        else
                        {
                            logger.Info($"{isLiveTrading}. BUY FAILED: out of money. {usdtAvail}");
                        }
                    }
                    else
                    {
                        // paper trading.
                        asset.HasTrade = true;
                        asset.BuyPrice = asset.Price;
                        logger.Info($"{isLiveTrading}. BUY: {asset.Ticker}. {shares} @ ${asset.BuyPrice}");
                    }
                }

                // check for sell signal.
                var assetsToSell = this.Assets.Where(a => !a.Ticker.Contains("BTC") && a.HasTrade && a.MacdSummary.CrossOverSignal == TrendEnum.Down);
                foreach (var asset in assetsToSell)
                {
                    if (isLiveTrading)
                    {
                        // live trading.
                        // check to see if we indeed have any coins to sell. 
                        if (wallet.ContainsKey(asset.Ticker))
                        {
                            var amountAvail = wallet[asset.Ticker]; // sell all.

                            var result = await api.PlaceOrderAsync(new ExchangeOrderRequest
                            {
                                Amount = amountAvail,
                                IsBuy = false,
                                Price = asset.Price,
                                MarketSymbol = asset.Ticker
                            });

                            logger.Info($"PlaceOrderAsync. {result.MarketSymbol}, {result.OrderId}, {result.Result}");
                            logger.Info($"SELL: {asset.Ticker}. ${asset.BuyPrice} - ${asset.Price} - { asset.UnrealizedPLPercentage }% - {amountAvail}");
                        }
                        else
                        {
                            logger.Info($"SELL ERROR: No position in {asset.Ticker}");
                        }
                    }
                    else
                    {
                        // paper trading.
                        logger.Info($"{isLiveTrading}. SELL: {asset.Ticker}. ${asset.BuyPrice} - ${asset.Price} - { asset.UnrealizedPLPercentage }%");
                        asset.HasTrade = false;
                        asset.BuyPrice = 0;
                    }
                }
            });
        }

        private async void doUpdateAssetTickTime()
        {
            int periodSeconds = Convert.ToInt32(this.selectedAssetTickTime * 60 * 60);
            var candles = await api.GetCandlesAsync(this.selectedAsset.Ticker, periodSeconds, null, DateTime.UtcNow, 240);
            IList<IQuote> quotes = new List<IQuote>();
            candles.ToList().ForEach(data =>
            {
                quotes.Add(new Quote() { Open = data.OpenPrice, Close = data.ClosePrice, Low = data.LowPrice, High = data.HighPrice, Volume = Convert.ToDecimal(data.BaseCurrencyVolume), Date = data.Timestamp });
            });

            this.selectedAsset.updateQuotes(quotes);
            this.displayChart(this.selectedAsset);
        }

        private void displayChart(Asset asset)
        {
            try
            {
                ohlcChartValues.Clear();
                ema7ChartValues.Clear();
                ema25ChartValues.Clear();
                ema99ChartValues.Clear();
                macdChartValues.Clear();
                macdSignalChartValues.Clear();
                macdHistogramChartValues.Clear();
                ohlcChartValues.AddRange(asset.OHLCPoints.Reverse().Take(100).Reverse());
                ema7ChartValues.AddRange(asset.EmaSummary.EMA7.Select(p => (double)p.Ema.GetValueOrDefault(0)).Reverse().Take(100).Reverse());
                ema25ChartValues.AddRange(asset.EmaSummary.EMA25.Select(p => (double)p.Ema.GetValueOrDefault(0)).Reverse().Take(100).Reverse());
                ema99ChartValues.AddRange(asset.EmaSummary.EMA99.Select(p => (double)p.Ema.GetValueOrDefault(0)).Reverse().Take(100).Reverse());

                macdChartValues.AddRange(asset.MacdChart.Select(p => (double)p.Macd.GetValueOrDefault(0)).Reverse().Take(100).Reverse());
                macdSignalChartValues.AddRange(asset.MacdChart.Select(p => (double)p.Signal.GetValueOrDefault(0)).Reverse().Take(100).Reverse());
                macdHistogramChartValues.AddRange(asset.MacdChart.Select(p => (double)p.Histogram.GetValueOrDefault(0)).Reverse().Take(100).Reverse());
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
