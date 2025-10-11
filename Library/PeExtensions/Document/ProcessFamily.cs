public static class DocumentProcessFamily {
    public static Document ProcessFamily(this Document famDoc, params Action<Document>[] callbacks) {
        if (!famDoc.IsFamilyDocument) throw new ArgumentException("Document is not a family document.");
        if (famDoc.FamilyManager is null)
            throw new InvalidOperationException("Family documents FamilyManager is null.");

        using var transFamily = new Transaction(famDoc, "Edit Family Document");
        _ = transFamily.Start();
        foreach (var callback in callbacks) callback(famDoc);
        _ = transFamily.Commit();
        return famDoc;
    }

    public static Document SaveFamily(
        this Document famDoc,
        string saveLocation
    ) {
        if (!famDoc.IsFamilyDocument) throw new ArgumentException("Document is not a family document.");
        if (famDoc.FamilyManager is null)
            throw new InvalidOperationException("Family documents FamilyManager is null.");

        if (saveLocation is null) return famDoc;
        if (!Directory.Exists(saveLocation)) _ = Directory.CreateDirectory(saveLocation);

        var family = famDoc.OwnerFamily;
        var familyFileName = $"{family.Name}.rfa";
        var fullSavePath = Path.Combine(saveLocation, familyFileName);

        var saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
        famDoc.SaveAs(fullSavePath, saveOptions);
        return famDoc;
    }

    public static Family LoadAndCloseFamily(this Document famDoc, Document doc, IFamilyLoadOptions options) {
        if (!famDoc.IsFamilyDocument) throw new ArgumentException("Document is not a family document.");
        if (famDoc.FamilyManager is null)
            throw new InvalidOperationException("Family documents FamilyManager is null.");

        var family = famDoc.LoadFamily(doc, options);
        var closed = famDoc.Close(false);
        return closed
            ? family
            : throw new InvalidOperationException("Failed to close family document after load error.");
    }
}