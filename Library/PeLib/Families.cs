namespace PeLib;

public class Families {
    /// <summary>
    ///     Edits a family document and loads the family back into the document (without saving it as a file).
    /// </summary>
    /// <param name="doc">The main model document (not the family document)</param>
    /// <param name="family">The family to edit</param>
    /// <param name="callbacks">The callbacks to execute. callbacks operate on the family document and return a result</param>
    /// <returns>The loaded family</returns>
    public static (Family, OperationResults) EditAndLoad(Document doc,
        Family family,
        params Action<Document, OperationResults>[] callbacks) {
        var famDoc = doc.EditFamily(family);
        if (!famDoc.IsFamilyDocument) throw new ArgumentException("Document is not a family document.");
        if (famDoc.FamilyManager is null)
            throw new InvalidOperationException("Family documents FamilyManager is null.");

        using var transFamily = new Transaction(famDoc, "Edit Family Document");
        _ = transFamily.Start();
        var resultAggregator = new OperationResults();
        foreach (var callback in callbacks) callback(famDoc, resultAggregator);
        _ = transFamily.Commit();


        var fam = famDoc.LoadFamily(doc, new EditAndLoadFamilyOptions());
        if (fam is null) throw new InvalidOperationException("Failed to load family after edit.");
        var closed = famDoc.Close(false);
        if (!closed) throw new InvalidOperationException("Failed to close family document after load error.");
        return (fam, resultAggregator);
    }

    public class OperationResults {
        private List<(string Operation, Result<object> Result)> Results { get; } = [];

        public void Add(string operation, Result<object> result) => this.Results.Add((operation, result));
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