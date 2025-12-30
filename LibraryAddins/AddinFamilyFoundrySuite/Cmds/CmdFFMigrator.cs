using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Aggregators;
using AddinFamilyFoundrySuite.Core.OperationGroups;
using AddinFamilyFoundrySuite.Core.Operations;
using AddinFamilyFoundrySuite.Core.OperationSettings;
using AddinFamilyFoundrySuite.Core.Snapshots;
using AddinFamilyFoundrySuite.Ui;
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
using ParameterInfo = AddinFamilyFoundrySuite.Ui.ParameterInfo;

namespace AddinFamilyFoundrySuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdFFMigrator : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        try {
            var storage = new Storage("FF Migrator");
            var settingsManager = storage.SettingsDir();
            var settings = settingsManager.Json<BaseSettings<ProfileRemap>>().Read();
            var profilesDir = settingsManager.SubDir("profiles").DirectoryPath;

            // Discover all profile JSON files
            var profiles = ProfileListItem.DiscoverProfiles(profilesDir);
            if (profiles.Count == 0) {
                throw new InvalidOperationException(
                    $"No profiles found in {profilesDir}. Create a profile JSON file to continue.");
            }

            // State for tracking current selection
            var context = new MigratorContext {
                Doc = doc,
                UiDoc = uiDoc,
                Storage = storage,
                SettingsManager = settingsManager,
                OnFinishSettings = settings.OnProcessingFinish
            };

            // Create preview panel
            var previewPanel = new ProfilePreviewPanel();

            // Store window reference to be captured in actions
            EphemeralWindow window = null;

            // Define actions for the palette
            var actions = new List<PaletteAction<ProfileListItem>> {
                new() {
                    Name = "Toggle Preview",
                    Execute = async _ => {
                        if (window?.ContentControl is Palette palette)
                            palette.ToggleSidebar();
                    },
                    CanExecute = _ => true
                },
                new() {
                    Name = "Process Families",
                    Execute = async _ => this.HandleProcessFamilies(context),
                    CanExecute = _ => context.PreviewData?.IsValid == true
                },
                new() {
                    Name = "Regenerate Schema",
                    Execute = async _ => this.HandleRegenerateSchema(context),
                    CanExecute = _ => context.SelectedProfile != null
                }
            };

            // Create the palette with sidebar
            window = PaletteFactory.Create("FF Migrator - Select Profile", profiles, actions,
                new PaletteOptions<ProfileListItem> {
                    Storage = storage,
                    PersistenceKey = item => item.TextPrimary,
                    SearchConfig = SearchConfig.PrimaryAndSecondary(),
                    FilterKeySelector = item => string.IsNullOrEmpty(item.ExtendsValue) ? "Base" : "Extended",
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

            // Disable ephemeral behavior so window doesn't close when clicking on sidebar
            window.EphemeralEnabled = false;

            window.Show();

            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }

    private void BuildPreviewData(ProfileListItem profileItem, MigratorContext context) {
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

    private PreviewData TryLoadPreviewData(ProfileListItem profileItem, MigratorContext context) {
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

    private PreviewData LoadValidPreviewData(ProfileListItem profileItem, MigratorContext context) {
        // Load the profile
        var profile = context.SettingsManager.SubDir("profiles")
            .JsonWithExtends<ProfileRemap>($"{profileItem.TextPrimary}.json")
            .Read();

        // Get raw APS parameter models (no Revit API dependencies, safe to store)
        var apsParamModels = profile.GetFilteredApsParamModels();

        // Build queue structure for preview (using temp file just for structure, not storing definitions)
        using var previewTempFile = new TempSharedParamFile(context.Doc);
        var previewApsParamData = BaseProfileSettings.ConvertToSharedParameterDefinitions(
            apsParamModels, previewTempFile);

        var queue = BuildQueue(profile, previewApsParamData);
        var operationMetadata = queue.GetExecutableMetadata();
        var families = profile.GetFamilies(context.Doc);

        // Extract APS parameter info
        var apsParameters = apsParamModels.Select(p => new ParameterInfo(
            p.Name,
            p.DownloadOptions.IsInstance,
            GetDataTypeName(p.DownloadOptions.GetSpecTypeId())
        )).ToList();

        // Extract AddAndSet parameter info (including internal params)
        var internalParams = BuildInternalParams();
        var allAddAndSetParams = profile.AddAndSetParams.Parameters
            .Concat(internalParams)
            .Concat(profile.AddAndSetParams.ParametersPerType.Select(p => new SetParamModel {
                Name = p.Name,
                DataType = p.DataType,
                IsInstance = p.IsInstance,
                PropertiesGroup = p.PropertiesGroup
            }));

        var addAndSetParameters = allAddAndSetParams
            .Select(p => new ParameterInfo(
                p.Name,
                p.IsInstance,
                GetDataTypeName(p.DataType)
            ))
            .ToList();

        // Extract family info with categories
        var familyInfos = families.Select(f => new FamilyInfo(
            f.Name,
            f.FamilyCategory?.Name ?? "Unknown"
        )).ToList();

        // Serialize profile to JSON
        var profileJson = JsonSerializer.Serialize(
            profile,
            new JsonSerializerOptions {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

        // Check operation enabled status from queue
        var operationInfos = new List<OperationInfo>();
        foreach (var op in queue.Operations) {
            var metadata = operationMetadata.FirstOrDefault(m => m.Name == op.Name);
            if (metadata != default) {
                var isEnabled = true;
                if (op is IOperation<IOperationSettings> opWithSettings)
                    isEnabled = opWithSettings.Settings?.Enabled ?? true;
                operationInfos.Add(new OperationInfo(
                    metadata.Name,
                    metadata.Description,
                    metadata.Type,
                    metadata.IsMerged,
                    isEnabled
                ));
            }
        }

        return new PreviewData {
            ProfileName = profileItem.TextPrimary,
            FilePath = profileItem.FilePath,
            CreatedDate = profileItem._fileInfo.CreationTime,
            ModifiedDate = profileItem._fileInfo.LastWriteTime,
            LineCount = profileItem.LineCount,
            Operations = operationInfos,
            ApsParameters = apsParameters,
            AddAndSetParameters = addAndSetParameters,
            Families = familyInfos,
            ProfileJson = profileJson,
            IsValid = true
        };
    }

    private static string GetDataTypeName(ForgeTypeId dataType) {
        if (dataType == null || string.IsNullOrEmpty(dataType.TypeId))
            return "Text";

        var typeId = dataType.TypeId;
        var lastDash = typeId.LastIndexOf('-');
        return lastDash >= 0 ? typeId[(lastDash + 1)..] : typeId;
    }

    private static PreviewData CreateValidationErrorPreview(ProfileListItem profileItem, JsonValidationException ex) =>
        new() {
            ProfileName = profileItem.TextPrimary,
            IsValid = false,
            RemainingErrors = ex.ValidationErrors,
            AppliedFixes = new List<string>()
        };

    private static PreviewData
        CreateSanitizationErrorPreview(ProfileListItem profileItem, JsonSanitizationException ex) {
        var preview = new PreviewData {
            ProfileName = profileItem.TextPrimary,
            IsValid = false,
            AppliedFixes = ex.AppliedMigrations,
            RemainingErrors = new List<string>()
        };

        if (ex.AddedProperties.Any())
            preview.RemainingErrors.Add($"Added properties: {string.Join(", ", ex.AddedProperties)}");

        if (ex.RemovedProperties.Any())
            preview.RemainingErrors.Add($"Removed properties: {string.Join(", ", ex.RemovedProperties)}");

        return preview;
    }

    private static PreviewData CreateGenericErrorPreview(ProfileListItem profileItem, Exception ex) =>
        new() {
            ProfileName = profileItem.TextPrimary,
            IsValid = false,
            RemainingErrors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" },
            AppliedFixes = new List<string>()
        };

    private void HandleRegenerateSchema(MigratorContext context) {
        if (context.SelectedProfile == null) return;

        // Force schema regeneration by reading the profile (triggers WriteSchema)
        _ = context.SettingsManager.SubDir("profiles")
            .JsonWithExtends<ProfileRemap>($"{context.SelectedProfile.TextPrimary}.json")
            .Read();
        new Ballogger()
            .Add(Log.INFO, new StackFrame(), $"Schema regenerated for {context.SelectedProfile.TextPrimary}")
            .Show();
    }

    private void HandleProcessFamilies(MigratorContext context) {
        if (context.SelectedProfile == null) return;
        if (!context.PreviewData.IsValid) {
            new Ballogger()
                .Add(Log.ERR, new StackFrame(), "Cannot process families - profile has validation errors")
                .Show();
            return;
        }

        // Load profile fresh for execution
        var profile = context.SettingsManager.SubDir("profiles")
            .JsonWithExtends<ProfileRemap>($"{context.SelectedProfile.TextPrimary}.json")
            .Read();

        // Get raw APS parameter models and convert with fresh TempSharedParamFile
        var apsParamModels = profile.GetFilteredApsParamModels();

        // Create fresh TempSharedParamFile and convert raw APS models to SharedParameterDefinitions.
        // The temp file stays alive for the entire ProcessFamilies operation.
        using var tempFile = new TempSharedParamFile(context.Doc);
        var apsParamData = BaseProfileSettings.ConvertToSharedParameterDefinitions(
            apsParamModels, tempFile);

        var queue = BuildQueue(profile, apsParamData);

        ProcessFamilies(
            context.Doc,
            context.UiDoc,
            context.Storage,
            profile,
            queue,
            context.SelectedProfile.TextPrimary,
            context.OnFinishSettings
        );

        // TempSharedParamFile is disposed here AFTER ProcessFamilies completes
    }

    private static List<SetParamModel> BuildInternalParams() => [
        new() {
            Name = "PE_E___NumberOfPoles",
            ValueOrFormula =
                "if(PE_E___Voltage = 120, 1, if(PE_E___Voltage = 208, 2, (if(PE_E___Voltage = 240, 2, 1))))"
        },
        new() {
            Name = "PE_E___ApparentPower",
            ValueOrFormula = "PE_E___Voltage * PE_E___MCA * 0.8 * if(PE_E___NumberOfPoles = 3, sqrt(3), 1)"
        },
        new() {
            Name = "_FOUNDRY LAST PROCESSED AT",
            PropertiesGroup = new ForgeTypeId(""),
            DataType = SpecTypeId.String.Text,
            IsInstance = false,
            ValueOrFormula = $"\"{DateTime.Now:yyyy_MM_dd HH:mm:ss}\""
        }
    ];

    /// <summary>
    ///     Builds the operation queue from profile settings and APS parameter data.
    ///     This is used both for preview (with temp conversions) and execution (with real conversions).
    /// </summary>
    private static OperationQueue BuildQueue(
        ProfileRemap profile,
        List<SharedParameterDefinition> apsParamData
    ) {
        var apsParamNames = apsParamData.Select(p => p.ExternalDefinition.Name).ToList();

        var mappingDataAllNames = profile.AddAndMapSharedParams.MappingData
            .SelectMany(m => m.CurrNames)
            .Concat(apsParamNames);

        var internalParams = BuildInternalParams();
        var addAndSet = new AddAndSetParamsSettings {
            OverrideExistingValues = profile.AddAndSetParams.OverrideExistingValues,
            CreateFamParamIfMissing = profile.AddAndSetParams.CreateFamParamIfMissing,
            Parameters = profile.AddAndSetParams.Parameters.Concat(internalParams).ToList(),
            ParametersPerType = profile.AddAndSetParams.ParametersPerType
        };

        return new OperationQueue()
            .Add(new PurgeParams(profile.PurgeParams, mappingDataAllNames))
            .Add(new PurgeNestedFamilies(profile.PurgeNestedFamilies))
            .Add(new PurgeReferencePlanes(profile.PurgeReferencePlanes))
            .Add(new AddAndMapSharedParams(profile.AddAndMapSharedParams, apsParamData))
            .Add(new AddAndSetParams(addAndSet))
            .Add(new MakeElecConnector(profile.MakeElectricalConnector))
            .Add(new PurgeParams(profile.PurgeParams, apsParamNames))
            .Add(new SortParams(profile.SortParams));
    }

    private static void ProcessFamilies(
        Document doc,
        UIDocument uiDoc,
        Storage storage,
        ProfileRemap profile,
        OperationQueue queue,
        string profileName,
        OnProcessingFinishSettings onFinish
    ) {
        var outputFolderPath = storage.OutputDir().DirectoryPath;

        // Request both parameter and refplane snapshots
        var collectorQueue = new CollectorQueue()
            .Add(new ParamSectionCollector())
            .Add(new RefPlaneSectionCollector());

        using var processor = new OperationProcessor(doc, profile.ExecutionOptions);
        var logs = processor
            .SelectFamilies(() => {
                var picked = Pickers.GetSelectedFamilies(uiDoc);
                return picked.Any() ? picked : profile.GetFamilies(doc);
            })
            .ProcessQueue(queue, collectorQueue, outputFolderPath, onFinish);

        _ = new ProcessingResultBuilder(storage)
            .WithProfile(profile, profileName)
            .WithOperationMetadata(queue)
            .WithFamilyResults(logs.familyContexts)
            .WithTotalTime(logs.totalMs)
            .WriteOutput(onFinish.OpenOutputFilesOnCommandFinish);

        var balloon = new Ballogger();
        foreach (var ctx in logs.familyContexts)
            _ = balloon.Add(Log.INFO, new StackFrame(), $"Processed {ctx.FamilyName} in {ctx.TotalMs}ms");
        balloon.Show();
    }
}

public class ProfileRemap : BaseProfileSettings {
    [Description("Settings for deleting unused parameters")]
    [Required]
    public PurgeParamsSettings PurgeParams { get; init; } = new();

    [Description("Settings for deleting unused nested families")]
    [Required]
    public DefaultOperationSettings PurgeNestedFamilies { get; init; } = new();

    [Description("Settings for deleting unused reference planes")]
    [Required]
    public PurgeReferencePlanesSettings PurgeReferencePlanes { get; init; } = new();

    [Description("Settings for parameter mapping (add/replace and remap)")]
    [Required]
    public MapParamsSettings AddAndMapSharedParams { get; init; } = new();

    [Description("Settings for setting parameter values and adding family parameters.")]
    [Required]
    public AddAndSetParamsSettings AddAndSetParams { get; init; } = new();

    [Description("Settings for hydrating electrical connectors")]
    [Required]
    public MakeElecConnectorSettings MakeElectricalConnector { get; init; } = new();

    [Description("Settings for sorting parameters within each property group.")]
    [Required]
    public SortParamsSettings SortParams { get; init; } = new();
}

public class MigratorContext {
    public Document Doc { get; init; }
    public UIDocument UiDoc { get; init; }
    public Storage Storage { get; init; }
    public SettingsManager SettingsManager { get; init; }
    public OnProcessingFinishSettings OnFinishSettings { get; init; }

    // UI state: what's currently selected and displayed
    public ProfileListItem SelectedProfile { get; set; }
    public PreviewData PreviewData { get; set; }
    public Dictionary<string, PreviewData> PreviewCache { get; } = new();
}