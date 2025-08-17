using Autodesk.Revit.DB.Plumbing;
using static PE_Lib.Filters; // Fixes CS0138 by using 'using static' for the Filters type

namespace PE_Lib;

internal class Utils {
    // Helper method to get current Revit version
    public static string? GetRevitVersion() {
#if REVIT2023
            return "2023";
#elif REVIT2024
            return "2024";
#elif REVIT2025
            return "2025";
#elif REVIT2026
        return "2026";
#else
            return null;
#endif
    }

    /// <summary>
    ///     Gets the total length of all Pipe elements in the document, optionally filtered by material name.
    /// </summary>
    public static double TotalPipeLength(Document doc, string materialName = null) {
        var pipes = AllElementsOfType<Pipe>(doc);
        var totalLength = 0.0;
        foreach (var pipe in pipes) {
            // If materialName is specified, filter by materia
            if (!string.IsNullOrEmpty(materialName)) {
                var matIds = pipe.GetMaterialIds(false);
                var hasMaterial = matIds
                    .Select(id => doc.GetElement(id) as Material)
                    .Any(mat =>
                        mat != null
                        && mat.Name.Equals(materialName, StringComparison.OrdinalIgnoreCase)
                    );
                if (!hasMaterial)
                    continue;
            }

            var lengthParam = pipe.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
            if (lengthParam != null && lengthParam.StorageType == StorageType.Double)
                totalLength += lengthParam.AsDouble();
        }

        // Convert from internal units (feet) to linear feet
        return totalLength;
    }

    /// <summary>
    ///     Gets the total volume of all Pipe elements in the document, optionally filtered by system type name.
    /// </summary>
    public static double TotalPipeVolume(Document doc, string pst = "") {
        var pipes = AllElementsOfType<Pipe>(
            doc,
            pipe =>
                pipe.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM) // EXTRACT THIS LATER
                    .AsValueString() == pst
        );
        var totalVolume = 0.0;
        foreach (var pipe in pipes) {
            var volParam = pipe.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
            if (volParam != null && volParam.StorageType == StorageType.Double) {
                var volume = volParam.AsDouble();
                totalVolume += volume;
            }
        }

        return totalVolume;
    }

    /// <summary>
    ///     Gets a dictionary of MEP equipment counts by family and type name.
    /// </summary>
    public static Dictionary<string, int> CountMEPEquipmentByType(Document doc) {
        var equipmentCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var collector = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_MechanicalEquipment)
            .OfClass(typeof(FamilyInstance));
        foreach (FamilyInstance fi in collector) {
            var familyName = fi.Symbol?.Family?.Name ?? "<No Family>";
            var typeName = fi.Symbol?.Name ?? "<No Type>";
            var key = $"{familyName} : {typeName}";
            if (!equipmentCounts.ContainsKey(key))
                equipmentCounts[key] = 0;
            equipmentCounts[key]++;
        }

        return equipmentCounts;
    }
}