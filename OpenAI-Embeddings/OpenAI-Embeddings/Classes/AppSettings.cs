using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAI_Embeddings.Classes
{
    internal class AppSettings
    {
        public bool IsEncrypted { get; set; }
        public Dictionary<string, string> Values { get; set; }
        public string BlobConnectionString { get; set; }
        public string BlobInputContainer { get; set; }
        public string BlobProcessedContainer { get; set; }
        public string BlobArchivedContainer { get; set; }
        public string BlobFaultContainer { get; set; }
    }
}
