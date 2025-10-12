# Family Foundry Operations Architecture

## Overview

The Family Foundry system uses a typed operation pattern that provides:

1. **Type-safe settings** - Operations declare their settings type via generics
2. **Automatic settings discovery** - Settings are auto-extracted from the
   profile by type
3. **Self-describing operations** - Each operation has name/description metadata
4. **Batching optimization** - Consecutive operations of the same type are
   batched
5. **Frontend-ready** - Operation metadata can be queried for UI display

## Creating a New Operation

### 1. Define Settings (if needed)

```csharp
public class MyOperationSettings : IOperationSettings {
    [Description("Some configuration value")]
    [Required]
    public string SomeValue { get; set; }
    
    public int AnotherValue { get; set; }
}
```

**For operations with no settings**, use the built-in `NoSettings` type.

### 2. Implement the Operation

```csharp
public class MyOperation : Operation<MyOperationSettings> {
    public override OperationType Type => OperationType.Doc; // or OperationType.Type
    public override string Name => "My Operation";
    public override string Description => "Does something useful";
    
    protected override void ExecuteCore(Document doc, MyOperationSettings settings) {
        // Your operation logic here
        Debug.WriteLine($"Running with value: {settings.SomeValue}");
    }
}
```

### 3. Add Settings to Profile

```csharp
public class MyProfile : BaseProfileSettings {
    [Description("Settings for my operation")]
    [Required]
    public MyOperationSettings MyOperation { get; init; } = new();
}
```

**Important**: The property type must match the operation's settings type. The
property name can be anything.

### 4. Use in Command

```csharp
public class MyCommand : FamilyFoundryBase<MySettings, MyProfile>, IExternalCommand {
    public Result Execute(...) {
        this.Init();
        
        var queue = new OperationEnqueuer(doc, this._profile)
            .Add(new MyOperation())
            .Add(new AnotherOperation());
        
        // Optional: Get metadata for logging/UI
        var metadata = queue.GetOperationMetadata();
        foreach (var op in metadata) {
            Debug.WriteLine($"[Batch {op.BatchGroup}] {op.Name}");
        }
        
        this.ProcessQueue(queue);
        return Result.Succeeded;
    }
}
```

## Operation Types

### OperationType.Doc

Document-level operations run **once** on the entire family document.

**Use for:**

- Adding parameters
- Modifying geometry
- Deleting unused parameters
- Any operation that affects the whole family

### OperationType.Type

Type-level operations run **for each family type** in the document.

**Use for:**

- Remapping parameter values
- Setting type-specific values
- Any operation that needs to iterate through types

The enqueuer automatically batches consecutive type operations for better
performance.

## Batching Behavior

Operations are batched by type:

```csharp
.Add(new DocOp1())       // Batch 0 (Doc)
.Add(new DocOp2())       // Batch 0 (Doc)
.Add(new TypeOp1())      // Batch 1 (Type)
.Add(new TypeOp2())      // Batch 1 (Type)
.Add(new DocOp3())       // Batch 2 (Doc)
```

Type operations in the same batch only set the `FamilyManager.CurrentType` once
per type, improving performance.

## Error Handling

If an operation's settings are missing from the profile, you'll get a clear
error:

```
Operation 'My Operation' requires settings of type 'MyOperationSettings',
but profile 'MyProfile' does not have a property of this type.

Add this property to MyProfile:
    [Required]
    public MyOperationSettings MyOperation { get; init; } = new();
```

## Migration from Legacy API

The old lambda-based API is still supported but marked as obsolete:

```csharp
// OLD (deprecated)
.DocOperation(doc => doc.DoSomething())
.TypeOperation(doc => doc.DoSomethingElse())

// NEW (preferred)
.Add(new DoSomethingOperation())
.Add(new DoSomethingElseOperation())
```

## Examples

See existing operations for reference:

- `DeleteUnusedParamsOperation` - Simple doc operation with no settings
- `AddApsParamsOperationTyped` - Doc operation with complex settings
- `RemapParamsOperationTyped` - Type operation with settings
- `HydrateElectricalConnectorOperationTyped` - Doc operation with no settings
