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

        const int THIRTYMINUTES_MS = 30 * 60 * 1000;
        private ExchangeSharp.ExchangeBinanceUSAPI api;
        private IWebSocket socket;

        private int progressPercentage = 0;
        private DateTime lastUpdated;
        private DateTime nextUpdate;

        private Asset selectedAsset;

        private ChartValues<double> closeChartValues = new ChartValues<double>();
        private ChartValues<double> macdChartValues = new ChartValues<double>();
        private ChartValues<double> macdSignalChartValues = new ChartValues<double>();
        private ChartValues<double> macdHistogramChartValues = new ChartValues<double>();

        #endregion

        #region ICommands

        public ICommand EnumeratePairsCommand { get; private set; }

        #endregion

        #region Properties

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

        #endregion

        public MainViewModel()
        {
            // Initialize the api.
            api = new ExchangeSharp.ExchangeBinanceUSAPI();

            // ICommand binding
            this.EnumeratePairsCommand = new RelayCommand(this.executeEnumerate);

            // Pre-allocate memory
            this.Assets = new ObservableCollection<Asset>();
            this.SeriesCollection = new SeriesCollection();
            this.MacdCollection = new SeriesCollection();

            this.SeriesCollection.Add(new LineSeries() { Values = closeChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 2 });

            this.MacdCollection.Add(new LineSeries() { Values = macdChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });
            this.MacdCollection.Add(new LineSeries() { Values = macdSignalChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });
            this.MacdCollection.Add(new LineSeries() { Values = macdHistogramChartValues, ScalesYAt = 0, Fill = Brushes.Transparent, PointGeometrySize = 0 });
        }

        private async void executeEnumerate()
        {
            // Get a list of markets.
            var pairs = (await api.GetTickersAsync()).Where(p => p.Value.MarketSymbol.Contains("USDT") && !p.Value.MarketSymbol.Contains("USDTUSD")).Take(20).ToList();
            pairs.ForEach(pair => {
                this.Assets.Add(new Asset(pair.Value.MarketSymbol));
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

            // Run the timer event.
            var stateTimer = new Timer((stateObj) => executeStart(stateObj));
            stateTimer.Change(500, THIRTYMINUTES_MS);
        }

        private async void executeStart(Object stateInfo)
        {
            this.LastUpdated = DateTime.Now;
            this.NextUpdate = this.LastUpdated.AddMilliseconds(THIRTYMINUTES_MS);

            // some cleanup.
            ProgressPercentage = 0;
            int numOfTickers = 0;

            var tickers = this.Assets.Select(p => p.Ticker).ToArray();
            numOfTickers = tickers.Count();

            await Task.Factory.StartNew(async () => {
                for (int idx = 0; idx < numOfTickers; idx++) 
                {
                    var ticker = tickers[idx];
                    var candles = await api.GetCandlesAsync(ticker, 43200, null, DateTime.UtcNow, 240);
                    IList<IQuote> quotes = new List<IQuote>();
                    candles.ToList().ForEach(data =>
                    {
                        quotes.Add(new Quote() { Open = data.OpenPrice, Close = data.ClosePrice, Low = data.LowPrice, High = data.HighPrice, Volume = Convert.ToDecimal(data.BaseCurrencyVolume), Date = data.Timestamp });
                    });

                    var asset = this.Assets.First(a => a.Ticker == ticker);
                    asset.updateQuotes(quotes);

                    this.ProgressPercentage = Convert.ToInt32(((double)(idx+1) / (double)numOfTickers) * 100.0);
                }            
            });           
        }

        private void displayChart(Asset asset)
        {
            try
            {
                closeChartValues.Clear();
                macdChartValues.Clear();
                macdSignalChartValues.Clear();
                macdHistogramChartValues.Clear();
                closeChartValues.AddRange(asset.Candles.Select(p => (double)p.Close));
                macdChartValues.AddRange(asset.MacdChart.Select(p => (double)p.Macd.GetValueOrDefault(0)));
                macdSignalChartValues.AddRange(asset.MacdChart.Select(p => (double)p.Signal.GetValueOrDefault(0)));
                macdHistogramChartValues.AddRange(asset.MacdChart.Select(p => (double)p.Histogram.GetValueOrDefault(0)));
            } catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}
