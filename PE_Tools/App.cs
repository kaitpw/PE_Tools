using AddinApsAuthSuite;
using AddinFamilyFoundrySuite.Cmds;
using Nice3point.Revit.Extensions;
using PeRevit.Ui;
using Wpf.Ui.Appearance;

namespace PE_Tools;

internal class App : IExternalApplication {
    public Result OnStartup(UIControlledApplication app) {
        // Set up assembly resolver for Wpf.Ui and other dependencies
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

        // Initialize WPF.UI theme system - defaults to Dark theme
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        // 1. Create ribbon tab
        const string tabName = "PE TOOLS";
        try {
            app.CreateRibbonTab(tabName);
        } catch (Exception) {
            new Ballogger()
                .Add(Log.INFO, null, $"{tabName} already exists in the current Revit instance.")
                .Show();
        }

        // 2. Create ribbon panel
        const string ribbonPanelName1 = "Manage";
        const string ribbonPanelName2 = "Tools";
        const string ribbonPanelName3 = "Migration";
        var panelManage = UiHelpers.CreateRibbonPanel(app, tabName, ribbonPanelName1);
        var panelTools = UiHelpers.CreateRibbonPanel(app, tabName, ribbonPanelName2);
        var panelMigration = UiHelpers.CreateRibbonPanel(app, tabName, ribbonPanelName3);

        var manageStackButton = panelManage.AddPullDownButton("General");
        // var ffManagerStackButton = panelMigration.AddSplitButton("Manager");

#if !REVIT2023 && !REVIT2024 // APS Auth not supported in Revit 2023/2024
        ButtonDataHydrator.AddButtonData([
            manageStackButton.AddPushButton<CmdApsAuthPKCE>("OAuth PKCE"),
            manageStackButton.AddPushButton<CmdApsAuthNormal>("OAuth Normal")
        ]);
#endif

        ButtonDataHydrator.AddButtonData([
            panelMigration.AddPushButton<CmdFFManager>("FF Manager"),
            panelMigration.AddPushButton<CmdFFManagerSnapshot>("FF Manager Snapshot"),
            panelMigration.AddPushButton<CmdFFMigrator>("FF Migrator"),
            panelMigration.AddPushButton<CmdFFMakeATVariants>("Make AT Variants"),
            manageStackButton.AddPushButton<CmdUpdate>("Update"),
            manageStackButton.AddPushButton<CmdCacheParametersService>("Cache Params Svc"),

            panelTools.AddPushButton<CmdMep2040>("MEP 2040"),
            panelTools.AddPushButton<CmdCommandPalette>("Command Palette"),
            panelTools.AddPushButton<CmdOpenView>("Open View"),
            panelTools.AddPushButton<CmdOpenSchedule>("Open Schedule"),
            panelTools.AddPushButton<CmdOpenSheet>("Open Sheet"),
            panelTools.AddPushButton<CmdOpenFamily>("Open Family"),
            panelTools.AddPushButton<CmdTapMaker>("Tap Maker")
        ]);

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication a) {
        AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
        return Result.Succeeded;
    }

    private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
        // Get the assembly name being requested
        var assemblyName = new AssemblyName(args.Name);

        // Only handle assemblies we know about
        if (assemblyName.Name != "Wpf.Ui" && assemblyName.Name != "Wpf.Ui.Abstractions") return null;

        // Get the directory where this add-in's DLL is located
        var addinPath = typeof(App).Assembly.Location;
        var addinDirectory = Path.GetDirectoryName(addinPath);

        // Construct the path to the requested assembly
        var assemblyPath = Path.Combine(addinDirectory, $"{assemblyName.Name}.dll");

        // Load and return the assembly if it exists
        if (File.Exists(assemblyPath)) return Assembly.LoadFrom(assemblyPath);

        return null;
    }
}

public static class ButtonDataHydrator {
    private static readonly Dictionary<string, ButtonDataRecord> ButtonDataRecords = new() {
        {
            nameof(CmdUpdate), new ButtonDataRecord {
                SmallImage = "monitor-down16.png",
                LargeImage = "monitor-down32.png",
                ToolTip =
                    "Update the PE Tools addin suite to the latest release. You will need to restart Revit. TODO; fix this"
            }
        }, {
            nameof(CmdCacheParametersService),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Cache the parameters service data for use in the Family Foundry command."
            }
        }, {
            nameof(CmdApsAuthPKCE), new ButtonDataRecord {
                SmallImage = "id-card16.png",
                LargeImage = "id-card32.png",
                ToolTip =
                    "Get an access token from Autodesk Platform Services. This is primarily for testing purposes, but running it will not hurt anything."
            }
        }, {
            nameof(CmdApsAuthNormal), new ButtonDataRecord {
                SmallImage = "id-card16.png",
                LargeImage = "id-card32.png",
                ToolTip =
                    "Get an access token from Autodesk Platform Services. This is primarily for testing purposes, but running it will not hurt anything."
            }
        }, {
            nameof(CmdMep2040),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Analyze MEP sustainability metrics (pipe length, refrigerant volume, mech equipment count)."
            }
        }, {
            nameof(CmdCommandPalette), new ButtonDataRecord {
                SmallImage = "square-terminal16.png",
                LargeImage = "square-terminal32.png",
                ToolTip =
                    "Search and execute Revit commands quickly without looking through Revit's tabs, ribbons, and panels. Not all commands are guaranteed to run."
            }
        }, {
            nameof(CmdOpenView),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Search and open views in the current document."
            }
        }, {
            nameof(CmdOpenSchedule),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Search and open schedules in the current document."
            }
        }, {
            nameof(CmdOpenSheet),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Search and open sheets in the current document."
            }
        }, {
            nameof(CmdOpenFamily),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Search families in the document. Click to edit family, Ctrl+Click to select all instances."
            }
        }, {
            nameof(CmdTapMaker), new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip =
                    "Add a (default) 6\" tap to a clicked point on a duct face. Works in all views and on both round/rectangular ducts.",
                LongDescription =
                    """
                    Add a (default) 6" tap to a clicked point on a duct face. Works in all views and on both round/rectangular ducts.
                    Automatic click-point adjustments will prevent overlaps (with other taps) and overhangs (over face edges).
                    Automatic size adjustments will size down a duct until it fits on a duct face.

                    In the event an easy location or size adjustment is not found, no tap will be placed.
                    """
            }
        }, {
            nameof(CmdFFMigrator),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Process families in a variety of ways from the Family Foundry."
            }
        }, {
            nameof(CmdFFManager),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Manage families in a variety of ways from the Family Foundry."
            }
        }, {
            nameof(CmdFFManagerSnapshot), new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip =
                    "Running this will output a JSON file with a config the represents the reference planes, dimensions, and family parameters of the currently open family"
            }
        }, {
            nameof(CmdFFTagMigrator),
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Process tags in a variety of ways from the Family Foundry."
            }
        }, {
            nameof(CmdFFMakeATVariants), new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip =
                    "Test command that processes a family 3 times with incrementing TEST_PROCESS_NUMBER parameter."
            }
        }
    };

    public static void AddButtonData(List<PushButton> buttons) {
        foreach (var button in buttons) {
            Debug.WriteLine("button.ClassName: " + button.ClassName);
            var key = button.ClassName.Split('.').Last();
            if (ButtonDataRecords.TryGetValue(key, out var btnData)) {
                _ = button.SetImage(btnData.SmallImage)
                    .SetLargeImage(btnData.LargeImage)
                    .SetToolTip(btnData.ToolTip);
                if (!string.IsNullOrEmpty(btnData.LongDescription))
                    _ = button.SetLongDescription(btnData.LongDescription);
            } else
                throw new Exception($"{key} was not found in ButtonDataRecords.");
        }
    }

    public record ButtonDataRecord {
        private readonly string _largeImage;
        private readonly string _smallImage;
        public string Shortcuts { get; init; }

        public required string SmallImage {
            get => ValidateUri(this._smallImage);
            init => this._smallImage = value;
        }

        public required string LargeImage {
            get => ValidateUri(this._largeImage);
            init => this._largeImage = value;
        }

        public required string ToolTip { get; init; }
        public string LongDescription { get; init; }
        public string ContextualHelp { get; init; }

        private static string ValidateUri(string fileName) =>
            new Uri($"pack://application:,,,/PE_Tools;component/Resources/{fileName}", UriKind.Absolute).ToString();
    }
}