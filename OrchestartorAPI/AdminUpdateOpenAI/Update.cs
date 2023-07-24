using System.Net;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace AdminUpdateOpenAI
{
    public class Update
    {
        private readonly ILogger _logger;

        public Update(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Update>();
        }

        [Function("update")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("OpenAI Admin Update");
            var contents = await ReadBodyAsStringAsync(req.Body);
            UpdateAdminParams(Environment.GetEnvironmentVariable("BlobConnectionString"), Environment.GetEnvironmentVariable("BlobContainer"), Environment.GetEnvironmentVariable("BlobName"), contents);
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Updated!");

            return response;
        }
        private async Task<string> ReadBodyAsStringAsync(Stream body)
        {
            using (StreamReader reader = new StreamReader(body, Encoding.UTF8))
            {
                var str = await reader.ReadToEndAsync();
                return str;
            }
        }
        public static string RetrieveAdminParams(string connectionString, string containerName, string blobName)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            BlobDownloadInfo download = blobClient.Download();

            using (StreamReader reader = new StreamReader(download.Content))
            {
                string documentContent = reader.ReadToEnd();
                return documentContent;
            }

        }
        public static void UpdateAdminParams(string connectionString, string containerName, string blobName, string updatedContent)
        {

            JObject jsonObject = JObject.Parse(updatedContent);
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            using (MemoryStream stream = new MemoryStream())
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(updatedContent.Replace("\r", " ").Replace("\n", " "));
                writer.Flush();
                stream.Position = 0;

                blobClient.Upload(stream, overwrite: true);
            }
        }
    }
}
