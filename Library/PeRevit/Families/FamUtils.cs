namespace PeRevit.Families;

public class FamUtils {
    // TODO: add support for processing the current family dopcument
    /// <summary>
    ///     Edits a family document and loads the family back into the document (without saving it as a file).
    /// </summary>
    /// <param name="doc">The main model document (not the family document)</param>
    /// <param name="family">The family to edit</param>
    /// <param name="callbacks">The callbacks to execute. callbacks operate on the family document and return a result</param>
    /// <returns>The loaded family</returns>
    public static Family EditAndLoad(Document doc,
        Family family,
        params Action<Document>[] callbacks) {
        var famDoc = doc.EditFamily(family);
        if (!famDoc.IsFamilyDocument) throw new ArgumentException("Document is not a family document.");
        if (famDoc.FamilyManager is null)
            throw new InvalidOperationException("Family documents FamilyManager is null.");

        using var transFamily = new Transaction(famDoc, "Edit Family Document");
        _ = transFamily.Start();
        foreach (var callback in callbacks) callback(famDoc);
        _ = transFamily.Commit();

        var fam = famDoc.LoadFamily(doc, new EditAndLoadFamilyOptions())
                  ?? throw new InvalidOperationException("Failed to load family after edit.");
        var closed = famDoc.Close(false);
        return closed
            ? fam
            : throw new InvalidOperationException("Failed to close family document after load error.");
    }

    /// <summary>
    ///     Edits a family document, saves it to the specified location, and loads the family back into the document.
    /// </summary>
    /// <param name="doc">The main model document (not the family document)</param>
    /// <param name="family">The family to edit</param>
    /// <param name="saveLocation">Directory path where to save the processed family file</param>
    /// <param name="callbacks">The callbacks to execute. callbacks operate on the family document and return a result</param>
    /// <returns>The loaded family</returns>
    public static Family EditAndLoadAndSave(Document doc,
        Family family,
        string saveLocation,
        params Action<Document>[] callbacks) {
        var famDoc = doc.EditFamily(family);
        if (!famDoc.IsFamilyDocument) throw new ArgumentException("Document is not a family document.");
        if (famDoc.FamilyManager is null)
            throw new InvalidOperationException("Family documents FamilyManager is null.");

        using var transFamily = new Transaction(famDoc, "Edit Family Document");
        _ = transFamily.Start();
        foreach (var callback in callbacks) callback(famDoc);
        _ = transFamily.Commit();

        // Save the family to the specified location
        if (!Directory.Exists(saveLocation)) _ = Directory.CreateDirectory(saveLocation);

        var familyFileName = $"{family.Name}.rfa";
        var fullSavePath = Path.Combine(saveLocation, familyFileName);

        var saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
        famDoc.SaveAs(fullSavePath, saveOptions);

        var fam = famDoc.LoadFamily(doc, new EditAndLoadFamilyOptions())
                  ?? throw new InvalidOperationException("Failed to load family after edit.");
        var closed = famDoc.Close(false);
        return closed
            ? fam
            : throw new InvalidOperationException("Failed to close family document after load error.");
    }

    public static Family EditOpenFamily(Document famDoc, params Action<Document>[] callbacks) {
        using var transFamily = new Transaction(famDoc, "Edit Family Document");
        _ = transFamily.Start();
        foreach (var callback in callbacks) callback(famDoc);
        _ = transFamily.Commit();
        return famDoc.OwnerFamily;
    }
}

internal class EditAndLoadFamilyOptions : IFamilyLoadOptions {
    public bool OnFamilyFound(
        bool familyInUse,
        out bool overwriteParameterValues) {
        overwriteParameterValues = true;
        return true;
    }

    public bool OnSharedFamilyFound(
        Family sharedFamily,
        bool familyInUse,
        out FamilySource source,
        out bool overwriteParameterValues) {
        source = FamilySource.Project;
        overwriteParameterValues = true;
        return true;
    }
}