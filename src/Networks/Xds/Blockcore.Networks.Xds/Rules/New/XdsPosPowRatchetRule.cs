using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Rules;
using Blockcore.Consensus.ScriptInfo;
using Microsoft.Extensions.Logging;
using Blockcore.Features.Consensus.Rules.CommonRules;

namespace Blockcore.Networks.Xds.Rules.New
{
    public class XdsPosPowRatchetRule : PartialValidationConsensusRule
    {
        /// <summary>
        /// The block height (inclusive), where the PosPowRatchet algorithm starts).
        /// </summary>
        public const int PosPowRatchetStartHeight = 130;

        public override Task RunAsync(RuleContext context)
        {
            if (context.SkipValidation)
                return Task.CompletedTask;

            // Check for consistency
            var newHeight = GetHeightOfBlockToValidateSafe(context);

            // are we there?
            if (newHeight < PosPowRatchetStartHeight)
                return Task.CompletedTask;

            bool isProofOfStake = BlockStake.IsProofOfStake(context.ValidationContext.BlockToValidate);

            bool isEvenHeight = newHeight % 2 == 0;

            if (isEvenHeight && isProofOfStake)     // even block heights must be Proof-of-Stake
                return Task.CompletedTask;          // ok, Proof-of-Stake

            if (!isEvenHeight && !isProofOfStake)   // odd block heights must be Proof-of-Work
                return Task.CompletedTask;          // ok, Proof-of-Work

            // ohh no!
            this.Logger.LogTrace("(-)[BAD-POS-POW-RATCHET-SEQUENCE]");
            XdsConsensusErrors.BadPosPowRatchetSequence.Throw();

            return Task.CompletedTask;
        }

        /// <summary>
        /// From <see cref="CoinbaseHeightRule"/>. Very safe way to determine the true
        /// height of the block being checked.
        /// </summary>
        /// <returns>The height in the chain of the block being checked.</returns>
        int GetHeightOfBlockToValidateSafe(RuleContext context)
        {
            int newHeight = context.ValidationContext.ChainedHeaderToValidate.Height;
            Block block = context.ValidationContext.BlockToValidate;

            var expect = new Script(Op.GetPushOp(newHeight));
            Script actual = block.Transactions[0].Inputs[0].ScriptSig;
            if (!this.StartWith(actual.ToBytes(true), expect.ToBytes(true)))
            {
                this.Logger.LogTrace("(-)[BAD_COINBASE_HEIGHT]");
                ConsensusErrors.BadCoinbaseHeight.Throw();
            }

            return newHeight;
        }

        /// <summary>
        /// Checks if first <paramref name="subset.Lenght"/> entries are equal between two arrays.
        /// </summary>
        /// <param name="bytes">Main array.</param>
        /// <param name="subset">Subset array.</param>
        /// <returns><c>true</c> if <paramref name="subset.Lenght"/> entries are equal between two arrays. Otherwise <c>false</c>.</returns>
        private bool StartWith(byte[] bytes, byte[] subset)
        {
            if (bytes.Length < subset.Length)
                return false;

            for (int i = 0; i < subset.Length; i++)
            {
                if (subset[i] != bytes[i])
                    return false;
            }

            return true;
        }
    }
}
