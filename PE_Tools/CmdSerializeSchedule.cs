using Nice3point.Revit.Extensions;
using PeRevit.Lib;
using PeRevit.Ui;
using PeServices.Storage;
using PeUi.Core;
using PeUi.Core.Services;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdSerializeSchedule : IExternalCommand {
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elementSet) {
        try {
            var uiapp = commandData.Application;
            var doc = uiapp.ActiveUIDocument.Document;

            var items = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(s => !s.Name.Contains("<Revision Schedule>"))
                .OrderBy(s => s.Name)
                .Select(s => new ScheduleSerializePaletteItem(s));

            var actions = new List<PaletteAction<ScheduleSerializePaletteItem>> {
                new() {
                    Name = "Serialize",
                    Execute = item => {
                        try {
                            var storage = new Storage("Schedule Manager");
                            var outputDir = storage.OutputDir();
                            var spec = ScheduleHelper.SerializeSchedule(item.Schedule);
                            var filename = outputDir.Json<ScheduleSpec>(spec.Name).Write(spec);

                            new Ballogger().Add(Log.INFO, new StackFrame(),
                                $"Serialized schedule '{item.Schedule.Name}' to {filename}").Show();
                        } catch (Exception ex) {
                            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
                        }
                    }
                }
            };

            var window = PaletteFactory.Create("Schedule Serializer", items, actions,
                new PaletteOptions<ScheduleSerializePaletteItem> {
                    SearchConfig = SearchConfig.PrimaryAndSecondary(), FilterKeySelector = item => item.TextPill
                });
            window.Show();

            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Failed;
        }
    }
}

public class ScheduleSerializePaletteItem(ViewSchedule schedule) : IPaletteListItem {
    public ViewSchedule Schedule { get; } = schedule;
    public string TextPrimary => this.Schedule.Name;

    public string TextSecondary {
        get {
            var category = Category.GetCategory(this.Schedule.Document, this.Schedule.Definition.CategoryId);
            return category?.Name ?? string.Empty;
        }
    }

    public string TextPill { get; } = schedule.FindParameter("Discipline")?.AsValueString();

    public Func<string> GetTextInfo => () => {
        var category = Category.GetCategory(this.Schedule.Document, this.Schedule.Definition.CategoryId);
        var fieldCount = this.Schedule.Definition.GetFieldCount();
        return $"Id: {this.Schedule.Id}" +
               $"\nCategory: {category?.Name ?? "Unknown"}" +
               $"\nFields: {fieldCount}" +
               $"\nDiscipline: {this.TextPill}";
    };

    public BitmapImage Icon => null;
    public Color? ItemColor => null;
}