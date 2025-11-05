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
        var cacheFilename = "parameters-service-cache";
        var apsParamsCache = Storage.Global().StateJsonFile<ParametersApi.Parameters>(cacheFilename);

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
    public string GetClientId() => Storage.Global().SettingsFile().Read().ApsWebClientId1;
    public string GetClientSecret() => Storage.Global().SettingsFile().Read().ApsWebClientSecret1;
#else
    public string GetClientId() => Storage.Global().SettingsFile().Read().ApsDesktopClientId1;
    public string GetClientSecret() => null;
#endif
    public string GetAccountId() => Storage.Global().SettingsFile().Read().Bim360AccountId;
    public string GetGroupId() => Storage.Global().SettingsFile().Read().ParamServiceGroupId;
    public string GetCollectionId() => Storage.Global().SettingsFile().Read().ParamServiceCollectionId;
}