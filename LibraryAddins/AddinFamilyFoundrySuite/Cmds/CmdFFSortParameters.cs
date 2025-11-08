using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Operations;
using PeRevit.Ui;

namespace AddinFamilyFoundrySuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdFFSortParameters : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var doc = commandData.Application.ActiveUIDocument.Document;

        try {
            // TODO: MUCH MUCH later down the line make this read from settings.
            // We need to wait till we've made primitive settings UI for this sort of thing.
            var queue = new OperationQueue()
                .Add(new SortParams(new SortParamsSettings()));
            var processor = new OperationProcessor(doc, new ExecutionOptions());
            var logs = processor.ProcessQueue(queue);
            // need to make a better way to extract logs
            // Balloger the log from this at some point
            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}