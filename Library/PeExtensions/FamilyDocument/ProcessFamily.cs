using Microsoft.VisualBasic;
using PeRevit.Lib;

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
        
        if (fm.Types.Size == 0) {
            using var trans = new Transaction(famDoc, "Create Default Family Type");
            _ = trans.Start();
            try {
                _ = fm.NewType("Default");
                Debug.WriteLine($"[EnsureDefaultType] Created default type for family: {famDoc.Document.Title}");
            } catch (Exception ex) {
                Debug.WriteLine($"[EnsureDefaultType] Failed to create default type for family {famDoc.Document.Title}: {ex.Message}");
                throw;
            }
            _ = trans.Commit();
        }
        
        return famDoc;
    }

    public static FamilyDocument ProcessFamily(this FamilyDocument famDoc, params Action<FamilyDocument>[] callbacks) {
        foreach (var callback in callbacks) {
            using var trans = new Transaction(famDoc, "Edit Family Document");
            _ = trans.Start();
            callback(new FamilyDocument(famDoc));
            _ = trans.Commit();
            trans.Dispose();
        }

        return famDoc;
    }

    public static FamilyDocument SaveFamily(
        this FamilyDocument famDoc,
        Func<FamilyDocument, List<string>> getSaveLocations
    ) {
        var saveLocations = getSaveLocations(famDoc);
        if (saveLocations.Count == 0 || saveLocations.Count(l => l == null) > 0) return famDoc;
        foreach (var location in saveLocations) {
            if (!Directory.Exists(location)) _ = Directory.CreateDirectory(location);

            var family = famDoc.OwnerFamily;
            var familyFileName = $"{family.Name}.rfa";
            var fullSavePath = Path.Combine(location, familyFileName);

            var saveOptions = new SaveAsOptions { OverwriteExistingFile = true };
            famDoc.SaveAs(fullSavePath, saveOptions);
        }

        return famDoc;
    }

    public static Family LoadAndCloseFamily(this FamilyDocument famDoc, Document doc, IFamilyLoadOptions options) {
        var family = famDoc.LoadFamily(doc, options);
        var closed = famDoc.Close(false);
        return closed
            ? family
            : throw new InvalidOperationException("Failed to close family document after load error.");
    }
}