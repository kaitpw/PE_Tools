using System;
using System.ComponentModel;

namespace PE_CommandPalette.Models
{
    /// <summary>
    /// Represents a PostableCommand item with additional metadata for the command palette
    /// </summary>
    public class PostableCommandItem : INotifyPropertyChanged
    {
        private bool _isSelected;

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
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        /// <summary>
        /// Search relevance score for filtering
        /// </summary>
        public double SearchScore { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return Name;
        }
    }
}