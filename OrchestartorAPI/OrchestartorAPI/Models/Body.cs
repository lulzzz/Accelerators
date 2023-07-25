using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace OrchestartorAPI.Models
{
    [DataContract]
    internal class Body
    {
        [DataMember]
        public string guid { get; set; }
        [DataMember]
        public string question { get; set; }
    }
}
