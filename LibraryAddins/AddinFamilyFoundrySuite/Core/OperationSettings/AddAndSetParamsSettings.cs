using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.OperationSettings;

/// <summary>
///     For global values or formulas (with per-type fallback on failure)
/// </summary>
public class SetParamModel : AddAndSetData {
    public const string ByDefaultString = $"The default behavior is to set <{nameof(ValueOrFormula)}> as a formula, " +
                                          "not a value (even if it contains no parameter references).";

    /// <summary>
    ///     The value or formula to set. When setting this in code, wrap plain strings in double quotes
    ///     to prevent them from being interpreted as formulas that might reference parameters.
    /// </summary>
    [Description(
        $"{ByDefaultString} Unitted strings (eg. \"10 A\", \"10\"\", \"10in\", etc.) are acceptable but may be unreliable. " +
        "Prefer simply writing a number (ie. \"10\") without units")]
    public string ValueOrFormula { get; init; } = null;

    [Description(
        $"Use 'false' to set <{nameof(ValueOrFormula)}> as a value. {ByDefaultString}. " +
        $"A caveat of the default behavior is that when intending to set a parameter to simple number/text values, " +
        "the default behavior will \"lock\" that parameter from easy editting. " +
        "Setting this to 'false' allows either: 1) caclulating values per family type without setting a formula. " +
        "2) setting a simple number/text value without setting a formula.")]
    [Required]
    public bool SetAsFormula { get; init; } = true;
}

/// <summary>
///     For explicit per-type values (different value per named type)
/// </summary>
public class SetParamPerTypeModel : AddAndSetData {
    [Description("Dictionary of family type names to values. TODO: incomplete")]
    public Dictionary<string, string> ValuesPertype { get; init; } = new(); // TypeName → Value
}

public class AddAndSetParamsSettings : IOperationSettings {
    [Description("Overwrite a family's existing parameter value/s if they already exist.")]
    public bool OverrideExistingValues { get; init; } = true;

    [Description("Create a family parameter if it is missing.")]
    public bool CreateFamParamIfMissing { get; init; } = true;

    [Description(
        "List of parameters and values to set for all family types. Allows setting a uniform value for all family types, " +
        "calculating a value per family type, and setting a formula (applying to all family types).")]
    public List<SetParamModel> Parameters { get; init; } = [];

    [Description(
        "List of parameters and values to set for each family type. Allows setting an arbitrary value for each family type.")]
    public List<SetParamPerTypeModel> ParametersPerType { get; init; } = [];

    public bool Enabled { get; init; } = true;
}

public class AddAndSetData {
    public string Name { get; init; }

    /// <summary> Defaults to "Other" Properties Palette group</summary>
    public ForgeTypeId PropertiesGroup { get; init; } = new("");

    /// <summary> Defaults to "Text" data type</summary>
    public ForgeTypeId DataType { get; init; } = SpecTypeId.String.Text;

    /// <summary> Defaults to true (Instance parameter)</summary>
    public bool IsInstance { get; init; } = true;
}