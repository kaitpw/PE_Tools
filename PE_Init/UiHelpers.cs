using System;
using System.Collections.Generic;
using System.Text;

namespace PE_Init
{
    internal class UiHelpers
    {
        internal static void CreateTab(UIControlledApplication app, string name)
        {
            try
            {
                app.CreateRibbonTab(name);
            }
            catch (Exception err)
            {
                Debug.Print(err.Message);
            }
        }

        internal static RibbonPanel CreateRibbonPanel(
            UIControlledApplication app,
            string tabName,
            string panelName
        )
        {
            RibbonPanel curPanel;

            if (GetRibbonPanelByName(app, tabName, panelName) == null)
                curPanel = app.CreateRibbonPanel(tabName, panelName);
            else
                curPanel = GetRibbonPanelByName(app, tabName, panelName);

            return curPanel;
        }

        internal static RibbonPanel GetRibbonPanelByName(
            UIControlledApplication app,
            string tabName,
            string panelName
        )
        {
            foreach (RibbonPanel tmpPanel in app.GetRibbonPanels(tabName))
            {
                if (tmpPanel.Name == panelName)
                    return tmpPanel;
            }

            return null;
        }
    }
}
