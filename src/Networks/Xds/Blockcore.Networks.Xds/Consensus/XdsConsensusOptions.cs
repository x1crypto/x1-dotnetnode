using Blockcore.Consensus;

namespace Blockcore.Networks.Xds.Consensus
{
    /// <inheritdoc />
    public class XdsConsensusOptions : PosConsensusOptions
    {
        /// <summary>
        /// The block height (inclusive), where the PosPowRatchet algorithm starts).
        /// </summary>
       const int PosPowRatchetStartHeightTestNet = 200;

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
    }
}