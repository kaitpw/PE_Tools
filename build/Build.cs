using System;
using System.IO;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using ricaun.Nuke;
using ricaun.Nuke.Components;
using Serilog;

// This allows generation of a GitHub Actions workflow file, however env vars needto be added manually
// env:
//   GitHubToken: ${{ secrets.GITHUB_TOKEN }}
//   NugetApiUrl: ${{ secrets.NUGET_API_URL }}
//   NugetApiKey: ${{ secrets.NUGET_API_KEY }}
//   SignFile: ${{ secrets.SIGN_FILE }}
//   SignPassword: ${{ secrets.SIGN_PASSWORD }}
[GitHubActions(
    "nuke",
    GitHubActionsImage.WindowsLatest,
    On = new[] { GitHubActionsTrigger.Push },
    // Enable full repo history checkout, necessary for GitVersion (not an issue with checkout@v1 though)
    FetchDepth = 0,
    // Enable Release publishing for GITHUB_TOKEN. Write permissions implicitly include read.
    EnableGitHubToken = true,
    WritePermissions = new[] { GitHubActionsPermissions.Contents }
)]
class Build : NukeBuild, IPublishRevit
{
    //string IHazMainProject.MainName => "PE_Tools";
    string IHazRevitPackageBuilder.VendorId => "Positive Energy"; // necessary
    string IHazRevitPackageBuilder.VendorDescription => "An ATX based MEP firm";
    string IHazInstallationFiles.InstallationFiles => "NukeFiles"; // directory with innstaller assets
    IssConfiguration IHazInstallationFiles.IssConfiguration =>
        new IssConfiguration() { Title = "PE Tools" }; // not necessary, just changes name of installer

    public static int Main() => Execute<Build>(x => x.From<IPublishRevit>().Build);
}
