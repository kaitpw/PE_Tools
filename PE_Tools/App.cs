using Nice3point.Revit.Extensions;
using PeRevitUI;
using PE_Tools.Properties;
using System.Windows.Controls;


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
                .Add(Balloon.Log.INFO, $"{tabName} already exists in the current Revit instance.")
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
         var cmdApsAuthPKCE = manageStackButton.AddPushButton<CmdApsAuthPKCE>("OAuth PKCE");
        var cmdApsAuthNormal = manageStackButton.AddPushButton<CmdApsAuthNormal>("OAuth Normal");
        var cmdParametersServiceTest = panelManage.AddPushButton<CmdParametersServiceTest>("Params Service Test");
#endif

        var cmdUpdate = manageStackButton.AddPushButton<CmdUpdate>("Update");

        var cmdMep2040 = panelTools.AddPushButton<CmdMep2040>("MEP 2040");
        var cmdCommandPalette = panelTools.AddPushButton<CmdCommandPalette>("Command Palette");
        var cmdTapMaker = panelTools.AddPushButton<CmdTapMaker>("Tap Maker");
        var cmdFamilyMigrator = panelMigration.AddPushButton<CmdFamilyMigrator>("Family Migrator");

        ButtonDatas.AddButtonData(new() { cmdUpdate, cmdApsAuthPKCE, cmdApsAuthNormal, cmdParametersServiceTest, cmdMep2040, cmdCommandPalette, cmdTapMaker, cmdFamilyMigrator });

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication a) => Result.Succeeded;
}

public class ButtonDatas {
        public static void AddButtonData(List<PushButton> buttons) {
            foreach (var button in buttons) {
                Debug.WriteLine("button.ClassName: " + button.ClassName);
                if (buttonDataRecords.TryGetValue(button.ClassName, out var buttonData)) {
                    _ = button.SetImage(buttonData.SmallImage.ToString());
                    _ = button.SetToolTip(buttonData.ToolTip);
                    if (!string.IsNullOrEmpty(buttonData.LongDescription)) {
                        _ = button.SetLongDescription(buttonData.LongDescription);
                    }
                }
            }
        }
            // button.SetToolTip();
            // button.AddShortcuts("1");
            // button.SetLongDescription();




        private static readonly Dictionary<string, ButtonDataRecord> buttonDataRecords = new() {
            { "PE_Tools.CmdUpdate", new(){
                Name = "Update",
                ToolTip = "Click this button to update PE Tools to the latest release. You will need to restart Revit",
                SmallImage = new Uri("PE_Tools/Resources/Red_16.png"),
            } },
            { "CmdApsAuthPKCE", new() {
                Name = "OAuth PKCE",
                ToolTip = "Click this button to get an access token from Autodesk Platform Services. This is primarily for testing purposes, but running it will not hurt anything.",
                SmallImage = new Uri("PE_Tools/Resources/Red_16.png"),
            } },
            { "CmdApsAuthNormal", new() {
                Name ="OAuth Normal",
                ToolTip = "Click this button to get an access token from Autodesk Platform Services. This is primarily for testing purposes, but running it will not hurt anything.",
                SmallImage = new Uri("PE_Tools/Resources/Red_16.png"),
            } },
            { "CmdMep2040", new() {
                Name = "MEP 2040",
                ToolTip = "Click to analyze MEP sustainability metrics (pipe length, refrigerant volume, equipment count).",
                SmallImage = new Uri("PE_Tools/Resources/Red_16.png"),
            } },
            { "CmdCommandPalette", new() {
                Name = "Command Palette",
                ToolTip = "Open the command palette to search and execute Revit commands quickly. This is a quick way to run commands without having to search through tabs and panels.",
                SmallImage = new Uri("PE_Tools/Resources/Red_16.png"),
            } },
            { "CmdTapMaker", new(){
                Name = "Tap Maker",
                ToolTip = "Add a (default) 6\" tap to a clicked point on a duct face. Works in all views and on both round/rect ducts. Click-point adjustments will prevent overlaps (with other taps) and overhangs (over face edges). Size adjustments will size down a duct until it fits on a duct face. In the event an easy location or size adjustment is not found, no tap will be placed.",
                SmallImage = new Uri("PE_Tools/Resources/Red_16.png"),
                LongDescription =
                """
            Add a (default) 6" tap to a clicked point on a duct face. Works in all views and on both round/rect ducts. \
            Click-point adjustments will prevent overlaps (with other taps) and overhangs (over face edges). \
            Size adjustments will size down a duct until it fits on a duct face.

            In the event an easy location or size adjustment is not found, no tap will be placed.
            """} },
            { "CmdFamilyMigrator", new() {
                Name = "Family Migrator",
                ToolTip = "Click to migrate families to the latest version.",
                SmallImage = new Uri("PE_Tools/Resources/Red_16.png"),
            } },
            { "CmdParametersServiceTest", new() {
                Name = "Parameters Service Test",
                ToolTip = "Click to test the parameters service.",
                SmallImage = new Uri("PE_Tools/Resources/Red_16.png"),
            } },
        };
}


public record ButtonDataRecord {
    public string Name { get; set; }
    public string ToolTip { get; set; }
    public Uri LargeImage { get; set; }
    public Uri SmallImage { get; set; }
    public string LongDescription { get; set; }
    public string Shortcuts { get; set; }
    public string ContextualHelp { get; set; }
}