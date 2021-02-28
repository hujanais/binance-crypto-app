using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace crypto.Models
{
    class Trade : ViewModelBase
    {
        private decimal unrealizedPL;
        private decimal pl;

        public Trade()
        {
            BuySide = new Position { IsBuy = true };
            SellSide = new Position { IsBuy = false };
        }

        public Position BuySide { get; set; }
        public Position SellSide { get; set; }

        public decimal UnrealizedPL
        {
            get => unrealizedPL;
            set
            {
                this.unrealizedPL = value;
                this.RaisePropertyChanged(nameof(this.UnrealizedPL));
            }
        }

        public decimal PL
        {
            get => pl;
            set
            {
                this.pl = value;
                this.RaisePropertyChanged(nameof(this.PL));
            }        
        }
    }


    /// <summary>
    /// Describes a trade
    /// </summary>
    class Position : ViewModelBase
    {
        private bool hasPosition;
        private bool isBuy;
        private DateTime fillDate;
        private decimal fees;
        private decimal price;
        private decimal averagePrice;
        private string tradeId;
        private decimal amount;

        public bool IsBuy
        {
            get => isBuy;
            set
            {
                this.isBuy = value;
                this.RaisePropertyChanged(nameof(this.IsBuy));
            }
        }

        public DateTime FillDate
        {
            get => fillDate;
            set
            {
                this.fillDate = value;
                this.RaisePropertyChanged(nameof(this.FillDate));
            }
        }

        public decimal Fees
        {
            get => fees;
            set
            {
                this.fees = value;
                this.RaisePropertyChanged(nameof(this.Fees));
            }
        }

        public decimal Price
        {
            get => price;
            set
            {
                this.price = value;
                this.RaisePropertyChanged(nameof(this.Price));
            }

        }

        public decimal AvgPrice
        {
            get => averagePrice;
            set
            {
                this.averagePrice = value;
                this.RaisePropertyChanged(nameof(this.AvgPrice));
            }

        }

        public string TradeId
        {
            get => tradeId;
            set
            {
                this.tradeId = value;
                this.RaisePropertyChanged(nameof(this.TradeId));
            }

        }

        public decimal Amount
        {
            get => amount;
            set
            {
                this.amount = value;
                this.RaisePropertyChanged(nameof(this.Amount));
            }
        }

        public bool HasPosition
        {
            get => hasPosition;
            set => hasPosition = value;
        }
    }
}