using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pa200Hw3.Messages.ImageProcessing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

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
                await messageActions.CompleteMessageAsync(message);
                return;
            }

            var extension = Path.GetExtension(messageData.RawImageUrl);
            var fileName = $"{messageData.ImageGuid}{extension}";
            var blobServiceClient = new BlobServiceClient(_blobStorageConnectionString);

            var processedContainerClient = blobServiceClient.GetBlobContainerClient(_processedImagesContainerName);
            var processedBlobClient = processedContainerClient.GetBlobClient(fileName);
            var alreadyExists = await processedBlobClient.ExistsAsync();
            if (alreadyExists?.Value is true)
            {
                await messageActions.CompleteMessageAsync(message);
                return;
            }

            var rawContainerClient = blobServiceClient.GetBlobContainerClient(_rawImagesContainerName);
            var rawBlobClient = rawContainerClient.GetBlobClient(fileName);
            using var rawImageStream = new MemoryStream();
            await rawBlobClient.DownloadToAsync(rawImageStream);
            rawImageStream.Position = 0;

            using var processedImage = await Image.LoadAsync(rawImageStream);
            processedImage.Mutate(x => x.Grayscale());
            using var processedImageStream = new MemoryStream();
            await processedImage.SaveAsync(processedImageStream, processedImage.Metadata.DecodedImageFormat!);
            processedImageStream.Position = 0;


            await processedBlobClient.UploadAsync(processedImageStream, false);
            _logger.LogInformation($"Successfully processed image {messageData.ImageGuid}");

            await messageActions.CompleteMessageAsync(message);
        }
        catch (Exception ex)
        {
            try
            {
                await messageActions.CompleteMessageAsync(message);
            }
            catch (Exception exs)
            {
                // ignored
            }

            _logger.LogError($"Error processing image: {ex.Message}");
        }
    }
}