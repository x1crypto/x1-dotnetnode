using System.Diagnostics;
using Blockcore;
using Blockcore.Base;
using Blockcore.Consensus.Chain;
using Blockcore.Networks;
using Blockcore.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using X1Site.Web.Models;

namespace X1Site.Web.Controllers
{
    public class HomeViewModel
    {
        public Network Network;
        public ChainedHeader Tip;

    }

    public class HomeController : Controller
    {
        readonly ILogger<HomeController> logger;
        readonly IFullNode node;
        readonly IChainState chainState;

        public HomeController(ILogger<HomeController> logger, IFullNode node)
        {
            this.logger = logger;
            this.node = node;
            this.chainState = node.Services.ServiceProvider.GetService(typeof(IChainState)) as IChainState;
        }

        public IActionResult Index()
        {
            var model = new HomeViewModel { Tip = this.chainState.ConsensusTip, Network = this.node.Network };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
