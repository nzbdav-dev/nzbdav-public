using System.Text.RegularExpressions;
using NzbWebDAV.Clients;
using Usenet.Nzb;

namespace NzbWebDAV.Extensions;

public static class NzbFileExtensions
{
    public static string[] GetOrderedSegmentIds(this NzbFile file)
    {
        return file.Segments
            .OrderBy(x => x.Number)
            .Select(x => x.MessageId.Value)
            .ToArray();
    }

    public static async Task<string> GetFileName
    (
        this NzbFile file,
        UsenetStreamingClient client,
        CancellationToken ct = default
    )
    {
        // prioritize subject filename with fallback to header filename.
        // unless header filename has a valid extension and subject filename doesn't.
        var subjectFilename = file.GetSubjectFileName();
        if (subjectFilename == "") return await file.GetHeaderFileName(client, ct);
        var subjectFilenameExtension = Path.GetExtension(subjectFilename).TrimStart('.');
        if (subjectFilenameExtension.Length is >= 2 and <= 4) return subjectFilename;
        var headerFilename = await file.GetHeaderFileName(client, ct);
        var headerFilenameExtension = Path.GetExtension(headerFilename).TrimStart('.');
        if (headerFilenameExtension.Length is >=2 and <=4) return headerFilename;
        return subjectFilename;
    }

    public static async Task<string> GetHeaderFileName
    (
        this NzbFile file,
        UsenetStreamingClient client,
        CancellationToken ct = default
    )
    {
        var firstSegment = file.Segments[0].MessageId.Value;
        var header = await client.GetSegmentYencHeaderAsync(firstSegment, default);
        return header.FileName;
    }

    public static string GetSubjectFileName(this NzbFile file)
    {
        return GetFirstValidNonEmptyFilename(
            () => TryParseSubjectFilename1(file),
            () => TryParseSubjectFilename2(file)
        );
    }

    private static string TryParseSubjectFilename1(this NzbFile file)
    {
        // The most common format is when filename appears in double quotes
        // example: `[1/8] - "file.mkv" yEnc 12345 (1/54321)`
        var match = Regex.Match(file.Subject, "\\\"(.*)\\\"");
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string TryParseSubjectFilename2(this NzbFile file)
    {
        // Otherwise, use sabnzbd's regex
        // https://github.com/sabnzbd/sabnzbd/blob/b6b0d10367fd4960bad73edd1d3812cafa7fc002/sabnzbd/nzbstuff.py#L106
        var match = Regex.Match(file.Subject, @"\b([\w\-+()' .,]+(?:\[[\w\-\/+()' .,]*][\w\-+()' .,]*)*\.[A-Za-z0-9]{2,4})\b");
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string GetFirstValidNonEmptyFilename(params Func<string>[] funcs)
    {
        return funcs
            .Select(x => x.Invoke())
            .Where(x => x == Path.GetFileName(x))
            .FirstOrDefault(x => x != "") ?? "";
    }
}