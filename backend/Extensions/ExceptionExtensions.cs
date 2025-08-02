using NzbWebDAV.Exceptions;

namespace NzbWebDAV.Extensions;

public static class ExceptionExtensions
{
    public static bool IsNonRetryableDownloadException(this Exception exception)
    {
        return exception is NonRetryableDownloadException
            or SharpCompress.Common.InvalidFormatException
            or Usenet.Exceptions.InvalidYencDataException;
    }

    public static bool IsRetryableDownloadException(this Exception exception)
    {
        return exception is NzbWebDAV.Exceptions.UsenetArticleNotFoundException ||
               (exception is InvalidOperationException invalidOp && invalidOp.Message.Contains("providers failed"));
    }
}