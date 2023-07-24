using Microsoft.SemanticKernel.SkillDefinition;
using Newtonsoft.Json;
using OrchestartorAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrchestartorAPI.skills.CreateQueryFilterSkill
{
    internal class CreateQueryFilterSkill
    {
        [SKFunction("CreateQueryFilterSkill")]
        //[SKFunctionContextParameter(Name = "Query", Description = "Pass the query to the cognitive search and get a result")]
        public async Task<string> CreateQueryFilter(string datasource)
        {
           
            return @"Summarize and create a query filter from the following: " + datasource;
           

        }
    }
}
