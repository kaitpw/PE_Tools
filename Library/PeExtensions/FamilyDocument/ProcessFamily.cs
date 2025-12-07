using UIFrameworkServices;

namespace PeExtensions.FamDocument;

public static class FamilyDocumentProcessFamily {
    public static FamilyDocument GetFamilyDocument(this Document doc, Family family = null) {
        if (doc.IsFamilyDocument) return new FamilyDocument(doc);
        if (family == null) throw new ArgumentNullException(nameof(family));
        var famDoc = doc.EditFamily(family);
        return new FamilyDocument(famDoc);
    }

    /// <summary>
    ///     Ensures the family has at least one type. If no types exist, creates a default type.
    /// </summary>
    public static FamilyDocument EnsureDefaultType(this FamilyDocument famDoc) {
        var fm = famDoc.FamilyManager;

        var emptyNameFamilyType =
            fm.Types.Cast<FamilyType>().FirstOrDefault(type => string.IsNullOrWhiteSpace(type.Name));

        var hasOnlyOneEmptyName = fm.Types.Size == 1 && emptyNameFamilyType != null;
        if (fm.Types.Size != 0 && !hasOnlyOneEmptyName) return famDoc;

        using var trans = new Transaction(famDoc, "Create Default Family Type");
        _ = trans.Start();
        var defaultType = fm.NewType("Default");
        fm.CurrentType = defaultType;
        _ = trans.Commit();

        return famDoc;
    }

    public static FamilyDocument ProcessWithoutSaving(this FamilyDocument famDoc,
        params Action<FamilyDocument>[] callbacks) {
        foreach (var callback in callbacks) {
            using var trans = new Transaction(famDoc, "Edit Family Document");
            _ = trans.Start();
            callback(new FamilyDocument(famDoc));
            _ = trans.Commit();
        }

        return famDoc;
    }

    /// <summary>
    ///     Saves a variant of the family document to a given path.
    /// </summary>
    public static FamilyDocument ProcessAndSaveVariant(
        this FamilyDocument famDoc,
        string outputDirectory,
        string suffix,
        Action<FamilyDocument> callback
    ) {
        var originalFamPath = famDoc.PathName;
        var originalFamilyName = famDoc.Document.Title;
        var createdFamPath = Path.Combine(outputDirectory, $"{originalFamilyName}{suffix}.rfa");

        // First Assimilate the transaction group to "close" transaction-related stuff
        using var tGroup = new TransactionGroup(famDoc, "Process And Save Variant");
        _ = tGroup.Start();
        callback.Invoke(famDoc);
        _ = tGroup.Assimilate();

        // Then save the new document, this turns the current document into the new document
        famDoc.SaveAs(createdFamPath,
            new SaveAsOptions { OverwriteExistingFile = true, Compact = true, MaximumBackups = 1 });

        // Undo the transaction group to revert to old file state
        QuickAccessToolBarService.performMultipleUndoRedoOperations(true, 1);

        // make the current document the original document again
        famDoc.SaveAs(originalFamPath, new SaveAsOptions { OverwriteExistingFile = true, Compact = true });
        return famDoc;
    }


    public static FamilyDocument SaveToLocations(
        this FamilyDocument famDoc,
        Func<FamilyDocument, List<string>> getSaveLocations
    ) {
        var saveLocations = getSaveLocations(famDoc);
        if (saveLocations.Count == 0 || saveLocations.Count(l => l == null) > 0) return famDoc;
        foreach (var location in saveLocations) {
            if (location == null) continue;
            if (!Directory.Exists(location)) _ = Directory.CreateDirectory(location);

            var family = famDoc.OwnerFamily;
            var familyFileName = $"{family.Name}.rfa";
            var fullSavePath = Path.Combine(location, familyFileName);

            var saveOptions = new SaveAsOptions { OverwriteExistingFile = true, Compact = true, MaximumBackups = 1 };
            famDoc.SaveAs(fullSavePath, saveOptions);
        }

        return famDoc;
    }

    public static Family LoadAndClose(this FamilyDocument famDoc, Document doc, IFamilyLoadOptions options) {
        var family = famDoc.LoadFamily(doc, options);
        var closed = famDoc.Close(false);
        return closed
            ? family
            : throw new InvalidOperationException("Failed to close family document after load error.");
    }
}