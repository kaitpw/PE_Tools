using PeRevitUI;

namespace PE_Tools;

internal class App : IExternalApplication {
    public Result OnStartup(UIControlledApplication app) {
        // 1. Create ribbon tab
        const string tabName = "PE TOOLS";
        try {
            app.CreateRibbonTab(tabName);
        } catch (Exception) {
            Balloon.ShowSingle($"{tabName} already exists in the current Revit instance.");
        }

        // 2. Create ribbon panel
        const string ribbonPanelName1 = "Manage";
        const string ribbonPanelName2 = "Tools";
        var panelManage = UiHelpers.CreateRibbonPanel(app, tabName, ribbonPanelName1);
        var panelTools = UiHelpers.CreateRibbonPanel(app, tabName, ribbonPanelName2);

        // 3. Create button data instances
        var cmdUpdate = CmdUpdate.GetButtonData();
        var cmdMep2040 = CmdMep2040.GetButtonData();
        var cmdCommandPalette = CmdCommandPalette.GetButtonData();
        var cmdTapMaker = CmdTapMaker.GetButtonData();

        // 4. Add buttons to panel
        _ = panelManage.AddItem(cmdUpdate) as PushButton;
        _ = panelTools.AddItem(cmdMep2040) as PushButton;
        _ = panelTools.AddItem(cmdCommandPalette) as PushButton;
        _ = panelTools.AddItem(cmdTapMaker) as PushButton;

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication a) => Result.Succeeded;
}