using System.Windows.Media.Imaging;

namespace PE_Init;

internal class ButtonDataClass {
    public ButtonDataClass(
        string name,
        string text,
        string className,
        byte[] largeImage,
        byte[] smallImage,
        string toolTip
    ) {
        this.Data = new PushButtonData(name, text, GetAssemblyName(), className);
        this.Data.ToolTip = toolTip;

        this.Data.LargeImage = ConvertToImageSource(largeImage);
        this.Data.Image = ConvertToImageSource(smallImage);

        // set command availability
        var nameSpace = this.GetType().Namespace;
        this.Data.AvailabilityClassName = $"{nameSpace}.CommandAvailability";
    }

    public ButtonDataClass(
        string name,
        string text,
        string className,
        byte[] largeImage,
        byte[] smallImage,
        byte[] largeImageDark,
        byte[] smallImageDark,
        string toolTip
    ) {
        this.Data = new PushButtonData(name, text, GetAssemblyName(), className);
        this.Data.ToolTip = toolTip;

        // add check for light vs dark mode
        var theme = UIThemeManager.CurrentTheme;
        if (theme == UITheme.Dark) {
            this.Data.LargeImage = ConvertToImageSource(largeImageDark);
            this.Data.Image = ConvertToImageSource(smallImageDark);
        } else {
            this.Data.LargeImage = ConvertToImageSource(largeImage);
            this.Data.Image = ConvertToImageSource(smallImage);
        }

        // set command availability
        var nameSpace = this.GetType().Namespace;
        this.Data.AvailabilityClassName = $"{nameSpace}.CommandAvailability";
    }

    public PushButtonData Data { get; set; }

    public static Assembly GetAssembly() => Assembly.GetExecutingAssembly();

    public static string GetAssemblyName() => GetAssembly().Location;

    public static BitmapImage ConvertToImageSource(byte[] imageData) {
        using (var mem = new MemoryStream(imageData)) {
            mem.Position = 0;
            var bmi = new BitmapImage();
            bmi.BeginInit();
            bmi.StreamSource = mem;
            bmi.CacheOption = BitmapCacheOption.OnLoad;
            bmi.EndInit();

            return bmi;
        }
    }

    public bool IsCommandAvailable(
        UIApplication applicationData,
        CategorySet selectedCategories
    ) {
        var result = false;
        var activeDoc = applicationData.ActiveUIDocument;
        if (activeDoc != null && activeDoc.Document != null) result = true;

        return result;
    }
}