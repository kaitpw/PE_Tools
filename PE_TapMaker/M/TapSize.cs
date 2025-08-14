namespace PE_TapMaker.M
{
    public class TapSize
    {
        public double SizeInches { get; set; }
        public string DisplayName { get; set; }

        public TapSize(double sizeInches)
        {
            SizeInches = sizeInches;
            DisplayName = $"{sizeInches}\"";
        }

        public TapSize(double sizeInches, string customDisplayName)
        {
            SizeInches = sizeInches;
            DisplayName = customDisplayName;
        }

        public static List<TapSize> GetStandardTapSizes()
        {
            return new List<TapSize>
            {
                new TapSize(4),
                new TapSize(5),
                new TapSize(6),
                new TapSize(7),
                new TapSize(8),
                new TapSize(9),
                new TapSize(10),
                new TapSize(12),
                new TapSize(14),
                new TapSize(16),
                new TapSize(18),
                new TapSize(20),
                new TapSize(22),
                new TapSize(24),
            };
        }
    }
}
