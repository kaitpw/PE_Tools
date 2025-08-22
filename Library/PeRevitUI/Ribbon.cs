using Autodesk.Windows.ToolBars;
using Autodesk.Windows;
using System.ComponentModel;
using System.Windows.Data;
using UIFramework;

namespace PeRevitUI;

public class Ribbon {
    public static IEnumerable<DiscoveredTab> GetAllTabs() {
        var tabs = Autodesk.Windows.ComponentManager.Ribbon.Tabs;
        var tabList = new List<DiscoveredTab>();
        foreach (var tab in tabs) {
            if (!tab.IsVisible || !tab.IsEnabled) continue;
            tabList.Add(new DiscoveredTab {
                Id = tab.Id,
                Name = tab.Title,
                Panels = tab.Panels,
                DockedPanels = tab.DockedPanelsView,
                RibbonControl = tab.RibbonControl
            });
        }
        return tabList;
    }

    public static IEnumerable<DiscoveredPanel> GetAllPanels() {
        var tabs = GetAllTabs();
        var panelList = new List<DiscoveredPanel>();
        foreach (var tab in tabs) {
            foreach (var panel in tab.Panels) {
                if (!panel.IsVisible || !panel.IsEnabled) continue;
                panelList.Add(new DiscoveredPanel {
                    Tab = panel.Tab,
                    Cookie = panel.Cookie,
                    Source = panel.Source,
                    RibbonControl = panel.RibbonControl,
                });
            }
        }
        return panelList;
    }

    /// <summary>
    /// Retrieves all commands from the ribbon with specialized handling for each item type.
    /// </summary>
    public static IEnumerable<DiscoveredCommand> GetAllCommands() {
        var panels = GetAllPanels();
        var commandList = new List<DiscoveredCommand>();
        
        foreach (var panel in panels) {
            foreach (var item in panel.Source.Items) {
                if (!item.IsVisible || !item.IsEnabled) continue;
                var command = ProcessRibbonItem(item, panel, commandList);
                if (command != null) {
                    commandList.Add(command);
                }
            }
        }
        
        // Deduplicate by ID, keeping the first occurrence of each unique ID
        var uniqueCommands = commandList
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .ToList();
            
        return uniqueCommands;
    }

    /// <summary>
    /// Processes individual ribbon items based on their type with specialized handling.
    /// </summary>
    private static DiscoveredCommand ProcessRibbonItem(dynamic item, DiscoveredPanel panel, List<DiscoveredCommand> commandList) {
        var command = new DiscoveredCommand {
            Id = item.Id?.ToString() ?? "",
            Name = item.Name?.ToString() ?? "",
            Text = item.Text?.ToString() ?? "",
            ToolTip = item.ToolTip,
            Description = item.Description?.ToString() ?? "",
            ToolTipResolver = item.ToolTipResolver,
            Tab = panel.Tab.Title,
            Panel = panel.Cookie,
            ItemType = item.GetType().Name,
        };

        // Recursively process child items for container types
        if (HasItemsCollection(item) && item.Items?.Count > 0) {
            foreach (var childItem in item.Items) {
                var childCommand = ProcessRibbonItem(childItem, panel, commandList);
                if (childCommand != null) {
                    commandList.Add(childCommand);
                }
            }
        }

        return command;
    }

    /// <summary> Determines if a ribbon item type supports having child items. </summary>
    private static bool HasItemsCollection(dynamic item) {
        // Cast to object first to avoid dynamic dispatch issues with extension methods
        var itemType = ((object)item).GetType().Name;
        var containerTypes = new[] { // Do not change unless to add
            "RibbonFoldPanel", 
            "RibbonRowPanel", 
            "RibbonSplitButton", 
            "RibbonChecklistButton", 
            "RvtMenuSplitButton", 
            "SplitRadioGroup", 
            "DesignOptionCombo",
            "RibbonMenuItem",
        };
        
        return containerTypes.Contains(itemType);
    }
}


public class DiscoveredTab {
    /// <summary> Name, what you see in UI. RibbonTab.Title, DefaultTitle, AutomationName are always same</summary>
    public string Name { get; set; }
    /// <summary> Internal ID, not sure what it's used for</summary>
    public string Id { get; set; }
    /// <summary> Panels contained within the tab</summary>
    public Autodesk.Windows.RibbonPanelCollection Panels { get; set; }
    /// <summary> TBD: Not sure what this is, but possibly useful</summary>
    public ICollectionView DockedPanels { get; set; }
    /// <summary> TBD: Not sure what this is, but possibly useful</summary>
    public Autodesk.Windows.RibbonControl RibbonControl { get; set; }
}

    public class DiscoveredPanel {
        /// <summary> The parent tab of this panel</summary> 
        public Autodesk.Windows.RibbonTab Tab { get; set; }
        /// <summary> Internal ID, not sure what it's used for and has a strange format</summary>
        public string Cookie { get; set; }
        /// <summary> Can access Panel items via RibbonPanelSource.Items</summary>
        public Autodesk.Windows.RibbonPanelSource Source { get; set; }
        /// <summary> TBD: Not sure what this is, but possibly useful</summary>
        public Autodesk.Windows.RibbonControl RibbonControl { get; set; }
}

public class DiscoveredCommand {
    /// <summary> 
    ///     ID, if postable then it will be the CommandId found in KeyboardShortcuts.xml.
    ///     i.e. either "SCREAMING_SNAKE_CASE" for internal PostableCommand's 
    ///     or the "CustomCtrl_%.." format for external addin commands.
    ///     There are often near duplicates, like ID_OBJECTS_FAMSYM and ID_OBJECTS_FAMSYM_RibbonListButton
    ///     It is also often empty or not a commandId at all. 
    /// </summary>
    public string Id { get; set; }
    /// <summary> 
    /// Human-readable name of the command, often empty.
    /// If empty, this.Text may be non-empty. Both may also be empty.
    /// </summary>
    public string Name { get; set; }
    /// <summary> 
    /// Another type of name, always similar to Name, often empty.
    /// RibbonItem.Text, AutomationName, and TextBinding always seem to be same.
    /// </summary>
    public string Text { get; set; }

    /// <summary> Often empty, look into ToolTipResolver for more information. </summary>
    public object ToolTip { get; set; }
    /// <summary> A standin for tooltip? seems to be non-empty more often than Tooltip is.</summary>
    public string Description { get; set; }
    public object ToolTipResolver { get; set; }
    public string Tab { get; set; }
    public string Panel { get; set; }
    /// <summary> Type of the item, e.g. RibbonButton, RibbonToggleButton, etc. </summary>
    public string ItemType { get; set; }
}


