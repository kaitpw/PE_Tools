namespace PeUtils;

public class Files {
    /// <summary> Computes the SHA256 hash of a file </summary>
    public static string ComputeFileHashFromPath(string filePath) {
        using var stream = File.OpenRead(filePath);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha.ComputeHash(stream);
        return Convert.ToBase64String(hashBytes);
    }

    public static string ComputeFileHashFromText(string fileText)
    {
        if (fileText == null) throw new ArgumentNullException(nameof(fileText));

        var bytes = System.Text.Encoding.UTF8.GetBytes(fileText);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}