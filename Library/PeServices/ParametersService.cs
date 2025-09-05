using Autodesk.DataManagement;

namespace PeServices;

public class ParametersService {
    private readonly DataManagementClient _dataManagementClient;

    public ParametersService() => this._dataManagementClient = new DataManagementClient();

    // public string GetHubs() {
    //     // var hubs = this._dataManagementClient.GetHubAsync().Result;
}