using Json.Schema.Generation;
using PE_Tools.Properties;
using PeRevitUI;
using PeServices;
using PeServices.Aps;
using PeServices.Aps.Models;

namespace AddinCmdApsAuth;

[Transaction(TransactionMode.Manual)]
public class CmdParametersServiceTest : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements
    ) {
        // RevitTask.Initialize(commandData.Application);

        try {
            var storage = new Storage("ParametersServiceTest");
            var settings = storage.Settings().Json<ParametersServiceTest>().Read();
            var (acc, gp, col) = (settings.AccountId, settings.GroupId, settings.CollectionId);
            var auth = new OAuth(settings);
            var (token, tokenErr) = auth.GetToken();
            if (tokenErr is not null) throw tokenErr;
            new Balloon().Add(Balloon.Log.INFO, token).Show();

            var parametersService = new Parameters();
            var messages = new List<string> { "Parameters Service Test", "\n" };

            var tcs = new TaskCompletionSource<Result<List<string>>>();

            _ = Task.Run(async () => {
                try {
                    var hubs = await Hubs.GetHubs(token);
                    var hub = hubs.Data.First().Id;
                    if (!string.IsNullOrEmpty(acc)) hub = acc;
                    foreach (var h in hubs.Data) messages.Add("Hubs (plural)      : " + h.Id);
                    messages.Add("SELECTED          -> " + hub + "\n");

                    var groups = await parametersService.GetGroups(hub, token);
                    var group = groups.Results.First().Id;
                    if (!string.IsNullOrEmpty(gp)) group = gp;
                    foreach (var g in groups.Results) messages.Add("Groups (plural)    : " + g.Id);
                    messages.Add("SELECTED          -> " + group + "\n");

                    var collections = await parametersService.GetCollections(hub, group, token);
                    var collection = collections.Results.First().Id;
                    if (!string.IsNullOrEmpty(col)) collection = col;
                    foreach (var c in collections.Results)
                        messages.Add("Collections (plural): " + c.Title + ": " + c.Id);
                    messages.Add("SELECTED          -> " + collection + "\n");

                    var parameters =
                        await parametersService.GetParameters(hub, group, collection,
                            token);
                    var parameter = parameters.Results.First();
                    foreach (var p in parameters.Results) messages.Add("Parameters (plural): " + p.Name);

                    tcs.SetResult(messages);
                } catch (Exception ex) {
                    tcs.SetResult(ex);
                }
            });

            tcs.Task.Wait();
            var (msg, msgErr) = tcs.Task.Result;
            if (msgErr is not null) throw msgErr;
            var balloon = new Balloon();
            foreach (var m in msg) balloon.Add(Balloon.Log.TEST, m);
            balloon.Show();

            return Result.Succeeded;
        } catch (Exception ex) {
            new Balloon().Add(new StackFrame(), ex, true).Show();
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