using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace LibraryAnalyzers.PeAnalyzers;
/// <summary>
///     Analyzer for checking if settings classes for use with Storage json are valid.
///     This is still a Work In Progress. BE MINDFUL OF ITS ERRORS.
/// </summary>

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class JsonSettingsAnalyzer : DiagnosticAnalyzer
{
    public const string NoFieldsRuleId = "PE001";
    public const string NoAbstractRuleId = "PE002";
    public const string MustBePublicRuleId = "PE003";
    public const string RequiredNeedsDefaultRuleId = "PE004";
    public const string UseInitNotSetRuleId = "PE005";

    private static readonly DiagnosticDescriptor NoFieldsRule = new(
        NoFieldsRuleId,
        "Settings classes should not have fields",
        "Field '{0}' in settings class '{1}' should be a property. JSON serialization works with properties, not fields.",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Settings classes used with Json<T> should use properties instead of fields for proper serialization.");

    private static readonly DiagnosticDescriptor NoAbstractRule = new(
        NoAbstractRuleId,
        "Settings classes cannot be abstract",
        "Settings class '{0}' cannot be abstract. Json<T> requires concrete types that can be instantiated.",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Settings classes must be concrete to allow instantiation by the JSON serializer.");

    private static readonly DiagnosticDescriptor MustBePublicRule = new(
        MustBePublicRuleId,
        "Settings class and properties must be public",
        "{0} '{1}' in settings class must be public for JSON serialization",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Settings classes and their properties must be public for the JSON serializer to access them.");

    private static readonly DiagnosticDescriptor RequiredNeedsDefaultRule = new(
        RequiredNeedsDefaultRuleId,
        "Required properties must have default values",
        "Property '{0}' has [Required] attribute but no default value. Required properties need defaults for recovery scenarios.",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Properties marked with [Required] must have default values to support automatic JSON recovery.");

    private static readonly DiagnosticDescriptor UseInitNotSetRule = new(
        UseInitNotSetRuleId,
        "Settings properties should use 'init' instead of 'set'",
        "Property '{0}' should use 'init' accessor instead of 'set' to ensure immutability after construction",
        "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Settings properties should be immutable after construction to prevent accidental modification.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(NoFieldsRule, NoAbstractRule, MustBePublicRule, RequiredNeedsDefaultRule, UseInitNotSetRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol == null) return;

        // Only analyze classes that implement IOperationSettings (or end with "Settings" for backwards compatibility)
        // OR are nested within a settings class
        if (!IsSettingsClass(classSymbol) && !IsNestedInSettingsClass(classSymbol)) return;

        // Rule: No abstract classes
        if (classSymbol.IsAbstract)
        {
            var diagnostic = Diagnostic.Create(NoAbstractRule, classDeclaration.Identifier.GetLocation(), classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        // Rule: Class must be public
        if (classSymbol.DeclaredAccessibility != Accessibility.Public)
        {
            var diagnostic = Diagnostic.Create(MustBePublicRule, classDeclaration.Identifier.GetLocation(), "Class", classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        // Check all members
        foreach (var member in classSymbol.GetMembers())
        {
            // Rule: No fields (except compiler-generated backing fields)
            if (member is IFieldSymbol field && !field.IsImplicitlyDeclared)
            {
                var diagnostic = Diagnostic.Create(NoFieldsRule, member.Locations[0], field.Name, classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }

            // Check properties
            if (member is IPropertySymbol property)
            {
                // Rule: Properties must be public
                if (property.DeclaredAccessibility != Accessibility.Public)
                {
                    var diagnostic = Diagnostic.Create(MustBePublicRule, property.Locations[0], "Property", property.Name);
                    context.ReportDiagnostic(diagnostic);
                }

                // Rule: Required properties must have default values
                var hasRequired = property.GetAttributes().Any(attr =>
                    attr.AttributeClass?.Name == "RequiredAttribute" ||
                    attr.AttributeClass?.Name == "Required");

                if (hasRequired)
                {
                    var propertySyntax = property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as PropertyDeclarationSyntax;
                    if (propertySyntax?.Initializer == null)
                    {
                        var diagnostic = Diagnostic.Create(RequiredNeedsDefaultRule, property.Locations[0], property.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }

                // Rule: Prefer init over set
                if (property.SetMethod != null && !property.SetMethod.IsInitOnly)
                {
                    var diagnostic = Diagnostic.Create(UseInitNotSetRule, property.Locations[0], property.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private bool IsSettingsClass(INamedTypeSymbol classSymbol)
    {
        // Check if class implements IOperationSettings interface
        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (iface.Name == "IOperationSettings")
                return true;
        }



        return false;
    }

    private bool IsNestedInSettingsClass(INamedTypeSymbol classSymbol)
    {
        // Check if this class is nested within a settings class
        var containingType = classSymbol.ContainingType;
        while (containingType != null)
        {
            if (IsSettingsClass(containingType)) return true;
            containingType = containingType.ContainingType;
        }

        return false;
    }
}

