using System.ComponentModel.DataAnnotations;

namespace Pa200Hw3.Mvc.Models;

public class ImageUploadViewModel
{
    [Required]
    [Display(Name = "Select Image")]
    public IFormFile ImageFile { get; set; }

    public string? ProcessedImageUrl { get; set; }
}