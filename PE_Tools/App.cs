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

            // 4. Add buttons to panel
            PushButton myButton1 = panel.AddItem(btnData1) as PushButton;

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
    }
}
