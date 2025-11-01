namespace PeExtensions.FamDocument;

/// <summary>
///     A type-safe wrapper around a Revit Document that is guaranteed to be a valid family document.
///     All validation checks are performed at construction time, ensuring type safety throughout the codebase.
/// </summary>
public readonly struct FamilyDocument {
    private readonly Document _document;

    /// <summary>
    ///     Creates a new FamilyDocument wrapper, validating that the document is a family document
    ///     with a valid FamilyManager.
    /// </summary>
    /// <param name="document">The Revit document to wrap</param>
    /// <exception cref="ArgumentException">Thrown when the document is not a family document</exception>
    /// <exception cref="InvalidOperationException">Thrown when the FamilyManager is null</exception>
    public FamilyDocument(Document document) {
        if (!document.IsFamilyDocument)
            throw new ArgumentException("Document is not a family document.", nameof(document));
        if (document.FamilyManager is null)
            throw new InvalidOperationException("Family document's FamilyManager is null.");

        _document = document;
    }

    /// <summary>
    ///     Gets the underlying Revit Document.
    /// </summary>
    public Document Document => _document;

    /// <summary>
    ///     Implicit conversion to Document for seamless integration with existing Revit API methods.
    /// </summary>
    public static implicit operator Document(FamilyDocument familyDocument) => familyDocument._document;

    // Forward common Document properties for seamless usage
    public FamilyManager FamilyManager => _document.FamilyManager;
    public Family OwnerFamily => _document.OwnerFamily;
    public string PathName => _document.PathName;

    // Forward common Document methods
    public void SaveAs(string filePath, SaveAsOptions options) => _document.SaveAs(filePath, options);
    public bool Close(bool save) => _document.Close(save);
    public Family LoadFamily(Document doc, IFamilyLoadOptions options) => _document.LoadFamily(doc, options);
    public Units GetUnits() => _document.GetUnits();
}

