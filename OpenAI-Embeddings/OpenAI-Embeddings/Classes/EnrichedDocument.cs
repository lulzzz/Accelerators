using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAI_Embeddings.Classes
{
    public class EnrichedDocument : ProjectOpenAi
    {
        // SOURCE
        public string ID => this.Source + "-" + this.Document;
        public new string Source { get; set; }
        public new string Document { get; set; }
        public new string Url { get; set; }
        public string JsonFileName => this.Source.Replace(" ", string.Empty) + "-" +
                        this.Document
                        .Replace(" ", string.Empty)
                        .Replace("'", string.Empty)
                        .Replace(":", string.Empty)
                        + ".json";

        // ENRICHMENT - Order of properties is specific so the properties don't get lost in JSON with large amount of text
        public int TextLength { get; set; }
        public int TokenLength { get; set; }
        public string Text { get; set; }
        public List<string> Paragraphs { get; set; } = new List<string>(100);
        public List<List<float>> ParagraphEmbeddings { get; set; } = new List<List<float>>(100);
        public string NormalizedText { get; set; }
        public string[] WordTokens { get; set; }
        public string[] WordTokensRemovedStopWords { get; set; }

        public List<WordCount> TopWordCounts { get; set; }
    }
}
