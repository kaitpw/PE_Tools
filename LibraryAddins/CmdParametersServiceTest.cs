using PeRevit.Ui;
using PeServices.Aps;
using PeServices.Storage;

namespace AddinCmdParametersServiceTest;

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
            var aps = new Aps(settings);

            var messages = new List<string> { "Parameters Service Test", "\n" };
            var tcs = new TaskCompletionSource<Result<List<string>>>();

            _ = Task.Run(async () => {
                try {
                    var hubs = await aps.Hubs().GetHubs();
                    var hub = hubs.Data.First().Id;
                    // if (!string.IsNullOrEmpty(acc)) hub = acc;
                    foreach (var h in hubs.Data) messages.Add("Hubs (plural)      : " + h.Id);
                    messages.Add("SELECTED          -> " + hub + "\n");

                    var groups = await aps.Parameters(settings).GetGroups();
                    var group = groups.Results.First().Id;
                    // if (!string.IsNullOrEmpty(gp)) group = gp;
                    foreach (var g in groups.Results) messages.Add("Groups (plural)    : " + g.Id);
                    messages.Add("SELECTED          -> " + group + "\n");

                    var collections = await aps.Parameters(settings).GetCollections();
                    var collection = collections.Results.First().Id;
                    // if (!string.IsNullOrEmpty(col)) collection = col;
                    foreach (var c in collections.Results)
                        messages.Add("Collections (plural): " + c.Title + ": " + c.Id);
                    messages.Add("SELECTED          -> " + collection + "\n");

                    var parameters =
                        await aps.Parameters(settings).GetParameters();
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
            // var balloon = new Balloon();
            // foreach (var m in msg) _ = balloon.Add(Log.TEST, m);
            // balloon.Show();
            foreach (var m in msg) Debug.WriteLine(m);

            return Result.Succeeded;
        } catch (Exception ex) {
            new Balloon().Add(new StackFrame(), ex, true).Show();
            return Result.Failed;
        }
    }
}

public class ParametersServiceTest : Aps.IOAuthTokenProvider, Aps.IParametersTokenProvider {
    public string GetClientId() => Storage.GlobalSettings().Json().Read().ApsDesktopClientId1;
    public string GetClientSecret() => null;

    public string GetAccountId() => Storage.GlobalSettings().Json().Read().Bim360AccountId;
    public string GetGroupId() => Storage.GlobalSettings().Json().Read().ParamServiceGroupId;
    public string GetCollectionId() => Storage.GlobalSettings().Json().Read().ParamServiceCollectionId;
}