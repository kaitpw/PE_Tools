using AddinCmdPalette.Schedules;
using PeServices.Storage;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdOpenSchedule : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        try {
            var uiapp = commandData.Application; 

            // Create persistence service
            var persistence = new Storage(nameof(CmdOpenSchedule));

            // Create and show palette using new API
            var palette = SchedulePaletteService.Create(uiapp, persistence);
            palette.Show();

            return Result.Succeeded;
        } catch (Exception ex) {
            throw new InvalidOperationException($"Error opening schedule palette: {ex.Message}");
        }
    }
}

