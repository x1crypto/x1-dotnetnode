using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blockcore;
using Blockcore.Base;
using Blockcore.Consensus.Chain;
using Blockcore.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace X1Site.Web.Controllers
{
    public enum EntitySearchResult
    {
        Transaction,
        Block,
        Address,
        NotFound
    }

    public class SearchController : Controller
    {
        readonly ILogger<SearchController> logger;
        readonly IFullNode node;
        readonly IBlockStore blockStore;
        readonly ChainIndexer chainIndexer;

        public SearchController(ILogger<SearchController> logger, IFullNode node)
        {
            this.logger = logger;
            this.node = node;
            this.blockStore = node.Services.ServiceProvider.GetService(typeof(IBlockStore)) as IBlockStore;
            this.chainIndexer = node.Services.ServiceProvider.GetService(typeof(ChainIndexer)) as ChainIndexer;
        }

        [Route("search")]
        public async Task<ActionResult> Index(string id)
        {
            try
            {
                var res = await SearchEntityById(id);

                switch (res)
                {
                    case EntitySearchResult.Block:
                        return RedirectToAction("Index", "Block", new { id = id });
                        //case EntitySearchResult.Transaction:
                        //    return RedirectToAction("Index", "Transaction", new { id = id });
                        //case EntitySearchResult.Address:
                        //    return RedirectToAction("Index", "Address", new { id = id });
                }

                return NotFound();
            }
            catch (Exception e)
            {
                return NotFound();
            }

        }

        public async Task<EntitySearchResult> SearchEntityById(string id)
        {
            try
            {
                id = id.Trim();

                if (int.TryParse(id, out var height))
                {
                    var chainedHeader = this.chainIndexer.GetHeader(height);
                    if (chainedHeader != null)
                    {
                        var block = this.blockStore.GetBlock(chainedHeader.HashBlock);
                        if (block != null)
                        {
                            return EntitySearchResult.Block;
                        }
                    }
                }

                if (uint256.TryParse(id, out var hashBlock))
                {
                    var block = this.blockStore.GetBlock(hashBlock);
                    if (block != null)
                    {
                        return EntitySearchResult.Block;
                    }
                }

                //if (id.Length == 64)
                //{
                //    var transaction = await _transactionService.GetTransaction(id);

                //    if (transaction != null)
                //    {
                //        return EntitySearchResult.Transaction;
                //    }
                //}

                //if (id.Length == 34 || id.Equals("OP_RETURN", StringComparison.OrdinalIgnoreCase))
                //{
                //    var address = await _addressService.GetAddress(id);

                //    if (address != null)
                //    {
                //        return EntitySearchResult.Address;
                //    }
                //}

            }
            catch (Exception ex)
            {
                this.logger.LogError(ex.Message);
            }

            return EntitySearchResult.NotFound;
        }
    }
}
