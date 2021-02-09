using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blockcore.Consensus.ScriptInfo;

namespace X1Site.Web.Models
{
    public class AddressModel
    {
        public Script ScriptPubKey { get; set; }

        public string Address { get; set; }

        public string Description { get; set; }
    }
}
