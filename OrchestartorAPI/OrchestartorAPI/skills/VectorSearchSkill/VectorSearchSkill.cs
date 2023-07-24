using Microsoft.SemanticKernel.SkillDefinition;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using OrchestartorAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel;

namespace OrchestartorAPI.skills.VectorSearchSkill
{
    internal class VectorSearchSkill
    {
        [SKFunction("Vector Search")]
        public async Task<string> ProcessQuery(string query)
        {
            var results = string.Empty;
            var openAIClient = new OpenAIClient(
                new Uri("https://openaiappliedai.openai.azure.com"), new Azure.AzureKeyCredential(Environment.GetEnvironmentVariable("OpenAIKey")));

            var bag = JsonConvert.DeserializeObject<Suitcase>(query);

            var searchMessage = new SearchMessage();
            searchMessage.SearchString = bag.Ask;

            var embeddings = new EmbeddingsOptions(searchMessage.SearchString);
            var result = await openAIClient.GetEmbeddingsAsync("text-embedding-ada-002", embeddings);
            var embeddingsVector = result.Value.Data[0].Embedding;
            searchMessage.EmbeddingsJsonString = System.Text.Json.JsonSerializer.Serialize(embeddingsVector);


            var dataSet = new DataSet();

            // Execute script to create database objects (tables, stored procedures)
            using (SqlConnection connection = new SqlConnection(Environment.GetEnvironmentVariable("SQLServer")))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(string.Empty, connection))
                {
                    command.CommandText = "spSearchProjectOpenAiVectors";
                    command.CommandType = System.Data.CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@jsonOpenAIEmbeddings", searchMessage.EmbeddingsJsonString);

                    var sqlAdapter = new SqlDataAdapter(command);
                    sqlAdapter.Fill(dataSet);
                }

                connection.Close();
            }

            var paragraphResults = dataSet.Tables[0].AsEnumerable()
                .Select(dataRow => new ParagraphResults
                {
                    Id = dataRow.Field<int>("Id"),
                    Document = dataRow.Field<string>("Document"),
                    Source = dataRow.Field<string>("Source"),
                    CosineDistance = dataRow.Field<double>("CosineDistance"),
                    Paragraph = dataRow.Field<string>("Paragraph")
                }).ToList();

            searchMessage.TopParagraphSearchResults = paragraphResults;
            var semanticKernel = Kernel.Builder.Build();
           
            semanticKernel.Config.AddAzureTextCompletionService(
                Environment.GetEnvironmentVariable("DavinciModelAlias"),
                Environment.GetEnvironmentVariable("DavinciDeployment"),
                Environment.GetEnvironmentVariable("DavinciEndpoint"),
                Environment.GetEnvironmentVariable("OpenAIKey"));

            string answerQuestionContext = """
    Answer the following question based on the context paragraph below: 
    ---Begin Question---
    {{$SEARCHSTRING}}
    ---End Question---
    ---Begin Paragraph---
    {{$PARAGRAPH}}
    ---End Paragraph---
    ---Begin HISTORY---
    {{$HISTORY}}
    ---End HISTORY---
    """;

            var questionContext = new ContextVariables();
            questionContext.Set("SEARCHSTRING", searchMessage.SearchString);
            questionContext.Set("PARAGRAPH", searchMessage.TopParagraphSearchResults[0].Paragraph);
            questionContext.Set("HISTORY", results);

            var questionPromptConfig = new PromptTemplateConfig
            {
                Description = "Search & Answer",
                Completion =
        {
            MaxTokens = 1000,
            Temperature = 0.7,
            TopP = 0.6,
        }
            };

            var myPromptTemplate = new PromptTemplate(
                answerQuestionContext,
                questionPromptConfig,
                semanticKernel
            );

            var myFunctionConfig = new SemanticFunctionConfig(questionPromptConfig, myPromptTemplate);
            var answerFunction = semanticKernel.RegisterSemanticFunction(
                "VectorSearchAndAnswer",
                "AnswerFromQuestion",
                myFunctionConfig);

            var openAIQuestionAnswer = await semanticKernel.RunAsync(questionContext, answerFunction);
            bag.Contents = string.Join(" ", openAIQuestionAnswer.Result.ToString());
            //bag.Citations = string.Join(" ", topThreeCitations);
            return JsonConvert.SerializeObject(bag);
        

        }
    }
}
