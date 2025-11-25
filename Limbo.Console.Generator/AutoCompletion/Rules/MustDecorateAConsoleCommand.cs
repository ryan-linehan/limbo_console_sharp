using System.Collections.Generic;
using System.Linq;
using Limbo.Console.Sharp;
using Microsoft.CodeAnalysis;

namespace Limbo.Console.Generator.AutoCompletion.Rules
{
    internal class MustDecorateAConsoleCommand : IValidationRule
    {
        public static readonly DiagnosticDescriptor Descriptor = new DiagnosticDescriptor(
            id: "LIMBO1000",
            title: "Must be decorated with [ConsoleCommand]",
            messageFormat: "Method {0} must be decorated with the [ConsoleCommand] attribute to use AutoComplete",
            category: "Limbo.Console.Generator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );
        public IEnumerable<Diagnostic> Validate(IMethodSymbol methodSymbol, IEnumerable<AutoCompleteDefinition> autoCompletes)
        {
            if (methodSymbol.GetAttributes().Any(a => a?.AttributeClass?.Name == nameof(ConsoleCommandAttribute)))
                return Enumerable.Empty<Diagnostic>();

            var methodName = $"{methodSymbol.ContainingType.Name}.{methodSymbol.Name}";
            return new[] { Diagnostic.Create(Descriptor, autoCompletes.First().Location, methodName) };
        }
    }
}