using AddinFamilyFoundrySuite.Core.Settings;

namespace AddinFamilyFoundrySuite.Core;

public interface IFamilyFoundry<TSettings, TProfile>
    where TSettings : BaseSettings<TProfile>, new()
    where TProfile : BaseProfileSettings, new() {
    TSettings _settings { get; }
    TProfile _profile { get; }
    void InitAndFetch();
}