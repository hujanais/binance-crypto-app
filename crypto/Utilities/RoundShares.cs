using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace crypto.Utilities
{
    /// <summary>
    /// Round cryptos to buy.
    /// </summary>
    public abstract class RoundShares
    {        
        public static decimal GetRoundedShares(decimal stake, decimal price)
        {
            decimal numOfShares = stake / price;

            numOfShares = Math.Round(numOfShares, 1);   // round to 0.1 coins

            return numOfShares;
        }


    }
}
