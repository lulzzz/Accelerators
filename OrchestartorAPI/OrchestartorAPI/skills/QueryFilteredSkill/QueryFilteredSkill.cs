using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Newtonsoft.Json;
using OrchestartorAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace OrchestartorAPI.skills.QueryFilteredSkill
{
    internal class QueryFilteredSkill
    {
        [SKFunction("QueryFiltered")]
        [SKFunctionContextParameter(Name = "Input", Description = "Take a question and generate a query")]
        [SKFunctionContextParameter(Name = "Prompt", Description = "Prompt")]
        public async Task<string> GetQuery(SKContext context)
        {

            var kernel = GetDavinciKernel();
            var input = context["Input"];
            var Prompt = context["Prompt"];

            var prompt = Prompt; // "Below is a history of the conversation so far, and a new question asked by the user that needs to be answered by searching in a knowledge base that contains documents related to the conversation. Generate a search query based on the conversation and the new question. Do not include cited source filenames and document names e.g info.txt or doc.pdf in the search query terms. Do not include any text inside [] or <<>> in the search query terms. If the question is not in English, translate the question to English before generating the search query.{{$input}}";// Environment.GetEnvironmentVariable("DavinciPrompt");
            var summarize = kernel.CreateSemanticFunction(prompt);

            var res = await summarize.InvokeAsync(input);
            return res.Result.ToString();

        }

        private IKernel GetDavinciKernel()
        {
            #region Azure Chat Completion Service Values.....
            var kernel = Kernel.Builder.Build();
            //kernel.Config.AddAzureTextCompletionService("Any", "text-davinci-003-demo", "https://openaiappliedai.openai.azure.com/", "d1fff11e908340328629256d31f929b3");
            kernel.Config.AddAzureTextCompletionService(
            Environment.GetEnvironmentVariable("DavinciModelAlias"),
            Environment.GetEnvironmentVariable("DavinciDeployment"),
            Environment.GetEnvironmentVariable("DavinciEndpoint"),
            Environment.GetEnvironmentVariable("OpenAIKey"));

            return kernel;
            #endregion
        }
    }
}

