using AddinCmdApsAuth;
using PeRevitUI;

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

        // 3. Create button data instances
        var cmdUpdate = CmdUpdate.GetButtonData();
#if !REVIT2023 && !REVIT2024 // APS Auth not supported in Revit 2023/2024
        var cmdApsAuthNormal = CmdApsAuthNormal.GetButtonData();
        var cmdApsAuthPKCE = CmdApsAuthPKCE.GetButtonData();
#endif
        var cmdMep2040 = CmdMep2040.GetButtonData();
        var cmdCommandPalette = CmdCommandPalette.GetButtonData();
        var cmdTapMaker = CmdTapMaker.GetButtonData();
        var cmdFamilyMigrator = CmdFamilyMigrator.GetButtonData();

        // 4. Add buttons to panel
        _ = panelManage.AddItem(cmdUpdate) as PushButton;
#if !REVIT2023 && !REVIT2024 // APS Auth not supported in Revit 2023/2024
        _ = panelManage.AddItem(cmdApsAuthNormal) as PushButton;
        _ = panelManage.AddItem(cmdApsAuthPKCE) as PushButton;
#endif
        _ = panelTools.AddItem(cmdMep2040) as PushButton;
        _ = panelTools.AddItem(cmdCommandPalette) as PushButton;
        _ = panelTools.AddItem(cmdTapMaker) as PushButton;
        _ = panelMigration.AddItem(cmdFamilyMigrator) as PushButton;

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication a) => Result.Succeeded;
}