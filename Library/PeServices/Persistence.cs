// namespace PeServices;
//
// public enum Addins {
//     CommandPalette
//     // add to this as we add addins
// }
//
// // stateless and functional class, it should basically only be used to access the persistence files via a common API
// internal class Persistence {
//     /// <summary>
//     ///     The path to Documents/[assembly name] which is PE Tools for now
//     /// </summary>
//     private static string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
//         Assembly.GetExecutingAssembly().GetName().Name);
//
//
//     // // all methods should make a file if it doesn't exist
//     // public static bool SettingsUpdate(Addins addin, settingsSchema, TODO) {
//     //     // Assume settings files are always JSON called settings.json in the root of the addin folder 
//     //     // (not in a separate settings folder, unlike state and output folders)
//     //     // so itd be Documents/[assembly name]/settings.json
//
//     //     // the settings json schema should always follow this:
//     //     // {
//     //     //     "profiles": [
//     //     //         "profile-whatever" :
//     //     //         {
//     //     //             "key": "value",
//     //     //             any amount of nesting
//     //     //         },
//     //     //         "profile-whatever" :
//     //     //         {
//     //     //             "key": "value"
//     //     //         },
//     //     //     ]
//     //     // }
//     // }
//
//     // public static bool SettingsLoad(Addins addin, settingsSchema, TODO) { }
//
//     // public static bool SettingsSave(Addins addin, settingsSchema, TODO) { }
//
//     public static bool StateSave(Addins addin, TODO) {
//         // Documents/[assembly name]/state/[any file type]
//     }
//
//     public static bool StateSave(Addins addin, stateSchema, TODO) {
//         // Documents/[assembly name]/state/[json]
//     }
//
//     public static bool StateUpdate(Addins addin, stateSchema, TODO) {
//         // Documents/[assembly name]/state/[json]
//     }
//
//     public static bool StateLoad(Addins addin, TODO) { }
//
//     // public static bool OutputSave(Addins addin, TODO) {
//     //     // Documents/[assembly name]/output/[any file type, but probably json or md]
//     // }
//
//     // public static bool OutputLoad(Addins addin, TODO) { }
// }
//
// // NOTES:
// // - just do the state methods for now to get a feel for it, and only make it for json for now
// // - use JsonSchema.Net and JsonSchema.Net.generation to generate the schemas
// // - make a base settings schema at some point

