using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using System.Text.Json;
using OpenAI_BlobProcessing.Models;
using System.IO;
using Path = System.IO.Path;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace OpenAI_BlobProcessing
{
   
    public class BlobProcessor
    {
        private readonly ILogger _logger;
        private BlobContainers containers = new BlobContainers("input", "archived", "processed", "fault");
        
        public BlobProcessor(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<BlobProcessor>();
        }

        [Function("BlobProcess")]
        public async Task Run([BlobTrigger("input/{name}", Connection = "BlobConnectionString")] string myBlob, string name)
        {

            BlobServiceClient sourceBlobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("BlobConnectionString"));
            BlobContainerClient sourceContainerClient = sourceBlobServiceClient.GetBlobContainerClient(Environment.GetEnvironmentVariable("BlobInputContainer"));

            /*
             1. Determine the Type
             2. Do to the correct processor to extract the data
             3. If a processor does not exist, send it to the "fault" container
             4. Once the text is extracted, create a file in the "processed" folder. Next API will create the embeddings
             5. Try/Catch/Finally - Send the file to the "Fault" folder with the prefix "failed"
             9. SaS Token
             
             */
            bool result;
            var extension = Path.GetExtension(name).ToLower();
            switch (extension)
            {
                case ".pdf":
                    await ProcessPDFDoc(name, sourceContainerClient, sourceBlobServiceClient);
                    break;
                case ".docx":
                    await ProcessWordDoc(name, sourceContainerClient, sourceBlobServiceClient);
                    break;
                case ".txt":
                    await ProcessTextDoc(name, myBlob, sourceBlobServiceClient);
                    break;
                case ".sql":
                    await ProcessTextDoc(name, myBlob, sourceBlobServiceClient);
                    break;
                case ".json":
                    await ProcessTextDoc(name, myBlob, sourceBlobServiceClient);
                    break;
                case ".html":
                    await ProcessTextDoc(name, myBlob, sourceBlobServiceClient);
                    break;
                case ".css":
                    await ProcessTextDoc(name, myBlob, sourceBlobServiceClient);
                    break;
                case ".js":
                    await ProcessTextDoc(name, myBlob, sourceBlobServiceClient);
                    break;
                case ".csv":
                    await ProcessTextDoc(name, myBlob, sourceBlobServiceClient);
                    break;
                default:
                    MoveToAppropriateContainer(name, containers.Fault, null, sourceBlobServiceClient);
                    break;
            }            

        }

        private async Task ProcessTextDoc(string name, string myBlob, BlobServiceClient sourceBlobServiceClient)
        {
            _logger.LogInformation("Starting processing of Text document: " + name);
            MoveToAppropriateContainer(name, "processed", myBlob, sourceBlobServiceClient);

        }

        private async Task ProcessWordDoc(string name, BlobContainerClient sourceContainerClient, BlobServiceClient sourceBlobServiceClient)
        {           
            _logger.LogInformation("Starting processing of Word document: " + name);
            try
            {
                // Get a reference to the source blob
                BlobClient sourceBlobClient = sourceContainerClient.GetBlobClient(name);
                StringBuilder extractedText = new StringBuilder();

                // Read the Word document content
                using (MemoryStream wordStream = new MemoryStream())
                {
                    await sourceBlobClient.DownloadToAsync(wordStream);
                    wordStream.Position = 0;

                    // Extract text from the Word document
                    using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(wordStream, false))
                    {
                        Body body = wordDoc.MainDocumentPart.Document.Body;

                        // Iterate over the paragraphs in the document and extract the text
                        foreach (Paragraph paragraph in body.Elements<Paragraph>())
                        {
                            extractedText.AppendLine(paragraph.InnerText);
                        }
                    }
                }

                MoveToAppropriateContainer(name, "processed", extractedText.ToString(), sourceBlobServiceClient);
            }
            catch (Exception)
            {
                MoveToAppropriateContainer(name, "fault", null, sourceBlobServiceClient);
            }


        }

        private async Task ProcessPDFDoc(string name, BlobContainerClient sourceContainerClient, BlobServiceClient sourceBlobServiceClient)
        {
            _logger.LogInformation("Starting processing of PDF document: " + name);           
            try
            {
                // Get a reference to the source blob
                BlobClient sourceBlobClient = sourceContainerClient.GetBlobClient(name);
                StringBuilder extractedText = new StringBuilder();
                // Read the PDF content
                using (MemoryStream pdfStream = new MemoryStream())
                {
                    await sourceBlobClient.DownloadToAsync(pdfStream);
                    pdfStream.Position = 0;

                    // Extract text from the PDF
                    
                    PdfReader pdfReader = new PdfReader(pdfStream);
                    for (int page = 1; page <= pdfReader.NumberOfPages; page++)
                    {
                        extractedText.AppendLine(PdfTextExtractor.GetTextFromPage(pdfReader, page));
                    }
                    pdfReader.Close();
                }
                MoveToAppropriateContainer(name, "processed",extractedText.ToString(), sourceBlobServiceClient);               
            }
            catch (Exception)
            {                
                MoveToAppropriateContainer(name, "fault", null, sourceBlobServiceClient);
            }
            
        }

        private async Task MoveToAppropriateContainer(string name , string container,string data, BlobServiceClient sourceBlobServiceClient)
        {
            // name is the actual file name
            // data will contain the extracted text
            if (container == containers.Processed)
            {
                BlobContainerClient sourceContainerClient = sourceBlobServiceClient.GetBlobContainerClient(container);
                byte[] byteArray = Encoding.UTF8.GetBytes(data);
                var nameTxt = name.ToLower().Replace(".pdf", ".txt").Replace(".docx", ".txt").Replace(".sql", ".txt").Replace(".json", ".txt").Replace(".html", ".txt").Replace(".css", ".txt").Replace(".js", ".txt");
                using (var stream = new MemoryStream(byteArray))
                {
                    sourceContainerClient.UploadBlob(nameTxt, stream);
                }
            }
            if (container == containers.Fault)
            {
                // Move the file to the fault container
                // Prefix the file name with "in-error"
                // Remove it from Input Container
                var fileName = "in-error-" + data;
                BlobContainerClient sourceContainerClient = sourceBlobServiceClient.GetBlobContainerClient(containers.Input);
                BlobContainerClient destinationContainerClient = sourceBlobServiceClient.GetBlobContainerClient(containers.Fault);

                BlobClient sourceBlobClient = sourceContainerClient.GetBlobClient(name);
                BlobClient destinationBlobClient = destinationContainerClient.GetBlobClient("in-error-"+name);

                await destinationBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
            }
            try
            {
                BlobContainerClient sourceDeleteContainerClient = sourceBlobServiceClient.GetBlobContainerClient(containers.Input);
                BlobClient blobClient = sourceDeleteContainerClient.GetBlobClient(name);
                await blobClient.DeleteAsync();
            }
            catch (Exception ex)
            {

                throw;
            }
        }
    }
}
