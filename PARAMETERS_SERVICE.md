# Understanding Revit Parameter Download API with APS JSON

This guide explains how to use the Revit API's parameter download functionality with APS (Autodesk Platform Services) JSON data.

## 1. DownloadParameter and DownloadParameterOptions Methods

### ParameterUtils.DownloadParameterOptions(ForgeTypeId parameterTypeId)

This method retrieves a parameter's configuration from the Parameters Service.

**Arguments:**

- **parameterTypeId**: A `ForgeTypeId` object constructed with the parameter ID from the APS JSON.
  - **Required JSON Part**: The `id` field in the parameter JSON object
  - **Example**: `"parameters.6cd2c1f1f48d41f4aa5f4b51e535595f:f7b120fafd3141dc8b44e7d2757c0b77-1.0.0"`

This method returns a `ParameterDownloadOptions` object which contains:
- Categories bound to the parameter
- Whether the parameter is instance or type based
- Parameter visibility
- Group assignment in properties palette

### ParameterUtils.DownloadParameter(Document document, ParameterDownloadOptions options, ForgeTypeId parameterTypeId)

This method creates a shared parameter element in the document based on a definition downloaded from the Parameters Service.

**Arguments:**

- **document**: The Revit document in which to create the parameter
- **options**: A `ParameterDownloadOptions` object (typically from `DownloadParameterOptions`)
- **parameterTypeId**: The same `ForgeTypeId` used with `DownloadParameterOptions`

**Return Value**: A `SharedParameterElement` that represents the newly created parameter in the document

## 2. ParameterDownloadOptions Class

### Constructor Options

The `ParameterDownloadOptions` class has these constructors:

1. **Default Constructor**: `ParameterDownloadOptions()`
   - Creates an empty options object that can be configured

2. **Full Constructor**: `ParameterDownloadOptions(ISet<ElementId> categories, Boolean isInstance, Boolean visible, ForgeTypeId groupTypeId)`
   - **categories**: Set of category IDs to bind the parameter to
   - **isInstance**: `true` for instance parameters, `false` for type parameters
   - **visible**: `true` if parameter should be visible in UI, `false` if hidden (API-only)
   - **groupTypeId**: Parameter group assignment in properties palette

### Properties and Methods

- **IsInstance**: Boolean indicating if parameter binds to instances (`true`) or types (`false`)
- **Visible**: Boolean indicating if parameter is visible to users
- **GetCategories()**: Returns the set of category IDs for binding
- **SetCategories(ISet<ElementId> categories)**: Sets the categories for binding
- **GetGroupTypeId()**: Gets the parameter group ID
- **SetGroupTypeId(ForgeTypeId groupId)**: Sets the parameter group ID

## 3. Mapping APS JSON to ParameterDownloadOptions

The APS JSON contains several fields that correspond to `ParameterDownloadOptions` properties:

### Parameter ID
```json
"id": "parameters.6cd2c1f1f48d41f4aa5f4b51e535595f:f7b120fafd3141dc8b44e7d2757c0b77-1.0.0"
```
Used to create the `ForgeTypeId` for `DownloadParameterOptions` and `DownloadParameter`.

### Instance/Type Assignment
```json
"metadata": [
  {
    "id": "instanceTypeAssociation",
    "value": "INSTANCE"  // or "TYPE"
  }
]
```
Maps to the `IsInstance` property:
- `"INSTANCE"` → `IsInstance = true`
- `"TYPE"` → `IsInstance = false`

### Visibility
```json
"metadata": [
  {
    "id": "isHidden",
    "value": false  // or true
  }
]
```
Maps to the `Visible` property (inverse of `isHidden`):
- `isHidden = false` → `Visible = true`
- `isHidden = true` → `Visible = false`

### Parameter Group
```json
"metadata": [
  {
    "id": "group",
    "value": {
      "bindingId": "ABYV-32458-BXMZ-08934",
      "id": "autodesk.parameter:group-1.0.0"
    }
  }
]
```
The group information can be used with `GroupTypeId` in Revit to set the properties palette group.

## 4. Mapping Categories from JSON to Revit Categories

The APS JSON includes category information in the metadata:

```json
"metadata": [
  {
    "id": "categories",
    "value": [
      {
        "bindingId": "ACFV-53196-DHUJ-19599",
        "id": "autodesk.revit.category.family:ductTerminal-1.0.0"
      }
    ]
  }
]
```

To map these categories to Revit categories for `ParameterDownloadOptions`:

1. **Extract category IDs**: From each item in the `categories` array with `id` field
2. **Parse the category name**: Extract the portion after the last colon and before the version number
   - Example: From `"autodesk.revit.category.family:ductTerminal-1.0.0"` extract `ductTerminal`
3. **Convert to Revit category**: Use the `BuiltInCategory` enum or `Category.GetCategory` method:

```csharp
// Sample code to convert category names to Revit ElementIds
CategorySet categorySet = app.Create.NewCategorySet();
foreach (var categoryInfo in categoriesFromJson)
{
    string categoryName = ExtractCategoryName(categoryInfo.id);
    // Try to find matching built-in category
    BuiltInCategory builtInCategory = GetBuiltInCategoryFromName(categoryName);
    Category category = Category.GetCategory(doc, builtInCategory);
    if (category != null)
    {
        categorySet.Insert(category);
    }
}

// Helper method to extract category name from JSON id
private string ExtractCategoryName(string categoryId)
{
    // Extract the part between the last ':' and '-'
    int colonIndex = categoryId.LastIndexOf(':');
    int hyphenIndex = categoryId.LastIndexOf('-');
    
    if (colonIndex >= 0 && hyphenIndex > colonIndex)
    {
        return categoryId.Substring(colonIndex + 1, hyphenIndex - colonIndex - 1);
    }
    return null;
}
```

### Notes on Category Mapping

- The `DownloadParameterOptions` method automatically extracts and interprets categories from the APS parameter definition
- The automatic extraction is the recommended approach as it correctly interprets category IDs
- If manual category assignment is needed, you need to map APS category IDs to Revit category IDs
- A common pattern in the APS JSON is:
  - `"autodesk.revit.category.family:categoryName-version"` for family categories
  - `"autodesk.revit.category.local:categoryName-version"` for project categories

## Conclusion

The Revit API's `ParameterUtils.DownloadParameterOptions` and `ParameterUtils.DownloadParameter` methods provide a streamlined way to download parameters from APS into Revit. The key connection is the parameter ID in the APS JSON, which contains all the necessary information for Revit to locate and download the complete parameter definition.
