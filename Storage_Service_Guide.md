# Storage Service Quick Reference Guide

## Overview

The Storage Service (`PeServices.Storage`) provides type-safe, standardized file
storage for Revit addins. It enforces a consistent folder structure and provides
generic wrappers for JSON and CSV operations with schema validation.

## Base Path Structure

All storage is organized under: `MyDocuments/PE_Tools/{addinName}/`

Each addin gets its own directory with three storage types:

- **Settings** (`settings/`): Read-only configuration files
- **State** (`state/`): Read/write stateful data (supports granular CSV updates)
- **Output** (`output/`): Write-only user-facing results

## Core Usage Pattern

```csharp
// 1. Instantiate Storage with your addin name
var storage = new Storage("FF Manager");

// 2. Access storage managers
var settingsManager = storage.Settings();
var stateManager = storage.State();
var outputManager = storage.Output();

// 3. Read/Write with type safety
var settings = settingsManager.Json<MySettings>().Read();
stateManager.Csv<MyState>().WriteRow("key", data);
outputManager.Json<object>("results.json").Write(results);
```

## Storage Managers

### Settings Manager (`storage.Settings()`)

- **Purpose**: Configuration files that persist between sessions
- **Access**: Read-only JSON/CSV
- **Default file**: `settings/settings.json`
- **Profiles**: Supports profile subdirectory
  (`settings/profiles/{ProfileName}.json`)

```csharp
var settings = storage.Settings().Json<BaseSettings<TProfile>>().Read();
var profile = settings.GetProfile(settingsManager); // Loads from profiles/{CurrentProfile}.json
```

### State Manager (`storage.State()`)

- **Purpose**: Frequently updated stateful data
- **Access**: Full read/write JSON, granular CSV row operations
- **Default files**: `state/state.json` or `state/state.csv`
- **CSV Features**: Dictionary-based with key-value row operations

```csharp
// CSV: Read all, read row, write row
var csv = storage.State().Csv<MyRecord>();
var allData = csv.Read(); // Returns Dictionary<string, MyRecord>
var row = csv.ReadRow("key");
csv.WriteRow("key", new MyRecord { ... });

// JSON: Full read/write
var json = storage.State().Json<MyState>();
var state = json.Read();
json.Write(updatedState);
```

### Output Manager (`storage.Output()`)

- **Purpose**: User-facing output files (logs, reports, exports)
- **Access**: Write-only
- **No default file**: Must specify filename
- **Common use**: Timestamped JSON reports

```csharp
var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
storage.Output().Json<object>($"{timestamp}.json").Write(results);
var outputPath = storage.Output().GetFolderPath();
```

## Global Storage

For cross-addin shared data, use the `Global()` manager. All global data is stored in the `Global\` directory:

```csharp
// Global settings (Global/settings.json)
var globalSettings = Storage.Global().Settings().Read();

// Global state (Global/{filename}.json or Global/{filename}.csv)
var cache = Storage.Global().State("parameters-service-cache").Json<ParamModel>().Read();

// Global logging (Global/log.txt, auto-cleanup, max 500 lines)
Storage.Global().Log("Message");
```

## Family Foundry Integration Pattern

Family Foundry commands follow this standard pattern:

```csharp
public Result Execute(ExternalCommandData commandData, ...) {
    // 1. Initialize storage
    var storage = new Storage("FF Manager");
    var settingsManager = storage.Settings();
    
    // 2. Load settings and active profile
    var settings = settingsManager.Json<BaseSettings<ProfileFamilyManager>>().Read();
    var profile = settings.GetProfile(settingsManager);
    var outputFolderPath = storage.Output().GetFolderPath();
    
    // 3. Access global state (e.g., APS parameters cache)
    var apsParams = Storage.Global().State("parameters-service-cache")
        .Json<ParamModel>().Read();
    
    // 4. Write output files
    var (summary, detailed) = OperationLogger.GenerateLogData(...);
    storage.Output().Json<object>($"{timestamp}.json").Write(summary);
    
    // 5. Optionally open output files
    if (settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish) {
        FileUtils.OpenInDefaultApp(outputPath);
    }
}
```

## Key Features

### Type Safety

All operations are generic: `Json<T>` and `Csv<T>` where `T : class, new()`.
Your classes serve as both the runtime type and schema definition.

### JSON Schema Validation

- Automatic schema generation from class properties
- Validation on read/write with helpful error messages
- Auto-recovery: Invalid JSON files are fixed and re-saved with default values
- Schema files saved as `{filename}.schema.json` alongside JSON files

### CSV Dictionary Model

CSV files use a key-value model:

- First column is always the key
- Remaining columns map to properties of `T`
- Supports `Read()`, `ReadRow(key)`, `WriteRow(key, data)`, `Write(dictionary)`
- Automatic type conversion for primitives, enums, nullable types

### Profile System

Settings support multiple profiles:

- Main `settings.json` contains `CurrentProfile` and global options
- Profile-specific data in `settings/profiles/{ProfileName}.json`
- Load via `settings.GetProfile(settingsManager)`

## Common Patterns

### Command Usage Tracking (PostableCommandHelper example)

```csharp
public class PostableCommandHelper(Storage storage) {
    private readonly CsvReadWriter<CommandUsageData> _state = 
        storage.State().Csv<CommandUsageData>();
    
    public void UpdateUsage(CommandRef cmd) {
        var data = new CommandUsageData {
            CommandId = cmd.Value.ToString(),
            Score = currentScore + 1,
            LastUsed = DateTime.Now
        };
        _state.WriteRow(data.CommandId, data);
    }
}
```

### Cache Validation

```csharp
var cache = Storage.Global().State("cache").Json<CacheData>();
if (!cache.IsCacheValid(maxAgeMinutes: 60, validator: c => c.Items.Any())) {
    // Refresh cache
}
```

## Important Notes

1. **File Creation**: JSON files are auto-created with default values if missing
   (unless `throwIfNotExists: true`)
2. **CSV Requirements**: Record types must have parameterless constructor and
   public properties
3. **JSON Requirements**: Classes must have `[Required]` attributes on critical
   properties for validation
4. **Error Handling**: Storage operations throw exceptions on validation
   failures - no silent fallbacks
5. **Thread Safety**: Not thread-safe - designed for single-threaded Revit API
   context

## See Also

- `Library/PeServices/Storage/Storage.md` - Detailed design document
- `LibraryAddins/AddinFamilyFoundrySuite/Cmds/CmdFFManager.cs` - Full command
  example
- `LibraryAddins/AddinCmdPalette/Helpers/PostableCommandHelper.cs` - CSV state
  usage example
