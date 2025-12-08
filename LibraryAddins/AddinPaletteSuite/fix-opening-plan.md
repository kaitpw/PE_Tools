# Fix Family Document Temp Path Instability

## Status: IMPLEMENTED ✓

Changes implemented in `Library/PeExtensions/UiApplication/UiApplication.cs` on
2025-12-08.

## Problem Analysis

When switching to an already-open family document, the OLD code:

1. Created a **new GUID-based temp directory** every time
   (`Guid.NewGuid().ToString()`)
2. Called `SaveAs` to save the family document to this new path
3. Called `OpenAndActivateDocument` with the new temp path

This caused `doc.PathName` to change each activation, breaking MRU duplicate
detection and potentially causing confusion about which file the user is
editing.

## Why Temp Files Are Necessary (Verified via Testing)

Testing conducted on 2025-12-08 confirmed:

| Document Type                     | Can Re-Open via OpenAndActivateDocument?           |
| --------------------------------- | -------------------------------------------------- |
| Local .rvt                        | YES - works with both string and ModelPath         |
| Cloud .rvt                        | YES - but MUST use ModelPath overload (not string) |
| Family .rfa (opened from disk)    | NO - FileNotFoundException even if file exists     |
| Family via EditFamily (fresh)     | NO - document has NO PathName (empty string)       |
| Family WITH PathName (temp saved) | **YES** - can be re-opened directly!               |

Key findings:

- `EditFamily` creates a document with **NO PathName** (empty string, not
  pointing to source)
- Revit throws `FileNotFoundException` when trying to re-open .rfa files opened
  directly from disk, even when the file exists
- `ShowElements` is unreliable for activating already-open documents from
  another document context
- **NEW**: Once a family has a PathName (from a previous temp save),
  `OpenAndActivateDocument` works directly without needing SaveAs again!

## Solution (IMPLEMENTED)

Two-part optimization:

### 1. Use Stable Temp Paths

Instead of random GUIDs, use consistent paths:

```
%TEMP%\PE_Tools_FamilyCache\{sanitizedFamilyName}.rfa
```

This ensures the same family document always uses the same temp path within and
across Revit sessions.

### 2. Skip SaveAs When PathName Exists

If a family document already has a PathName (from a previous activation), use
`OpenAndActivateDocument` directly without calling SaveAs. This:

- Avoids unnecessary file I/O
- Keeps the PathName stable
- Preserves MRU tracking

## Implementation (COMPLETE)

### File: `Library/PeExtensions/UiApplication/UiApplication.cs`

#### New Method: `SaveFamilyToStableTempFile`

```csharp
private static string SaveFamilyToStableTempFile(Document famDoc, string familyName) {
    // Use a stable directory (no GUIDs) so the same family always gets the same path
    var tempDir = Path.Combine(Path.GetTempPath(), "PE_Tools_FamilyCache");
    if (!Directory.Exists(tempDir))
        _ = Directory.CreateDirectory(tempDir);

    // Sanitize family name and ensure no double extension
    var baseName = Path.GetFileNameWithoutExtension(familyName);
    if (string.IsNullOrEmpty(baseName)) baseName = familyName;
    var safeName = SanitizeFileName(baseName);
    var tempPath = Path.Combine(tempDir, $"{safeName}.rfa");

    Debug.WriteLine($"[SaveFamilyToStableTempFile] Saving to stable temp path: {tempPath}");
    famDoc.SaveAs(tempPath, new SaveAsOptions { OverwriteExistingFile = true });

    return tempPath;
}

private static string SanitizeFileName(string name) {
    var invalid = Path.GetInvalidFileNameChars();
    return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
}
```

#### Updated Method: `ActivateOpenFamilyDocument`

```csharp
private static void ActivateOpenFamilyDocument(UIApplication uiApp, Document famDoc, string familyName) {
    // OPTIMIZATION: If family already has a PathName, try direct activation first
    if (!string.IsNullOrEmpty(famDoc.PathName)) {
        Debug.WriteLine($"[ActivateOpenFamilyDocument] Family has PathName, trying direct activation");
        try {
            _ = uiApp.OpenAndActivateDocument(famDoc.PathName);
            return; // Success!
        } catch (Exception ex) {
            Debug.WriteLine($"[ActivateOpenFamilyDocument] Direct activation failed, falling back to SaveAs");
        }
    }

    // No PathName or direct activation failed - use SaveAs to stable temp path
    var tempPath = SaveFamilyToStableTempFile(famDoc, familyName);
    _ = uiApp.OpenAndActivateDocument(tempPath);
}
```

### Cleanup: ViewReference/MruViewBuffer Workarounds

The workarounds added earlier are now less critical but kept as defensive
coding:

- `ViewReference.IsTempOrFamilyPath()` - safety net for temp path detection
- `MruViewBuffer.GetDocumentKey()` - uses Title for temp paths

## Flow Diagram

```
Family Activation Request
         │
         ▼
  Is family already open?
         │
    ┌────┴────┐
    │ YES     │ NO
    ▼         ▼
  Is it      EditFamily()
  active?        │
    │            ▼
 ┌──┴──┐    Family doc created
 │YES  │NO  (no PathName)
 │     │         │
 ▼     ▼         ▼
Done  Has PathName? ◄──────┐
         │                 │
    ┌────┴────┐            │
    │ YES     │ NO         │
    ▼         ▼            │
  Try       SaveAs to      │
  direct    stable temp ───┘
  open         │
    │          ▼
    │    OpenAndActivate
    │          │
    └──────────┴─────► Done
```

## Edge Cases Considered

1. **Two families with same name from different projects**: The temp file will
   be overwritten when switching between them, but this is acceptable since you
   can only have one active at a time.

2. **User makes edits then switches away and back**: SaveAs (when needed) saves
   the current state. Subsequent activations use the existing PathName and skip
   SaveAs entirely, preserving the in-memory document state.

3. **Invalid characters in family name**: The `SanitizeFileName` helper handles
   this.

4. **Temp file cleanup**: Files accumulate in `PE_Tools_FamilyCache` but are
   small (.rfa) and overwritten. Can add cleanup on Revit startup if needed
   later.

## Test Evidence

Test results in `PE_Tools/test-reopen-fam-output.txt` show:

- **Fresh EditFamily**: PathName is empty, SaveAs required, works correctly
- **Already-open family WITH PathName**: Direct `OpenAndActivateDocument` works!
- **ShowElements**: Unreliable for cross-document activation (confirmed failure)
- **Temp file workaround**: 100% success rate across all test scenarios
