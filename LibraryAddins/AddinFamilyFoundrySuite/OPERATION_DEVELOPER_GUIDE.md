# Family Foundry Operation Guide

## Operation Types

### DocOperation

Use for **document-level** operations (runs once per family).

```csharp
public class MyDocOp : DocOperation<MySettings> {
    public MyDocOp(MySettings settings) : base(settings) { }
    public override string Description => "What this does";
    
    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();
        // Your logic here - executes once for the entire family
        return new OperationLog(this.Name, logs);
    }
}
```

### TypeOperation

Use for **type-level** operations (runs once per family type, e.g.,
reading/writing type parameter values).

```csharp
public class MyTypeOp : TypeOperation<MySettings> {
    public MyTypeOp(MySettings settings) : base(settings) { }
    public override string Description => "What this does";
    
    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();
        // Your logic here - executes for each family type
        // FamilyManager.CurrentType is already set by the framework
        return new OperationLog(this.Name, logs);
    }
}
```

## Settings

```csharp
public class MySettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
    // Add your properties here
}
```

## Logging

```csharp
// Success
logs.Add(new LogEntry { Item = "ParameterName" });

// Failure
logs.Add(new LogEntry { Item = "ParameterName", Error = ex.Message });
```

## Minimal Sandbox Test

```csharp
// 1. Hardcode settings
var settings = new MySettings { Enabled = true };

// 2. Create operation
var operation = new MyDocOp(settings);

// 3. Create queue and processor
var queue = new OperationQueue().Add(operation);
var processor = new OperationProcessor<BaseProfileSettings>(
    doc,
    _ => new List<Family>(),
    _ => new List<(ExternalDefinition, ForgeTypeId, bool)>(),
    new ExecutionOptions { SingleTransaction = true }
);

// 4. Execute
var (results, totalMs) = processor.ProcessQueue(
    queue,
    @"C:\Temp\Output",
    new TestLoadAndSaveOptions()
);

// 5. Print results
foreach (var (familyName, (logs, _)) in results) {
    foreach (var log in logs) {
        Console.WriteLine($"{log.OperationName}: {log.SuccessCount} success, {log.FailedCount} failed");
        foreach (var entry in log.Entries) {
            var status = entry.Error is null ? "✓" : "✗";
            Console.WriteLine($"  {status} {entry.Item} {entry.Error}");
        }
    }
}

processor.Dispose();

// Helper class
class TestLoadAndSaveOptions : ILoadAndSaveOptions {
    public bool LoadFamily { get; set; } = false;
    public bool SaveFamilyToInternalPath { get; set; } = false;
    public bool SaveFamilyToOutputDir { get; set; } = true;
}
```

## Complete Example

```csharp
using AddinFamilyFoundrySuite.Core;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class DeleteUnusedParams : DocOperation<DeleteUnusedParamsSettings> {
    public DeleteUnusedParams(DeleteUnusedParamsSettings settings) : base(settings) { }
    
    public override string Description => "Delete unused parameters";
    
    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();
        var parameters = doc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .Where(p => !ParameterUtils.IsBuiltInParameter(p.Id))
            .ToList();
        
        foreach (var param in parameters) {
            try {
                if (!param.AssociatedParameters.Cast<Parameter>().Any()) {
                    doc.FamilyManager.RemoveParameter(param);
                    logs.Add(new LogEntry { Item = param.Definition.Name });
                }
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = param.Definition.Name, Error = ex.Message });
            }
        }
        
        return new OperationLog(this.Name, logs);
    }
}

public class DeleteUnusedParamsSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
}
```
