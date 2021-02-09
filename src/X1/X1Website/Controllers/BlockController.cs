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
    public class BlockModel
    {
        public uint256 Hash { get; set; }
        public int Height { get; set; }
        public DateTime Time { get; set; }
        public int Confirmations { get; set; }
        public double Difficulty { get; set; }
        public uint256 MerkleRoot { get; set; }
        public long Nonce { get; set; }
        public int TotalTransactions { get; set; }
        public uint256 PreviousBlock { get; set; }
        public uint256 NextBlock { get; set; }
        public IList<Transaction> Transactions { get; set; }
        public bool IsProofOfStake { get; set; }
        public string Chainwork { get; set; }
        public string Bits { get; set; }
        public string VersionHex { get; set; }
        public int Size { get; set; }
        public int StrippedSize { get; set; }
        public int Version { get; set; }
        public TimeSpan TimeAgo { get; set; }


        // UI
        public int Count { get; set; }
        public int Start { get; set; }
        public int CurrentPage { get; set; }
        public int Max { get; set; }
    }
    public class BlockController : Controller
    {
        readonly ILogger<BlockController> logger;
        readonly IFullNode node;
        readonly IBlockStore blockStore;
        readonly ChainIndexer chainIndexer;

        public BlockController(ILogger<BlockController> logger, IFullNode node)
        {
            this.logger = logger;
            this.node = node;
            this.blockStore = node.Services.ServiceProvider.GetService(typeof(IBlockStore)) as IBlockStore;
            this.chainIndexer = node.Services.ServiceProvider.GetService(typeof(ChainIndexer)) as ChainIndexer;
        }

        [Route("block/{id}")]
        public IActionResult Index(string id)
        {
            try
            {
                id = id.Trim();

                this.ViewData["id"] = id;

                Block block = null;
                ChainedHeader chainedHeader = null;

                if (int.TryParse(id, out var height))
                {
                    chainedHeader = this.chainIndexer.GetHeader(height);
                    if (chainedHeader != null)
                    {
                        block = this.blockStore.GetBlock(chainedHeader.HashBlock);
                        
                    }
                }

                if (uint256.TryParse(id, out var hashBlock))
                {
                   
                    block = this.blockStore.GetBlock(hashBlock);
                    chainedHeader = this.chainIndexer.GetHeader(hashBlock);
                }

                if (block != null && chainedHeader != null)
                {
                    var model = new BlockModel
                    {
                        Height = chainedHeader.Height,
                        Difficulty = block.Header.Bits.Difficulty,
                        Bits = block.Header.Bits.ToCompact().ToString("x8"),
                        Chainwork = chainedHeader.ChainWork.ToString(),
                        Confirmations = this.chainIndexer.Tip.Height - chainedHeader.Height + 1,
                        TimeAgo = DateTime.UtcNow - block.Header.BlockTime,
                        Hash = chainedHeader.HashBlock,
                        MerkleRoot = block.Header.HashMerkleRoot,
                        Time = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time).UtcDateTime,
                        TotalTransactions = block.Transactions.Count,
                        Transactions = block.Transactions,
                        IsProofOfStake = block.Transactions.Count > 1 && block.Transactions[1].IsCoinStake,
                        NextBlock = this.chainIndexer.GetHeader(chainedHeader.Height + 1)?.HashBlock,
                        Version = block.Header.Version,
                        Nonce = block.Header.Nonce,
                        VersionHex = block.Header.Version.ToString("x8"),
                        Size = block.GetSerializedSize(this.node.Network.Consensus.ConsensusFactory,
                            SerializationType.Network),
                        PreviousBlock = chainedHeader.Previous.HashBlock

                    };

                    model.Max = model.Transactions.Count;
                        

                        
                    return View(model);
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
