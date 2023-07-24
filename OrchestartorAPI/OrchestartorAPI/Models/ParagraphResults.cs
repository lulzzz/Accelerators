using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrchestartorAPI.Models
{
    internal class ParagraphResults
    {
        public int Id { get; set; }
        public string Document { get; set; }
        public string Source { get; set; }
        public string Paragraph { get; set; }
        public double CosineDistance { get; set; }
    }
}
