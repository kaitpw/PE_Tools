using Json.Schema.Generation;

namespace PeServices.Aps.Models;

public class OAuth {
    /// <summary>Interface for providing APS authentication credentials to the OAuth class</summary>
    public interface IApsTokenProvider {
        string GetClientId();
        string GetClientSecret();
    }
}