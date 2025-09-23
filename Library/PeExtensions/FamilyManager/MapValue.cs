using System.Globalization;
using System.Text.RegularExpressions;

namespace PeExtensions;

public static class FamilyManagerMapValue {
    /// <summary>
    ///     Make a MapValue object that can be used to map a parameter value to another parameter value.
    /// </summary>
    /// <remarks>
    ///     YOU MUST set FamilyManager.CurrentType BEFORE using this method. Both getting and setting CurrentType 
    ///     are VERY expensive operations, thus it is not done inside this method. Do it at the highest-level possible
    ///     in your loop/s.
    /// </remarks>
    public static ParameterValueMapper MapValue(this Document doc) => new ParameterValueMapper(doc);

    public class ParameterValueMapper {
        private FamilyManager FamilyManager { get; set; }
        private Units Units { get; set; }

        public object SourceValue { get; set; }
        public FamilyParameter SourceParam { get; set; }
        public FamilyParameter TargetParam { get; set; }

        public string SourceName => this.SourceParam?.Definition.Name ?? "Unknown";
        public string TargetName => this.TargetParam?.Definition.Name ?? "Unknown";
        public ForgeTypeId SourceDataType => this.SourceParam?.Definition.GetDataType();
        public ForgeTypeId TargetDataType => this.TargetParam?.Definition.GetDataType();
        public StorageType SourceStorageType => this.SourceParam?.StorageType ?? StorageType.None;
        public StorageType TargetStorageType => this.TargetParam?.StorageType ?? StorageType.None;

        public bool IsSameDataType => this.SourceDataType == this.TargetDataType;
        public bool IsSameStorageType => this.SourceStorageType == this.TargetStorageType;
        public bool IsTargetElectrical =>
           this.TargetParam?.Definition.GetDataType().TypeId.Contains(".electrical:") == true;

        public string ErrorMessage =>
           $"Cannot map value from {this.SourceName} ({this.SourceDataType}) to {this.TargetName} ({this.TargetDataType})";

        internal ParameterValueMapper(Document doc) {
            ArgumentNullException.ThrowIfNull(doc);
            this.FamilyManager = doc.FamilyManager;
            this.Units = doc.GetUnits();
        }

        /// <summary> Set the source value to be mapped.</summary>
        public ParameterValueMapper Source(object sourceValue) {
            this.SourceValue = sourceValue;
            return this;
        }

        /// <summary>Set the source parameter to be mapped.</summary>
        public ParameterValueMapper Source(FamilyParameter sourceParam) {
            this.SourceParam = sourceParam;
            this.SourceValue = this.FamilyManager.GetValue(sourceParam);
            return this;
        }

        /// <summary>Set the target parameter to be mapped.</summary>
        public ParameterValueMapper Target(FamilyParameter targetParam) {
            this.TargetParam = targetParam;
            return this;
        }

        /// <summary>Map the source value to the target value only if the DATA TYPES are the same.</summary>
        public Result<FamilyParameter> MapStrictly() {
            try {
                return this.FamilyManager.SetValueStrict(this.TargetParam, this.SourceValue);
            } catch (Exception ex) {
                return new Exception($"{this.ErrorMessage}:\n{ex.Message}");
            }
        }

        public Result<FamilyParameter> MapCoercivelyToElectrical() {
            try {
                if (!this.IsTargetElectrical)
                    return new ArgumentException("Target parameter is not electrical");

                var validSrcTypes = new[] { SpecTypeId.String.Text, SpecTypeId.Number, SpecTypeId.Int.Integer };
                if (!validSrcTypes.Contains(this.SourceDataType))
                    throw new ArgumentException($"Source data type {this.SourceDataType} is not coercible to an electrical parameter");

                var currVal = this.SourceDataType switch {
                    var t when t == SpecTypeId.String.Text => ExtractDouble(this.SourceValue as string),
                    var t when t == SpecTypeId.Number => this.SourceValue as double? ?? 0,
                    var t when t == SpecTypeId.Int.Integer => this.SourceValue as int? ?? 0,
                    _ => 0
                };

                var unitTypeId = this.Units.GetFormatOptions(this.TargetDataType).GetUnitTypeId();
                var convertedVal = UnitUtils.ConvertToInternalUnits(currVal, unitTypeId);

                return this.FamilyManager.SetValueStrict(this.TargetParam, convertedVal);

            } catch (Exception ex) {
                return new Exception($"{this.ErrorMessage}:\n{ex.Message}");
            }
        }

        private static double ExtractDouble(string input) {
            var numericString = Regex.Replace(input, @"[^\d.]", "");

            return string.IsNullOrWhiteSpace(numericString) || numericString == "."
                ? throw new ArgumentException("No valid numeric characters found in the string.", nameof(input))
                : double.Parse(numericString, CultureInfo.InvariantCulture);
        }
    }
}