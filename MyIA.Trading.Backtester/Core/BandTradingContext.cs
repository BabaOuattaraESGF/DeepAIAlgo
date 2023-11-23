using System;

namespace MyIA.Trading.Backtester
{
	public class BandTradingContext : TradingContext
	{
		public TradingStrategy Strategy
		{
			get
			{
				return (TradingStrategy)base.BaseStrategy;
			}
		}

		public BandTradingContext()
		{
		}

		public BandTradingContext(TradingContext objBaseContext) : base(objBaseContext.CurrentOrders, objBaseContext.NewOrders, objBaseContext.Market, objBaseContext.Exchange, objBaseContext.LastTrend, objBaseContext.BaseStrategy)
		{
		}
	}
}