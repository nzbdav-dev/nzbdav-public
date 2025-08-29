using NzbWebDAV.Clients;
using NzbWebDAV.Exceptions;
using NzbWebDAV.Queue.DeobfuscationSteps._3.GetFileInfos;
using NzbWebDAV.Utils;
using Serilog;
using Usenet.Nzb;

namespace NzbWebDAV.Queue.FileProcessors;

public class FileProcessor(
    NzbFile nzbFile,
    GetFileInfosStep.FileInfo fileinfo,
    UsenetStreamingClient usenet,
    CancellationToken ct
) : BaseProcessor
{
    public static bool CanProcess(string filename)
    {
        return true;
    }

    public override async Task<BaseProcessor.Result?> ProcessAsync()
    {
        try
        {
            return new Result()
            {
                NzbFile = nzbFile,
                FileName = fileinfo.FileName,
                FileSize = fileinfo.FileSize ?? await usenet.GetFileSizeAsync(nzbFile, ct),
            };
        }

        // Ignore missing articles if it's not a video file.
        // In that case, simply skip the file altogether.
        catch (UsenetArticleNotFoundException) when (!FilenameUtil.IsVideoFile(fileinfo.FileName))
        {
            Log.Warning($"File `{fileinfo.FileName}` has missing articles. Skipping file since it is not a video.");
            return null;
        }
    }

    public new class Result : BaseProcessor.Result
    {
        public NzbFile NzbFile { get; init; } = null!;
        public string FileName { get; init; } = null!;
        public long FileSize { get; init; }
    }
}