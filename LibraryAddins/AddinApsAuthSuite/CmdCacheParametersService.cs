using PeServices.Aps;
using PeServices.Aps.Models;
using PeServices.Storage;

namespace AddinApsAuthSuite;

[Transaction(TransactionMode.Manual)]
public class CmdCacheParametersService : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements) {
        var cacheFilename = "parameters-service-cache.json";
        var apsParamsCache = Storage.GlobalState(cacheFilename).Json<ParametersApi.Parameters>();

        var svcAps = new Aps(new CacheParametersService());
        var _ = Task.Run(async () =>
            await svcAps.Parameters(new CacheParametersService()).GetParameters(
                apsParamsCache)
        ).Result;

        return Result.Succeeded;
    }
}

public class CacheParametersService : Aps.IOAuthTokenProvider, Aps.IParametersTokenProvider {
#if DEBUG
    public string GetClientId() => Storage.GlobalSettings().Json().Read().ApsWebClientId1;
    public string GetClientSecret() => Storage.GlobalSettings().Json().Read().ApsWebClientSecret1;
#else
    public string GetClientId() => Storage.GlobalSettings().Json().Read().ApsDesktopClientId1;
    public string GetClientSecret() => null;
#endif
    public string GetAccountId() => Storage.GlobalSettings().Json().Read().Bim360AccountId;
    public string GetGroupId() => Storage.GlobalSettings().Json().Read().ParamServiceGroupId;
    public string GetCollectionId() => Storage.GlobalSettings().Json().Read().ParamServiceCollectionId;
}