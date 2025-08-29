namespace NzbWebDAV.Extensions;

public static class ProgressExtensions
{
    public static Progress<int>? ToPercentage(this IProgress<int>? progress, int total)
    {
        return new Progress<int>(x => progress?.Report(x * 100 / total));
    }

    public static Progress<int>? Scale(this IProgress<int>? progress, int numerator, int denominator)
    {
        return new Progress<int>(x => progress?.Report(x * numerator / denominator));
    }

    public static Progress<int>? Offset(this IProgress<int>? progress, int offset)
    {
        return new Progress<int>(x => progress?.Report(x + offset));
    }
}