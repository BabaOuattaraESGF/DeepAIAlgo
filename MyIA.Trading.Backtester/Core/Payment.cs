using System;
using System.ComponentModel;
using System.Diagnostics;

namespace MyIA.Trading.Backtester
{
	[DefaultProperty("Label")]
	[Serializable]
	public class Payment
	{
		public decimal Amount
		{
			[DebuggerNonUserCode]
			get;
			[DebuggerNonUserCode]
			set;
		}

		public string Currency
		{
			[DebuggerNonUserCode]
			get;
			[DebuggerNonUserCode]
			set;
		}

		public string Label
		{
			[DebuggerNonUserCode]
			get;
			[DebuggerNonUserCode]
			set;
		}

		public DateTime Time
		{
			[DebuggerNonUserCode]
			get;
			[DebuggerNonUserCode]
			set;
		}

		public Payment()
		{
			this.Currency = "BTC";
		}
	}
}