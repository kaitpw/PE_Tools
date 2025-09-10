using Autodesk.Revit.UI.Selection;

namespace PeRevit.Lib;

public class Pickers {
    public static Result<(Element element, Face elementFace, UV clickPosition)> FacePosition(
        UIApplication uiApplication,
        ISelectionFilter selectionFilter,
        string selectionPrompt
    ) {
        try {
            var uiDoc = uiApplication.ActiveUIDocument;
            var doc = uiDoc.Document;
            var reference = uiDoc.Selection.PickObject(
                ObjectType.Face,
                selectionFilter,
                $"SELECT A FACE POSITION ({selectionPrompt})"
            );

            var element = doc.GetElement(reference.ElementId);
            var face = element.GetGeometryObjectFromReference(reference) as Face;
            if (face == null)
                return new InvalidOperationException("Selected reference is not a face");

            return (element, face, reference.UVPoint);
        } catch (Exception e) {
            return e;
        }
    }

    public static Result<(Element element, Face elementFace)> Face(
        UIApplication uiApplication,
        ISelectionFilter selectionFilter,
        string selectionPrompt
    ) {
        try {
            var uiDoc = uiApplication.ActiveUIDocument;
            var doc = uiDoc.Document;
            var reference = uiDoc.Selection.PickObject(
                ObjectType.Face,
                selectionFilter,
                "SELECT A FACE (" + selectionPrompt + ")"
            );

            var element = doc.GetElement(reference.ElementId);
            var face = element.GetGeometryObjectFromReference(reference) as Face;
            if (face == null)
                return new InvalidOperationException("Selected reference is not a face");

            return (element, face);
        } catch (Exception e) {
            return e;
        }
    }
}