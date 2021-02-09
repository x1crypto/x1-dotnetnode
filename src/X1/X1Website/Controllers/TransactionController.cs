using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Blockcore;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.Interfaces;
using Blockcore.Networks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.DataEncoders;
using X1Site.Web.Models;

namespace X1Site.Web.Controllers
{
    public enum TransactionType
    {
        Unknown = 0,
        PoW_Reward_Coinbase = 1,
        PoS_Reward = 2,
        Money = 3
    }

    public class TransactionModel
    {
        public Transaction Transaction { get; set; }

        public ChainedHeader ChainedHeader { get; set; }

        public TransactionType TransactionType
        {
            get
            {
                if (this.Transaction == null)
                    return TransactionType.Unknown;
                if (this.Transaction.IsCoinBase)
                    return TransactionType.PoW_Reward_Coinbase;
                if (this.Transaction.IsCoinStake)
                    return TransactionType.PoS_Reward;
                return TransactionType.Money;
            }
        }

        public string OriginalJson { get; set; }

        public long Confirmations { get; set; }
        public DateTime Time { get; set; }
        public string Blockhash { get; set; }
        public IList<TxInModel> TransactionIn { get; set; }
        public IList<TxOutModel> TransactionsOut { get; set; }
        public bool IsCoinBase { get; set; }
        public string Hex { get; set; }
        public Block Block { get; set; }
        public uint Size { get; set; }

        public decimal TotalOut
        {
            get
            {
                if (TransactionsOut == null) return 0;

                return TransactionsOut.Sum(x => x.Value);
            }
        }

        public decimal Fees
        {
            get
            {
                if (TransactionType == TransactionType.PoS_Reward || TransactionType == TransactionType.PoW_Reward_Coinbase)
                    return 0;

                return TransactionIn.Sum(x => x.PrevOutValue) - TransactionsOut.Sum(x => x.Value);
            }
        }

        public string GetTransactionTypeText()
        {
            switch (this.TransactionType)
            {
                case TransactionType.PoW_Reward_Coinbase:
                    return "Proof-of-Work Miner Reward";
                case TransactionType.PoS_Reward:
                    return "Proof-of-Stake Block Reward";
                case TransactionType.Money:
                    return "X1 Money Transfer";
                case TransactionType.Unknown:
                    return "Unknown";
                default:
                    return "Undefined";
            }
        }
    }

    public class TxInModel
    {

        /// <summary>
        /// Marks the first tx, The data in "coinbase" can be anything; it isn't used. 
        /// Bitcoin puts the current compact-format target and the arbitrary-precision "extraNonce" number there, which increments every time the Nonce field in the block header overflows.
        /// https://en.bitcoin.it/wiki/Transaction#general_format_.28inside_a_block.29_of_each_input_of_a_transaction_-_Txin
        /// </summary>
        public string Coinbase { get; set; }

        /// <summary>
        /// Normally 0xFFFFFFFF/4294967295; irrelevant unless transaction's lock_time is > 0.
        /// </summary>
        public uint Sequence { get; set; }

        public uint256 PrevOutTxId { get; set; }

        public uint PrevOutN { get; set; }

        public AddressModel PrevOutAddressModel { get; set; }

        /// <summary>
        /// This index is not from the spec, just for display purposes.
        /// </summary>
        public int Index { get; set; }
        public decimal PrevOutValue { get; set; }
        
    }

    public class TxOutModel
    {
        public int Index { get; set; }
        public decimal Value { get; set; }
        public AddressModel AddressModel { get; set; }
    }
    public class TransactionController : Controller
    {
        readonly ILogger<TransactionController> logger;
        readonly IFullNode node;
        readonly IBlockStore blockStore;
        readonly ChainIndexer chainIndexer;
        readonly IPooledTransaction pooledTransaction;

        public TransactionController(ILogger<TransactionController> logger, IFullNode node)
        {
            this.logger = logger;
            this.node = node;
            this.blockStore = node.Services.ServiceProvider.GetService(typeof(IBlockStore)) as IBlockStore;
            this.chainIndexer = node.Services.ServiceProvider.GetService(typeof(ChainIndexer)) as ChainIndexer;
            this.pooledTransaction = node.Services.ServiceProvider.GetService(typeof(IPooledTransaction)) as IPooledTransaction;
        }

        [Route("transaction/{id}")]
        public async Task<IActionResult> Index(string id)
        {
            try
            {
                id = id.Trim();

                this.ViewData["id"] = id;

                var txId = uint256.Parse(id);

                var tx = await GetTransactionAsync(txId);

                ChainedHeader chainedHeader = null;
                if (tx != null)
                {
                    chainedHeader = GetTransactionBlock(txId);
                }



                if (tx != null)
                {
                    var txModel = new TransactionModel
                    {
                        ChainedHeader = chainedHeader,
                        Transaction = tx,
                        TransactionIn = new List<TxInModel>(),
                        TransactionsOut = new List<TxOutModel>(),
                    };

                    for (var i = 0; i < tx.Inputs.Count; i++)
                    {
                        var txInModel = new TxInModel
                        {
                            Index = i,
                            Sequence = tx.Inputs[i].Sequence.Value, // todo
                            PrevOutTxId = tx.Inputs[i].PrevOut.Hash,
                            PrevOutN = tx.Inputs[i].PrevOut.N,
                        };

                        if (txInModel.PrevOutTxId != uint256.Zero)
                        {
                            var prevTx = await GetTransactionAsync(txInModel.PrevOutTxId);
                            txInModel.PrevOutValue = prevTx.Outputs[txInModel.PrevOutN].Value.ToDecimal(MoneyUnit.BTC);
                            txInModel.PrevOutAddressModel = prevTx.Outputs[txInModel.PrevOutN].ScriptPubKey.GetDisplay(this.node.Network);
                        }
                        else
                        {
                            txInModel.PrevOutAddressModel = ScriptExtensions.GetDisplay(null, this.node.Network);
                        }

                        txModel.TransactionIn.Add(txInModel);
                    }

                    for (var i = 0; i < tx.Outputs.Count; i++)
                    {
                        TxOut txOut = tx.Outputs[i];

                        var txOutModel = new TxOutModel
                        {
                            Value = txOut.Value.ToDecimal(MoneyUnit.BTC),
                            Index = i,
                            AddressModel = txOut.ScriptPubKey.GetDisplay(this.node.Network)
                        };
                        txModel.TransactionsOut.Add(txOutModel);
                    }
                    return View(txModel);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex.Message);

            }

            return NotFound();
        }

        async Task<Transaction> GetTransactionAsync(uint256 txId)
        {
            // Look for the transaction in the mempool, and if not found, look in the indexed transactions.
            var tx = await this.pooledTransaction.GetTransaction(txId).ConfigureAwait(false);
            if (tx == null)
                tx = this.blockStore.GetTransactionById(txId);

            return tx;
        }

        /// <summary>
        /// Retrieves the block that the transaction is in.
        /// </summary>
        /// <param name="trxid">The transaction id.</param>
        /// <returns>Returns the <see cref="ChainedHeader"/> that the transaction is in. Returns <c>null</c> if not found.</returns>
        private ChainedHeader GetTransactionBlock(uint256 trxid)
        {
            ChainedHeader block = null;

            uint256 blockid = this.blockStore?.GetBlockIdByTransactionId(trxid);
            if (blockid != null)
            {
                block = this.chainIndexer?.GetHeader(blockid);
            }

            return block;
        }

        //public async Task<Transaction> GetTransaction(string id)
        //{
        //    try
        //    {


        //        GetRawTransactionRpcModel tx = await RpcClient.GetRawTransactionAsync(id);
        //        if (tx == null)
        //            return null;

        //        TransactionType transactiontype = GetTransactionType(tx);

        //        var transaction = new Transaction
        //        {
        //            OriginalJson = tx.OriginalJson,
        //            TransactionType = transactiontype,
        //            Blockhash = tx.Blockhash,
        //            TransactionId = tx.Txid,
        //            Size = tx.Size,
        //            TransactionIn = new List<VIn>(),
        //            TransactionsOut = new List<Out>(),
        //            Time = tx.GetTime()
        //        };


        //        int index = 0;
        //        foreach (var rpcIn in tx.Vin)
        //        {
        //            var vIn = new VIn
        //            {
        //                Index = index,
        //                Coinbase = rpcIn.Coinbase,
        //                Sequence = rpcIn.Sequence,
        //                ScriptSigHex = rpcIn.ScriptSig?.Hex,
        //                AssetId = null,
        //                // pointer to previous tx/vout:
        //                PrevTxIdPointer = rpcIn.Txid,
        //                PrevVOutPointer = (int)rpcIn.Vout,
        //                // we'll try to fetch this id possible
        //                PrevVOutFetchedAddress = null,
        //                PrevVOutFetchedValue = 0
        //            };

        //            if (rpcIn.Txid != null)
        //            {
        //                // Retrieve the origin address by retrieving the previous transaction and extracting the receive address and value
        //                var previousTx = await RpcClient.GetRawTransactionAsync(rpcIn.Txid);
        //                if (previousTx != null)
        //                {
        //                    var n = rpcIn.Vout;
        //                    Debug.Assert(n == previousTx.Vout[n].N);
        //                    vIn.PrevVOutFetchedAddress = previousTx.Vout[n].ScriptPubKey.Addresses.First();
        //                    vIn.PrevVOutFetchedValue = previousTx.Vout[n].Value;
        //                }
        //            }
        //            transaction.TransactionIn.Add(vIn);
        //        }



        //        index = 0;
        //        foreach (var output in tx.Vout)
        //        {
        //            var @out = new Out
        //            {
        //                TransactionId = transaction.TransactionId,
        //                Value = output.Value,
        //                Quantity = output.N,
        //                AssetId = null,
        //                Index = index++,
        //            };

        //            if (output.ScriptPubKey.Addresses != null) // Satoshi 14.2
        //                @out.Address = output.ScriptPubKey.Addresses.FirstOrDefault();
        //            else
        //            {
        //                string hexScript = output.ScriptPubKey.Hex;

        //                if (!string.IsNullOrEmpty(hexScript))
        //                {
        //                    byte[] decodedScript = Encoders.Hex.DecodeData(hexScript);
        //                    Script script = new Script(decodedScript);
        //                    if (transactiontype == TransactionType.PoW_Reward_Coinbase && @out.Index == 0 || transactiontype == TransactionType.PoS_Reward && @out.Index == 1)
        //                    {
        //                        var network = Config.Network;
        //                        var bech32 = network.GetBech32Encoder(Bech32Type.WITNESS_PUBKEY_ADDRESS, false).Encode(0, script.ToBytes());
        //                        if (bech32 != null)
        //                        {
        //                            @out.Address = bech32;
        //                        }
        //                        else
        //                        {
        //                            @out.Address = script.ToString();
        //                        }
        //                    }
        //                    else
        //                    {
        //                        @out.Address = script.ToString();
        //                    }


        //                }
        //                else
        //                {
        //                    //Debug.Assert(output.ScriptPubKey.Type == NonStandardAddress);
        //                    @out.Address = output.ScriptPubKey.Type;
        //                }
        //            }
        //            transaction.TransactionsOut.Add(@out);
        //        }

        //        return transaction;
        //    }
        //    catch { }
        //    return null;
        //}


    }
}
