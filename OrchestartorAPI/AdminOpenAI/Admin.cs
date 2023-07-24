using System.Net;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;


using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;

namespace AdminOpenAI
{
    public class Admin
    {
        private readonly ILogger _logger;

        public Admin(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Admin>();
        }

        [Function("adm")]
        public static IActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            // Set the connection string and container name for your Azure Blob Storage
            string connectionString = Environment.GetEnvironmentVariable("BlobConnectionString"); 
            string containerName = Environment.GetEnvironmentVariable("BlobContainer");

            // Get the uploaded file from the request
            var file = req.Form.Files.GetFile("file");

            if (file == null)
            {
                return null;
            }

            // Generate a unique name for the blob using the current timestamp and the original file name
            string blobName = $"{System.DateTime.UtcNow.ToString("yyyyMMddHHmmss")}-{file.FileName}";

            // Create a blob service client
            var blobServiceClient = new BlobServiceClient(connectionString);

            // Get a reference to the blob container
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Upload the file to the blob container
            using (var stream = file.OpenReadStream())
            {
                containerClient.UploadBlob(blobName, stream);
            }

            log.LogInformation($"File {blobName} uploaded successfully to Azure Blob Storage");

            return null;
        }

        public static string RetrieveTextDocument(string connectionString, string containerName, string blobName)
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
        public static void UpdateAndUploadTextFile(string connectionString, string containerName, string blobName, string updatedContent)
        {
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
