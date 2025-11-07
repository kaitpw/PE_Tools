// TODO: Migrate this!!!!!!!!!!

using Newtonsoft.Json;
using PeExtensions.FamDocument;
using PeExtensions.FamDocument.SetValue;
using PeServices.Storage.Core;
using PeServices.Storage.Core.Json.Converters;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddAndSetValueAsValue(AddFamilyParamsSettings settings)
    : TypeOperation<AddFamilyParamsSettings>(settings) {
    public override string Description => "Add Family Parameters and set the value for each family type to the same value.";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new Dictionary<string, LogEntry>();

        var sortedParameters = this.Settings.FamilyParamData.Where(p => p.GlobalValue is not null);
        foreach (var p in sortedParameters) {

            var parameter = doc.AddFamilyParameter(p.Name, p.PropertiesGroup, p.DataType, p.IsInstance);

            if (this.Settings.OverrideExistingValues)
                _ = doc.SetValue(parameter, p.GlobalValue, ValueCoercionStrategy.CoerceSimple);
            logs[p.Name] = new LogEntry { Item = p.Name };

        }

        return new OperationLog(this.Name, logs.Values.ToList());
    }
}
