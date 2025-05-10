using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Pa200Hw3.Messages.ImageProcessing;
using Pa200Hw3.Mvc.Models;

namespace Pa200Hw3.Mvc.Controllers;

public class ImageUploadController : Controller
{
    private readonly string _serviceBusConnectionString;
    private readonly string _queueName;
    private readonly string _blobStorageConnectionString;
    private readonly string _rawImagesContainerName;
    private readonly string _processedImagesContainerName;


    public ImageUploadController(IConfiguration configuration)
    {
        _serviceBusConnectionString = configuration.GetSection("ServiceBus")["SendPolicyConnectionString"]!;
        _queueName = configuration.GetSection("ServiceBus")["ImageProcessingQueueName"]!;
        _blobStorageConnectionString = configuration.GetSection("BlobStorage")["ConnectionString"]!;
        _rawImagesContainerName = configuration.GetSection("BlobStorage")["RawImagesContainerName"]!;
        _processedImagesContainerName = configuration.GetSection("BlobStorage")["ProcessedImagesContainerName"]!;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(new ImageUploadViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Index(ImageUploadViewModel model)
    {
        if (ModelState.IsValid)
        {
            var imageGuid = Guid.NewGuid().ToString();
            var extension = Path.GetExtension(model.ImageFile.FileName);
            var blobName = $"{imageGuid}{extension}";

            var blobServiceClient = new BlobServiceClient(_blobStorageConnectionString);
            var tempContainerClient = blobServiceClient.GetBlobContainerClient(_rawImagesContainerName);

            try
            {
                var blobClient = tempContainerClient.GetBlobClient(blobName);
                using (var stream = model.ImageFile.OpenReadStream())
                {
                    await blobClient.UploadAsync(stream);
                }

                var blobUrl = blobClient.Uri.ToString();

                var message = new ProcessImageMessage
                {
                    ImageGuid = imageGuid,
                    RawImageUrl = blobUrl,
                };
                var messageBody = JsonSerializer.Serialize(message);

                await using (var client = new ServiceBusClient(_serviceBusConnectionString))
                {
                    var sender = client.CreateSender(_queueName);
                    var serviceBusMessage = new ServiceBusMessage(messageBody);
                    await sender.SendMessageAsync(serviceBusMessage);
                    await sender.CloseAsync();
                }

                var processedContainerClient = blobServiceClient.GetBlobContainerClient(_processedImagesContainerName);
                var processedImageUrl = $"{processedContainerClient.Uri}/{blobName}";
                return View(new ImageUploadViewModel { ProcessedImageUrl = processedImageUrl });
            }
            catch (Exception ex)
            {
                ViewBag.Message = $"Error uploading image: {ex.Message}";
                return View(model);
            }
        }


        ViewBag.Message = "Please select an image to upload.";
        return View(model);
    }
}