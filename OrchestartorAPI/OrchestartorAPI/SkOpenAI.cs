using System.Net;
using System.Reflection;
using System.Text;
using Grpc.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Orchestration;
using Newtonsoft.Json;
using OrchestartorAPI.Models;
using OrchestartorAPI.skills.CognitiveSearchSkill;
using OrchestartorAPI.skills.CreatePromptSkill;
using OrchestartorAPI.skills.QueryFilteredSkill;
using StackExchange.Redis;
using Azure.Storage.Blobs;
using System.IO;
using Azure.Storage.Blobs.Models;
using OrchestartorAPI.skills.VectorSearchSkill;
using Azure.Core;
using Azure;
using System.Threading.Tasks;

namespace OrchestartorAPI
{
    public class SkOpenAI
    {
        private readonly ILogger _logger;
        private string _cache = string.Empty;
        public SkOpenAI(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SkOpenAI>();
        }

        [Function("openAI")]
        public async Task<string> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            try
            {

                Log(_logger, "Semantic Kernel Chat App");

                var requestParams = JsonConvert.DeserializeObject<RequestModel>(await ReadBodyAsStringAsync(req.Body));

                Log(_logger, "Request Params: " + requestParams);
                var bag = new Suitcase() { Ask = requestParams.question };
                SetEnvironmentVariables(bag, requestParams);

                if (bag.Ask.Contains(@"/admin"))
                {
                    _logger.LogInformation("Admin Mode");
                    AdminMode(bag);
                }

                var myKernel = GetGpt4Kernel(bag);

                ConnectionMultiplexer redisConnection = GetRedisConnection(bag);
                IDatabase redisDb = redisConnection.GetDatabase();

                await GetMessagesFromCache(redisDb, requestParams, bag);
                bag.QueryFilter = await GetQueryString(myKernel, redisDb, requestParams, bag);
                Log(_logger, bag.QueryFilter);

                var myCognitiveSkill = myKernel.ImportSkill(new CognitiveSearchSkill(), "CognitiveSearchSkill");
                var myVectorSkill = myKernel.ImportSkill(new VectorSearchSkill(), "VectorSearchSkill");
                var myCreatePromptSkill = myKernel.ImportSkill(new CreatePromptSkill(), "CreatePrompt");

                bag = JsonConvert.DeserializeObject<Suitcase>((await myKernel.RunAsync(
                                                                     new ContextVariables(JsonConvert.SerializeObject(bag)),
                                                                     myVectorSkill["ProcessQuery"],
                                                                     //myCognitiveSkill["ProcessQuery"],
                                                                     myCreatePromptSkill["CreatePrompt"])).ToString());

                //var planner = new SequentialPlanner(myKernel);
                //var plan = await planner.CreatePlanAsync("Is the data current?");

                //// Execute the plan
                //var result = await plan.InvokeAsync();

                //Console.WriteLine("Plan results:");
                //Console.WriteLine(result.Result);



                var reply = await GetChat(myKernel, bag);

                var response = req.CreateResponse(HttpStatusCode.OK);

                if (reply.StartsWith("I'm sorry"))
                {
                    bag.Citations = string.Empty;
                }

                response.WriteString(reply + " " + bag.Citations);
                await UpdateCache(redisDb, requestParams, bag);
                //await UpdateLog(bag, requestParams);
                var d = new ResultData() { Data = reply + " " + bag.Citations };
                return JsonConvert.SerializeObject(d);

            }
            catch (Exception ex)
            {
                Log(_logger, "Exception: " + ex.Message);
                return null;

            }
        }

        private void AdminMode(Suitcase bag)
        {
            string connectionString = bag.BlobConnectionString;
            string containerName = bag.BlobContainer;
            string blobName = bag.BlobName;

            string documentContent = RetrieveTextDocument(connectionString, containerName, blobName);
            string updatedContent = "Jimbo";

            UpdateAndUploadTextFile(connectionString, containerName, blobName, updatedContent);
        }

        private void SetEnvironmentVariables(Suitcase bag, RequestModel requestParams)
        {
            // If not provided by input, take from environment variable

            if (requestParams.OpenAI_Key != null && requestParams.OpenAI_Key.Trim() != string.Empty)
            {
                bag.OpenAI_Key = requestParams.OpenAI_Key.Trim();
            }
            else
            {
                bag.OpenAI_Key = Environment.GetEnvironmentVariable("OpenAIKey");
            }

            if (requestParams.RedisConnectionString != null && requestParams.RedisConnectionString.Trim() != string.Empty)
            {
                bag.RedisConnectionString = requestParams.RedisConnectionString;
            }
            else
            {
                bag.RedisConnectionString = Environment.GetEnvironmentVariable("RedisConnectionString");
            }

            if (requestParams.RedisApiKey != null && requestParams.RedisApiKey.Trim() != string.Empty)
            {
                bag.RedisApiKey = requestParams.RedisApiKey;
            }
            else
            {
                bag.RedisApiKey = Environment.GetEnvironmentVariable("redis-api-key");
            }

            if (requestParams.CognitiveApiKey != null && requestParams.CognitiveApiKey.Trim() != string.Empty)
            {
                bag.CognitiveApiKey = requestParams.CognitiveApiKey;
                Environment.SetEnvironmentVariable("CognitiveApiKey", requestParams.CognitiveApiKey);
            }
            else
            {
                bag.CognitiveApiKey = Environment.GetEnvironmentVariable("CognitiveApiKey");
            }

            if (requestParams.RedisExpires != null && requestParams.RedisExpires.Trim() != string.Empty)
            {
                try
                {
                    bag.RedisExpires = Convert.ToInt32(requestParams.RedisExpires.Trim());
                }
                catch (Exception)
                {

                    bag.RedisExpires = Convert.ToInt32(Environment.GetEnvironmentVariable("RedisExpires").Trim());
                }               
            }
            else
            {
                bag.RedisExpires = Convert.ToInt32(Environment.GetEnvironmentVariable("RedisExpires").Trim());
            }

            if (requestParams.CognitiveUrl != null && requestParams.CognitiveUrl.Trim() != string.Empty)
            {
                bag.CognitiveUrl = requestParams.CognitiveUrl;
                Environment.SetEnvironmentVariable("CognitiveUrl", requestParams.CognitiveUrl);
            }
            else
            {
                bag.CognitiveUrl = Environment.GetEnvironmentVariable("CognitiveUrl");
                
            }

            if (requestParams.GptDeployment != null && requestParams.GptDeployment.Trim() != string.Empty)
            {
                bag.GptDeployment = requestParams.GptDeployment;
            }
            else
            {
                bag.GptDeployment = Environment.GetEnvironmentVariable("GptDeployment");
            }

            if (requestParams.GptModelAlias != null && requestParams.GptModelAlias.Trim() != string.Empty)
            {
                bag.GptModelAlias = requestParams.GptModelAlias;
            }
            else
            {
                bag.GptModelAlias = Environment.GetEnvironmentVariable("GptModelAlias");
            }

            if (requestParams.GptEndpoint != null && requestParams.GptEndpoint.Trim() != string.Empty)
            {
                bag.GptEndpoint = requestParams.GptEndpoint;
            }
            else
            {
                bag.GptEndpoint = Environment.GetEnvironmentVariable("GptEndpoint");
            }

            if (requestParams.DavinciPrompt != null && requestParams.DavinciPrompt.Trim() != string.Empty)
            {
                bag.DavinciPrompt = requestParams.DavinciPrompt;
                Environment.SetEnvironmentVariable("DavinciPrompt", requestParams.DavinciPrompt);
            }
            else
            {
                bag.DavinciPrompt = Environment.GetEnvironmentVariable("DavinciPrompt");
            }

            if (requestParams.DavinciModelAlias != null && requestParams.DavinciModelAlias.Trim() != string.Empty)
            {
                bag.DavinciModelAlias = requestParams.DavinciModelAlias;
                Environment.SetEnvironmentVariable("DavinciModelAlias", requestParams.DavinciModelAlias);
            }
            else
            {
                bag.DavinciModelAlias = Environment.GetEnvironmentVariable("DavinciModelAlias");
            }

            if (requestParams.DavinciDeployment != null && requestParams.DavinciDeployment.Trim() != string.Empty)
            {
                bag.DavinciDeployment = requestParams.DavinciDeployment;
                Environment.SetEnvironmentVariable("DavinciDeployment", requestParams.DavinciDeployment);
            }
            else
            {
                bag.DavinciDeployment = Environment.GetEnvironmentVariable("DavinciDeployment");
            }
            if (requestParams.DavinciEndpoint != null && requestParams.DavinciEndpoint.Trim() != string.Empty)
            {
                bag.DavinciEndpoint = requestParams.DavinciEndpoint;
                Environment.SetEnvironmentVariable("DavinciEndpoint", requestParams.DavinciEndpoint);
            }
            else
            {
                bag.DavinciEndpoint = Environment.GetEnvironmentVariable("DavinciEndpoint");
            }

            if (requestParams.MetaPrompt != null && requestParams.MetaPrompt.Trim() != string.Empty)
            {
                bag.MetaPrompt = requestParams.MetaPrompt;
            }
            else
            {
                bag.MetaPrompt = Environment.GetEnvironmentVariable("MetaPrompt");
            }

            if (requestParams.BlobConnectionString != null && requestParams.BlobConnectionString.Trim() != string.Empty)
            {
                bag.BlobConnectionString = requestParams.BlobConnectionString;
            }
            else
            {
                bag.BlobConnectionString = Environment.GetEnvironmentVariable("BlobConnectionString");
            }
            if (requestParams.BlobContainer != null && requestParams.BlobContainer.Trim() != string.Empty)
            {
                bag.BlobContainer = requestParams.BlobContainer;
            }
            else
            {
                bag.BlobContainer = Environment.GetEnvironmentVariable("BlobContainer");
            }
            if (requestParams.BlobName != null && requestParams.BlobName.Trim() != string.Empty)
            {
                bag.BlobName = requestParams.BlobName;
            }
            else
            {
                bag.BlobName = Environment.GetEnvironmentVariable("BlobName");
            }



            if (requestParams.TopP != null && requestParams.TopP.Trim() != string.Empty)
            {
                try
                {
                    bag.TopP = Convert.ToDouble(requestParams.TopP.Trim());
                }
                catch (Exception)
                {

                    bag.TopP = Convert.ToDouble(Environment.GetEnvironmentVariable("TopP").Trim());
                }
               
            }
            else
            {
                bag.TopP = Convert.ToDouble(Environment.GetEnvironmentVariable("TopP").Trim());
            }

            if (requestParams.MaxTokens != null && requestParams.MaxTokens.Trim() != string.Empty && requestParams.MaxTokens.Trim() != "0")
            {
                try
                {
                    bag.MaxTokens = Convert.ToInt32(requestParams.MaxTokens.Trim());
                }
                catch (Exception)
                {

                    bag.MaxTokens = Convert.ToInt32(Environment.GetEnvironmentVariable("MaxTokens").Trim());
                }

            }
            else
            {
                bag.MaxTokens = Convert.ToInt32(Environment.GetEnvironmentVariable("MaxTokens").Trim());
            }

            if (requestParams.PresencePenalty != null && requestParams.PresencePenalty.Trim() != string.Empty)
            {
                try
                {
                    bag.PresencePenalty = Convert.ToDouble(requestParams.PresencePenalty.Trim());
                }
                catch (Exception)
                {

                    bag.PresencePenalty = Convert.ToDouble(Environment.GetEnvironmentVariable("PresencePenalty").Trim());
                }

            }
            else
            {
                bag.PresencePenalty = Convert.ToDouble(Environment.GetEnvironmentVariable("PresencePenalty").Trim());
            }

            if (requestParams.FrequencyPenalty != null && requestParams.FrequencyPenalty.Trim() != string.Empty)
            {
                try
                {
                    bag.FrequencyPenalty = Convert.ToDouble(requestParams.FrequencyPenalty.Trim());
                }
                catch (Exception)
                {

                    bag.FrequencyPenalty = Convert.ToDouble(Environment.GetEnvironmentVariable("FrequencyPenalty").Trim());
                }

            }
            else
            {
                bag.FrequencyPenalty = Convert.ToDouble(Environment.GetEnvironmentVariable("FrequencyPenalty").Trim());
            }

            if (requestParams.Temperature != null && requestParams.Temperature.Trim() != string.Empty)
            {
                try
                {
                    bag.Temperature = Convert.ToDouble(requestParams.Temperature.Trim());
                }
                catch (Exception)
                {

                    bag.Temperature = Convert.ToDouble(Environment.GetEnvironmentVariable("Temperature").Trim());
                }

            }
            else
            {
                bag.Temperature = Convert.ToDouble(Environment.GetEnvironmentVariable("Temperature").Trim());
            }

            //CitationsUrl
            if (requestParams.CitationsUrl != null && requestParams.CitationsUrl.Trim() != string.Empty)
            {
                bag.CitationsUrl = requestParams.CitationsUrl;
            }
            else
            {
                bag.CitationsUrl = Environment.GetEnvironmentVariable("CitationsUrl");
            }

        }

        private async Task<string> GetChat(IKernel myKernel, Suitcase bag)
        {
            _logger.LogInformation("In GetChat");

            IChatCompletion chatGPT = myKernel.GetService<IChatCompletion>();
          
            var chat = (OpenAIChatHistory)chatGPT.CreateNewChat(string.Empty);
            chat.AddUserMessage(bag.MetaPrompt += "'Assistant' : ''");
            var reply = await chatGPT.GenerateMessageAsync(chat, new ChatRequestSettings() { Temperature = bag.Temperature, MaxTokens=bag.MaxTokens, FrequencyPenalty=bag.FrequencyPenalty, PresencePenalty=bag.PresencePenalty, TopP=bag.TopP });
            bag.History += "'Assistant' : '" + reply + "' " + "''";
            return reply;

        }
        private async Task<string> GetQueryString(IKernel myKernel, IDatabase redisDb, RequestModel requestParams, Suitcase bag)
        {
            var myDavinciSkill = myKernel.ImportSkill(new QueryFilteredSkill(), "DavinciQueryGeneratorSkill");
            
            var myDavinciContext = new ContextVariables();
            myDavinciContext.Set("Input", GenreateQueryContext(requestParams, bag));
            myDavinciContext.Set("prompt", bag.DavinciPrompt);

            var query = await myKernel.RunAsync(myDavinciContext, myDavinciSkill["GetQuery"]);
            return query.ToString();

        }

        private static IKernel GetGpt4Kernel(Suitcase bag)
        {

            #region Azure Chat Completion Service Values.....
            var modelAlias = bag.GptModelAlias;
            var deploymentID = bag.GptDeployment;
            var endpoint = bag.GptEndpoint;
            var key = bag.OpenAI_Key;
            #endregion

            var myKernel = Kernel.Builder.Build();       
            myKernel.Config.AddAzureChatCompletionService(modelAlias, deploymentID, endpoint, key);
            return myKernel;
        }
        private async Task<string> ReadBodyAsStringAsync(Stream body)
        {
            using (StreamReader reader = new StreamReader(body, Encoding.UTF8))
            {
                var str = await reader.ReadToEndAsync();
                return str;
            }
        }
        private string GenreateQueryContext(RequestModel requestParams, Suitcase bag)
        {
           return @"
                    \n " +
                    bag.History + @"\n

                    \n <|im_start|>" +
                    requestParams.question + @"<|im_end|>\nSearch Query: """"  ";
        }
        private ConnectionMultiplexer GetRedisConnection(Suitcase bag)
        {
            try
            {
                string RedisConnectionString = bag.RedisConnectionString;
                return ConnectionMultiplexer.Connect(RedisConnectionString);
            }
            catch (Exception ex)
            {               
                Log(_logger, "Error connecting to cache: " + ex.Message, true);
                throw;
            }
        }
        private async Task GetMessagesFromCache(IDatabase redisDb, RequestModel requestParam, Suitcase bag)
        {
            // PREFIX ALL CACHE GUIDS WITH "OPEN_AI-"

            if (!redisDb.KeyExists("OPEN_AI-" + requestParam.guid))
            {
                return;
            }

            _cache = await redisDb.StringGetAsync("OPEN_AI-" + requestParam.guid);
            bag.History = _cache;
        }
        private async Task UpdateCache(IDatabase redisDb, RequestModel model, Suitcase bag)
        {
            // Delete the existing _cache for this key
            redisDb.KeyDelete(model.guid);
            
            // PREFIX ALL CACHE GUIDS WITH "OPEN_AI-"            
            redisDb.StringSetAsync("OPEN_AI-" + model.guid, bag.History);

        }

        public static string RetrieveTextDocument(string connectionString, string containerName, string blobName)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            BlobDownloadInfo download = blobClient.Download();

            using (StreamReader reader = new StreamReader(download.Content))
            {
                string documentContent = reader.ReadToEnd();
                return documentContent;
            }

        }
        private async static Task UpdateLog(Suitcase bag, RequestModel model)
        {
            string connectionString = Environment.GetEnvironmentVariable("BlobConnectionString");
            string containerName = Environment.GetEnvironmentVariable("LoggingContainer");

            // Generate a unique name for the blob using the current timestamp and the original file name
            string blobName = model.guid + DateTime.Now.Month +"-"+ DateTime.Now.Day + "-" + DateTime.Now.Year + "-"+DateTime.Now.Hour +"."+DateTime.Now.Minute+ ".log";



            // Create a blob service client
            var blobServiceClient = new BlobServiceClient(connectionString);


            // Get a reference to the blob container
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Upload the file to the blob container
            byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(bag));

            // Upload the byte array as the blob content and overwrite if it already exists
            using (MemoryStream stream = new MemoryStream(data))
            {
                await containerClient.UploadBlobAsync(blobName, stream);
            }



        }
        public static void UpdateAndUploadTextFile(string connectionString, string containerName, string blobName, string updatedContent)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            using (MemoryStream stream = new MemoryStream())
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(updatedContent);
                writer.Flush();
                stream.Position = 0;

                blobClient.Upload(stream, overwrite: true);
            }
        }
        private void Log(ILogger _logger, string message, bool isError = false)
        {
            if (isError)
            {
                _logger.LogError("Error connecting to cache: " + message);
            }
            else
            {
                _logger.LogInformation(message);
            }
        }

    }
}
