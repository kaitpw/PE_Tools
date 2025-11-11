using AddinCmdPalette.Core;
using PeServices.Storage;
using PeServices.Storage.Core;

namespace AddinCmdPalette.Services;

/// <summary>
///     Standard implementation of search/filter service with fuzzy matching and persistence
/// </summary>
public class SearchFilterService {
    private readonly CsvReadWriter<ItemUsageData> _state;
    private readonly Func<ISelectableItem, string> _keyGenerator;
    private readonly double _minFuzzyScore;
    private readonly bool _enableUsageTracking;
    private Dictionary<string, ItemUsageData> _usageCache = new();

    public SearchFilterService(
        Storage storage,
        Func<ISelectableItem, string> keyGenerator,
        string persistenceKey,
        double minFuzzyScore = 0.7,
        bool enableUsageTracking = true
    ) {
        this._keyGenerator = keyGenerator;
        this._minFuzzyScore = minFuzzyScore;
        this._enableUsageTracking = enableUsageTracking;
        this._state = storage.StateDir().Csv<ItemUsageData>();
    }

    public List<ISelectableItem> Filter(string searchText, IEnumerable<ISelectableItem> items) {
        if (string.IsNullOrWhiteSpace(searchText)) {
            return items
                .OrderByDescending(this.GetUsageCount)
                .ThenByDescending(this.GetLastUsed)
                .ToList();
        }

        var filtered = new List<ISelectableItem>();
        var searchLower = searchText.ToLowerInvariant();

        foreach (var item in items) {
            var score = this.CalculateSearchScore(item.PrimaryText.ToLowerInvariant(), searchLower);
            if (score > 0) {
                item.SearchScore = score;
                filtered.Add(item);
            }
        }

        return filtered
            .OrderByDescending(i => i.SearchScore)
            .ThenByDescending(this.GetUsageCount)
            .ThenByDescending(this.GetLastUsed)
            .ToList();
    }

    public void RecordUsage(ISelectableItem item) {
        if (!this._enableUsageTracking) return;

        var key = this._keyGenerator(item);
        var existing = this._usageCache.GetValueOrDefault(key);
        var usageCount = (existing?.UsageCount ?? 0) + 1;

        var usageData = new ItemUsageData {
            ItemKey = key,
            UsageCount = usageCount,
            LastUsed = DateTime.Now
        };

        this._state.WriteRow(key, usageData);
        this._usageCache[key] = usageData;
    }

    public void LoadUsageData(IEnumerable<ISelectableItem> items) {
        if (!this._enableUsageTracking) return;

        this._usageCache = this._state.Read();
    }

    private int GetUsageCount(ISelectableItem item) {
        var key = this._keyGenerator(item);
        return this._usageCache.GetValueOrDefault(key)?.UsageCount ?? 0;
    }

    private DateTime GetLastUsed(ISelectableItem item) {
        var key = this._keyGenerator(item);
        return this._usageCache.GetValueOrDefault(key)?.LastUsed ?? DateTime.MinValue;
    }

    /// <summary>
    ///     Calculates search relevance score using fuzzy matching
    /// </summary>
    private double CalculateSearchScore(string text, string search) {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(search))
            return 0;

        var baseScore = 0.0;
        if (text == search) baseScore += 100;
        if (text.StartsWith(search)) baseScore += 70;
        if (text.Contains(search)) baseScore += 50;

        var fuzzyScore = this.CalculateFuzzyScore(text, search);
        if (fuzzyScore >= this._minFuzzyScore) {
            return baseScore + (fuzzyScore * 50);
        }

        return baseScore > 0 ? baseScore : 0;
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

