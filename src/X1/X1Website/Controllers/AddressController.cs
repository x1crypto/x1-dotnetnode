using System;
using System.Collections.Generic;
using Blockcore;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace X1Site.Web.Controllers
{
    
    public class AddressController : Controller
    {
        readonly ILogger<AddressController> logger;
        readonly IFullNode node;
        readonly IBlockStore blockStore;
        readonly ChainIndexer chainIndexer;

        public AddressController(ILogger<AddressController> logger, IFullNode node)
        {
            this.logger = logger;
            this.node = node;
            this.blockStore = node.Services.ServiceProvider.GetService(typeof(IBlockStore)) as IBlockStore;
            this.chainIndexer = node.Services.ServiceProvider.GetService(typeof(ChainIndexer)) as ChainIndexer;
        }

        [Route("address/{id}")]
        public IActionResult Index(string id)
        {
            try
            {
                id = id.Trim();

                this.ViewData["id"] = id;

                Block block = null;

                if (int.TryParse(id, out var height))
                {
                    var chainedHeader = this.chainIndexer.GetHeader(height);
                    if (chainedHeader != null)
                    {
                        block = this.blockStore.GetBlock(chainedHeader.HashBlock);
                        
                    }
                }

                if (uint256.TryParse(id, out var hashBlock))
                {
                    block = this.blockStore.GetBlock(hashBlock);
                }

                if (block != null)
                {
                    return View(block);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex.Message);
                
            }

            return NotFound();
        }
    }
}
