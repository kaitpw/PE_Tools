using PeRevit.Lib;
using PeRevit.Ui;
using PeServices.Storage;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdCreateSchedule : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        try {
            var storage = new Storage("Schedule Manager");
            var settingsManager = storage.SettingsDir();
            var settings = settingsManager.Json<ScheduleSettings>().Read();
            var profile = settingsManager.SubDir("schedules")
                .JsonWithExtends<ScheduleSpec>($"{settings.CurrentProfile}.json").Read();

            using var trans = new Transaction(doc, "Create Schedule");
            _ = trans.Start();

            var schedule = ScheduleHelper.CreateSchedule(doc, profile);

            _ = trans.Commit();

            var balloon = new Ballogger();
            balloon.Add(Log.INFO, new StackFrame(),
                $"Created schedule '{schedule.Name}' from profile '{settings.CurrentProfile}'").Show();

            // Open the schedule view
            uiDoc.ActiveView = schedule;

            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Failed;
        }
    }
}

public class ScheduleSettings {
    [Description(
        "Current profile to use for the command. This determines which schedule profile is used when creating a schedule.")]
    [Required]
    public string CurrentProfile { get; set; } = "Default";
}