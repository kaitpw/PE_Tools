using System;
using System.Collections.Generic;
using System.Text;

namespace PE_Lib
{
    internal class UiUtils
    {
        // from ricaun.Github, checkt he buildingcoder implementation too
        public static void ShowBalloon(string title, string category = null)
        {
            if (title == null)
                return;
            Autodesk.Internal.InfoCenter.ResultItem ri =
                new Autodesk.Internal.InfoCenter.ResultItem();
            ri.Category = category ?? typeof(Utils).Assembly.GetName().Name;
            ri.Title = title.Trim();
            Autodesk.Windows.ComponentManager.InfoCenterPaletteManager.ShowBalloon(ri);
        }
    }
}
