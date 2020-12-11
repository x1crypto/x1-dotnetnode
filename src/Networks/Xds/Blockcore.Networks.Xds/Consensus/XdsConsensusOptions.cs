using System;
using System.Diagnostics;
using Blockcore.Consensus;
using Blockcore.Consensus.BlockInfo;
using Blockcore.Consensus.Chain;
using Blockcore.Features.Consensus;
using Blockcore.Utilities;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;

namespace Blockcore.Networks.Xds.Consensus
{
    /// <inheritdoc />
    public class XdsConsensusOptions : PosConsensusOptions
    {
        /// <summary>
        /// The block height (inclusive), where the PosPowRatchet algorithm starts).
        /// </summary>
       const int PosPowRatchetStartHeightTestNet = 200;

        /// <summary>
        /// The block height (inclusive), where the PosPowRatchet algorithm starts).
        /// </summary>
        const int PosPowRatchetTargetCalculationHeightTestNet = 575;

        // TODO: move this to IConsensus
        /// <summary>Time interval in minutes that is used in the retarget calculation.</summary>
        private const uint RetargetIntervalMinutes = 16;

        private readonly Network network;

        public XdsConsensusOptions(Network network)
        {
            this.network = network;
        }
       

        /// <inheritdoc />
        public override int GetStakeMinConfirmations(int height, Network network)
        {
            // StakeMinConfirmations must equal MaxReorgLength so that nobody can stake in isolation and then force a reorg
            return (int)network.Consensus.MaxReorgLength;
        }

        /// <inheritdoc />
        public override bool IsAlgorithmAllowed(bool isProofOfStake, int newBlockHeight)
        {
            if (this.network.NetworkType == NetworkType.Testnet)
            {
                if (newBlockHeight < PosPowRatchetStartHeightTestNet)
                    return true;

                bool isPosHeight = newBlockHeight % 2 == 0; // for XDS, even block heights must be Proof-of-Stake

                if (isProofOfStake && isPosHeight)
                    return true;

                if (!isProofOfStake && !isPosHeight)
                    return true;

                return false;
            }

            return true;
        }

        public override Target GetNextTargetRequired<T>(T chain, ChainedHeader chainedHeader, IConsensus consensus, bool proofOfStake, ILogger logger)
        {
            var stakeChain = chain as IStakeChain;

            Guard.NotNull(stakeChain, nameof(stakeChain));

            // Genesis block.
            if (chainedHeader == null)
            {
                logger.LogTrace("(-)[GENESIS]:'{0}'", consensus.PowLimit);
                return consensus.PowLimit;
            }

            // Find the last two blocks that correspond to the mining algo
            // (i.e if this is a POS block we need to find the last two POS blocks).
            BigInteger targetLimit = proofOfStake
                ? consensus.ProofOfStakeLimitV2
                : consensus.PowLimit.ToBigInteger();

            // First block, the newer one
            ChainedHeader lastPowPosBlock = GetLastPowPosChainedBlock(stakeChain, chainedHeader, proofOfStake);
            if (lastPowPosBlock.Previous == null)
            {
                var res = new Target(targetLimit);
                logger.LogTrace("(-)[FIRST_BLOCK]:'{0}'", res);
                return res;
            }

            // Second block, the older one.
            ChainedHeader prevLastPowPosBlock = GetLastPowPosChainedBlock(stakeChain, lastPowPosBlock.Previous, proofOfStake);
            if (prevLastPowPosBlock.Previous == null)
            {
                var res = new Target(targetLimit);
                logger.LogTrace("(-)[SECOND_BLOCK]:'{0}'", res);
                return res;
            }

            // This is used in tests to allow quickly mining blocks.
            if (!proofOfStake && consensus.PowNoRetargeting)
            {
                logger.LogTrace("(-)[NO_POW_RETARGET]:'{0}'", lastPowPosBlock.Header.Bits);
                return lastPowPosBlock.Header.Bits;
            }

            if (proofOfStake && consensus.PosNoRetargeting)
            {
                logger.LogTrace("(-)[NO_POS_RETARGET]:'{0}'", lastPowPosBlock.Header.Bits);
                return lastPowPosBlock.Header.Bits;
            }

            // The middle block, the block with the other algo in between frst and second
            ChainedHeader middleBlock = GetLastPowPosChainedBlock(stakeChain, chainedHeader, !proofOfStake);
            Debug.Assert(middleBlock != null, "can't be null, second block is older than middle block");
            Debug.Assert(lastPowPosBlock.Height == middleBlock.Height +1, "middle block height must be 1 less than first block");
            Debug.Assert(prevLastPowPosBlock.Height == middleBlock.Height - 1, "middle block height must be 1 more than second block");

            // the timestamp of the older block - this would normally be used
            var prevLastPowPosBlockTime = prevLastPowPosBlock.Header.Time; 
            var middleBlockTime = middleBlock.Header.Time;

            // the time in seconds the other algorithm has used, which is must be hidden
            var middleGapSeconds = middleBlockTime - prevLastPowPosBlockTime;

            // add the middleGapSeconds to the timestamp of the second block, to compensate the time it took to create the middle block
            var adjustedPrevLastPowPosBlockTime = prevLastPowPosBlockTime + middleGapSeconds;

            // pass in adjustedPrevLastPowPosBlockTime instead of the timestamp of the second block, and continue as normal
            Target finalTarget = this.CalculateRetarget(lastPowPosBlock.Header.Time, lastPowPosBlock.Header.Bits, adjustedPrevLastPowPosBlockTime, targetLimit, logger);

            return finalTarget;
        }

        /// <inheritdoc/>
        public Target CalculateRetarget(uint firstBlockTime, Target firstBlockTarget, uint secondBlockTime, BigInteger targetLimit, ILogger logger)
        {
            uint targetSpacing = (uint)this.network.Consensus.TargetSpacing.TotalSeconds;
            uint actualSpacing = firstBlockTime > secondBlockTime ? firstBlockTime - secondBlockTime : targetSpacing;

            if (actualSpacing > targetSpacing * 10)
                actualSpacing = targetSpacing * 10;

            uint targetTimespan = RetargetIntervalMinutes * 60;
            uint interval = targetTimespan / targetSpacing;

            BigInteger target = firstBlockTarget.ToBigInteger();

            long multiplyBy = (interval - 1) * targetSpacing + actualSpacing + actualSpacing;
            target = target.Multiply(BigInteger.ValueOf(multiplyBy));

            long divideBy = (interval + 1) * targetSpacing;
            target = target.Divide(BigInteger.ValueOf(divideBy));

            logger.LogDebug("The next target difficulty will be {0} times higher (easier to satisfy) than the previous target.", (double)multiplyBy / (double)divideBy);

            if ((target.CompareTo(BigInteger.Zero) <= 0) || (target.CompareTo(targetLimit) >= 1))
                target = targetLimit;

            var finalTarget = new Target(target);

            return finalTarget;
        }

        private ChainedHeader GetLastPowPosChainedBlock(IStakeChain stakeChain, ChainedHeader startChainedHeader, bool proofOfStake)
        {
            Guard.NotNull(stakeChain, nameof(stakeChain));
            Guard.Assert(startChainedHeader != null);

            BlockStake blockStake = stakeChain.Get(startChainedHeader.HashBlock);

            while ((startChainedHeader.Previous != null) && (blockStake.IsProofOfStake() != proofOfStake))
            {
                startChainedHeader = startChainedHeader.Previous;
                blockStake = stakeChain.Get(startChainedHeader.HashBlock);
            }

            return startChainedHeader;
        }

        public override bool UseCustomRetarget(int height)
        {
            if (this.network.NetworkType == NetworkType.Testnet)
            {
                if (height < PosPowRatchetTargetCalculationHeightTestNet)
                    return false;

                return true;

            }

            return false;
        }
    }
}