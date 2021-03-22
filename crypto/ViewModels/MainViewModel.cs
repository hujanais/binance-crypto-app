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
using System.Windows;
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
        private int numOfOpenTrades = 0;
        private double pl = 0.0;

        private Asset selectedAsset;
        private double selectedTickTime;

        private ChartValues<OhlcPoint> ohlcChartValues = new ChartValues<OhlcPoint>();
        private ChartValues<double> ema7ChartValues = new ChartValues<double>();
        private ChartValues<double> ema25ChartValues = new ChartValues<double>();
        private ChartValues<double> ema99ChartValues = new ChartValues<double>();

        private ChartValues<double> macdChartValues = new ChartValues<double>();
        private ChartValues<double> macdSignalChartValues = new ChartValues<double>();
        private ChartValues<double> macdHistogramChartValues = new ChartValues<double>();

        NLog.Logger logger = NLog.LogManager.GetLogger("crypto-app");

        decimal balanceLowWaterMark = 275m;
        decimal stakeSize = 250m;

        #endregion

        #region ICommands

        public ICommand EnumeratePairsCommand { get; private set; }
        public ICommand ResetCommand { get; private set; }
        public ICommand SellCommand { get; private set; }
        #endregion

        #region Properties

        /// <summary>
        /// The candle resolution in hours.
        /// </summary>
        public IList<double> TickTimes { get; private set; }

        public int NumOfOpenTrades
        {
            get => this.numOfOpenTrades;
            set
            {
                this.numOfOpenTrades = value;
                this.RaisePropertyChanged(nameof(this.NumOfOpenTrades));
            }
        }

        public double PL
        {
            get => this.pl;
            set
            {
                this.pl = value;
                this.RaisePropertyChanged(nameof(this.PL));
            }
        }

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
            this.ResetCommand = new RelayCommand<Asset>(this.doReset);
            this.SellCommand = new RelayCommand<Asset>(this.doSell);

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

            this.TickTimes = new ObservableCollection<double> { 4, 8, 12};
            this.SelectedTickTime = 8;
        }

        private async void doSell(Asset asset)
        {
            if (MessageBox.Show("Confirm Sale?", "Confirmation", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                Dictionary<string, decimal> wallet = null;
                wallet = await api.GetAmountsAvailableToTradeAsync();
                var currency = asset.Ticker.Replace("USD", string.Empty);
                if (wallet.ContainsKey(currency))
                {
                    var amountAvail = wallet[currency]; // sell all.
                    try
                    {
                        var result = await api.PlaceOrderAsync(new ExchangeOrderRequest
                        {
                            Amount = amountAvail,
                            IsBuy = false,
                            Price = asset.Bid,
                            MarketSymbol = asset.Ticker
                        });

                        asset.HasTrade = false;
                        asset.BuyPrice = 0;
                        logger.Info($"PlaceOrderAsync-Sell. {result.MarketSymbol}, {result.OrderId}, {result.Result}");
                    }
                    catch (Exception ex)
                    {
                        logger.Info($"PlaceOrderAsync-Sell. {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// sometimes the user sells the coin from somewhere else so this is to reset the coin.
        /// </summary>
        /// <param name="asset"></param>
        private void doReset(Asset asset)
        {
            asset.HasTrade = false;
            asset.BuyPrice = 0m;
        }

        //private async void doBuy(Asset asset)
        //{
        //    ExchangeMarket market = await api.GetExchangeMarketFromCacheAsync(asset.Ticker);

        //    Dictionary<string, decimal> wallet = null;
        //    wallet = await api.GetAmountsAvailableToTradeAsync();

        //    // get the number of shares to buy.
        //    var shares = RoundShares.GetRoundedShares(12, asset.Price);

        //    try
        //    {
        //        // place limit order for 0.01 bitcoin at ticker.Ask USD
        //        var order = new ExchangeOrderRequest
        //        {
        //            Amount = shares,
        //            IsBuy = true,
        //            Price = asset.Price,
        //            MarketSymbol = asset.Ticker
        //        };
        //        var result = await api.PlaceOrderAsync(order);
        //        logger.Info($"PlaceOrderAsync-Buy. {result.MarketSymbol} ${result.Price}.  {result.Result}. {result.OrderId}");
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Info($"PlaceOrderAsync-Buy. {ex.Message}");
        //    }
        //}

        /// <summary>
        /// You can call this method multiple times with no issues.
        /// </summary>
        private async void executeEnumerate()
        {
            // Get a list of markets based on USD for better liquidity.
            // remove all cryptos with 0 bid. dead coins.
            // remove all cryptos with volume less than 150K USD.
            // remove BTC, ETH.
            // remove all stable-coins.
            var coinsToRemove = new string[] { "USDC", "BUSD", "USDT", "DAI", "BTC", "ETH", "PAX"};
            var pairs = (await api.GetTickersAsync()).Where(p => p.Value.Volume.QuoteCurrency == "USD" &&
            !coinsToRemove.Contains(p.Value.Volume.BaseCurrency) &&
            p.Value.Bid > 0 && p.Value.Volume.QuoteCurrencyVolume > 150000m).OrderBy(k => k.Key).ToList();

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
                            this.Assets[i].Ask = foundTkr.Value.Ask;
                            this.Assets[i].Bid = foundTkr.Value.Bid;
                        }
                    }
                });
            }

            // Run this once to hydrate the screen. 
            await executeStart(null);

            // Now we want to run the timer on the hour XX:01 time.
            var currentTime = DateTime.Now;
            var minutesAway = 60 - currentTime.Minute + 1;

            // start the timer event.
            if (stateTimer != null)
            {
                stateTimer.Dispose();
            }

            stateTimer = new Timer(async(objState) => await executeStart(objState));
            stateTimer.Change(minutesAway * 60 * 1000, ONEHOUR_MS);
        }

        /// <summary>
        /// This gets run by the timer once per hour but trading only occurs at 
        /// 12AM, 8AM and 4PM based on the 8 hour candle.
        /// </summary>
        /// <returns></returns>
        private async Task executeStart(object objState)
        {
            this.LastUpdated = DateTime.Now;
            this.NextUpdate = this.LastUpdated.AddMilliseconds(ONEHOUR_MS);

            // some cleanup.
            ProgressPercentage = 0;
            int numOfTickers = 0;
            this.IsReady = false;

            var tickers = this.Assets.Select(p => p.Ticker).ToArray();
            numOfTickers = tickers.Count();

            // update wallet if live trading.
            decimal usdtAvail = 0m;
            Dictionary<string, decimal> wallet = new Dictionary<string, decimal>();
            if (isLiveTrading)
            {
                wallet = await api.GetAmountsAvailableToTradeAsync();
                if (wallet.ContainsKey("USD"))
                {
                    usdtAvail = wallet["USD"];
                }
            }

            // Get the data.
            for (int idx = 0; idx < numOfTickers; idx++)
            {
                var ticker = tickers[idx];
                // ok, this is a lazy way.  obviously you can just use a 2 or 4 hour candle to extrapolate data but just not worth the thinking...
                int periodSeconds = Convert.ToInt32(2 * 60 * 60); // Get 2 hour candles for sell signal
                var candlesFast = await api.GetCandlesAsync(ticker, periodSeconds, null, DateTime.Now, 250);
                periodSeconds = Convert.ToInt32(8 * 60 * 60); // Get 8 hour candles for buy signal
                var candlesSlow = await api.GetCandlesAsync(ticker, periodSeconds, null, DateTime.Now, 250);

                IList<IQuote> quotesFast = new List<IQuote>();
                IList<IQuote> quotesSlow = new List<IQuote>();
                candlesFast.ToList().ForEach(data =>
                {
                    quotesFast.Add(new Quote() { Open = data.OpenPrice, Close = data.ClosePrice, Low = data.LowPrice, High = data.HighPrice, Volume = Convert.ToDecimal(data.BaseCurrencyVolume), Date = data.Timestamp });
                });
                candlesSlow.ToList().ForEach(data =>
                {
                    quotesSlow.Add(new Quote() { Open = data.OpenPrice, Close = data.ClosePrice, Low = data.LowPrice, High = data.HighPrice, Volume = Convert.ToDecimal(data.BaseCurrencyVolume), Date = data.Timestamp });
                });


                var asset = this.Assets.First(a => a.Ticker == ticker);
                asset.updateQuotes(quotesFast, quotesSlow);

                // update the wallet size.
                if (wallet.ContainsKey(asset.BaseCurrency))
                {
                    asset.Amount = wallet[asset.BaseCurrency];
                }

                this.ProgressPercentage = Convert.ToInt32(((double)(idx + 1) / (double)numOfTickers) * 100.0);
                this.ProgressBarMessage = $"updating: {progressPercentage}%";
            }

            this.IsReady = true;

            // Buying can occur only when the time is a 12AM, 8AM and 4PM UTC because we are using the 8-hour candle.
            var currentUTC = DateTime.UtcNow;
            if (currentUTC.Hour == 0 || currentUTC.Hour == 8 || currentUTC.Hour == 16)
            {
                // Check for buy signal.
                // prevent BTC from being traded.
                var assetsToBuy = this.Assets.Where(a => (a.MacdSummary.CrossOverSignal == TrendEnum.Up && !a.HasTrade && a.Price > 0));
                foreach (var asset in assetsToBuy)
                {
                    // get the number of shares to buy.
                    var shares = RoundShares.GetRoundedShares(stakeSize, asset.Price);

                    if (isLiveTrading)
                    {
                        // live trading
                        if (usdtAvail >= balanceLowWaterMark)
                        {
                            try
                            {
                                var order = new ExchangeOrderRequest
                                {
                                    Amount = shares,
                                    IsBuy = true,
                                    Price = asset.Ask,
                                    MarketSymbol = asset.Ticker
                                };
                                var result = await api.PlaceOrderAsync(order);
                                logger.Info($"BUY: PlaceOrderAsync. {result.MarketSymbol} ${result.Price}.  {result.Result}. {result.OrderId}");

                                // reduce banksize
                                usdtAvail -= stakeSize;

                                asset.HasTrade = true;
                                asset.BuyPrice = asset.Price;
                            }
                            catch (Exception ex)
                            {
                                logger.Info($"PlaceOrderAsync-Buy. {ex.Message}");
                            }
                        }
                        else
                        {
                            logger.Info($"{isLiveTrading}. BUY {asset.Ticker} FAILED: out of money. {usdtAvail}");
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
            }

            // TODO: need to update sell condition to be more aggresive.
            // Sell using 2-hour MACD cross-over signal
            var assetsToSell = this.Assets.Where(a => a.HasTrade && a.MacdSummary.TrendSignalFast == TrendEnum.Down);
            foreach (var asset in assetsToSell)
            {
                if (isLiveTrading)
                {
                    // live trading.
                    // check to see if we indeed have any coins to sell.
                    var currency = asset.BaseCurrency;
                    if (wallet.ContainsKey(currency))
                    {
                        // the wallet key has the USDT stripped out. so that BATUSDT = BAT.
                        var amountAvail = wallet[currency]; // sell all.

                        try
                        {
                            var result = await api.PlaceOrderAsync(new ExchangeOrderRequest
                            {
                                Amount = amountAvail,
                                IsBuy = false,
                                Price = asset.Bid,
                                MarketSymbol = asset.Ticker
                            });

                            logger.Info($"PlaceOrderAsync-Sell. {result.MarketSymbol}, {result.OrderId}, {result.Result}");
                            asset.HasTrade = false;
                            asset.BuyPrice = 0;
                        }
                        catch (Exception ex)
                        {
                            logger.Info($"PlaceOrderAsync-Sell. {ex.Message}");
                        }
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

            // calculate some summary info.
            var trades = this.Assets.Where(a => a.HasTrade);
            trades.Select(t => t.UnrealizedPLPercentage).Sum();
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
