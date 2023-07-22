﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAI_BlobProcessing.Classes
{
    public class SearchMessage
    {
        public string SearchString { get; set; }
        public string EmbeddingsJsonString { get; set; }
        public List<ParagraphResults> TopParagraphSearchResults { get; set; }
    }
}
