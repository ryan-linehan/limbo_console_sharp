using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Limbo.Console.Generator.AutoCompletion;
using Limbo.Console.Generator.AutoCompletion.Rules;

namespace Limbo.Console.Sharp.Generator
{
    /// <summary>
    /// Generates a single function to register all functions in the class labeled with <see cref="ConsoleCommandAttribute"/> 
    /// </summary>
    [Generator]
    public sealed class ConsoleCommandGenerator : IIncrementalGenerator
    {
        /// <summary>
        /// Validation rules for console commands
        /// </summary>
        private static readonly IEnumerable<IValidationRule> ValidationRules = new IValidationRule[]
        {
                    new MixedAutoCompleteUsage()
        };
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var methodsWithAttr = context.SyntaxProvider
              .CreateSyntaxProvider(
                predicate: (s, _) => s is MethodDeclarationSyntax m && m.AttributeLists.Count > 0,
                transform: (ctx, _) => GetCommandMethod(ctx))
              .Where(m => m.MethodInfo != null);

            var compilationAndMethods = context.CompilationProvider.Combine(methodsWithAttr.Collect());

            context.RegisterSourceOutput(compilationAndMethods, (spc, source) =>
            {
                var (compilation, methodResults) = source;

                // Report diagnostics
                foreach (var result in methodResults)
                {
                    foreach (var diag in result.Diagnostics)
                    {
                        spc.ReportDiagnostic(diag);
                    }
                }

                var grouped = methodResults
                  .Where(m => m.MethodInfo != null
                               // Only script out methods with no errors - we don't want to be the reason that the file doesn't generate
                               // we want to make sure that RegisterConsoleCommands() always generates even if it is empty
                               && m.Diagnostics.All(x => x.DefaultSeverity != DiagnosticSeverity.Error)
                            )
                  .GroupBy(m => m.MethodInfo.ContainingType, SymbolEqualityComparer.Default);

                foreach (var group in grouped)
                {
                    var typeSymbol = group.Key;
                    var src = GenerateRegisterFunction(typeSymbol, group.Select(x => x.MethodInfo).ToArray());
                    spc.AddSource($"{typeSymbol.Name}_ConsoleCommands.g.cs", SourceText.From(src, Encoding.UTF8));
                }
            });
        }

        private static CommandMethodResult GetCommandMethod(GeneratorSyntaxContext context)
        {
            var methodSyntax = (MethodDeclarationSyntax)context.Node;
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodSyntax) as IMethodSymbol;
            // TODO: Refactor to rule interface
            if (methodSymbol is null || !methodSymbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == nameof(ConsoleCommandAttribute)))
                return new CommandMethodResult(null, ImmutableArray<Diagnostic>.Empty);

            var attrData = methodSymbol.GetAttributes().First(attr => attr.AttributeClass?.Name == nameof(ConsoleCommandAttribute));
            var args = attrData.ConstructorArguments;

            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
            (var autoCompletes, var diagnosticResults) = AutoCompletes.Parse(methodSymbol, context);
            var info = new CommandMethodInfo(methodSymbol, args, autoCompletes);

            diagnostics.AddRange(diagnosticResults);
            foreach (var item in ValidationRules)
            {
                var ruleDiagnostics = item.Validate(methodSymbol, autoCompletes);
                diagnostics.AddRange(ruleDiagnostics);
            }

            return new CommandMethodResult(info, diagnostics.ToImmutable());
        }

        private static string GenerateRegisterFunction(ISymbol classSymbol, CommandMethodInfo[] methods)
        {
            var ns = classSymbol.ContainingNamespace.ToDisplayString();
            var sb = new StringBuilder();

            sb.AppendLine("using Godot;");
            sb.AppendLine("using Limbo.Console.Sharp;");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(ns) && !classSymbol.ContainingNamespace.IsGlobalNamespace)
            {
                sb.AppendLine($"namespace {ns} {{");
            }

            var accessibility = GetAccessibilityString(classSymbol.DeclaredAccessibility);


            sb.AppendLine($"{accessibility} partial class {classSymbol.Name} {{");
            AddRegisterConsoleCommands(sb, methods);
            sb.AppendLine();
            AddUnregisterConsoleCommands(sb, methods);
            sb.AppendLine("}"); // class

            if (!string.IsNullOrEmpty(ns) && !classSymbol.ContainingNamespace.IsGlobalNamespace)
            {
                sb.AppendLine("}"); // namespace
            }

            return sb.ToString();
        }

        private static string GetAccessibilityString(Accessibility accessDefinition)
        {
            switch (accessDefinition)
            {
                case Accessibility.Public:
                    return "public";
                case Accessibility.Internal:
                    return "internal";
                case Accessibility.Private:
                    return "private";
                case Accessibility.Protected:
                    return "protected";
                case Accessibility.ProtectedAndInternal:
                    return "protected internal";
                case Accessibility.ProtectedOrInternal:
                    return "internal protected";
                default:
                    return "internal";
            }
        }

        private static void AddRegisterConsoleCommands(StringBuilder sb, IEnumerable<CommandMethodInfo> methods)
        {
            sb.AppendLine(" private void RegisterConsoleCommands() {");

            foreach (var method in methods)
            {
                var callable = method.Method.Parameters.Length == 0
                    ? $"new Callable(this, nameof({method.Method.Name}))"
                    : $"new Callable(this, \"{method.Method.Name}\")"; // TODO: consider arg-aware logic

                var registerCall = method.Description != null
                    ? $"LimboConsole.RegisterCommand({callable}, \"{method.Name}\", \"{method.Description}\");"
                    : $"LimboConsole.RegisterCommand({callable}, \"{method.Name}\");";

                sb.AppendLine("    " + registerCall);

                foreach (var autoComplete in method.AutoCompletes)
                {
                    sb.AppendLine($"    LimboConsole.AddArgumentAutocompleteSource(\"{method.Name}\", {autoComplete.ArgIndex}, Callable.From(() => {autoComplete.SourceMethod}()));");
                }
            }

            sb.AppendLine("  }");
        }

        private static void AddUnregisterConsoleCommands(StringBuilder sb, IEnumerable<CommandMethodInfo> methods)
        {
            // NOTE: Limbo will automatically unregister the registed auto-completes
            // when its command is unregistered, so we don't need to handle
            sb.AppendLine("  private void UnregisterConsoleCommands() {");

            foreach (var method in methods)
            {
                var unregisterCall = $"LimboConsole.UnregisterCommand(\"{method.Name}\");";
                sb.AppendLine("    " + unregisterCall);
            }

            sb.AppendLine("  }"); // end Unregister
        }

        private sealed class CommandMethodInfo
        {
            public CommandMethodInfo(IMethodSymbol method, ImmutableArray<TypedConstant> args, IEnumerable<AutoCompleteDefinition> autoCompletes)
            {
                Method = method;
                ContainingType = method.ContainingType;
                string name = args.Length > 0 ? args[0].Value?.ToString() : null;
                // If the name is not provided we'll opt to use the name of the method the attribute is on to drive the name
                Name = string.IsNullOrEmpty(name) ? method.Name : name;
                Description = args.Length > 1 ? args[1].Value?.ToString() : null;
                AutoCompletes = autoCompletes.ToList();
            }

            public IMethodSymbol Method { get; }
            public INamedTypeSymbol ContainingType { get; }
            public string Name { get; }
            public string Description { get; }
            public List<AutoCompleteDefinition> AutoCompletes { get; }
        }
        private struct CommandMethodResult
        {
            public CommandMethodInfo MethodInfo
            { get; }
            public ImmutableArray<Diagnostic> Diagnostics { get; }

            public CommandMethodResult(CommandMethodInfo methodInfo, ImmutableArray<Diagnostic> diagnostics)
            {
                MethodInfo = methodInfo;
                Diagnostics = diagnostics;
            }
        }
    }
}
