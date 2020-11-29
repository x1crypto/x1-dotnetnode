using System;
using System.Linq;
using System.Threading.Tasks;
using Blockcore.Builder;
using Blockcore.Configuration;
using Blockcore.Features.NodeHost;
using Blockcore.Features.BlockStore;
using Blockcore.Features.ColdStaking;
using Blockcore.Features.Consensus;
using Blockcore.Features.Diagnostic;
using Blockcore.Features.MemoryPool;
using Blockcore.Features.Miner;
using Blockcore.Features.RPC;
using Blockcore.Networks.Xds;
using Blockcore.Utilities;
using NBitcoin.Protocol;

namespace StratisD
{
    public class Program
    {
#pragma warning disable IDE1006 // Naming Styles

        public static async Task Main(string[] args)
#pragma warning restore IDE1006 // Naming Styles
        {
            try
            {
                var nodeSettings = new NodeSettings(networksSelector: Networks.Xds, args: args);

                // extra support during network upgrade
                if (nodeSettings.Network is XdsMain && DateTime.UtcNow < new DateTime(2021,01,01).AddSeconds(-1))
                {
                    string[] extraAddnodes = { "46.101.168.197", "134.122.89.152", "161.35.156.96" };
                    var argList = args.ToList();
                    foreach (var ip in extraAddnodes)
                    {
                        argList.Add($"-addnode={ip}");
                        argList.Add($"-whitelist={ip}");
                    }
                    argList.Add("iprangefiltering=0");
                    nodeSettings =new NodeSettings(networksSelector: Networks.Xds, args: argList.ToArray());
                }

                IFullNodeBuilder nodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseBlockStore()
                        .UsePosConsensus()
                        .UseMempool()
                        .UseColdStakingWallet()
                        .AddPowPosMining()
                        .UseNodeHost()
                        .AddRPC()
                        .UseDiagnosticFeature();

                await nodeBuilder.Build().RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.ToString());
            }
        }
    }
}