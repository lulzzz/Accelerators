namespace OpenAI_BlobProcessing.Models
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
