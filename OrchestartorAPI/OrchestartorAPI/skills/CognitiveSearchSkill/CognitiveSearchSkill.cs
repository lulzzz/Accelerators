using Microsoft.SemanticKernel.SkillDefinition;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using OrchestartorAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Orchestration;

namespace OrchestartorAPI.skills.CognitiveSearchSkill
{
    internal class CognitiveSearchSkill
    {
        [SKFunction("This skill does a Cognitive Search. It takes in a query string, which is not passed through")]       
        public async Task<string> ProcessQuery(string input)
        {          
            var bag = JsonConvert.DeserializeObject<Suitcase>(input);

            var queryParameter = bag.QueryFilter;//  context["Query"];            
            string url = Environment.GetEnvironmentVariable("CognitiveUrl");
            string urlWithParam = $"{url}&search={queryParameter}";
            using (HttpClient client = new HttpClient())
            {
                // Choose one of the URL options above and use it in the GetAsync method

                client.DefaultRequestHeaders.Add("api-key", Environment.GetEnvironmentVariable("CognitiveApiKey"));

                HttpResponseMessage response = await client.GetAsync(urlWithParam);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    JObject sourcesDeserialized = JObject.Parse(responseBody.ToString());
                    var topThreeContents = sourcesDeserialized["value"]
                        .Select(c => c["sourcefile"].ToString() + ":" + c["content"].ToString())
                        .Take(3);/// THIS IS SOURCES

                    var topThreeCitations = sourcesDeserialized["value"]
                        .Select(c => "[<a href='" + bag.CitationsUrl + c["sourcepage"].ToString() + "'>" +c["sourcepage"].ToString()+"</a>]")
                        .Take(3);/// THIS IS SOURCES

                    //<a href="https://example.com">Visit Example Website</a>

                    bag.Contents = string.Join(" ", topThreeContents);
                    bag.Citations = string.Join(" ", topThreeCitations);
                    return JsonConvert.SerializeObject(bag);
                }
                else
                {
                    Console.WriteLine($"API call failed with status code: {response.StatusCode}");
                }
            }
            return string.Empty;

        }
    }
}

