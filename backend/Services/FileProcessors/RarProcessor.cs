using System.Text.RegularExpressions;
using NzbWebDAV.Clients;
using NzbWebDAV.Exceptions;
using NzbWebDAV.Extensions;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using Usenet.Nzb;
using Usenet.Yenc;

namespace NzbWebDAV.Services.FileProcessors;

public class RarProcessor(NzbFile nzbFile, UsenetProviderManager usenet, CancellationToken ct) : BaseProcessor
{
    public static bool CanProcess(NzbFile file)
    {
        return IsRarFile(file.GetSubjectFileName());
    }

    public override async Task<BaseProcessor.Result> ProcessAsync()
    {
        try
        {
            await using var stream = await usenet.GetFileStreamAsync(nzbFile, concurrentConnections: 1, ct);
            var filename = GetFileName(stream.FirstYencHeader);
            return new Result()
            {
                NzbFile = nzbFile,
                PartSize = stream.Length,
                ArchiveName = GetArchiveName(filename),
                PartNumber = GetPartNumber(filename),
                StoredFileSegments = GetRarHeaders(stream)
                    .Select(x => new StoredFileSegment()
                    {
                        PathWithinArchive = x.GetFileName(),
                        Offset = x.GetDataStartPosition(),
                        ByteCount = x.GetAdditionalDataSize(),
                    }).ToArray(),
            };
        }
        catch (CryptographicException ex)
        {
            throw new PasswordProtectedRarException("Password-protected RARs are not supported");
        }
    }

    private static bool IsRarFile(string? filename)
    {
        if (string.IsNullOrEmpty(filename)) return false;
        return filename.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(filename, @"\.r(\d+)$", RegexOptions.IgnoreCase);
    }

    private string GetFileName(YencHeader? header)
    {
        // try getting the filename from subject and header
        var fromSubject = nzbFile.GetSubjectFileName();
        var fromHeader = header?.FileName ?? "";

        // if only one of them ends in .rar, or .rXX, return that one
        if (IsRarFile(fromSubject) && !IsRarFile(fromHeader)) return fromSubject;
        if (IsRarFile(fromHeader) && !IsRarFile(fromSubject)) return fromHeader;

        // if both end in .rar or .rXX, return whichever has a higher part number
        return new[] { fromSubject, fromHeader }.MaxBy(GetPartNumber)!;
    }

    private string GetArchiveName(string filename)
    {
        // remove the .rar extension and remove the .partXX if it exists
        var sansExtension = Path.GetFileNameWithoutExtension(filename);
        sansExtension = Regex.Replace(sansExtension, @"\.part\d+$", "");
        return sansExtension;
    }

    private int GetPartNumber(string filename)
    {
        // handle the `.partXXX.rar` format
        var partMatch = Regex.Match(filename, @"\.part(\d+)\.rar$", RegexOptions.IgnoreCase);
        if (partMatch.Success) return int.Parse(partMatch.Groups[1].Value);

        // handle the `.rXXX` format
        var rMatch = Regex.Match(filename, @"\.r(\d+)$", RegexOptions.IgnoreCase);
        if (rMatch.Success) return int.Parse(rMatch.Groups[1].Value);

        // handle the `.rar` format.
        return -1;
    }

    private List<IRarHeader> GetRarHeaders(Stream stream)
    {
        var headerFactory = new RarHeaderFactory(StreamingMode.Seekable, new ReaderOptions());
        var headers = new List<IRarHeader>();
        foreach (var header in headerFactory.ReadHeaders(stream))
        {
            // we only care about file headers
            if (header.HeaderType != HeaderType.File || header.IsDirectory() || header.GetFileName() == "QO") continue;

            // we only support stored files (compression method m0).
            if (header.GetCompressionMethod() != 0)
                throw new UnsupportedRarCompressionMethodException(
                    "Only rar files with compression method m0 are supported.");

            // add the headers
            headers.Add(header);

            // break early if we think there are no more headers.
            var left = stream.Length - (header.GetDataStartPosition() + header.GetCompressedSize());
            if (left < 1000) break;
        }

        return headers;
    }

    public new class Result : BaseProcessor.Result
    {
        public NzbFile NzbFile { get; init; } = null!;
        public long PartSize { get; init; }
        public string ArchiveName { get; init; } = null!;
        public int PartNumber { get; init; }
        public StoredFileSegment[] StoredFileSegments { get; init; } = [];
    }

    public class StoredFileSegment
    {
        public string PathWithinArchive { get; init; } = null!;
        public long Offset { get; init; }
        public long ByteCount { get; init; }
    }
}