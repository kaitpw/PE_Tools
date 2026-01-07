using PE_Tools.ScheduleManagerUi;
using PeRevit.Lib;
using PeRevit.Ui;
using PeServices.Storage;
using PeServices.Storage.Core;
using PeUi.Components;
using PeUi.Core;
using PeUi.Core.Services;
using PeUtils.Files;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdCreateSchedule : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        try {
            var storage = new Storage("Schedule Manager");
            var settingsManager = storage.SettingsDir();
            var schedulesSubDir = settingsManager.SubDir("schedules", recursiveDiscovery: true);

            // Discover all schedule profile JSON files
            var profiles = ScheduleListItem.DiscoverProfiles(schedulesSubDir);
            if (profiles.Count == 0) {
                throw new InvalidOperationException(
                    $"No schedule profiles found in {schedulesSubDir.DirectoryPath}. Create a profile JSON file to continue.");
            }

            // State for tracking current selection
            var context = new ScheduleManagerContext {
                Doc = doc, UiDoc = uiDoc, Storage = storage, SettingsManager = settingsManager
            };

            // Create preview panel
            var previewPanel = new SchedulePreviewPanel();

            // Store window reference to be captured in actions
            EphemeralWindow window = null;

            // Define actions for the palette
            var actions = new List<PaletteAction<ScheduleListItem>> {
                new() {
                    Name = "Create Schedule",
                    Execute = async _ => this.HandleCreateSchedule(context),
                    CanExecute = _ => context.PreviewData?.IsValid == true
                },
                new() {
                    Name = "Place Sample Families",
                    Execute = async _ => this.HandlePlaceSampleFamilies(context),
                    CanExecute = _ => context.SelectedProfile != null
                }
            };

            // Create the palette with sidebar
            window = PaletteFactory.Create("Schedule Manager - Select Profile", profiles, actions,
                new PaletteOptions<ScheduleListItem> {
                    Storage = storage,
                    PersistenceKey = item => item.TextPrimary,
                    SearchConfig = SearchConfig.PrimaryAndSecondary(),
                    FilterKeySelector = item => item.CategoryName,
                    OnSelectionChangedDebounced = item => {
                        this.BuildPreviewData(item, context);
                        if (context.PreviewData != null) {
                            previewPanel.UpdatePreview(context.PreviewData);
                            // Auto-expand sidebar when preview data is available
                            if (window?.ContentControl is Palette palette)
                                palette.ExpandSidebar(new GridLength(450));
                        }
                    },
                    Sidebar = new PaletteSidebar {
                        Content = previewPanel,
                        InitialState = SidebarState.Collapsed,
                        Width = new GridLength(450),
                        ExitKeys = [Key.Escape]
                    }
                });

            window.Show();

            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }

    private void BuildPreviewData(ScheduleListItem profileItem, ScheduleManagerContext context) {
        if (profileItem == null) {
            context.PreviewData = null;
            return;
        }

        // Check cache first
        if (context.PreviewCache.TryGetValue(profileItem.TextPrimary, out var cachedPreview)) {
            context.PreviewData = cachedPreview;
            context.SelectedProfile = profileItem;
            return;
        }

        context.SelectedProfile = profileItem;
        context.PreviewData = this.TryLoadPreviewData(profileItem, context);
        context.PreviewCache[profileItem.TextPrimary] = context.PreviewData;
    }

    private SchedulePreviewData TryLoadPreviewData(ScheduleListItem profileItem, ScheduleManagerContext context) {
        try {
            return this.LoadValidPreviewData(profileItem, context);
        } catch (JsonValidationException ex) {
            return CreateValidationErrorPreview(profileItem, ex);
        } catch (JsonSanitizationException ex) {
            return CreateSanitizationErrorPreview(profileItem, ex);
        } catch (Exception ex) {
            return CreateGenericErrorPreview(profileItem, ex);
        }
    }

    private SchedulePreviewData LoadValidPreviewData(ScheduleListItem profileItem, ScheduleManagerContext context) {
        // Load the profile
        var profile = context.SettingsManager.SubDir("schedules", recursiveDiscovery: true)
            .Json<ScheduleSpec>($"{profileItem.TextPrimary}.json")
            .Read();

        // Serialize profile to JSON
        var profileJson = JsonSerializer.Serialize(
            profile,
            new JsonSerializerOptions {
                WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

        return new SchedulePreviewData {
            ProfileName = profileItem.TextPrimary,
            CategoryName = profile.CategoryName,
            IsItemized = profile.IsItemized,
            Fields = profile.Fields,
            SortGroup = profile.SortGroup,
            ProfileJson = profileJson,
            FilePath = profileItem.FilePath,
            CreatedDate = profileItem._fileInfo.CreationTime,
            ModifiedDate = profileItem._fileInfo.LastWriteTime,
            IsValid = true
        };
    }

    private static SchedulePreviewData CreateValidationErrorPreview(ScheduleListItem profileItem,
        JsonValidationException ex) =>
        new() { ProfileName = profileItem.TextPrimary, IsValid = false, RemainingErrors = ex.ValidationErrors };

    private static SchedulePreviewData
        CreateSanitizationErrorPreview(ScheduleListItem profileItem, JsonSanitizationException ex) {
        var preview = new SchedulePreviewData {
            ProfileName = profileItem.TextPrimary, IsValid = false, RemainingErrors = []
        };

        if (ex.AddedProperties.Any())
            preview.RemainingErrors.Add($"Added properties: {string.Join(", ", ex.AddedProperties)}");

        if (ex.RemovedProperties.Any())
            preview.RemainingErrors.Add($"Removed properties: {string.Join(", ", ex.RemovedProperties)}");

        return preview;
    }

    private static SchedulePreviewData CreateGenericErrorPreview(ScheduleListItem profileItem, Exception ex) =>
        new() {
            ProfileName = profileItem.TextPrimary,
            IsValid = false,
            RemainingErrors = [$"{ex.GetType().Name}: {ex.Message}"]
        };

    private void HandleCreateSchedule(ScheduleManagerContext ctx) {
        if (ctx.SelectedProfile == null) return;
        if (!ctx.PreviewData.IsValid) {
            new Ballogger()
                .Add(Log.ERR, new StackFrame(), "Cannot create schedule - profile has validation errors")
                .Show();
            return;
        }

        // Load profile fresh for execution
        var profile = ctx.SettingsManager.SubDir("schedules", recursiveDiscovery: true)
            .Json<ScheduleSpec>($"{ctx.SelectedProfile.TextPrimary}.json")
            .Read();

        ScheduleCreationResult result;
        using (var trans = new Transaction(ctx.Doc, "Create Schedule")) {
            _ = trans.Start();
            result = ScheduleHelper.CreateSchedule(ctx.Doc, profile);
            _ = trans.Commit();
        }

        // Write output to storage
        var outputPath = this.WriteCreationOutput(ctx, result);

        // Build balloon message
        var balloon = new Ballogger();
        _ = balloon.Add(Log.INFO, new StackFrame(),
            $"Created schedule '{result.ScheduleName}' from profile '{ctx.SelectedProfile.TextPrimary}'");

        if (result.AppliedHeaderGroups.Count > 0) {
            _ = balloon.Add(Log.INFO, new StackFrame(),
                $"Applied {result.AppliedHeaderGroups.Count} header group(s)");
        }

        if (result.SkippedCalculatedFields.Count > 0) {
            _ = balloon.Add(Log.WARN, new StackFrame(),
                $"{result.SkippedCalculatedFields.Count} calculated field(s) require manual creation - see output file");
        }

        balloon.Show();

        // Open the schedule view
        ctx.UiDoc.ActiveView = result.Schedule;

        // Open output file if there are calculated fields
        if (result.SkippedCalculatedFields.Count > 0 && !string.IsNullOrEmpty(outputPath))
            FileUtils.OpenInDefaultApp(outputPath);
    }

    private void HandlePlaceSampleFamilies(ScheduleManagerContext context) {
        var profile = context.SettingsManager.SubDir("schedules", recursiveDiscovery: true)
            .Json<ScheduleSpec>($"{context.SelectedProfile.TextPrimary}.json")
            .Read();

        // Get families of the schedule's category
        var category = context.Doc.Settings.Categories.get_Item(profile.CategoryName);

        if (category == null) {
            new Ballogger()
                .Add(Log.WARN, new StackFrame(), $"Category '{profile.CategoryName}' not found")
                .Show();
            return;
        }

        var allFamilies = new FilteredElementCollector(context.Doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(f => f.FamilyCategory?.Id == category.Id)
            .ToList();

        if (allFamilies.Count == 0) {
            new Ballogger()
                .Add(Log.WARN, new StackFrame(), $"No {profile.CategoryName} families found in the project")
                .Show();
            return;
        }

        // Use Revit's native schedule filtering to find families that match the profile's filters
        var matchingFamilyNames = ScheduleHelper.GetFamiliesMatchingFilters(
            context.Doc,
            profile,
            allFamilies);

        if (matchingFamilyNames.Count == 0) {
            new Ballogger()
                .Add(Log.WARN, new StackFrame(), "No families match the schedule filters")
                .Show();
            return;
        }

        FamilyPlacementHelper.PromptAndPlaceFamilies(
            context.UiDoc.Application,
            matchingFamilyNames,
            "Schedule Manager");
    }

    private string WriteCreationOutput(ScheduleManagerContext ctx, ScheduleCreationResult result) {
        try {
            var outputData = new {
                result.ScheduleName,
                ProfileName = ctx.SelectedProfile.TextPrimary,
                CreatedAt = DateTime.Now,
                result.AppliedHeaderGroups,
                SkippedCalculatedFields = result.SkippedCalculatedFields.Select(f => new {
                    f.FieldName, f.CalculatedType, f.Guidance, f.PercentageOfField
                }).ToList(),
                result.Warnings
            };

            var outputPath = ctx.Storage.OutputDir().Json<object>("schedule-creation").Write(outputData);
            return outputPath;
        } catch (Exception ex) {
            Debug.WriteLine($"Failed to write output: {ex.Message}");
            return null;
        }
    }
}

public class ScheduleManagerContext {
    public Document Doc { get; init; }
    public UIDocument UiDoc { get; init; }
    public Storage Storage { get; init; }
    public SettingsManager SettingsManager { get; init; }

    // UI state: what's currently selected and displayed
    public ScheduleListItem SelectedProfile { get; set; }
    public SchedulePreviewData PreviewData { get; set; }
    public Dictionary<string, SchedulePreviewData> PreviewCache { get; } = new();
}

public class ScheduleSettings {
    [Description(
        "Current profile to use for the command. This determines which schedule profile is used when creating a schedule.")]
    [Required]
    public string CurrentProfile { get; set; } = "Default";
}