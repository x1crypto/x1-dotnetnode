using Blockcore.Networks;

namespace X1.X1Network
{
    public static class Networks
    {
        public static NetworksSelector X1
        {
            get
            {
                return new NetworksSelector(() => new X1Main(), () => new X1Test(), () => new X1RegTest());
            }
        }
    }
}