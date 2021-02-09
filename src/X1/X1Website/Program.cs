using System;
using System.Linq;
using Blockcore;
using Blockcore.Builder;
using Blockcore.Configuration;
using Blockcore.Features.BlockStore;
using Blockcore.Features.Consensus;
using Blockcore.Features.MemoryPool;
using Blockcore.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using X1.X1Network;

namespace X1Site.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Startup.FullnodeInstance = FullnodeMain(args);
            var _ = Startup.FullnodeInstance.RunAsync();
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });


       static IFullNode FullnodeMain(string[] args)
        {
            try
            {
                var nodeSettings = new NodeSettings(networksSelector: Networks.X1, args: args);

                // extra support during network upgrade
                if (nodeSettings.Network is X1Main && DateTime.UtcNow < new DateTime(2021, 03, 01).AddSeconds(-1)) // till end of Feb
                {
                    string[] extraAddnodes = { "46.101.168.197", "134.122.89.152", "161.35.156.96" };
                    var argList = args.ToList();
                    foreach (var ip in extraAddnodes)
                    {
                        argList.Add($"-addnode={ip}");
                        argList.Add($"-whitelist={ip}");
                    }
                    argList.Add("iprangefiltering=0");
                    nodeSettings = new NodeSettings(networksSelector: Networks.X1, args: argList.ToArray());
                }

                IFullNodeBuilder nodeBuilder = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool();

                return nodeBuilder.Build();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.ToString());
                return null;
            }
        }
    }
}
