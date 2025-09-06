using Json.Schema.Generation;
using PE_Tools.Properties;
using PeRevitUI;
using PeServices;
using PeServices.Aps;

namespace AddinCmdApsAuth;

[Transaction(TransactionMode.Manual)]
public class CmdParametersServiceTest : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements) {
        try {
            var storage = new Storage("ParametersServiceTest");
            var settings = storage.Settings().Json<ParametersServiceTest>().Read();
            var (acc, gp, col) = (settings.AccountId, settings.GroupId, settings.CollectionId);
            var auth = new OAuth(settings);
            var (token, tokenErr) = auth.GetToken();
            if (tokenErr is not null) throw tokenErr;
            new Balloon().Add(Balloon.Log.INFO, token).Show();

            var parametersService = new Parameters();
            Exception capturedErr = null;
            // var hubs = [];
            // var accounts = [];
            // var groups = [];
            // var collections = [];
            // var parameters = [];
            _ = Task.Run(async () => {
                try {
                    var hubs = await Hubs.GetHubs(token);
                    var firstHub = hubs.Data.First();
                    var groups = await parametersService.GetGroups(firstHub.Id, token);
                    var firstGroup = groups.Results.First();
                    var collections = await parametersService.GetCollections(firstHub.Id, firstGroup.Id, token);
                    var firstCollection = collections.Results.First();
                    var parameters =
                        await parametersService.GetParameters(firstHub.Id, firstGroup.Id, firstCollection.Id,
                            token);
                    var firstParameter = parameters.Results.First();
                } catch (Exception ex) {
                    capturedErr = ex;
                }
            });

            if (capturedErr != null) throw capturedErr;
            return Result.Succeeded;
        } catch (Exception ex) {
            new Balloon().Add(Balloon.Log.ERR, ex.Message).Show();
            return Result.Failed;
        }
    }

    internal static PushButtonData GetButtonData() =>
        new ButtonDataClass(
            "Parameters Service Test",
            MethodBase.GetCurrentMethod().DeclaringType?.FullName,
            Resources.Blue_32,
            Resources.Blue_16,
            "Click this button to test the parameters service for. This is primarily for testing purposes, but running it will not hurt anything."
        ).Data;
}

public class ParametersServiceTest : ApsAuthSettingsPKCE {
    [Description(
        "The account ID derived from an 'id' field returned by `project/v1/hubs` but with the 'b.' prefix sliced off. If left empty, the first item of 'data' will be used.")]
    [Required]
    public string AccountId { get; set; } = "";

    [Description(
        "The group ID derived from an 'id' field returned by `parameters/v1/accounts/<accountId>/groups`. If left empty, the first item of 'results' will be used.")]
    [Required]
    public string GroupId { get; set; } = "";

    [Description(
        "The collection ID derived from an 'id' field returned by `parameters/v1/accounts/<accountId>/groups/<groupId>/collections`. If left empty, the first item of 'results' will be used.")]
    public string CollectionId { get; set; } = "";
}