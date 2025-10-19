// Fixes CS0138 by using 'using static' for the Filters type

namespace PeRevit.Utils;

internal class Utils {
    // Helper method to get current Revit version
    public static string GetRevitVersion() {
#if REVIT2023
return "2023";
#elif REVIT2024
return "2024";
#elif REVIT2025
        return "2025";
#elif REVIT2026
return "2026";
#else
                return null;
#endif
    }

    public static DefinitionFile MakeTempSharedParamTxt(Document famDoc) {
        var app = famDoc.Application;
        var tempSharedParamFile = Path.GetTempFileName() + ".txt";
        using (File.Create(tempSharedParamFile)) { } // Create empty file

        app.SharedParametersFilename = tempSharedParamFile;
        try {
            return app.OpenSharedParameterFile();
        } catch (Exception ex) {
            throw new Exception($"Failed to create temp shared parameter file: {ex.Message}");
        }
    }
}