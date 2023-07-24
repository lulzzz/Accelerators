using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrchestartorAPI.Models
{
    internal class RequestModel
    {
        public string question { get; set; }
        public string guid { get; set; }
        public string MetaPrompt { get; set; }
        // Environment Variables
        public string OpenAI_Key { get; set; }
        public string RedisConnectionString { get; set; }
        public string RedisApiKey { get; set; }
        public string RedisExpires { get; set; }
        public string CognitiveUrl { get; set; }
        public string CognitiveApiKey { get; set; }
        public string GptDeployment { get; set; }
        public string GptModelAlias { get; set; }
        public string GptEndpoint { get; set; }
        public string Temperature { get; set; }
        public string MaxTokens { get; set; }
        public string FrequencyPenalty { get; set; }
        public string PresencePenalty { get; set; }      
        public string TopP { get; set; }
        
        public string DavinciDeployment { get; set; }
        public string DavinciModelAlias { get; set; }
        public string DavinciEndpoint { get; set; }
        public string DavinciPrompt { get; set; }
        public string BlobConnectionString { get; set; }
        public string BlobContainer { get; set; }
        public string BlobName { get; set; }
        public string CitationsUrl { get; set; }


    }
}
