using PeLib;

namespace PE_Addin_CommandPalette.H;

/// <summary>
/// Immutable reference to either an internal PostableCommand or an external command id.
/// </summary>
public readonly record struct CommandRef {
    private readonly PostableCommand? _internal;
    private readonly string _external;
    private CommandRef(PostableCommand i) => _internal = i;
    private CommandRef(string e) => _external = e;

    public static implicit operator CommandRef(PostableCommand i) => new(i);
    public static implicit operator CommandRef(string e) => new(e);

    public object Value => _internal.HasValue ? _internal.Value : _external;

    public Result<RevitCommandId> GetCommandId() {
        RevitCommandId id;
        if (_internal.HasValue) {
            id = RevitCommandId.LookupPostableCommandId(_internal.Value);
            return id is not null ? id : new InvalidOperationException($"CommandId is null for internal command ({_internal})");
        }
        if (string.IsNullOrEmpty(_external)) return new ArgumentNullException(nameof(_external));
        id = RevitCommandId.LookupCommandId(_external);
        return id is not null ? id : new InvalidOperationException($"CommandId is null for external command ({_external})");
    }
    
    /// <summary>
    /// Returns the RevitCommandId for this reference if the command is postable. Else it returns null
    /// TODO: Implement more robust/nuanced postability checking. Need to figure this out!!!!!
    /// </summary>
    public Result<RevitCommandId> GetAvailableCommandId(UIApplication uiApp) {
        var (id, idErr) = this.GetCommandId();
        return idErr is not null
            ? idErr
            : uiApp.CanPostCommand(id)
                ? id
                : null;
    }
}

/// <summary>
/// Service for executing PostableCommand items in Revit
/// </summary>
public class CommandExecutionHelper {

    /// <summary>
    /// Executes the specified command.
    /// </summary>
    public Result<bool> ExecuteCommand(UIApplication uiApp, CommandRef command) {
        var (validId, validIdErr) = command.GetAvailableCommandId(uiApp);
        if (validIdErr is not null) return validIdErr;
        if (validId is null) return new InvalidOperationException($"Command cannot be executed at this time ({command})");
        try {
            uiApp.PostCommand(validId);
            PostableCommandHelper.Instance.UpdateCommandUsage(command);
            return true;
        } catch (Exception ex) {
            return new InvalidOperationException($"Command failed to execute ({command})", ex);
        }
    }

    /// <summary>
    /// Checks if a command is available for execution.
    /// </summary>
    public bool IsCommandAvailable(UIApplication uiApp, CommandRef command) {
        var (validId, validIdErr) = command.GetAvailableCommandId(uiApp);
        if (validIdErr is not null) UiUtils.ShowDebugBalloon(validIdErr.Message);
        return validId is not null && validIdErr is null;
    }
    /// <summary>
    /// Returns a human-readable availability status.
    /// </summary>
    public string GetCommandStatus(UIApplication uiApp, CommandRef command) {
        var (validId, validIdErr) = command.GetAvailableCommandId(uiApp);
        if (validIdErr is not null) UiUtils.ShowDebugBalloon(validIdErr.Message);
        return validIdErr is not null
            ? "Availability Unknown"
            : validId is not null
                ? "Available"
                : "Unavailable";
    }
}