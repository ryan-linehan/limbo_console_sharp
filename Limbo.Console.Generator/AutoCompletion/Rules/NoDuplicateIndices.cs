using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Limbo.Console.Generator.AutoCompletion.Rules
{
    internal class NoDuplicateIndices : IValidationRule
    {
        public static readonly DiagnosticDescriptor Descriptor = new DiagnosticDescriptor(
            id: "LIMBO1001",
            title: "Duplicate argument indices",
            messageFormat: "Method has duplicate AutoComplete argument indices defined: {0}",
            category: "Limbo.Console.Generator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public IEnumerable<Diagnostic> Validate(IMethodSymbol methodSymbol, IEnumerable<AutoCompleteDefinition> autoCompletes)
        {
            var duplicateArgs = autoCompletes
                                      .GroupBy(ac => ac.ArgIndex)
                                      .Where(g => g.Count() > 1).ToList();

            if (!duplicateArgs.Any())
                return Enumerable.Empty<Diagnostic>();

            List<Diagnostic> diagnostics = new List<Diagnostic>();
            foreach (var item in duplicateArgs)
            {
                var autoCompleteInfo = duplicateArgs.SelectMany(x => x).ToList();
                foreach (var duplicate in autoCompleteInfo)
                {
                    diagnostics.Add(Diagnostic.Create(Descriptor, duplicate.Location, item.Key));
                }
            }
            return diagnostics;
        }
    }
}