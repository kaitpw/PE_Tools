using System;
using System.IO;
using Nuke.Common;
using Nuke.Common.IO;
using ricaun.Nuke;
using ricaun.Nuke.Components;
using Serilog;

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
