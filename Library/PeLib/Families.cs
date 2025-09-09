namespace PeLib;

public class Families {
    public static Result<Family> EditAndLoad(Document doc,
        Family family,
        params Action<Document>[] callbacks) {
        var famDoc = doc.EditFamily(family);
        if (!famDoc.IsFamilyDocument) return new ArgumentException("Document is not a family document.");
        if (famDoc.FamilyManager is null)
            return new InvalidOperationException("Family documents FamilyManager is null.");

        using var transFamily = new Transaction(famDoc, "Edit Family Document");
        _ = transFamily.Start();
        foreach (var callback in callbacks) callback(famDoc);
        _ = transFamily.Commit();


        var fam = famDoc.LoadFamily(doc, new EditAndLoadFamilyOptions());
        if (fam is null) return new InvalidOperationException("Failed to load family after edit.");
        var closed = famDoc.Close(false);
        if (!closed) return new InvalidOperationException("Failed to close family document after load error.");
        return fam;
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