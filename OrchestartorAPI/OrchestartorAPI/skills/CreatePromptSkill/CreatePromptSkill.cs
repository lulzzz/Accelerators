using Microsoft.SemanticKernel.SkillDefinition;
using Newtonsoft.Json;
using OrchestartorAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrchestartorAPI.skills.CreatePromptSkill
{
    internal class CreatePromptSkill
    {
        [SKFunction("CreatePromptSkill")]
        //[SKFunctionContextParameter(Name = "Query", Description = "Pass the query to the cognitive search and get a result")]
        public async Task<string> CreatePrompt(string datasource)
        {
            var bag = JsonConvert.DeserializeObject<Suitcase>(datasource);
            if (bag.History == null) {
                bag.History = string.Empty;
            }
        
            bag.History += "'User' : '" + bag.Ask + "'";
            var meta = bag.MetaPrompt;

            string prompt = @"'system' : '" + meta + @"

            

                    Sources: " +

                    bag.Contents + @"'," + bag.History;


            bag.MetaPrompt = prompt;
            return JsonConvert.SerializeObject(bag);

        }
    }
}
