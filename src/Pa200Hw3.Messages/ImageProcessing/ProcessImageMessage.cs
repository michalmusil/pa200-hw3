namespace Pa200Hw3.Messages.ImageProcessing;

public class ProcessImageMessage
{
    public required string ImageGuid { get; set; }
    public required string RawImageUrl { get; set; }
}