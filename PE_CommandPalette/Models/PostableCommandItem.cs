using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PE_CommandPalette.Models
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
        /// Description or tooltip for the command
        /// </summary>
        public string Description { get; set; }

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

        public override string ToString()
        {
            return Name;
        }
    }
}
