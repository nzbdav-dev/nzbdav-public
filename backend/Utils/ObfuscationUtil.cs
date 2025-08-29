using System.Text.RegularExpressions;

namespace NzbWebDAV.Utils;

public static class ObfuscationUtil
{
    /// <summary>
    /// Checks if a filename is likely obfuscated.
    /// source: https://github.com/sabnzbd/sabnzbd/blob/64034c5636563b66360aa9dfc1a0b624f4db5cc3/sabnzbd/deobfuscate_filenames.py#L105
    /// </summary>
    /// <param name="filename">The filename or full path to check.</param>
    /// <returns>True if the filename is likely obfuscated, otherwise False.</returns>
    public static bool IsProbablyObfuscated(string filename)
    {
        // Use Path.GetFileNameWithoutExtension for .NET
        var fileBaseName = Path.GetFileNameWithoutExtension(filename);

        // ---
        // First: patterns that are certainly obfuscated

        // ...blabla.H.264/b082fa0beaa644d3aa01045d5b8d0b36.mkv is certainly obfuscated
        // Obfuscated: 32 hex digits
        if (Regex.IsMatch(fileBaseName, @"^[a-f0-9]{32}$"))
        {
            return true;
        }

        // 0675e29e9abfd2.f7d069dab0b853283cc1b069a25f82.6547
        // Obfuscated: starting with 40+ lower case hex digits and/or dots
        if (Regex.IsMatch(fileBaseName, @"^[a-f0-9.]{40,}$"))
        {
            return true;
        }

        // "[BlaBla] something [More] something 5937bc5e32146e.bef89a622e4a23f07b0d3757ad5e8a.a02b264e [Brrr]"
        // So: square brackets plus 30+ hex digit
        if (Regex.IsMatch(fileBaseName, @"[a-f0-9]{30}") && Regex.Matches(fileBaseName, @"\[\w+\]").Count >= 2)
        {
            return true;
        }

        // /some/thing/abc.xyz.a4c567edbcbf27.BLA is certainly obfuscated
        // Obfuscated: starts with 'abc.xyz'
        if (Regex.IsMatch(fileBaseName, @"^abc\.xyz"))
        {
            return true;
        }

        // ---
        // Then: patterns that are not obfuscated but typical, clear names

        // These are signals for the obfuscation versus non-obfuscation
        var decimals = fileBaseName.Count(char.IsDigit);
        var upperChars = fileBaseName.Count(char.IsUpper);
        var lowerChars = fileBaseName.Count(char.IsLower);
        var spacesDots = fileBaseName.Count(c => c is ' ' or '.' or '_'); // space-like symbols

        // Example: "Great Distro"
        if (upperChars >= 2 && lowerChars >= 2 && spacesDots >= 1)
        {
            return false;
        }

        // Example: "this is a download"
        if (spacesDots >= 3)
        {
            return false;
        }

        // Example: "Beast 2020"
        if ((upperChars + lowerChars >= 4) && decimals >= 4 && spacesDots >= 1)
        {
            return false;
        }

        // Example: "Catullus", starts with a capital, and most letters are lower case
        // Not obfuscated: starts with a capital, and most letters are lower case
        if (fileBaseName.Length > 0 && char.IsUpper(fileBaseName[0]) && lowerChars > 2 &&
            (double)upperChars / lowerChars <= 0.25)
        {
            return false;
        }

        // Finally: default to obfuscated
        return true;
    }
}