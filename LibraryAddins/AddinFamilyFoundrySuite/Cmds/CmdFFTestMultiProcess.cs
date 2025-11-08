using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.Operations;
using PeRevit.Ui;
using PeServices.Storage;

namespace AddinFamilyFoundrySuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdFFTestMultiProcess : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var doc = commandData.Application.ActiveUIDocument.Document;

        try {
            if (!doc.IsFamilyDocument) {
                new Ballogger().Add(Log.ERR, new StackFrame(), "This command only works on family documents")
                    .Show();
                return Result.Failed;
            }

            var storage = new Storage("FF Test Multi Process");
            var outputFolderPath = storage.OutputDir().DirectoryPath;

            var variants = new List<(string variant, OperationQueue queue)> {
                ("_v1", CreateOperationsForVariant(1)),
                ("_v2", CreateOperationsForVariant(2)),
                ("_v3", CreateOperationsForVariant(3))
            };

            var processor = new OperationProcessor(doc, new ExecutionOptions());
            var outputs = processor.ProcessFamilyDocumentIntoVariants(variants, outputFolderPath);

            var balloon = new Ballogger();
            foreach (var output in outputs) {
                var (logs, error) = output.Logs;
                if (error != null) {
                    _ = balloon.Add(Log.ERR, new StackFrame(),
                        $"Failed to process {output.FamilyName}: {error.Message}");
                } else {
                    _ = balloon.Add(Log.INFO, new StackFrame(),
                        $"Processed {output.FamilyName} with {variants.Count} variants in {output.TotalMs:F0}ms");
                    foreach (var log in logs) {
                        _ = balloon.Add(Log.INFO, new StackFrame(),
                            $"  {log.OperationName}: {log.Entries.Count} entries");
                    }
                }
            }

            balloon.Show();

            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, new StackFrame(), ex, true).Show();
            return Result.Cancelled;
        }
    }

    private static OperationQueue CreateOperationsForVariant(int variantNumber) {
        Debug.WriteLine($"[CmdFFTestMultiProcess] Creating operations for variant {variantNumber}");
        var settings = new AddFamilyParamsSettings {
            FamilyParamData = [
                new FamilyParamModel {
                    Name = "TEST_PROCESS_NUMBER",
                    DataType = SpecTypeId.Int.Integer,
                    PropertiesGroup = new ForgeTypeId(""),
                    IsInstance = false,
                    GlobalValue = variantNumber
                }
            ]
        };
        return new OperationQueue()
            .Add(new AddFamilyParams(settings))
            .Add(new SetParamValueAsValue(settings))
            .Add(new DebugLogFamilyParams());
    }
}