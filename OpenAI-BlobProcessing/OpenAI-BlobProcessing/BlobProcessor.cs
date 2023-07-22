using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using System.Text.Json;
using OpenAI_BlobProcessing.Models;
using OpenAI_BlobProcessing.Classes;
using System.IO;
using Path = System.IO.Path;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OpenAI_BlobProcessing.Classes;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SemanticFunctions;
using Newtonsoft.Json;
using SharpToken;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Azure.Storage.Blobs;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Azure.AI.OpenAI;

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
                MoveToAppropriateContainer(name, "processed", extractedText.ToString(), sourceBlobServiceClient);
            }
            catch (Exception)
            {
                MoveToAppropriateContainer(name, "fault", null, sourceBlobServiceClient);
            }

        }

        private async Task MoveToAppropriateContainer(string name, string container, string data, BlobServiceClient sourceBlobServiceClient)
        {
            //_logger.LogInformation($"C# Blob trigger function Processed blob\n Name: {name} \n Data: {myBlob}");
            ////Console.Title = "Machine Intelligence (Text Analytics) with TPL Data Flows & Vector Embeddings";
            ///

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
                BlobClient destinationBlobClient = destinationContainerClient.GetBlobClient("in-error-" + name);

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

            var results = string.Empty;


            // START the timer
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var totalTokenLength = 0;
            var totalTextLength = 0;

            // CONFIG 
            // Instantiate new ML.NET Context
            // Note: MlContext is thread-safe
            var mlContext = new MLContext(100);

            // var openAIEmbeddingsGeneration = new OpenAITextEmbeddingGeneration("text-embedding-ada-002", openAIAPIKey);

            // Azure OpenAI (Azure not OpenAI)
            var openAIClient = new OpenAIClient(
                new Uri("https://openaiappliedai.openai.azure.com"), new Azure.AzureKeyCredential(Environment.GetEnvironmentVariable("APIKey")));
            // OpenAI Client (not Azure OpenAI)
            //var openAIClient = new OpenAIClient(openAIAPIKey);

            // GET Current Environment Folder
            // Note: This will have the JSON documents from the checked-in code and overwrite each time run
            // Note: Can be used for offline mode in-stead of downloading (not to get your IP blocked), or use VPN
            var currentEnrichmentFolder = System.IO.Path.Combine(Environment.CurrentDirectory, "EnrichedDocuments");
            System.IO.Directory.CreateDirectory(currentEnrichmentFolder);

            // SET language to English
            StopWordsRemovingEstimator.Language language = StopWordsRemovingEstimator.Language.English;


            // SET the max degree of parallelism
            // Note: Default is to use 75% of the workstation or server cores.
            // Note: If cores are hyperthreaded, adjust accordingly (i.e. multiply *2)
            var isHyperThreaded = false;
            var executionDataFlowOptions = new ExecutionDataflowBlockOptions();
            executionDataFlowOptions.MaxDegreeOfParallelism =
                // Use 75% of the cores, if hyper-threading multiply cores *2
                Convert.ToInt32(Math.Ceiling((Environment.ProcessorCount * 0.75) *
                (isHyperThreaded ? 2 : 1)));
            executionDataFlowOptions.MaxMessagesPerTask = 1;

            // SET the Data Flow Block Options
            // This controls the data flow from the Producer level           
            // Note: If this is set to high, you may receive errors. In production, you would ensure request throughput
            var dataFlowBlockOptions = new DataflowBlockOptions
            {
                EnsureOrdered = true, // Ensures order, this is purely optional but messages will flow in order
                BoundedCapacity = 10,
                MaxMessagesPerTask = 10
            };

            // SET the data flow pipeline options
            // Note: OPTIONAL Set MaxMessages to the number of documents to process
            // Note: For example, setting MaxMessages to 2 will run only two documents through the pipeline
            // Note: Set MaxMessages to 1 to process in sequence
            var dataFlowLinkOptions = new DataflowLinkOptions
            {
                PropagateCompletion = true,
                MaxMessages = -1
            };

            // TPL: BufferBlock - Seeds the queue with selected Project OpenAI
            async Task ProduceOpenAis(BufferBlock<EnrichedDocument> queue,
                IEnumerable<ProjectOpenAi> projectOpenAis)
            {

                foreach (var projectOpenAi in projectOpenAis)
                {
                    // Add baseline information from the document list
                    var enrichedDocument = new EnrichedDocument
                    {
                        Document = projectOpenAi.Document,
                        Source = projectOpenAi.Source,
                        Url = projectOpenAi.Url
                    };

                    await queue.SendAsync(enrichedDocument);
                }

                queue.Complete();
            }

            // TPL Block: Download the requested OpenAI resources as a text string
            var downloadDocumentText = new TransformBlock<EnrichedDocument, EnrichedDocument>(async enrichedDocument =>
            {
                //Console.ForegroundColor = ConsoleColor.Gray;
                //Console.WriteLine("Downloading: '{0}'", enrichedDocument.Document);
                enrichedDocument.Text = await new HttpClient().GetStringAsync(enrichedDocument.Url);
                return enrichedDocument;
            }, executionDataFlowOptions);

            // TPL Block: Download the requested OpenAI resources as a text string
            var chunkedLinesEnrichment = new TransformBlock<EnrichedDocument, EnrichedDocument>(enrichedDocument =>
            {
                //Console.ForegroundColor = ConsoleColor.Gray;
                //Console.WriteLine("Chunking text for: '{0}'", enrichedDocument.Document);

                // Get the encoding for text-embedding-ada-002
                var cl100kBaseEncoding = GptEncoding.GetEncoding("cl100k_base");
                // Return the optimal text encodings, this is if tokens can be split perfect (no overlap)
                var encodedTokens = cl100kBaseEncoding.Encode(enrichedDocument.Text);

                enrichedDocument.TokenLength = encodedTokens.Count;
                totalTokenLength += enrichedDocument.TokenLength;

                return enrichedDocument;
            }, executionDataFlowOptions);

            // TPL Block: Performs text Machine Learning on the document texts
            var machineLearningEnrichment = new TransformBlock<EnrichedDocument, EnrichedDocument>(enrichedDocument =>
            {
                //Console.ForegroundColor = ConsoleColor.Gray;
                //Console.WriteLine("Machine Learning enrichment for: '{0}'", enrichedDocument.Document);

                enrichedDocument.TextLength = enrichedDocument.Text.Length;
                totalTextLength += enrichedDocument.TextLength;

                var textData = new TextData { Text = enrichedDocument.Text };
                var textDataArray = new TextData[] { textData };

                IDataView emptyDataView = mlContext.Data.LoadFromEnumerable(new List<TextData>());
                EstimatorChain<StopWordsRemovingTransformer> textPipeline = mlContext.Transforms.Text
                    .NormalizeText("NormalizedText", "Text", caseMode: TextNormalizingEstimator.CaseMode.Lower, keepDiacritics: true, keepPunctuations: false, keepNumbers: false)
                    .Append(mlContext.Transforms.Text.TokenizeIntoWords("WordTokens", "NormalizedText", separators: new[] { ' ' }))
                    .Append(mlContext.Transforms.Text.RemoveDefaultStopWords("WordTokensRemovedStopWords", "WordTokens", language: language));
                TransformerChain<StopWordsRemovingTransformer> textTransformer = textPipeline.Fit(emptyDataView);
                PredictionEngine<TextData, TransformedTextData> predictionEngine = mlContext.Model.CreatePredictionEngine<TextData, TransformedTextData>(textTransformer);

                var prediction = predictionEngine.Predict(textData);

                // Set ML Enriched document variables
                enrichedDocument.NormalizedText = prediction.NormalizedText;
                enrichedDocument.WordTokens = prediction.WordTokens;
                enrichedDocument.WordTokensRemovedStopWords = prediction.WordTokensRemovedStopWords;

                // Calculate Stop Words
                var result = enrichedDocument.WordTokensRemovedStopWords.AsParallel()
                    .Where(word => word.Length > 3)
                    .GroupBy(x => x)
                    .OrderByDescending(x => x.Count())
                    .Select(x => new WordCount { WordName = x.Key, Count = x.Count() }).Take(100)
                    .ToList();

                enrichedDocument.TopWordCounts = result;

                // Calculate the Paragraphs based on TokenCount
                var enrichedDocumentLines = Microsoft.SemanticKernel.Text.TextChunker.SplitPlainTextLines(enrichedDocument.Text, 200);
                enrichedDocument.Paragraphs = Microsoft.SemanticKernel.Text.TextChunker.SplitPlainTextParagraphs(enrichedDocumentLines, 600);

                return enrichedDocument;
            }, executionDataFlowOptions);

            // TPL Block: Get OpenAI Embeddings
            var retrieveEmbeddings = new TransformBlock<EnrichedDocument, EnrichedDocument>(async enrichedDocument =>
            {
                //Console.ForegroundColor = ConsoleColor.Gray;
                //Console.WriteLine("OpenAI Embeddings for: '{0}'", enrichedDocument.Document);

                foreach (var paragraph in enrichedDocument.Paragraphs)
                {
                    var embeddings = new EmbeddingsOptions(paragraph);
                    var result = await openAIClient.GetEmbeddingsAsync("text-embedding-ada-002", embeddings);
                    var embeddingsVector = result.Value.Data[0].Embedding;
                    enrichedDocument.ParagraphEmbeddings.Add(embeddingsVector.ToList());
                }

                return enrichedDocument;

            }, executionDataFlowOptions);

            // TPL Block: Persist the document embeddings and the final enriched document to Json
            var persistToDatabaseAndJson = new TransformBlock<EnrichedDocument, EnrichedDocument>(enrichedDocument =>
            {
                //Console.ForegroundColor = ConsoleColor.Gray;
                //Console.WriteLine("Persisting to DB & converting to JSON file: '{0}'", enrichedDocument.Document);

                // 1 - Save to SQL Server
                using (SqlConnection connection = new SqlConnection(Environment.GetEnvironmentVariable("SqlConnection")))
                {
                    connection.Open();

                    for (int i = 0; i != enrichedDocument.Paragraphs.Count; i++)
                    {
                        using (SqlCommand command = new SqlCommand(string.Empty, connection))
                        {
                            command.CommandText = "INSERT INTO ProjectOpenAi(Source, Document, Url, Paragraph, ParagraphEmbeddings) VALUES(@Source, @Document, @url, @paragraph, @paragraphEmbeddings)";
                            command.Parameters.AddWithValue("@Source", enrichedDocument.Source);
                            command.Parameters.AddWithValue("@Document", enrichedDocument.Document);
                            command.Parameters.AddWithValue("@url", enrichedDocument.Url);
                            command.Parameters.AddWithValue("@paragraph", enrichedDocument.Paragraphs[i]);
                            var jsonStringParagraphEmbeddings = JsonConvert.SerializeObject(enrichedDocument.ParagraphEmbeddings[i]);
                            command.Parameters.AddWithValue("@paragraphEmbeddings", jsonStringParagraphEmbeddings);

                            command.ExecuteNonQuery();
                        }
                    }
                    //MoveToAppropriateContainer(name, myBlob);
                    connection.Close();
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(string.Empty, connection))
                    {
                        command.CommandText = "spCreateProjectOpenAiVectorsIndex";
                        command.CommandTimeout = 0;
                        command.ExecuteNonQuery();
                    }
                    connection.Close();
                }
                return enrichedDocument;
            });
            // TPL Block: Search Vector Index
            var retrieveEmbeddingsForSearch = new TransformBlock<SearchMessage, SearchMessage>(async searchMessage =>
            {
                //Console.ForegroundColor = ConsoleColor.Gray;
                //Console.WriteLine("Retrieving OpenAI Embeddings for: '{0}'", searchMessage.SearchString);

                var embeddings = new EmbeddingsOptions(searchMessage.SearchString);
                var result = await openAIClient.GetEmbeddingsAsync("text-embedding-ada-002", embeddings);
                var embeddingsVector = result.Value.Data[0].Embedding;
                searchMessage.EmbeddingsJsonString = System.Text.Json.JsonSerializer.Serialize(embeddingsVector);

                return searchMessage;
            });

            // TPL Block: Search Vector Index
            var searchVectorIndex = new TransformBlock<SearchMessage, SearchMessage>(async searchMessage =>
            {
                //Console.ForegroundColor = ConsoleColor.Gray;
                //Console.WriteLine("Searching Project OpenAi Vector Index for '{0}'", searchMessage.SearchString);

                var dataSet = new DataSet();

                // Execute script to create database objects (tables, stored procedures)
                using (SqlConnection connection = new SqlConnection(Environment.GetEnvironmentVariable("SqlConnection")))
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

                return searchMessage;

            });

            var answerQuestionWithOpenAI = new ActionBlock<SearchMessage>(async searchMessage =>
            {
                
                // Azure OpenAI
                var semanticKernel = Kernel.Builder.Build();
                semanticKernel.Config.AddAzureTextCompletionService("text-davinci-003-demo", Environment.GetEnvironmentVariable("Endpoint"), Environment.GetEnvironmentVariable("APIKey"));

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

                //Console.ForegroundColor = ConsoleColor.Yellow;
                results += string.Format(@"Question: {0}. Answer: {1}", searchMessage.SearchString, openAIQuestionAnswer.Result + "\n");
                //Console.WriteLine("Answer for question '{0}'. Based on vector index search and OpenAI Text Completion: '{1}'", searchMessage.SearchString, openAIQuestionAnswer.Result);
            });

            // TPL: BufferBlock - Seeds the queue with selected Project OpenAI
            async Task ProduceOpenAisSearches(BufferBlock<SearchMessage> searchQueue,
                IEnumerable<SearchMessage> searchMessages)
            {
                //Console.ForegroundColor = ConsoleColor.Gray;
                //Console.WriteLine("Creating Document Search...");

                foreach (var searchMessage in searchMessages)
                {
                    await searchQueue.SendAsync(searchMessage);
                }

                searchQueue.Complete();
            }

            // TPL Pipeline: Build the pipeline workflow graph from the TPL Blocks
            var enrichmentPipeline = new BufferBlock<EnrichedDocument>(dataFlowBlockOptions);
            enrichmentPipeline.LinkTo(downloadDocumentText, dataFlowLinkOptions);
            downloadDocumentText.LinkTo(chunkedLinesEnrichment, dataFlowLinkOptions);
            chunkedLinesEnrichment.LinkTo(machineLearningEnrichment, dataFlowLinkOptions);
            machineLearningEnrichment.LinkTo(retrieveEmbeddings, dataFlowLinkOptions);
            retrieveEmbeddings.LinkTo(persistToDatabaseAndJson, dataFlowLinkOptions);
            //persistToDatabaseAndJson.LinkTo(printEnrichedDocument, dataFlowLinkOptions);

            // TPL: Start the producer by feeding it a list of documents
            var enrichmentProducer = ProduceOpenAis(enrichmentPipeline, ProjectOpenAiService.GetDocuments(Environment.GetEnvironmentVariable("BlobConnectionString")));

            // TPL: Since this is an asynchronous Task process, wait for the producer to finish putting all the messages on the queue
            // Works when queue is limited and not a "forever" queue
            await Task.WhenAll(enrichmentProducer);
            
            // Search the Vectors Index, then answer the question using Semantic Kernel
            var searchMessagePipeline = new BufferBlock<SearchMessage>(dataFlowBlockOptions);
            searchMessagePipeline.LinkTo(retrieveEmbeddingsForSearch, dataFlowLinkOptions);
            retrieveEmbeddingsForSearch.LinkTo(searchVectorIndex, dataFlowLinkOptions);
            searchVectorIndex.LinkTo(answerQuestionWithOpenAI, dataFlowLinkOptions);

            // TPL: Start the producer by feeding it a list of documents
            var documentSearchesProducer = ProduceOpenAisSearches(searchMessagePipeline, ProjectOpenAiService.GetQueries());
            await Task.WhenAll(documentSearchesProducer);

            BlobServiceClient sourceBlobServiceClienta = new BlobServiceClient(Environment.GetEnvironmentVariable("BlobConnectionString"));
            BlobContainerClient sourceContainerClienta = sourceBlobServiceClienta.GetBlobContainerClient("archived");

            byte[] byteArraya = Encoding.UTF8.GetBytes(data);
            using (var stream = new MemoryStream(byteArraya))
            {
                sourceContainerClienta.UploadBlob(name, stream);
            }

            try
            {
                BlobContainerClient sourceDeleteContainerClienta = sourceBlobServiceClienta.GetBlobContainerClient("processed");
                await foreach (BlobItem blobItem in sourceDeleteContainerClienta.GetBlobsAsync())
                {
                    // Delete the blob
                    await sourceDeleteContainerClienta.DeleteBlobAsync(blobItem.Name);
                }
            }
            catch (Exception ex)
            {
                int aaa = 12;
            }


            // TPL: Wait for the last block in the pipeline to process all messages.
            answerQuestionWithOpenAI.Completion.Wait();
            stopwatch.Stop();
            

                
            

        }
       
    }   
}
