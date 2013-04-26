using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuantBox.Helper.CTP
{
    public class Tick
    {
        public Tick()
        {
        }

        public Tick(string Symbol,
            double Price,int Size,
            double Bid,int BidSize,
            double Ask,int AskSize)
        {
            this.Symbol = Symbol;
            this.Price = Price;
            this.Bid = Bid;
            this.BidSize = BidSize;
            this.Ask = Ask;
            this.AskSize = AskSize;
        }

        public string Symbol;
        public double Price;
        public int Size;
        public double Bid;
        public int BidSize;
        public double Ask;
        public int AskSize;
    }
}
