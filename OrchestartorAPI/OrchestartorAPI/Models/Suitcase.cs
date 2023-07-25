using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OrchestartorAPI.Models
{
    internal class Suitcase
    {
        public string History { get; set; }
        public string QueryFilter { get; set; }
        public string guid { get; set; }
        public string Ask { get; set; }
        public string MetaPrompt { get; set; }
        public string Contents { get; set; }
        public string Citations { get; set; }
        // Environment Variables
        public string OpenAI_Key { get; set; }
        public string RedisConnectionString { get; set; }
        public string RedisApiKey { get; set; }
        public int RedisExpires { get; set; }
        public string CognitiveUrl { get; set; }
        public string CognitiveApiKey { get; set; }
        public string GptDeployment { get; set; }
        public string GptModelAlias { get; set; }
        public string GptEndpoint { get; set; }
        public double Temperature { get; set; }
        public int MaxTokens { get; set; }
        public double FrequencyPenalty { get; set; }
        public double PresencePenalty { get; set; }       
        public double TopP { get; set; }     
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
