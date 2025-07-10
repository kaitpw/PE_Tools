using System;
using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Utilities.Collections;
using ricaun.Nuke;
using ricaun.Nuke.Components;
using ricaun.Nuke.Extensions;
using ricaun.Nuke.Tools;

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
// Enable generation of env vars in the build yaml.
// ImportSecrets = new[] { nameof(SignFile), nameof(SignPassword) }
)]
public class Build : NukeBuild, IPublishRevit
{
    // Even though these are defined in ricaun.Nuke, Nuke expects them to be accessible for parameter injection)
    // [Secret]
    // [Parameter("Path or content of the signing file")]
    // public string SignFile;

    // [Secret]
    // [Parameter("Password for the signing file")]
    // public string SignPassword;

    //string IHazMainProject.MainName => "PE_Tools";
    string IHazRevitPackageBuilder.VendorId => "Positive Energy"; // necessary
    string IHazRevitPackageBuilder.VendorDescription => "An ATX based MEP firm";
    string IHazInstallationFiles.InstallationFiles => "NukeFiles"; // directory with innstaller assets
    IssConfiguration IHazInstallationFiles.IssConfiguration =>
        new IssConfiguration() { Title = "PE Tools" }; // not necessary, just changes name of installer

    public static int Main() => Execute<Build>(x => x.From<IPublishRevit>().Build);
}
