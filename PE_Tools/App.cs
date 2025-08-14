namespace PE_Tools
{
    internal class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            // 1. Create ribbon tab
            string tabName = "PE TOOLS";
            try
            {
                app.CreateRibbonTab(tabName);
            }
            catch (Exception)
            {
                Debug.Print($"{tabName} already exists in the current Revit instance.");
            }

            // 2. Create ribbon panel
            RibbonPanel panel = PE_Init.UiHelpers.CreateRibbonPanel(app, tabName, "Revit Tools 1");

            // 3. Create button data instances
            PushButtonData btnData1 = cmdUpdate.GetButtonData();
            PushButtonData btnData2 = cmdMep2040.GetButtonData();
            PushButtonData btnData3 = cmdCommandPalette.GetButtonData();
            PushButtonData btnData4 = cmdTapMaker.GetButtonData();

            // 4. Add buttons to panel
            PushButton myButton1 = panel.AddItem(btnData1) as PushButton;
            PushButton myButton2 = panel.AddItem(btnData2) as PushButton;
            PushButton myButton3 = panel.AddItem(btnData3) as PushButton;
            PushButton myButton4 = panel.AddItem(btnData4) as PushButton;

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
    }
}
