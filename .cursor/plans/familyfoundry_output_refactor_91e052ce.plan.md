---
name: FamilyFoundry Output Refactor
overview: Refactor FamilyFoundry with a canonical ParamSnapshot model that is deserializable/replayable, dual JSON+CSV output, timestamped directory structure, and family document collector support.
todos:
  - id: param-snapshot-model
    content: Create canonical ParamSnapshot record with ValuesPerType dict, replacing ParamCollectionResult
    status: pending
  - id: snapshot-compilation
    content: Create ParamSnapshotCompiler to generate FamilyParamModel and AddAndSetParamsSettings from snapshots
    status: pending
  - id: family-snapshot-container
    content: Create FamilySnapshot container class for extensible multi-section snapshots
    status: pending
  - id: collector-interface
    content: Create ISnapshotCollector<TInput> generic interface for composable collectors
    status: pending
  - id: param-collectors
    content: Create ProjectParamCollector and FamilyDocParamCollector implementations
    status: pending
  - id: update-context
    content: Update FamilyProcessingContext to use FamilySnapshot for pre/post snapshots
    status: pending
  - id: processor-famDoc
    content: Update OperationProcessor to use appropriate collector based on document type
    status: pending
  - id: output-structure
    content: Refactor ProcessingResultBuilder to create timestamped dir with family subdirs
    status: pending
  - id: json-serializer
    content: Add JSON serialization for ParamSnapshot list (readable, replayable format)
    status: pending
  - id: csv-serializer
    content: Add CSV serialization with per-type columns, plus CSV deserializer for replay
    status: pending
  - id: settings-output
    content: Add settings.json output per family with deserialized runtime settings
    status: pending
  - id: deprecate-old-ops
    content: Deprecate/remove LogFamilyParamsState (replaced by snapshot output)
    status: pending
  - id: update-commands
    content: Update CmdFFManager and other commands for new collector/output patterns
    status: pending
---

# FamilyFoundry Output and Collector Refactor

## Design Goals

Optimize for these (somewhat competing) concerns:

- **Readability**: minimal verbosity, easy to scan JSON/CSV
- **Writability**: single source of truth, no duplicate models to maintain
- **Comprehensive snapshot**: full audit trail of all values per type
- **Reversibility**: can deserialize and re-apply the same state

---

## Architecture Note: Extensibility for Future Collectors

The collector architecture is designed to be **composable and extensible**.

While this implementation focuses on parameters, the structure supports future

collectors for:

- Reference planes
- Dimensions
- Connectors
- Other family elements

These will likely need to be performed inside a Family Document (unlike our

current TempInstanceParamCollector).

The `FamilySnapshot` acts as a container that can hold multiple "sections" of

collected data, and collectors implement a generic interface allowing them to be

mixed and matched.

---

## 1. Create Canonical ParamSnapshot Model (Single Source of Truth)

This replaces `ParamCollectionResult` with a **deserializable** record that can

regenerate the same family state. It is a superset of

`SetParamModel`/`SetParamPerTypeModel` - you can derive those from this.

**New file:**

`LibraryAddins/AddinFamilyFoundrySuite/Core/Aggregators/Snapshots/ParamSnapshot.cs`

```csharp
/// <summary>
/// Canonical parameter snapshot - single source of truth for:
/// - Parameter definition (can recreate the param)
/// - Assignment mode (formula vs values)
/// - Per-type values (audit + replay)
/// </summary>
public record ParamSnapshot {
    // Identity
    [Required] public required string Name { get; init; }
    [Required] public required bool IsInstance { get; init; }

    // Definition (enough to create the parameter)
    public ForgeTypeId PropertiesGroup { get; init; } = new ForgeTypeId("");
    public ForgeTypeId DataType { get; init; } = SpecTypeId.String.Text;

    // Assignment mode - if Formula != null, it is the authoritative assignment
    public string? Formula { get; init; }

    // Per-type values: TypeName -> setter-acceptable string value
    // Null/empty means no value for that type
    public Dictionary<string, string?> ValuesPerType { get; init; } = new();

    // Audit metadata (not required for replay, but useful)
    public bool IsBuiltIn { get; init; } = false;
    public Guid? SharedGuid { get; init; } = null;
    public StorageType? StorageType { get; init; } = null;
}
```

**JSON output example (readable + replayable):**

```json
{
  "name": "Width",
  "isInstance": false,
  // we need to de/serialize properly, use our existing ForgeTypeIdConverter
  // and Nice3point.Revit.Extensions ForgeTypeId.ToLabel()
  "propertiesGroup": "Other", 
  "dataType": "Length", // autodesk.spec.aec:length-2.0.0 is underlying value, not user friendly
  
  "formula": null,
  "valuesPerType": {
    "Standard": "900",
    "Wide": "1200"
  }
}
```

---

## 2. Create Snapshot Compilation (Derive Settings from Snapshot)

This creates the coupling between `ParamSnapshot` and your existing

`AddAndSetParamsSettings`. The snapshot compiles down to operational settings.

**New file:**

`LibraryAddins/AddinFamilyFoundrySuite/Core/Aggregators/Snapshots/ParamSnapshotCompiler.cs`

```csharp
public static class ParamSnapshotCompiler {
    /// <summary>
    /// Convert snapshot to FamilyParamModel for AddFamilyParams operation.
    /// </summary>
    public static FamilyParamModel ToFamilyParamModel(this ParamSnapshot p) =>
        new() { 
            Name = p.Name, 
            PropertiesGroup = p.PropertiesGroup, 
            DataType = p.DataType, 
            IsInstance = p.IsInstance,
            Formula = p.Formula
        };

    /// <summary>
    /// Extract global assignments (formulas or uniform values across all types).
    /// </summary>
    public static IEnumerable<SetParamModel> ToGlobalAssignments(this IEnumerable<ParamSnapshot> snapshots) {
        foreach (var p in snapshots.Where(s => !s.IsBuiltIn)) {
            // Formula takes precedence
            if (!string.IsNullOrWhiteSpace(p.Formula)) {
                yield return new SetParamModel {
                    Name = p.Name, DataType = p.DataType, IsInstance = p.IsInstance,
                    PropertiesGroup = p.PropertiesGroup,
                    ValueOrFormula = p.Formula,
                    SetAsFormula = true
                };
                continue;
            }

            // If all non-null values are identical -> global value
            var distinct = p.ValuesPerType.Values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct().ToList();
            if (distinct.Count == 1) {
                yield return new SetParamModel {
                    Name = p.Name, DataType = p.DataType, IsInstance = p.IsInstance,
                    PropertiesGroup = p.PropertiesGroup,
                    ValueOrFormula = distinct[0],
                    SetAsFormula = false
                };
            }
        }
    }

    /// <summary>
    /// Extract per-type assignments (different values per type).
    /// </summary>
    public static IEnumerable<SetParamPerTypeModel> ToPerTypeAssignments(this IEnumerable<ParamSnapshot> snapshots) {
        foreach (var p in snapshots.Where(s => !s.IsBuiltIn)) {
            if (!string.IsNullOrWhiteSpace(p.Formula)) continue;

            var distinct = p.ValuesPerType.Values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct().ToList();
            if (distinct.Count <= 1) continue; // handled by global

            yield return new SetParamPerTypeModel {
                Name = p.Name, DataType = p.DataType, IsInstance = p.IsInstance,
                PropertiesGroup = p.PropertiesGroup,
                ValuesPertype = p.ValuesPerType
                    .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                    .ToDictionary(kv => kv.Key, kv => kv.Value!)
            };
        }
    }

    /// <summary>
    /// Compile full snapshot list into AddAndSetParamsSettings.
    /// </summary>
    public static AddAndSetParamsSettings ToAddAndSetSettings(this IEnumerable<ParamSnapshot> snapshots) => new() {
        Parameters = snapshots.ToGlobalAssignments().ToList(),
        ParametersPerType = snapshots.ToPerTypeAssignments().ToList()
    };
}
```

This replaces `LogFamilyParamsState` - the snapshot output now serves as both

audit log AND replay source.

---

## 3. Create FamilySnapshot Container

Container for all collected data, extensible for future collectors.

**New file:**

`LibraryAddins/AddinFamilyFoundrySuite/Core/Aggregators/Snapshots/FamilySnapshot.cs`

```csharp
/// <summary>
/// Container for all snapshot data collected from a family.
/// Designed to be extensible - future collectors add their sections here.
/// </summary>
public class FamilySnapshot {
    public required string FamilyName { get; init; }
    public DateTime CollectedAt { get; init; } = DateTime.Now;
    
    // Parameter snapshots (current scope)
    public List<ParamSnapshot> Parameters { get; set; } = [];
    
    // Future extensibility (not implemented yet):
    // public List<RefPlaneSnapshot> ReferencePlanes { get; set; } = [];
    // public List<DimensionSnapshot> Dimensions { get; set; } = [];
    // public List<ConnectorSnapshot> Connectors { get; set; } = [];
}
```

---

## 4. Create Generic Collector Interface

Design a generic interface that different collectors can implement. This allows

composing multiple collectors for a single snapshot run.

**New file:**

`LibraryAddins/AddinFamilyFoundrySuite/Core/Aggregators/ISnapshotCollector.cs`

```csharp
/// <summary>
/// Generic collector interface. TInput varies by context:
/// - (Document, Family) for project-based collection
/// - FamilyDocument for family-doc-based collection
/// </summary>
public interface ISnapshotCollector<TInput> {
    void Collect(TInput input, FamilySnapshot snapshot);
}

// Convenience interfaces for the two collection contexts
public interface IProjectSnapshotCollector : ISnapshotCollector<(Document doc, Family family)> { }
public interface IFamilyDocSnapshotCollector : ISnapshotCollector<FamilyDocument> { }
```

---

## 5. Create Parameter Collector Implementations

**Modify:** `TempInstanceParamCollector.cs` -> Rename to

`ProjectParamCollector.cs`

- Implements `IProjectSnapshotCollector`
- Refactor to populate `FamilySnapshot.Parameters` with actual `ParamSnapshot`

records

- Collects `ValuesPerType` by iterating all symbols

**New file:** `FamilyDocParamCollector.cs`

- Implements `IFamilyDocSnapshotCollector`
- Iterates `FamilyManager.Types` directly to collect parameter values
- No temp instance needed since we're already in the family document

---

## 6. Update FamilyProcessingContext

**Modify:**

`LibraryAddins/AddinFamilyFoundrySuite/Core/FamilyProcessingContext.cs`

```csharp
public FamilySnapshot? PreProcessSnapshot { get; set; }
public FamilySnapshot? PostProcessSnapshot { get; set; }
```

Update helper methods to query from `FamilySnapshot.Parameters`.

---

## 7. Update OperationProcessor for Dual-Context Collection

**Modify:** `LibraryAddins/AddinFamilyFoundrySuite/Core/OperationProcessor.cs`

The processor will accept both collector types and use the appropriate one:

```csharp
public class OperationProcessor(
    Document doc,
    ExecutionOptions executionOptions = null,
    IProjectSnapshotCollector projectCollector = null,
    IFamilyDocSnapshotCollector familyDocCollector = null
) { ... }
```

- `ProcessNormalDocument`: Uses `projectCollector` with `(doc, family)` tuple
- `ProcessFamilyDocument`: Uses `familyDocCollector` with `FamilyDocument`

Both populate `FamilySnapshot` which gets stored in context.

---

## 8. Restructure Output Directory Layout

Change from flat timestamped files to:

```
output/
  2024-12-11_14-30-00/           # Timestamped run directory
    FamilyName1/
      presnapshot.json           # Full ParamSnapshot list (replayable)
      presnapshot.csv            # Per-type columns (replayable)
      postsnapshot.json
      postsnapshot.csv
      abridged.json              # Summary with grouped errors
      detailed.json              # Full operation logs
      settings.json              # Deserialized runtime settings
    FamilyName2/
      ...
```

**Modify:**

`LibraryAddins/AddinFamilyFoundrySuite/Core/ProcessingResultBuilder.cs`

- `WriteOutput` creates timestamped run directory
- For each `FamilyProcessingContext`, create family-named subdirectory
- Write 7 files per family (both JSON and CSV for snapshots)

---

## 9. Dual-Format Snapshot Serialization

### JSON Format (readable, replayable)

Standard JSON serialization of `List<ParamSnapshot>`. Already shown in

section 1.

### CSV Format (spreadsheet-friendly, replayable)

Pivot the data so each family type becomes a column.

**New file:**

`LibraryAddins/AddinFamilyFoundrySuite/Core/Aggregators/Snapshots/SnapshotSerializer.cs`

**CSV output example:**

```csv
Name,IsInstance,PropertiesGroup,DataType,Formula,Standard,Wide,Fire Rated
Width,false,PG_GEOMETRY,length,,"900","1200","900"
Height,false,PG_GEOMETRY,length,,"2100","2100","2400"
Material,false,PG_MATERIALS,text,,Wood,Wood,Steel
Panel Count,false,PG_GEOMETRY,integer,Width / 300,,,
```

Columns:

- Fixed columns: `Name`, `IsInstance`, `PropertiesGroup`, `DataType`, `Formula`
- Dynamic columns: one per family type name (values from `ValuesPerType`)

**CSV Deserializer:** Reads the CSV back into `List<ParamSnapshot>`,

reconstructing `ValuesPerType` from the type columns.

```csharp
public static class SnapshotSerializer {
    // JSON
    public static string ToJson(this List<ParamSnapshot> snapshots) { ... }
    public static List<ParamSnapshot> FromJson(string json) { ... }
    
    // CSV (with type columns)
    public static string ToCsv(this List<ParamSnapshot> snapshots) { ... }
    public static List<ParamSnapshot> FromCsv(string csv) { ... }
}
```

---

## 10. Settings Output

Add `settings.json` per family containing the **deserialized runtime settings

object** (not raw file copy). This captures any defaults applied during

deserialization.

---

## 11. Delete LogFamilyParamsState And AddFamilyParams

The new snapshot output (JSON + CSV) replaces `LogFamilyParamsState`. delete it and update FFManagerSnapshot. Also delete AddFamilyParams and update FFManager with AddAndSetParams.

---

## 12. Update Command Files

**Modify:** `CmdFFManager.cs` and other FF commands:

```csharp
// Create collectors
var projectCollector = new ProjectParamCollector();
var familyDocCollector = new FamilyDocParamCollector();

using var processor = new OperationProcessor(
    doc, 
    executionOptions, 
    projectCollector, 
    familyDocCollector
);
```

The processor automatically uses the right collector based on

`doc.IsFamilyDocument`.

---

## Summary: File Changes

| Action | Path |

|--------|------|

| Create | `Core/Aggregators/Snapshots/ParamSnapshot.cs` |

| Create | `Core/Aggregators/Snapshots/ParamSnapshotCompiler.cs` |

| Create | `Core/Aggregators/Snapshots/FamilySnapshot.cs` |

| Create | `Core/Aggregators/Snapshots/SnapshotSerializer.cs` |

| Create | `Core/Aggregators/ISnapshotCollector.cs` |

| Create | `Core/Aggregators/FamilyDocParamCollector.cs` |

| Rename | `TempInstanceParamCollector.cs` -> `ProjectParamCollector.cs` |

| Modify | `Core/Aggregators/IFamilyParamCollector.cs` (deprecate or delete) |

| Modify | `Core/FamilyProcessingContext.cs` |

| Modify | `Core/OperationProcessor.cs` |

| Modify | `Core/ProcessingResultBuilder.cs` |

| Deprecate | `Core/Operations/LogFamilyParamsState.cs` |

| Modify | `Cmds/CmdFFManager.cs` (and other FF commands) |