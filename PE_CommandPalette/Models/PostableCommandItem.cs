using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PE_CommandPalette.M
{
    /// <summary>
    /// Represents a PostableCommand item with additional metadata for the command palette
    /// </summary>
    public partial class PostableCommandItem : ObservableObject
    {
        /// <summary>
        /// The actual PostableCommand enum value
        /// </summary>
        public PostableCommand Command { get; set; }

        /// <summary>
        /// Display name of the command
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Number of times this command has been used (for prioritization)
        /// </summary>
        public int UsageCount { get; set; }

        /// <summary>
        /// Last time this command was executed
        /// </summary>
        public DateTime LastUsed { get; set; }

        /// <summary>
        /// Whether this item is currently selected in the UI
        /// </summary>
        [ObservableProperty]
        private bool _isSelected;

        /// <summary>
        /// Search relevance score for filtering
        /// </summary>
        public double SearchScore { get; set; }

        /// <summary>
        /// Keyboard shortcuts for this command
        /// </summary>
        public List<string> Shortcuts { get; set; } = new List<string>();

        /// <summary>
        /// Menu paths for this command
        /// </summary>
        public List<string> Paths { get; set; } = new List<string>();

        /// <summary>
        /// For addin commands, stores the custom CommandId (e.g., CustomCtrl_%CustomCtrl_%...)
        /// </summary>
        public string CustomCommandId { get; set; }

        /// <summary>
        /// Gets the primary shortcut as a display string
        /// </summary>
        public string PrimaryShortcut => Shortcuts.Count > 0 ? Shortcuts[0] : string.Empty;

        /// <summary>
        /// Gets all shortcuts as a display string
        /// </summary>
        public string AllShortcuts => string.Join(", ", Shortcuts);

        /// <summary>
        /// Gets all paths as a display string
        /// </summary>
        public string AllPaths => string.Join("; ", Paths);

        /// <summary>
        /// Gets truncated paths for display (with tooltip for full paths)
        /// </summary>
        public string TruncatedPaths
        {
            get
            {
                if (Paths.Count == 0)
                    return string.Empty;

                string allPaths = AllPaths;
                if (allPaths.Length <= 50)
                    return allPaths;

                return allPaths.Substring(0, 47) + "...";
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
