namespace NzbWebDAV.Exceptions;

public class SeekPositionNotFoundException(string message) : NonRetryableDownloadException(message)
{
}