using Blockcore.Consensus.TransactionInfo;

namespace X1.X1Network.Consensus
{
    public class X1Transaction : Transaction
    {
        public override bool IsProtocolTransaction()
        {
            return this.IsCoinBase || this.IsCoinStake;
        }
    }
}