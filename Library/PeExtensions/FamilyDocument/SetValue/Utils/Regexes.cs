using System.Globalization;
using System.Text.RegularExpressions;

namespace PeExtensions.FamDocument.SetValue.Utils;

public static class Regexes {
    public static bool CanExtractInteger(string input) {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return false;
        var firstChar = trimmed[0];
        if (!char.IsDigit(firstChar) && firstChar != '-') return false;

        var numericString = Regex.Match(trimmed, @"^-?\d+").Value;
        return !string.IsNullOrWhiteSpace(numericString)
               && int.TryParse(
                   numericString,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out _
               );
    }

    // Check if string can be converted to double (similar rules)
    public static bool CanExtractDouble(string input) {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var trimmed = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return false;
        var firstChar = trimmed[0];
        if (!char.IsDigit(firstChar) && firstChar != '-' && firstChar != '.') return false;

        var numericString = Regex.Match(trimmed, @"^-?\d*\.?\d+").Value;
        return !string.IsNullOrWhiteSpace(numericString)
               && double.TryParse(
                   numericString,
                   NumberStyles.Float | NumberStyles.AllowLeadingSign,
                   CultureInfo.InvariantCulture,
                   out _
               );
    }

    public static int ExtractInteger(string input) {
        var trimmed = input.Trim();
        var match = Regex.Match(trimmed, @"^-?\d+");

        return !match.Success
            ? throw new ArgumentException(
                $"No valid integer found at the start of string: {input}",
                nameof(input)
            )
            : int.Parse(match.Value, CultureInfo.InvariantCulture);
    }

    public static double ExtractDouble(string input) {
        var trimmed = input.Trim();
        var match = Regex.Match(trimmed, @"^-?\d*\.?\d+");

        return !match.Success
            ? throw new ArgumentException(
                $"No valid numeric value found at the start of string: {input}",
                nameof(input)
            )
            : double.Parse(match.Value, CultureInfo.InvariantCulture);
    }
}