namespace PeRevitInit;

internal class CommandAvailability : IExternalCommandAvailability {
    public bool IsCommandAvailable(
        UIApplication applicationData,
        CategorySet selectedCategories
    ) {
        var result = false;
        var activeDoc = applicationData.ActiveUIDocument;
        if (activeDoc != null && activeDoc.Document != null) result = true;

        return result;
    }
}