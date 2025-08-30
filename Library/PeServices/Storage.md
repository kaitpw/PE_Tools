# Persitence Service

# Purpose

The purpose of this service is to provide a common API and general standardization for persisting data in addins. Essentially a wrapper over file read/write code which:
- standardizes *basic* read and write method for persisted files
- standardizes schema for setting files (to a reasonable extent)
- [future] common helper methods for writing to output file types (csv, pdf, rvt, etc.) 
- [future] common helper methods for working with persisted files (diffing, hashing, opening output file on save, etc.)

# Goals

- Fast performance.
- Dependency injection-like usage
- as  stateless and functional class as possible
- Simple and intuitive API which is generally the same for all file types (settings, state, temp, output)
- No clutter: make directories/files on a as-needed basis

# About 

This Service will read and write files to a predefined folder structure with sensible defaults. Overrides will provide the ability to read/write from non-default files, but usage of the class should ALWAYS encourage use of the defaults where they exist. 

## Persistence File structure
``` md
[assembly name]/
    ├── RevitAddin_A/
    │   ├── settings/
    │   │   └── settings.json
    │   ├── state/ 
    │   │   └── state.json
    │   ├── temp/ 
    │   │   └── temp.json
    │   └── output/
    │       ├── log_[datetime].txt
    │       └── results_[datetime].csv
    ├── RevitAddin_B/
    │   └── state/
    │       └── state.json
    └── RevitAddin_C/
        ├── settings/
        │   └── settings.json
        └── output/
            └── error_report_[datetime].csv
```

### `settings/`

- default file: settings.json
- file type: JSON only
- purpose: hold settings for an addin which persist across revit sessions. 
- notes: files here are meant to be added to, but rarely overwritten (unless importing settings or something like that). there *should* only be one file here, but the option to make other files should be available if the addin has like multiple screens/tabs/dialogs or something.

The settings file schema should have to parts. The first is global settings, the second is an optional profile based list of settings. For now the only service-level standardization I want on the settings JSON schema (besides the settings and profiles sections) is the three attributes listed below
```json
{
  "Settings": {
    "EnableCrossSessionPersistence": true,
    "OpenOutputFilesOnCommandFinish": true,
    "EnableLastRunLoggingToOutput": false,
  }
  "Profiles": [
    ... 
  ]
}
```

### `state/`

- default file: state.json
- file type: JSON or CSV only
- purpose: hold state for the addin which persists across revit sessions. 
- notes: files here are meant to be added to, but NEVER overwritten. there *should* only be one file here, but the option to make other files should be available

### `temp/`

- default file: none
- file type: any (but probably JSON or .rfa)
- examples: temp.json, temp.rfa, family_data.json
- purpose: hold state that needs an addin needs while its running or between runs, but not between Revit sessions. 
- notes: files here are meant to be overwritten

### `output/`

- default file: none
- file type: any
- examples: results_[datetime].csv, log_[datetime].txt, log_[datetime].md, family.rfa
- purpose: a catch all directory for files that should be kept/recorded. 
- notes: files here will not be used by the addin but rather by the user.

## Expected Usage

In the top-level of a command (i.e in `CmdCommandPalette.cs`) the persistence service will be created and passed downwards. This way we don't have to scatter a bunch of strings of the addin name everywhere and make sure they match.

For settings and state jsons, we will pass a JSON schema to the service. For WPF apps **view models and models should be as tightly coupled to the json schemas as possible**. for commands that simply run from a button, this is less of an issue. use JsonSchema.Net and JsonSchema.Net.Generation to make make classes the source of truth for the schemas passed to the Persistence service.

This is up to your discretion, but I think that schemas should probably be passes as type generics to each method for the best type support. Again up to you to decide. 

For state that is a CSV format, I'd also like to utilize a similar philosophy of type safety and one source of truth. the csv should basically be usable like a dictionary.

## Expected API

```cs
// Settings Methods, should only
Persistence.Settings<SettingsSchema>().ReadSettings() // typed output
Persistence.Settings<ProfilesSchema>().ReadProfiles() // typed output
Persistence.Settings<SettingsSchema>().WriteSettings() 
Persistence.Settings<ProfilesSchema>().WriteProfiles()
Persistence.Settings<SettingsSchema>("nondefaultsettings.json").ReadSettings() // typed output
Persistence.Settings<ProfilesSchema>("nondefaultsettings.json").ReadProfiles() // typed output
Persistence.Settings<SettingsSchema>("nondefaultsettings.json").WriteSettings() 
Persistence.Settings<ProfilesSchema>("nondefaultsettings.json").WriteProfiles()
Persistence.Settings().Import()
```

```cs
// State Methods
Persistence.State<DictionaryWithRecord>().ReadCsvRow()
Persistence.State<DictionaryWithRecord>().WriteCsvRow()
Persistence.State<DictionaryWithRecord>().ImportCsv()
Persistence.State<DictionaryWithRecord>("nondefaultstate.json").ReadCsvRow()
Persistence.State<DictionaryWithRecord>("nondefaultstate.json").WriteCsvRow()
Persistence.State<DictionaryWithRecord>("nondefaultstate.json").ImportCsv()
// json state methods
Persistence.State<StateSchema>().ReadJson()
Persistence.State<StateSchema>().WriteJson()
Persistence.State<StateSchema>().ImportJson()
```

```cs
// Temp Methods
Persistence.Temp().ReadCsv()
Persistence.Temp().WriteCsv()
Persistence.Temp().ReadJson()
Persistence.Temp().WriteJson()
```

```cs 
// Output Methods, only write
Persistence.Output().WriteCsv()
Persistence.Output().WriteJson()
Persistence.Output().WriteMd()
```



# Command Palette Integration

Our first test will be integrating this with the command palette to allow for reranking that persists both between revit sessions and within the same revit session. This does not work anymore because I *purposefully* made the PostableCommandHelper not a singleton anymore. Do not revert this change, I want state to be controlled by our new service.
 
Here are the settings I want for the command palette.
```json
{
  "Settings": {
    "EnableCrossSessionPersistence": true,
    "OpenOutputFilesOnCommandFinish": false,
    "EnableLastRunLoggingToOutput": false,
    "ShowUsageCount": true,
    "ShowAllShortcuts": true,
  },
  "Profiles": [
    ... no profiles needed for command palette addin
  ]
}
```

The data that i want to be stored in state should be in a CSV. The columns should be the commandId and a score. upon every use of a command, the scores should be updated. For now we can make the new score simply the usage count.