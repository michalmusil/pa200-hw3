using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Pa200Hw3.Mvc.Models;

namespace Pa200Hw3.Mvc.Controllers;

public class ImageViewController : Controller
{
    private readonly string _blobStorageConnectionString;
    private readonly string _processedImagesContainerName;

    public ImageViewController(IConfiguration configuration)
    {
        _blobStorageConnectionString = configuration.GetSection("BlobStorage")["ConnectionString"]!;
        _processedImagesContainerName =
            configuration.GetSection("BlobStorage")
                ["ProcessedImagesContainerName"]!;
    }

    public async Task<IActionResult> Index()
    {
        List<string> imageUrls = new List<string>();
        try
        {
            var blobServiceClient = new BlobServiceClient(_blobStorageConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_processedImagesContainerName);

            await foreach (var blobItem in containerClient.GetBlobsAsync())
            {
                BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);
                imageUrls.Add(blobClient.Uri.ToString());
            }

            return View(new ImageViewViewModel { ImageUrls = imageUrls });
        }
        catch
        {
            ViewBag.Message = "Failed to load images";
            return View(new ImageViewViewModel { ImageUrls = [] });
        }
    }
}