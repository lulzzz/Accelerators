using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAI_BlobProcessing.Models
{
    public struct BlobContainers
    {
        public string Input { get; }
        public string Archived { get; }
        public string Processed { get; }
        public string Fault { get; }

        public BlobContainers(string input, string archived, string processed, string fault)
        {
            Input = input;
            Archived = archived;
            Processed = processed;
            Fault = fault;
        }
    }
}
