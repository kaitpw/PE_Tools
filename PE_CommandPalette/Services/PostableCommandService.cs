using System;
using System.Collections.Generic;
using System.Linq;
using PE_CommandPalette.Models;

namespace PE_CommandPalette.Services
{
    /// <summary>
    /// Service for managing PostableCommand enumeration values and metadata
    /// </summary>
    public class PostableCommandService
    {
        private static readonly Lazy<PostableCommandService> _instance = 
            new Lazy<PostableCommandService>(() => new PostableCommandService());

        private List<PostableCommandItem> _allCommands;
        private readonly object _lockObject = new object();

        public static PostableCommandService Instance => _instance.Value;

        private PostableCommandService()
        {
        }

        /// <summary>
        /// Gets all PostableCommand items with metadata
        /// </summary>
        public List<PostableCommandItem> GetAllCommands()
        {
            if (_allCommands == null)
            {
                lock (_lockObject)
                {
                    if (_allCommands == null)
                    {
                        _allCommands = LoadPostableCommands();
                    }
                }
            }

            return _allCommands;
        }

        /// <summary>
        /// Filters commands based on search text using fuzzy matching
        /// </summary>
        public List<PostableCommandItem> FilterCommands(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return GetAllCommands().OrderByDescending(c => c.UsageCount)
                                     .ThenByDescending(c => c.LastUsed)
                                     .ToList();
            }

            var filtered = new List<PostableCommandItem>();
            var searchLower = searchText.ToLowerInvariant();

            foreach (var command in GetAllCommands())
            {
                var score = CalculateSearchScore(command.Name.ToLowerInvariant(), searchLower);
                if (score > 0)
                {
                    command.SearchScore = score;
                    filtered.Add(command);
                }
            }

            return filtered.OrderByDescending(c => c.SearchScore)
                          .ThenByDescending(c => c.UsageCount)
                          .ThenByDescending(c => c.LastUsed)
                          .ToList();
        }

        /// <summary>
        /// Updates usage statistics for a command
        /// </summary>
        public void UpdateCommandUsage(PostableCommandItem command)
        {
            command.UsageCount++;
            command.LastUsed = DateTime.Now;
        }

        /// <summary>
        /// Loads all PostableCommand enum values and creates metadata
        /// </summary>
        private List<PostableCommandItem> LoadPostableCommands()
        {
            var commands = new List<PostableCommandItem>();

            // Get all values from the PostableCommand enumeration
            var postableCommands = Enum.GetValues(typeof(PostableCommand))
                                      .Cast<PostableCommand>()
                                      .ToList();

            foreach (var command in postableCommands)
            {
                var commandItem = new PostableCommandItem
                {
                    Command = command,
                    Name = FormatCommandName(command.ToString()),
                    Description = GetCommandDescription(command),
                    UsageCount = 0,
                    LastUsed = DateTime.MinValue,
                    SearchScore = 0
                };

                commands.Add(commandItem);
            }

            return commands.OrderBy(c => c.Name).ToList();
        }

        /// <summary>
        /// Formats the command name for better display
        /// </summary>
        private string FormatCommandName(string commandName)
        {
            // Convert PascalCase to readable format
            // e.g., "CreateWall" -> "Create Wall"
            if (string.IsNullOrEmpty(commandName))
                return commandName;

            var result = new System.Text.StringBuilder();
            result.Append(commandName[0]);

            for (int i = 1; i < commandName.Length; i++)
            {
                if (char.IsUpper(commandName[i]) && !char.IsUpper(commandName[i - 1]))
                {
                    result.Append(' ');
                }
                result.Append(commandName[i]);
            }

            return result.ToString();
        }

        /// <summary>
        /// Gets a description for the command (placeholder for now)
        /// </summary>
        private string GetCommandDescription(PostableCommand command)
        {
            // This could be expanded to include actual command descriptions
            // For now, return a generic description
            return $"Execute {FormatCommandName(command.ToString())} command";
        }

        /// <summary>
        /// Calculates search relevance score using fuzzy matching
        /// </summary>
        private double CalculateSearchScore(string text, string search)
        {
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
            var fuzzyScore = CalculateFuzzyScore(text, search);
            return fuzzyScore > 0.5 ? fuzzyScore * 50 : 0;
        }

        /// <summary>
        /// Simple fuzzy matching algorithm
        /// </summary>
        private double CalculateFuzzyScore(string text, string search)
        {
            if (search.Length > text.Length)
                return 0;

            var matches = 0;
            var searchIndex = 0;

            for (int i = 0; i < text.Length && searchIndex < search.Length; i++)
            {
                if (text[i] == search[searchIndex])
                {
                    matches++;
                    searchIndex++;
                }
            }

            return (double)matches / search.Length;
        }
    }
}