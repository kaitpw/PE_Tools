using System.Globalization;
using System.Text.RegularExpressions;

namespace PeExtensions.FamManager.SetValue.Utils;

public static class Regexes
{
    public static bool CanExtractInteger(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return false;
        var firstChar = trimmed[0];
        if (!char.IsDigit(firstChar) && firstChar != '-') return false;

        var numericString = CanExtractIntRegexCompiled.Match(trimmed).Value;
        return !string.IsNullOrWhiteSpace(numericString)
               && int.TryParse(
                   numericString,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out _
               );
    }

    // Check if string can be converted to double (similar rules)
    public static bool CanExtractDouble(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return false;
        var firstChar = trimmed[0];
        if (!char.IsDigit(firstChar) && firstChar != '-' && firstChar != '.') return false;

        var numericString = CanExtractDoubleRegexCompiled.Match(trimmed).Value;
        return !string.IsNullOrWhiteSpace(numericString)
               && double.TryParse(
                   numericString,
                   NumberStyles.Float | NumberStyles.AllowLeadingSign,
                   CultureInfo.InvariantCulture,
                   out _
               );
    }

    public static int ExtractInteger(string input)
    {
        var trimmed = input.Trim();
        var match = ExtractIntRegexCompiled.Match(trimmed);

        return !match.Success
            ? throw new ArgumentException(
                $"No valid integer found at the start of string: {input}",
                nameof(input)
            )
            : int.Parse(match.Value, CultureInfo.InvariantCulture);
    }

    public static double ExtractDouble(string input)
    {
        var trimmed = input.Trim();
        var match = ExtractDoubleRegexCompiled.Match(trimmed);

        return !match.Success
            ? throw new ArgumentException(
                $"No valid numeric value found at the start of string: {input}",
                nameof(input)
            )
            : double.Parse(match.Value, CultureInfo.InvariantCulture);
    }
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
    private static readonly Regex CanExtractDoubleRegexCompiled = new(@"^-?\d*\.?\d+", RegexOptions.Compiled);

    private static readonly Regex ExtractIntRegexCompiled = new(@"^-?\d+", RegexOptions.Compiled);

    private static readonly Regex ExtractDoubleRegexCompiled = new(@"^-?\d*\.?\d+", RegexOptions.Compiled);

    private static readonly Regex CanExtractIntRegexCompiled = new(@"^-?\d+", RegexOptions.Compiled);
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
}