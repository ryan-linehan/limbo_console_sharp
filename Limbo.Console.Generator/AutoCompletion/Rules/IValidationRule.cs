using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Limbo.Console.Generator.AutoCompletion.Rules
{
    internal interface IValidationRule
    {
        /// <summary>
        /// Returns diagnostics results of issues with generator
        /// </summary>
        /// <param name="methodSymbol"></param>
        /// <param name="autoCompletes"></param>
        IEnumerable<Diagnostic> Validate(IMethodSymbol methodSymbol, IEnumerable<AutoCompleteDefinition> autoCompletes);
    }
}