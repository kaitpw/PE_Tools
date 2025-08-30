using AddinCmdPalette.Models;
using PeLib;
using PeRevitUI;
using PeServices;
using System.Text;
using System.Windows.Controls.Ribbon;

namespace AddinCmdPalette.Helpers;

/// <summary>
/// Record representing command usage data for storage
/// </summary>
public record CommandUsageData {
    public string CommandId { get; init; } = string.Empty;
    public int Score { get; init; }
    public DateTime LastUsed { get; init; }
}

/// <summary>
///     Service for managing PostableCommand enumeration values and metadata
/// </summary>
public class PostableCommandHelper {
    private List<PostableCommandItem> _allCommands;
    private readonly Storage _storage;

    public PostableCommandHelper(Storage storage) => _storage = storage;

    /// <summary>
    ///     Gets all PostableCommand items with metadata
    /// </summary>
    public List<PostableCommandItem> GetAllCommands() {
        if (this._allCommands == null) {
            this._allCommands = this.LoadPostableCommands();
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
            // Save the updated usage count to storage using the record
            var commandId = commandRef.Value.ToString() ?? string.Empty;
            var usageData = new CommandUsageData {
                CommandId = commandId,
                Score = commandItem.UsageCount,
                LastUsed = commandItem.LastUsed
            };
            _storage.State<CommandUsageData>().WriteCsvRow(commandId, usageData);
        }
    }

    /// <summary>
    ///     Refreshes the commands and shortcuts, clearing cached data
    /// </summary>
    public void RefreshCommands() => this._allCommands = null;// This will force a reload of both commands and shortcuts on next GetAllCommands() call

    /// <summary>
    ///     Loads all PostableCommand enum values and creates metadata
    /// </summary>
    private List<PostableCommandItem> LoadPostableCommands() {
        var commands = new List<PostableCommandItem>();
        var shortcutsService = KeyboardShortcutsHelper.Instance;

        // Check if shortcuts are current, if not, clear the cache to force reload
        if (!shortcutsService.IsShortcutsCurrent()) {
            shortcutsService.ClearCache();
        }

        // Get all values from the PostableCommand enumeration
        var ribbonCommands = PeRevitUI.Ribbon.GetAllCommands();

        foreach (var command in ribbonCommands) {
            var commandId = command.Id;
            var usageData = _storage.State<CommandUsageData>().ReadCsvRow(commandId); 
            
            var commandItem = new PostableCommandItem {
                Command = command.Id,
                UsageCount = usageData?.Score ?? 0,
                LastUsed = usageData?.LastUsed ?? DateTime.MinValue,
                SearchScore = 0,
            };
            // Try to get shortcut info from XML
            var (shortcutInfo, infoErr) = shortcutsService.GetShortcutInfo(command.Id);
            if (shortcutInfo is null) continue;
            if (infoErr is not null) {
                commandItem.Name = this.FormatCommandName(command.Name);
                commandItem.Paths = new List<string> { $"{command.Tab} > {command.Panel}" }; // TOTO: probablyRevise this logic
                continue;
            }
            if (shortcutInfo is not null) {
                commandItem.Name = shortcutInfo.CommandName;
                commandItem.Shortcuts = shortcutInfo.Shortcuts;
                commandItem.Paths = shortcutInfo.Paths;
            }
            commands.Add(commandItem);
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

        var baseScore = 0; 

        // Exact match gets highest score
        if (text == search)
            _ = baseScore + 100;

        // Starts with search gets high score
        if (text.StartsWith(search))
            _ = baseScore + 70;

        // Contains search gets medium score
        if (text.Contains(search))
            _ = baseScore + 90;

        // Fuzzy matching for partial matches
        var fuzzyScore = this.CalculateFuzzyScore(text, search);
        return fuzzyScore > 0.7 ? baseScore + (fuzzyScore * 50) : 0;
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