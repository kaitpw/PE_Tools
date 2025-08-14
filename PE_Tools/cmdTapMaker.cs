using System.Windows.Threading;
using PE_TapMaker.V;
using PE_TapMaker.VM;

namespace PE_Tools
{
    [Transaction(TransactionMode.Manual)]
    public class cmdTapMaker : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elementSet
        )
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;
                Document doc = uidoc?.Document;

                if (doc == null)
                    throw new InvalidOperationException("No active document found");

                // 1. First, select the duct face
                var faceResult = PE_TapMaker.H.TapMakerHelper.SelectDuctFace(uiapp);
                
                if (faceResult.face == null || faceResult.duct == null)
                {
                    // User cancelled or no valid selection
                    return Result.Cancelled;
                }

                // 2. Create the View
                TapMakerWindow tapMakerWindow = new TapMakerWindow();

                // 3. Get the Dispatcher from the View
                Dispatcher windowDispatcher = tapMakerWindow.Dispatcher;

                // 4. Create the ViewModel with pre-selected face
                TapMakerViewModel viewModel = new TapMakerViewModel(
                    commandData.Application,
                    windowDispatcher,
                    faceResult.face,
                    faceResult.duct
                );

                // 5. Set the DataContext
                tapMakerWindow.DataContext = viewModel;

                // 6. Give the ViewModel a reference to the window
                viewModel.SetWindow(tapMakerWindow);

                // 7. Show the View
                tapMakerWindow.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error opening tap maker: {ex.Message}");
            }
        }

        internal static PushButtonData GetButtonData()
        {
            string buttonInternalName = "CmdBtnTapMaker";
            string buttonTitle = "Tap Maker";

            PE_Init.ButtonDataClass myButtonData = new PE_Init.ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                MethodBase.GetCurrentMethod().DeclaringType?.FullName,
                Properties.Resources.Green_32,
                Properties.Resources.Green_16,
                "Add taps to duct faces. Select a duct face and choose the tap size."
            );

            return myButtonData.Data;
        }
    }
}
