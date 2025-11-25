using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Limbo.Console.Generator.AutoCompletion.Rules
{
    internal class MixedAutoCompleteUsage : IValidationRule
    {
        public static readonly DiagnosticDescriptor Descriptor = new DiagnosticDescriptor(
            id: "LIMBO1002",
            title: "Invalid AutoComplete Attribute Usage",
            messageFormat: "Cannot mix method-level and parameter-level AutoComplete attributes on the same method",
            category: "Limbo.Console.Generator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );
        public IEnumerable<Diagnostic> Validate(IMethodSymbol methodSymbol, IEnumerable<AutoCompleteDefinition> autoCompletes)
        {
            bool hasMethodLevel = methodSymbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "AutoCompleteAttribute");
            bool hasParameterLevel = methodSymbol.Parameters.Any(p => p.GetAttributes().Any(attr => attr.AttributeClass?.Name == "AutoCompleteAttribute"));

            if (hasMethodLevel && hasParameterLevel)
            {
                methodSymbol.Locations.First();
                return new[] { Diagnostic.Create(Descriptor, methodSymbol.Locations.First()) };
            }

            return Enumerable.Empty<Diagnostic>();

        }
    }
}
