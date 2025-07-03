using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace Lib
{
    public class Utils
    {
        /// <summary>
        /// Retrieves the associated Level of the active View in Revit.
        /// </summary>
        /// <param name="view">The View object for which to find the associated Level.</param>
        /// <returns>The Level object associated with the view, or null if no level is associated or found.</returns>
        public static Level LevelOfActiveView(View view)
        {
            if (view == null)
                return null;

            var doc = view.Document;
            var levelId = view.GenLevel.Id;

            if (levelId != ElementId.InvalidElementId && levelId != null)
                return doc.GetElement(levelId) as Level;

            return null;
        }

        /// <summary>
        /// Retrieves all elements of a specified type from the Revit document.
        /// This is a general method for finding multiple elements.
        /// </summary>
        /// <typeparam name="T">The type of Element to retrieve. Must inherit from Autodesk.Revit.DB.Element.</typeparam>
        /// <returns>An IEnumerable of elements of the specified type that match the predicate. Returns an empty enumerable if none are found.</returns>
        public static IEnumerable<T> AllElementsOfType<T>(Document doc, Func<T, bool> filter = null)
            where T : Element
        {
            if (doc == null)
                return Enumerable.Empty<T>();

            var collector = new FilteredElementCollector(doc);
            var elements = collector.OfClass(typeof(T)).OfType<T>();

            if (filter != null)
                return elements.Where(filter);
            else
                return elements;
        }

        /// <summary>
        /// Retrieves the first element of a specified type from the Revit document.
        /// This method generalizes the pattern of collecting elements by class and casting them.
        /// </summary>
        /// <typeparam name="T">The type of Element to retrieve. Must inherit from Autodesk.Revit.DB.Element.</typeparam>
        /// <returns>The first element of the specified type that matches the predicate, or null if none is found.</returns>
        public static T FirstElementOfType<T>(Document doc, Func<T, bool> filter = null)
            where T : Element
        {
            if (doc == null)
                return null;

            var collector = new FilteredElementCollector(doc);
            var elements = collector.OfClass(typeof(T)).OfType<T>();

            if (filter != null)
                return elements.Where(filter).FirstOrDefault();
            else
                return elements.FirstOrDefault();
        }

        /// <summary>
        /// Retrieves a FamilySymbol by its Family Name and Family Symbol Name (Type Name).
        /// Performs case-insensitive comparison.
        /// </summary>
        /// <param name="doc">The active Revit Document.</param>
        /// <param name="familyName">The name of the Family.</param>
        /// <param name="familySymbolName">The name of the Family Symbol (Type).</param>
        /// <returns>The matching FamilySymbol, or null if not found.</returns>
        public static FamilySymbol GetByNameFamilySymbol(
            Document doc,
            string familyName,
            string familySymbolName
        )
        {
            return FirstElementOfType<FamilySymbol>(
                doc,
                fs =>
                    fs.FamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase)
                    && fs.Name.Equals(familySymbolName, StringComparison.OrdinalIgnoreCase)
            );
        }

        // --- Specialized Methods using the Generic Helpers ---

        /// <summary>
        /// Retrieves an MEPSystemType by its Name.
        /// Performs case-insensitive comparison.
        /// </summary>
        /// <returns>The matching MEPSystemType, or null if not found.</returns>
        public static MEPSystemType GetByNameMEPSystemType(Document doc, string name)
        {
            return FirstElementOfType<MEPSystemType>(
                doc,
                mst => mst.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            );
        }

        /// <summary>
        /// Retrieves a DuctType by its Name.
        /// Performs case-insensitive comparison.
        /// </summary>
        /// <returns>The matching DuctType, or null if not found.</returns>
        public static DuctType GetByNameDuctType(Document doc, string name)
        {
            return FirstElementOfType<DuctType>(
                doc,
                dt => dt.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            );
        }

        /// <summary>
        /// Retrieves a PipeType by its Name.
        /// Performs case-insensitive comparison.
        /// </summary>
        /// <returns>The matching PipeType, or null if not found.</returns>
        public static PipeType GetByNamePipeType(Document doc, string name)
        {
            return FirstElementOfType<PipeType>(
                doc,
                pt => pt.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            );
        }
    }
}
