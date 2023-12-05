using System.Linq;
using Aricie;
using Aricie.DNN.UI.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace MyIA.Trading.Backtester
{
    /// <summary>
    /// That class serves defining simulation parameter for tradegies back testing. 
    /// A couple parameters are available to the user and a method runs the simulation given historical data
    /// </summary>
    [Serializable]
    public class SimulationInfo
    {

        public bool IncludeDepth { get; set; } 

        public bool FullTicker { get; set; } 

        /// <summary>
        /// Duration between each bot run. It should match the platform configuration for accurate results
        /// </summary>
        public STimeSpan BotPeriod {get;set;}

        /// <summary>
        /// Defines if a new custom strategy is to be used or the existing strategy instead
        /// </summary>
        public bool UseCustomStrategy { get; set; }


        public bool ShouldSerializeCustomStrategy()
        {
            return UseCustomStrategy;
        }

        /// <summary>
        /// The custom strategy to use if activated
        /// </summary>
        [ConditionalVisible("UseCustomStrategy", false, true)]
        public TradingStrategy CustomStrategy { get; set; }

        /// <summary>
        /// The date to start the simulation or the closest historical data if unavailable
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// The date to end the simulation or the closest historical data if unavailable
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Allows for much faster simulations by skipping suposedly useless bot runs 
        /// when the market does not move. Other parameters define the corresponding behavior.
        /// </summary>
        public bool FastSimulation { get; set; }
        
        /// <summary>
        /// The minimum number of bot runs without any new order getting issued before skipping starts 
        /// </summary>
        [ConditionalVisible("FastSimulation", false, true)]
        public decimal SkippedMinVoidRuns { get; set; }

        /// <summary>
        /// The maximum number of successive bot runs skipped 
        /// </summary>
        [ConditionalVisible("FastSimulation", false, true)]
        public decimal SkippedMaxRuns { get; set; }
       
        /// <summary>
        /// The maximum variation of the market since the last unskipped bot run to keep skipping bot runs
        /// </summary>
        [ConditionalVisible("FastSimulation", false, true)]
        public decimal SkippedVariationRate { get; set; }


        
        

        public SimulationInfo()
        {
            this.StartDate = DateTime.Now.Subtract(TimeSpan.FromDays(60)); ;
            this.EndDate = DateTime.Now;
            this.BotPeriod = new STimeSpan(TimeSpan.FromMinutes(5));
            this.CustomStrategy = new TradingStrategy();
            this.FastSimulation = true;
            this.SkippedVariationRate = 1m;
            this.SkippedMinVoidRuns = 2m;
            this.SkippedMaxRuns = 20m;
        }

        public TradingHistory RunSimulation(Wallet initialWallet, ITradingStrategy obStrategy, ExchangeInfo objExchange, IEnumerable<OrderTrade> exchangeHistory)
        {
            return SimulationInfo.RunSimulation(this, initialWallet, obStrategy, objExchange, exchangeHistory);
        }

        /// <summary>
        /// Static method that runs a simulation of a Market and bot runs for a given period, 
        /// with the parameters and historical data supplied as parameters
        /// </summary>
        /// <param name="objSimulation">An instance of the simulation info class with properties defining how to perform the simulation</param>
        /// <param name="initialWallet">The Wallet to use at the start of the simulation</param>
        /// <param name="obStrategy">The existing strategy to use if the simulation object does not specify a custom strategy</param>
        /// <param name="objExchange">An instance of the Exchange parameters</param>
        /// <param name="exchangeHistory">Historical data for the Exchange object</param>
        /// <returns>the trading history computed from the simulation with resulting balance, issued and executed orders</returns>
        public static TradingHistory RunSimulation(SimulationInfo objSimulation, Wallet initialWallet
            , ITradingStrategy obStrategy, ExchangeInfo objExchange, IEnumerable<OrderTrade> exchangeHistory)
        {
            var objSetup = new SimulationSetup() {Strategy = obStrategy, Walllet = initialWallet};
            var setups = new List<SimulationSetup>( new []{objSetup});
            return objSimulation.RunSimulations(setups, objExchange, exchangeHistory)[0];
        }

        public List<TradingHistory> RunSimulations(List<SimulationSetup> setups
            , ExchangeInfo objExchange, IEnumerable<OrderTrade> exchangeHistory)
        {
            return RunSimulations(this, setups, objExchange, exchangeHistory);
        }


        public static List<TradingHistory> RunSimulations(SimulationInfo objSimulation, List<SimulationSetup> setups
         , ExchangeInfo objExchange, IEnumerable<OrderTrade> exchangeHistory)
        {
            
            if (objSimulation.UseCustomStrategy)
            {
                foreach (var simulationSetup in setups)
                {
                    simulationSetup.Strategy = objSimulation.CustomStrategy;
                }
            }

            
            var objExchangeSimulator = new ExchangeSimulator()
            {
                Trades = exchangeHistory.ToList(),
                IncludeDepth = objSimulation.IncludeDepth,
                FullTicker = objSimulation.FullTicker
            };
            //var currentWallet = (Wallet)initialWallet.Clone();
            //var lastBotMarket = new MarketInfo(DateTime.MinValue);
            var lastBotTicker = 0m;
            var lastBotTime = DateTime.MinValue;
            MarketInfo objMarket = null;
            int nbEmptyRuns = 0;
            foreach (var historicTrade in objExchangeSimulator.Trades
                    .Where(objTrade => objTrade.Time > objSimulation.StartDate
                    && objTrade.Time < objSimulation.EndDate))
            {
                for (var setupIndex = 0; setupIndex < setups.Count; setupIndex++)
                {
                    var simulationSetup = setups[setupIndex];
                    var currentWallet = simulationSetup.Walllet;
                    if ((currentWallet.OrderedAsks.Count > 0 && historicTrade.Price > currentWallet.LowestAsk.Price)
                        || (currentWallet.OrderedBids.Count > 0 &&
                            historicTrade.Price < currentWallet.HighestBid.Price))
                    {
                        nbEmptyRuns = 0;
                        objMarket = objExchangeSimulator.GetMarket(historicTrade.Time);
                        var currentHistory = simulationSetup.TradingHistory;
                        objExchange.ExecuteOrders(objMarket, ref currentWallet, ref currentHistory);
                    }
                }


                if (historicTrade.Time.Subtract(lastBotTime) > objSimulation.BotPeriod.Value)
                {
                    foreach (var simulationSetup in setups)
                    {
                        var currentWallet = simulationSetup.Walllet;
                        
                        currentWallet.Time = historicTrade.Time;
                        bool isBigVariation =
                            Math.Abs((historicTrade.Price - lastBotTicker) / historicTrade.Price)
                            > objSimulation.SkippedVariationRate / 100;

                        if (!objSimulation.FastSimulation
                            || nbEmptyRuns < objSimulation.SkippedMinVoidRuns
                            || nbEmptyRuns >= objSimulation.SkippedMaxRuns
                            || isBigVariation)
                        {
                            if (objMarket == null || objMarket.Time<historicTrade.Time)
                            {
                                objMarket = objExchangeSimulator.GetMarket(historicTrade.Time);
                            }
                            var newOrders = simulationSetup.Strategy.ComputeNewOrders(currentWallet, objMarket, objExchange, simulationSetup.TradingHistory);
                            currentWallet.IntegrateOrders(newOrders.Orders.ToArray());
                            lastBotTicker = objMarket.Ticker.Last;
                            if (newOrders.Orders.Count == 0 && !isBigVariation && nbEmptyRuns < objSimulation.SkippedMaxRuns)
                            {
                                nbEmptyRuns++;
                            }
                            else
                            {
                                nbEmptyRuns = 0;
                            }
                        }
                        else
                        {
                            nbEmptyRuns++;
                        }
                        lastBotTime = historicTrade.Time;
                    }

                    
                }
            }
            return setups.Select(objSetup=>objSetup.TradingHistory).ToList();
        }


    }
}
