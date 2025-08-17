using PE_Init;

namespace PE_Tools;

internal class App : IExternalApplication {
    public Result OnStartup(UIControlledApplication app) {
        // 1. Create ribbon tab
        var tabName = "PE TOOLS";
        try {
            app.CreateRibbonTab(tabName);
        } catch (Exception) {
            Debug.Print($"{tabName} already exists in the current Revit instance.");
        }

        // 2. Create ribbon panel
        var panel = UiHelpers.CreateRibbonPanel(app, tabName, "Revit Tools 1");

        // 3. Create button data instances
        var btnData1 = cmdUpdate.GetButtonData();
        var btnData2 = cmdMep2040.GetButtonData();
        var btnData3 = cmdCommandPalette.GetButtonData();
        var btnData4 = cmdTapMaker.GetButtonData();

        // 4. Add buttons to panel
        var myButton1 = panel.AddItem(btnData1) as PushButton;
        var myButton2 = panel.AddItem(btnData2) as PushButton;
        var myButton3 = panel.AddItem(btnData3) as PushButton;
        var myButton4 = panel.AddItem(btnData4) as PushButton;

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication a) => Result.Succeeded;
}