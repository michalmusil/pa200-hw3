using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pa200Hw3.Messages.ImageProcessing;

namespace Pa200Hw3.ProcessImageFunction;

public class ProcessImageFunction
{
    private readonly ILogger<ProcessImageFunction> _logger;
    private readonly string _blobStorageConnectionString;
    private readonly string _rawImagesContainerName;
    private readonly string _processedImagesContainerName;

    public ProcessImageFunction(ILogger<ProcessImageFunction> logger, IConfiguration configuration)
    {
        _logger = logger;
        _blobStorageConnectionString = configuration.GetSection("BlobStorage")["ConnectionString"]!;
        _rawImagesContainerName = configuration.GetSection("BlobStorage")["RawImagesContainerName"]!;
        _processedImagesContainerName = configuration.GetSection("BlobStorage")["ProcessedImagesContainerName"]!;
    }

    [Function(nameof(ProcessImageFunction))]
    public async Task Run(
        [ServiceBusTrigger("image-processing", Connection = "ServiceBus:ListenPolicyConnectionString")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        try
        {
            var messageData = JsonSerializer.Deserialize<ProcessImageMessage>(message.Body);
            if (messageData == null || string.IsNullOrEmpty(messageData.RawImageUrl) ||
                string.IsNullOrEmpty(messageData.ImageGuid))
            {
                _logger.LogError("Invalid message format received.");
                return;
            }

            var extension = Path.GetExtension(messageData.RawImageUrl);
            var fileName = $"{messageData.ImageGuid}{extension}";

            var blobServiceClient = new BlobServiceClient(_blobStorageConnectionString);
            var rawContainerClient = blobServiceClient.GetBlobContainerClient(_rawImagesContainerName);
            var rawBlobClient = rawContainerClient.GetBlobClient(fileName);

            using var rawImageStream = new MemoryStream();
            await rawBlobClient.DownloadToAsync(rawImageStream);
            rawImageStream.Position = 0;

            var processedContainerClient = blobServiceClient.GetBlobContainerClient(_processedImagesContainerName);
            var processedBlobClient = processedContainerClient.GetBlobClient(fileName);

            var x = await processedBlobClient.ExistsAsync();
            if (x?.Value is true)
                return;

            await processedBlobClient.UploadAsync(rawImageStream, false);

            _logger.LogInformation(
                $"Successfully processed image {messageData.ImageGuid}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing image: {ex.Message}");
        }
    }
}