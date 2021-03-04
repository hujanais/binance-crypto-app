using crypto.Models;
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

        private Asset selectedAsset;
        private double selectedAssetTickTime;
        private double selectedTickTime;

        private ChartValues<double> closeChartValues = new ChartValues<double>();
        private ChartValues<double> ema7ChartValues = new ChartValues<double>();
        private ChartValues<double> ema25ChartValues = new ChartValues<double>();
        private ChartValues<double> ema99ChartValues = new ChartValues<double>();

        private ChartValues<double> macdChartValues = new ChartValues<double>();
        private ChartValues<double> macdSignalChartValues = new ChartValues<double>();
        private ChartValues<double> macdHistogramChartValues = new ChartValues<double>();

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
        public bool IsReady { 
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

            this.SeriesCollection.Add(new LineSeries() { Values = closeChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 2 });
            this.SeriesCollection.Add(new LineSeries() { Values = ema7ChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });
            this.SeriesCollection.Add(new LineSeries() { Values = ema25ChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });
            this.SeriesCollection.Add(new LineSeries() { Values = ema99ChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });

            this.MacdCollection.Add(new LineSeries() { Values = macdChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });
            this.MacdCollection.Add(new LineSeries() { Values = macdSignalChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });
            this.MacdCollection.Add(new LineSeries() { Values = macdHistogramChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });

            this.TickTimes = new ObservableCollection<double> { 0.25, 0.5, 1, 2, 4, 8, 12, 24 };
            this.SelectedTickTime = 12;
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
                if (this.Assets.FirstOrDefault(a => a.Ticker == pair.Value.MarketSymbol) == null) {
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
                closeChartValues.Clear();
                ema7ChartValues.Clear();
                ema25ChartValues.Clear();
                ema99ChartValues.Clear();
                macdChartValues.Clear();
                macdSignalChartValues.Clear();
                macdHistogramChartValues.Clear();
                closeChartValues.AddRange(asset.Candles.Select(p => (double)p.Close));
                ema7ChartValues.AddRange(asset.EmaSummary.EMA7.Select(p => (double)p.Ema.GetValueOrDefault(0)));
                ema25ChartValues.AddRange(asset.EmaSummary.EMA25.Select(p => (double)p.Ema.GetValueOrDefault(0)));
                ema99ChartValues.AddRange(asset.EmaSummary.EMA99.Select(p => (double)p.Ema.GetValueOrDefault(0)));

                macdChartValues.AddRange(asset.MacdChart.Select(p => (double)p.Macd.GetValueOrDefault(0)));
                macdSignalChartValues.AddRange(asset.MacdChart.Select(p => (double)p.Signal.GetValueOrDefault(0)));
                macdHistogramChartValues.AddRange(asset.MacdChart.Select(p => (double)p.Histogram.GetValueOrDefault(0)));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
