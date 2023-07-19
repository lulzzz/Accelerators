﻿using Azure.Storage.Blobs;
using OpenAI_Embeddings.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAI_Embeddings
{
    public static class ProjectOpenAiService
    {
        public static List<ProjectOpenAi> GetDocuments()
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient("DefaultEndpointsProtocol=https;AccountName=docfileprocessor;AccountKey=UEZX6OLm94afFkZkowkbV9ytqNCu77Z+AoB0u1VJ83d6OR5pz/Ahc8thQ8dJdvQMrn4O+9L6fAZ5+AStDxuGMg==;EndpointSuffix=core.windows.net");

            // Get a reference to the container
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("processed");

            // List all blobs in the container
            var documents = new List<ProjectOpenAi>();

            foreach (var blobItem in containerClient.GetBlobs())
            {
                // Get a reference to the blob
                BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);

                // Get the URL of the blob
                string blobUrl = blobClient.Uri.AbsoluteUri;

                // Add the URL to the list
                var one = new ProjectOpenAi() { Source = "Container: Processed", Url = blobUrl, Document = blobItem.Name };
                documents.Add(one);
            }

            return documents;
        }

        public static List<SearchMessage> GetQueries()
        {
            var searchMessages = new List<SearchMessage>
            {
                // Oscar Wilde
                new SearchMessage{
                    SearchString = "Was my July Bill hight or lower than last month?"

                }
            };

            return searchMessages;
        }
        public static List<SearchMessage> GetQueries(string message)
        {
            var searchMessages = new List<SearchMessage>
            {
                // Oscar Wilde
                new SearchMessage{
                    SearchString = message

                }
            };

            return searchMessages;
        }
    }
}