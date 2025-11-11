using AddinCmdPalette.Actions;
using AddinCmdPalette.Core;
using AddinCmdPalette.Services;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows.Input;
using PeServices.Storage;

namespace AddinCmdPalette.Families;

/// <summary>
///     Factory service for creating family palette instances
/// </summary>
public static class FamilyPaletteService {
    /// <summary>
    ///     Creates a family palette window configured for opening/editing Revit families
    /// </summary>
    public static SelectablePalette Create(
        UIApplication uiApp,
        Storage persistence
    ) {
        var doc = uiApp.ActiveUIDocument.Document;

        // Get all families in the document
        var families = new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .OrderBy(f => f.Name)
            .ToList();

        // Convert to ISelectableItem adapters
        var selectableItems = families
            .Select(family => new FamilyPaletteItem(family, doc))
            .Cast<ISelectableItem>()
            .ToList();

        // Create search filter service
        var searchService = new SearchFilterService(
            persistence,
            item => {
                if (item is FamilyPaletteItem familyItem)
                    return familyItem.Family.Id.ToString();
                return item.PrimaryText;
            },
            "FamilyPalette"
        );

        // Create actions
        var actions = new List<PaletteAction> {
            // Default action: Open family for editing (no modifiers)
            new() {
                Name = "Edit Family",
                ExecuteAsync = async item => {
                    if (item is FamilyPaletteItem familyItem) {
                        try {
                            var family = familyItem.Family;
                            if (!family.IsEditable) throw new InvalidOperationException($"Family '{family.Name}' is not editable.");
                            var famDoc = doc.EditFamily(family)
                                ?? throw new InvalidOperationException($"Failed to open family '{family.Name}' for editing.");
                            Debug.WriteLine($"[FamilyPalette] Attempting to edit family: {family.Name}");
                            Debug.WriteLine($"[FamilyPalette] Family.IsEditable: {family.IsEditable}");
                            Debug.WriteLine($"[FamilyPalette] Family.Id: {family.Id}");
                            Debug.WriteLine($"[FamilyPalette] famDoc.PathName: '{famDoc.PathName ?? "(null)"}'");
                            Debug.WriteLine($"[FamilyPalette] famDoc.IsModified: {famDoc.IsModified}");
                            Debug.WriteLine($"[FamilyPalette] famDoc.IsFamilyDocument: {famDoc.IsFamilyDocument}");
                            
                            // EditFamily() opens the document in the background but doesn't activate it
                            // For families without a PathName (in-memory), we need to save first
                            if (string.IsNullOrEmpty(famDoc.PathName)) {
                                var tempPath = Path.Combine(Path.GetTempPath(), $"{famDoc.Title}_{Guid.NewGuid()}.rfa");
                                famDoc.SaveAs(tempPath);
                                _ = uiApp.OpenAndActivateDocument(tempPath);
                            } else {
                                _ = uiApp.OpenAndActivateDocument(famDoc.PathName);
                            }
                        } catch (Autodesk.Revit.Exceptions.InvalidOperationException ex) {
                            throw new InvalidOperationException(
                                $"'{familyItem.Family.Name}' document may be read-only or workshared.",ex);
                        } catch (Exception  ex) {
                            throw new InvalidOperationException($"'{familyItem.Family.Name}' failed to open for editing: {ex.Message}", ex);
                        }
                    }
                },
                CanExecute = item => {
                    if (item is FamilyPaletteItem familyItem)
                        return familyItem.Family.IsEditable;
                    return false;
                }
            },
            // Ctrl+Click: Select all instances of the family
            new() {
                Name = "Select Instances",
                Modifiers = ModifierKeys.Control,
                MouseButton = MouseButton.Left,
                ExecuteAsync = async item => {
                    if (item is FamilyPaletteItem familyItem) {
                        try {
                            // Query can run async
                            var instances = await Task.Run(() => {
                                return new FilteredElementCollector(doc)
                                    .OfClass(typeof(FamilyInstance))
                                    .Cast<FamilyInstance>()
                                    .Where(fi => fi.Symbol.Family.Id == familyItem.Family.Id)
                                    .Select(fi => fi.Id)
                                    .ToList();
                            });
                            
                            // Selection must be set on main thread
                            uiApp.ActiveUIDocument.Selection.SetElementIds(instances);
                        } catch (Exception ex) {
                            throw new InvalidOperationException(
                                $"Failed to select instances of '{familyItem.Family.Name}': {ex.Message}",
                                ex
                            );
                        }
                    }
                },
                CanExecute = item => item is FamilyPaletteItem
            }
        };

        // Create view model
        var viewModel = new SelectablePaletteViewModel(selectableItems, searchService);

        // Create and return palette window
        var palette = new SelectablePalette(viewModel, actions);
        palette.Title = "Family Browser";
        return palette;
    }
}

