using Nice3point.Revit.Extensions;
using PeRevitUI;
#if !REVIT2023 && !REVIT2024 // APS Auth not supported in Revit 2023/2024
using AddinCmdApsAuthNormal;
using AddinCmdApsAuthPKCE;
using AddinCmdParametersServiceTest;
#endif

namespace PE_Tools;

internal class App : IExternalApplication {
    public Result OnStartup(UIControlledApplication app) {
        // 1. Create ribbon tab
        const string tabName = "PE TOOLS";
        try {
            app.CreateRibbonTab(tabName);
        } catch (Exception) {
            new Balloon()
                .Add(Log.INFO, $"{tabName} already exists in the current Revit instance.")
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

#if !REVIT2023 && !REVIT2024 // APS Auth not supported in Revit 2023/2024
        ButtonDataHydrator.AddButtonData([
            manageStackButton.AddPushButton<CmdApsAuthPKCE>("OAuth PKCE"),
            manageStackButton.AddPushButton<CmdApsAuthNormal>("OAuth Normal"),
            panelManage.AddPushButton<CmdParametersServiceTest>("Params Svc Test")
        ]);
#endif

        ButtonDataHydrator.AddButtonData([
            manageStackButton.AddPushButton<CmdUpdate>("Update"),
            panelTools.AddPushButton<CmdMep2040>("MEP 2040"),
            panelTools.AddPushButton<CmdCommandPalette>("Command Palette"),
            panelTools.AddPushButton<CmdTapMaker>("Tap Maker"),
            panelMigration.AddPushButton<CmdFamilyMigrator>("Family Migrator")
        ]);

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication a) => Result.Succeeded;
}

public static class ButtonDataHydrator {
    private static readonly Dictionary<string, ButtonDataRecord> ButtonDataRecords = new() {
        {
            "CmdUpdate", new ButtonDataRecord {
                SmallImage = "monitor-down16.png",
                LargeImage = "monitor-down32.png",
                ToolTip =
                    "Update the PE Tools addin suite to the latest release. You will need to restart Revit. TODO; fix this"
            }
        }, {
            "CmdApsAuthPKCE", new ButtonDataRecord {
                SmallImage = "id-card16.png",
                LargeImage = "id-card32.png",
                ToolTip =
                    "Get an access token from Autodesk Platform Services. This is primarily for testing purposes, but running it will not hurt anything."
            }
        }, {
            "CmdApsAuthNormal", new ButtonDataRecord {
                SmallImage = "id-card16.png",
                LargeImage = "id-card32.png",
                ToolTip =
                    "Get an access token from Autodesk Platform Services. This is primarily for testing purposes, but running it will not hurt anything."
            }
        }, {
            "CmdMep2040",
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Analyze MEP sustainability metrics (pipe length, refrigerant volume, mech equipment count)."
            }
        }, {
            "CmdCommandPalette", new ButtonDataRecord {
                SmallImage = "square-terminal16.png",
                LargeImage = "square-terminal32.png",
                ToolTip =
                    "Search and execute Revit commands quickly without looking through Revit's tabs, ribbons, and panels. Not all commands are guaranteed to run."
            }
        }, {
            "CmdTapMaker", new ButtonDataRecord {
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
            "CmdFamilyMigrator",
            new ButtonDataRecord {
                SmallImage = "Red_16.png",
                LargeImage = "Red_32.png",
                ToolTip = "Click to migrate families to the latest version."
            }
        }, {
            "CmdParametersServiceTest",
            new ButtonDataRecord {
                SmallImage = "Red_16.png", LargeImage = "Red_32.png", ToolTip = "Click to test the parameters service."
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