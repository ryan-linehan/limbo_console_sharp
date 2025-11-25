using System.Collections.Generic;
using System.Linq;
using Limbo.Console.Generator.AutoCompletion.Rules;
using Limbo.Console.Sharp;
using Microsoft.CodeAnalysis;

namespace Limbo.Console.Generator.AutoCompletion
{
    /// <summary>
    /// Supporting functions for parsring AutoCompletion when processing [ConsoleCommands] 
    /// </summary>
    public static class AutoCompletes
    {
        /// <summary>
        /// Validation rules for auto-completion attributes
        /// </summary>
        private static readonly IEnumerable<IValidationRule> ValidationRules = new IValidationRule[]
{
            new MustDecorateAConsoleCommand(),
            new NoDuplicateIndices(),
};
        /// <summary>
        /// Parses all of the [AutoComplete] attributes on the method, if any.
        /// </summary>
        /// <param name="methodSymbol"></param>
        /// <returns></returns>
        internal static (IEnumerable<AutoCompleteDefinition> autoCompleteDefinitions, IEnumerable<Diagnostic> diagnosticsResults)  Parse(IMethodSymbol methodSymbol, GeneratorSyntaxContext context)
        {
            var methodAutoCompletes = methodSymbol.GetAttributes()
                                            .Where(attr => attr.AttributeClass?.Name == nameof(AutoCompleteAttribute))
                                            .Select(attr => AsAutoCompleteDefinition(attr, null))
                                            .ToArray();
            // Parameter-level attributes
            var parameterAutoCompletes = methodSymbol.Parameters
                .SelectMany((param, index) =>
                    param.GetAttributes()
                        .Where(attr => attr.AttributeClass?.Name == nameof(AutoCompleteAttribute))
                        .Select(attr => AsAutoCompleteDefinition(attr, index))
                ).ToArray();

            List<AutoCompleteDefinition> autoCompletes = new List<AutoCompleteDefinition>();
            if (methodAutoCompletes.Length > 0)
                autoCompletes = new List<AutoCompleteDefinition>(methodAutoCompletes);
            else if (parameterAutoCompletes.Length > 0)
                autoCompletes = new List<AutoCompleteDefinition>(parameterAutoCompletes);

            var diagnosticResults = ValidateAutoCompletes(methodSymbol, autoCompletes);

            return (autoCompletes, diagnosticResults);
        }

        private static IEnumerable<Diagnostic> ValidateAutoCompletes(IMethodSymbol methodSymbol, IEnumerable<AutoCompleteDefinition> autoCompletes)
        {
            var autoCompletesArray = autoCompletes as AutoCompleteDefinition[] ?? autoCompletes.ToArray();
            if (!autoCompletesArray.Any())
                return Enumerable.Empty<Diagnostic>();
            List<Diagnostic> diagnostics = new List<Diagnostic>();
            foreach (var rule in ValidationRules)
            {
                diagnostics.AddRange(rule.Validate(methodSymbol, autoCompletesArray));
            }
            return diagnostics;
        }

        private static AutoCompleteDefinition AsAutoCompleteDefinition(AttributeData attr, int? index = null)
        {
            // Add warning for auto complete attributes that are on a parameter but not on a ConsoleCommand method
            // Add warning for auto complete attributes that are on a parameter and use the index value (it will be ignored)
            var sourceMethod = attr.ConstructorArguments[0].Value as string ?? string.Empty;
            var argIndex = attr.ConstructorArguments.Length > 1 ? (int)(attr.ConstructorArguments[1].Value ?? 0) : 0;
            if (index != null)
                argIndex = index.Value;
            return new AutoCompleteDefinition(sourceMethod, argIndex, attr.ApplicationSyntaxReference?.GetSyntax()?.GetLocation());
        }
    }
}