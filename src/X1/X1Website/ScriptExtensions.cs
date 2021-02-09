using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Networks;
using NBitcoin.DataEncoders;
using X1Site.Web.Models;

namespace X1Site.Web
{
    public static class ScriptExtensions
    {
        public static AddressModel GetDisplay(this Script scriptPubKey, Network network)
        {
            if (scriptPubKey == null)
                return new AddressModel { Address = "n/a", Description = "Script is null", ScriptPubKey = null };

            if (scriptPubKey.Length == 0)
                return new AddressModel { Address = "n/a", Description = "Empty script", ScriptPubKey = scriptPubKey };

            byte[] raw = scriptPubKey.ToBytes();

            switch (scriptPubKey)
            {
                case var _ when raw.Length == 22 && raw[0] == 0 && raw[1] == 20:
                    var hash160 = raw.Skip(2).Take(20).ToArray();
                    return new AddressModel { Address = hash160.ToPubKeyHashAddress(network), Description = "P2WPKH address", ScriptPubKey = scriptPubKey };
                case var _ when raw.Length == 34 && raw[0] == 0 && raw[1] == 32:
                    var hash256 = raw.Skip(2).Take(32).ToArray();
                    return new AddressModel { Address = hash256.ToScriptAddress(network), Description = "P2WSH address", ScriptPubKey = scriptPubKey };
                case var _ when raw[0] == (byte)OpcodeType.OP_RETURN:
                    return new AddressModel { Address = "op_return", Description = $"{raw.Length - 1} bytes of data", ScriptPubKey = scriptPubKey };
                default:
                    return new AddressModel { Address = "n/a", Description = $"Unknown script", ScriptPubKey = scriptPubKey };
            }
        }

        public static string ToPubKeyHashAddress(this byte[] hash160, Network network)
        {
            return network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS].Encode(0, hash160);
        }

        public static string ToScriptAddress(this byte[] hash256, Network network)
        {
            return network.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS].Encode(0, hash256);
        }
    }
}
