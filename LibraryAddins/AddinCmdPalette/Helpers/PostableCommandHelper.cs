using AddinCmdPalette.Models;
using PeLib;
using System.Text;

namespace AddinCmdPalette.Helpers;

/// <summary>
///     Service for managing PostableCommand enumeration values and metadata
/// </summary>
public class PostableCommandHelper {
    private static readonly Lazy<PostableCommandHelper> _instance = new(() => new PostableCommandHelper());

    private readonly object _lockObject = new();

    private List<PostableCommandItem> _allCommands;

    private PostableCommandHelper() { }

    public static PostableCommandHelper Instance => _instance.Value;

    /// <summary>
    ///     Gets all PostableCommand items with metadata
    /// </summary>
    public List<PostableCommandItem> GetAllCommands() {
        if (this._allCommands == null) {
            lock (this._lockObject) // for multi-thread safety
            {
                if (this._allCommands == null)
                    this._allCommands = this.LoadPostableCommands();
            }
        }

        return this._allCommands;
    }

    /// <summary>
    ///     Filters commands based on search text using fuzzy matching
    /// </summary>
    public List<PostableCommandItem> FilterCommands(string searchText) {
        if (string.IsNullOrWhiteSpace(searchText)) {
            return this.GetAllCommands()
                .OrderByDescending(c => c.UsageCount)
                .ThenByDescending(c => c.LastUsed)
                .ToList();
        }

        var filtered = new List<PostableCommandItem>();
        var searchLower = searchText.ToLowerInvariant();

        foreach (var command in this.GetAllCommands()) {
            var score = this.CalculateSearchScore(command.Name.ToLowerInvariant(), searchLower);
            if (score > 0) {
                command.SearchScore = score;
                filtered.Add(command);
            }
        }

        return filtered
            .OrderByDescending(c => c.SearchScore)
            .ThenByDescending(c => c.UsageCount)
            .ThenByDescending(c => c.LastUsed)
            .ToList();
    }

    /// <summary>
    ///     Updates usage statistics for a command
    /// </summary>
    public void UpdateCommandUsage(CommandRef commandRef) {
        var commandItem = this.GetAllCommands().FirstOrDefault(c => c.Command == commandRef);
        if (commandItem is not null) {
            commandItem.UsageCount++;
            commandItem.LastUsed = DateTime.Now;
        }
    }

    /// <summary>
    ///     Loads all PostableCommand enum values and creates metadata
    /// </summary>
    private List<PostableCommandItem> LoadPostableCommands() {
        var commands = new List<PostableCommandItem>();
        var shortcutsService = KeyboardShortcutsHelper.Instance;

        // Get all values from the PostableCommand enumeration
        var postableCommands = Enum.GetValues(typeof(PostableCommand))
            .Cast<PostableCommand>()
            .ToList();

        foreach (var command in postableCommands) {
            // Get the command ID name for lookup
            var commandIdName = RevitCommandId.LookupPostableCommandId(command).Name;

            // Try to get shortcut info from XML
            var shortcutInfo = shortcutsService.GetShortcutInfo(commandIdName);

            var commandItem = new PostableCommandItem {
                Command = command,
                Name = shortcutInfo?.CommandName ?? this.FormatCommandName(command.ToString()),
                UsageCount = 0,
                LastUsed = DateTime.MinValue,
                SearchScore = 0,
                Shortcuts = shortcutInfo?.Shortcuts ?? new List<string>(),
                Paths = shortcutInfo?.Paths ?? new List<string>()
            };

            commands.Add(commandItem);
        }

        // Add addin commands (CustomCtrl_*) from shortcuts
        var allShortcuts = shortcutsService.GetShortcuts();
        foreach (var kvp in allShortcuts) {
            var shortcutInfo = kvp.Value;
            if (
                shortcutInfo.CommandId != null
                && shortcutInfo.CommandId.StartsWith(
                    "CustomCtrl_%CustomCtrl_%",
                    StringComparison.OrdinalIgnoreCase
                )
            ) {
                // Avoid duplicates if already added as a PostableCommand
                var alreadyExists = commands.Any(c =>
                    string.Equals(
                        c.Command.Value as string,
                        shortcutInfo.CommandId,
                        StringComparison.OrdinalIgnoreCase
                    )
                );
                if (alreadyExists)
                    continue;

                var commandItem = new PostableCommandItem {
                    Command = shortcutInfo.CommandId,
                    Name = shortcutInfo.CommandName ?? shortcutInfo.CommandId,
                    UsageCount = 0,
                    LastUsed = DateTime.MinValue,
                    SearchScore = 0,
                    Shortcuts = shortcutInfo.Shortcuts ?? new List<string>(),
                    Paths = shortcutInfo.Paths ?? new List<string>()
                };
                commands.Add(commandItem);
            }
        }

        return commands.OrderBy(c => c.Name).ToList();
    }

    /// <summary>
    ///     Formats the command name for better display
    /// </summary>
    private string FormatCommandName(string commandName) {
        // Convert PascalCase to readable format
        // e.g., "CreateWall" -> "Create Wall"
        if (string.IsNullOrEmpty(commandName))
            return commandName;

        var result = new StringBuilder();
        _ = result.Append(commandName[0]);

        for (var i = 1; i < commandName.Length; i++) {
            if (char.IsUpper(commandName[i]) && !char.IsUpper(commandName[i - 1])) _ = result.Append(' ');
            _ = result.Append(commandName[i]);
        }

        return result.ToString();
    }

    /// <summary>
    ///     Calculates search relevance score using fuzzy matching
    /// </summary>
    private double CalculateSearchScore(string text, string search) {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(search))
            return 0;

        // Exact match gets highest score
        if (text == search)
            return 100;

        // Starts with search gets high score
        if (text.StartsWith(search))
            return 90;

        // Contains search gets medium score
        if (text.Contains(search))
            return 70;

        // Fuzzy matching for partial matches
        var fuzzyScore = this.CalculateFuzzyScore(text, search);
        return fuzzyScore > 0.5 ? fuzzyScore * 50 : 0;
    }

    /// <summary>
    ///     Simple fuzzy matching algorithm
    /// </summary>
    private double CalculateFuzzyScore(string text, string search) {
        if (search.Length > text.Length)
            return 0;

        var matches = 0;
        var searchIndex = 0;

        for (var i = 0; i < text.Length && searchIndex < search.Length; i++) {
            if (text[i] == search[searchIndex]) {
                matches++;
                searchIndex++;
            }
        }

        return (double)matches / search.Length;
    }
}