using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Operations;
using PeRevit.Lib;
using PeRevit.Ui;
using PeServices.Storage;
using PeUtils.Files;

namespace AddinFamilyFoundrySuite.Cmds;
// support add, delete, remap, sort, rename

[Transaction(TransactionMode.Manual)]
public class CmdFFManagerSnapshot : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSetf
    ) {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        try {
            var storage = new Storage("FF Manager");
            var outputFolderPath = storage.Output().DirectoryPath;

            // force this to never be single transaction
            var executionOptions = new ExecutionOptions {
                SingleTransaction = false,
                PreviewRun = false,
                OptimizeTypeOperations = true,
            };

            using var processor = new OperationProcessor(doc, executionOptions);

            var queue = new OperationQueue()
                .Add(new LogFamilyParamsState(outputFolderPath))
                .Add(new LogRefPlaneAndDims(outputFolderPath));

            var metadataString = queue.GetExecutableMetadataString();
            Debug.WriteLine(metadataString);

            var logs = processor
                .SelectFamilies(() => doc.IsFamilyDocument ? null : Pickers.GetSelectedFamilies(uiDoc)
                )
                .ProcessQueue(queue, outputFolderPath);

            var balloon = new Ballogger();
            foreach (var output in logs.familyResults)
                _ = balloon.Add(Log.INFO, new StackFrame(), $"Processed {output.familyName} in {output.totalMs}ms");
            balloon.Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }
}