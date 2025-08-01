using NzbWebDAV.Clients;
using NzbWebDAV.Extensions;
using Usenet.Nzb;

namespace NzbWebDAV.Services.FileProcessors;

public class FileProcessor(NzbFile nzbFile, UsenetProviderManager usenet, CancellationToken ct) : BaseProcessor
{
    public static bool CanProcess(NzbFile file)
    {
        // skip par2 files
        return !file.GetSubjectFileName().EndsWith(".par2", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<BaseProcessor.Result> ProcessAsync()
    {
        var firstSegment = nzbFile.Segments[0].MessageId.Value;
        var header = await usenet.GetSegmentYencHeaderAsync(firstSegment, default);
        var subjectFilename = nzbFile.GetSubjectFileName();
        var fileName = GetFileName(subjectFilename, header.FileName);

        return new Result()
        {
            NzbFile = nzbFile,
            FileName = fileName,
            FileSize = header.FileSize,
        };
    }

    private static string GetFileName(string subjectFilename, string headerFilename)
    {
        // prioritize the subject filename with fallback to header filename
        // unless the header filename has an `.mkv` extension while the subject filename doesn't.
        subjectFilename = Path.GetFileName(subjectFilename);
        headerFilename = Path.GetFileName(headerFilename);
        if (subjectFilename != "" && Path.GetExtension(subjectFilename) == ".mkv") return subjectFilename;
        if (headerFilename != "" && Path.GetExtension(headerFilename) == ".mkv") return headerFilename;
        return subjectFilename != "" ? subjectFilename : headerFilename;
    }

    public new class Result : BaseProcessor.Result
    {
        public NzbFile NzbFile { get; init; } = null!;
        public string FileName { get; init; } = null!;
        public long FileSize { get; init; }
    }
}