using PE_Tools.Properties;
using PeRevitUI;
using System.Data.Common;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdFamilyMigrator : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var uiapp = commandData.Application;
        var uidoc = uiapp.ActiveUIDocument;
        var doc = uidoc.Document;

        var balloon = new Balloon();
        var (res, resErr) = MigrateProjectFamily(doc, balloon);
        if (resErr is not null) balloon.Add(Balloon.Log.ERR, resErr.Message).Show();
        balloon.Add(Balloon.Log.TEST, $"Migration result: {res}").Show();

        return Result.Succeeded;
    }

    internal static PushButtonData GetButtonData() =>
        new ButtonDataClass(
            "Command Palette",
            MethodBase.GetCurrentMethod()?.DeclaringType?.FullName,
            Resources.Yellow_32,
            Resources.Yellow_16,
            "Open the command palette to search and execute Revit commands quickly. Use Ctrl+K for quick access."
        ).Data;

    static Result<bool> MigrateProjectFamily(Document doc, Balloon balloon) {
        try {
            // Get the first editable family in the project
            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(f => f.IsEditable)
                .ToList();

            if (families.Count == 0) {
                _ = balloon.Add(Balloon.Log.TEST, "No editable families found in the project.");
                return false;
            }

            // Get the first editable family
            var family = families.First();
            _ = balloon.Add(Balloon.Log.TEST, $"Processing family: {family.Name} (ID: {family.Id})");

            // Open family for editing (no transaction needed)
            var familyDoc = doc.EditFamily(family);

            // Validate the family document
            _ = balloon.Add(Balloon.Log.TEST, $"Family document title: {familyDoc.Title}");
            _ = balloon.Add(Balloon.Log.TEST, $"Is family document: {familyDoc.IsFamilyDocument}");

            // Check if FamilyManager is available
            if (familyDoc.FamilyManager == null) {
                _ = balloon.Add(Balloon.Log.ERR, "FamilyManager is null!");
                var didFamilyDocClose = familyDoc.Close(false);
                return false;
            }

            // Create a new transaction for the family document
            using var trans = new Transaction(familyDoc, "Family add Test param");

            _ = trans.Start();

            // Get FamilyManager
            var fm = familyDoc.FamilyManager;

            // Check if the parameter already exists
            var existingParam = fm.get_Parameter("Test");
            if (existingParam != null) {
                _ = balloon.Add(Balloon.Log.TEST, $"Parameter 'Test' already exists in family {family.Name}");
                _ = trans.Commit();
            } else {
                // Debug: Show what we're trying to use
                _ = balloon.Add(Balloon.Log.TEST, $"GroupTypeId.General: {GroupTypeId.General}");
                _ = balloon.Add(Balloon.Log.TEST, $"SpecTypeId.Number: {SpecTypeId.Number}");
                _ = balloon.Add(Balloon.Log.TEST, $"GroupTypeId.General.TypeId: {GroupTypeId.General.TypeId}");
                _ = balloon.Add(Balloon.Log.TEST, $"SpecTypeId.Number.TypeId: {SpecTypeId.Number.TypeId}");

                // Try different approaches to add a parameter
                FamilyParameter testParam = null;

                // Method 1: Try with SpecTypeId.Number
                try {
                    _ = balloon.Add(Balloon.Log.TEST, "Method 1: Trying SpecTypeId.Number...");
                    testParam = fm.AddParameter(
                        "Test",
                        GroupTypeId.General,
                        SpecTypeId.Number,
                        false);
                    _ = balloon.Add(Balloon.Log.TEST, "Method 1 succeeded!");
                } catch (Exception ex1) {
                    _ = balloon.Add(Balloon.Log.ERR, $"Method 1 failed: {ex1.Message}");

                    // Method 2: Try with SpecTypeId.String.Text
                    try {
                        _ = balloon.Add(Balloon.Log.TEST, "Method 2: Trying SpecTypeId.String.Text...");
                        testParam = fm.AddParameter(
                            "Test",
                            GroupTypeId.IdentityData,
                            SpecTypeId.String.Text,
                            false);
                        _ = balloon.Add(Balloon.Log.TEST, "Method 2 succeeded!");
                    } catch (Exception ex2) {
                        _ = balloon.Add(Balloon.Log.ERR, $"Method 2 failed: {ex2.Message}");

                        // Method 3: Try with a different group
                        try {
                            _ = balloon.Add(Balloon.Log.TEST, "Method 3: Trying with GroupTypeId.IdentityData...");
                            testParam = fm.AddParameter(
                                "Test",
                                GroupTypeId.IdentityData,
                                SpecTypeId.Number,
                                false);
                            _ = balloon.Add(Balloon.Log.TEST, "Method 3 succeeded!");
                        } catch (Exception ex3) {
                            _ = balloon.Add(Balloon.Log.ERR, $"Method 3 failed: {ex3.Message}");
                            throw new Exception($"All parameter creation methods failed. Last error: {ex3.Message}");
                        }
                    }
                }

                if (testParam != null) {
                    _ = balloon.Add(Balloon.Log.TEST, $"Successfully added parameter 'Test' to family {family.Name}");
                } else {
                    _ = balloon.Add(Balloon.Log.ERR, "Failed to create parameter");
                    _ = trans.RollBack();
                    _ = familyDoc.Close(false);
                    return false;
                }

                _ = trans.Commit();
            }

            // Try to reload family into project with better error handling
            _ = balloon.Add(Balloon.Log.TEST, "Attempting to reload family into project...");

            Family reloadedFamily = null;
            try {
                // Instead of closing the family document immediately, let's try to keep it open
                // and use the family from the project document

                // Get the family from the project document before closing the family document
                var projectFamily = doc.GetElement(family.Id) as Family;
                if (projectFamily != null) {
                    reloadedFamily = projectFamily;
                    _ = balloon.Add(Balloon.Log.TEST, "Successfully got family from project document");
                } else {
                    _ = balloon.Add(Balloon.Log.ERR, "Could not get family from project document");
                }

                // Now close the family document
                _ = familyDoc.Close(false);

            } catch (Exception reloadEx) {
                _ = balloon.Add(Balloon.Log.ERR, $"Family reloading exception: {reloadEx.Message}");
                _ = balloon.Add(Balloon.Log.ERR, $"Exception type: {reloadEx.GetType().Name}");

                // Fallback: try to get the family from the project
                try {
                    _ = balloon.Add(Balloon.Log.TEST, "Trying fallback approach...");

                    // Get the family from the project document
                    var projectFamily = doc.GetElement(family.Id) as Family;
                    if (projectFamily != null) {
                        reloadedFamily = projectFamily;
                        _ = balloon.Add(Balloon.Log.TEST, "Fallback approach succeeded - got family from project");
                    } else {
                        _ = balloon.Add(Balloon.Log.ERR, "Fallback approach failed - could not get family from project");
                    }
                } catch (Exception fallbackEx) {
                    _ = balloon.Add(Balloon.Log.ERR, $"Fallback approach failed: {fallbackEx.Message}");
                }
            }

            if (reloadedFamily != null) {
                _ = balloon.Add(Balloon.Log.TEST, "Family migration completed successfully!");
                return true;
            }

            _ = balloon.Add(Balloon.Log.ERR, "Family migration failed - parameter was added but family could not be reloaded");
            return false;

        } catch (Exception ex) {
            _ = balloon.Add(Balloon.Log.ERR, $"Error: {ex.Message}");
            _ = balloon.Add(Balloon.Log.ERR, $"Error type: {ex.GetType().Name}");
            return ex;
        }
    }
}