﻿using PeRevit.Ui;
using ricaun.Revit.Github;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdUpdate : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        // Revit application and document variables
        var uiapp = commandData.Application;
        var uidoc = uiapp.ActiveUIDocument;
        var doc = uidoc.Document;

        // Fetch the latest Github release
        var request = new GithubRequestService("kaitpw", "PE_Tools");
        var result = RunRequest(request);

        new Ballogger()
            .Add(Log.INFO, null, $"Download: {result}")
            .Show(() => { }, "None"
            // TODO: Figure out how to get the request to rerun
            // RunRequest(request),
            // "Click to Retry Download"
            );

        return Result.Succeeded;
    }


    public static Task<bool> RunRequest(GithubRequestService request) =>
        Task.Run(() => request.Initialize(Console.WriteLine));
}