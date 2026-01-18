using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ApiChangeDetector;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Extract and compare limbo_console API signatures");

        // Extract command
        var extractCommand = new Command("extract", "Extract API signatures from a GDScript file");
        var inputOption = new Option<FileInfo>("--input", "Input GDScript file path") { IsRequired = true };
        inputOption.AddAlias("-i");
        var outputOption = new Option<FileInfo?>("--output", "Output JSON file path (stdout if not specified)");
        outputOption.AddAlias("-o");

        extractCommand.AddOption(inputOption);
        extractCommand.AddOption(outputOption);
        extractCommand.SetHandler(ExtractHandler, inputOption, outputOption);

        // Compare command
        var compareCommand = new Command("compare", "Compare two API snapshots");
        var baselineOption = new Option<FileInfo>("--baseline", "Baseline JSON file") { IsRequired = true };
        baselineOption.AddAlias("-b");
        var currentOption = new Option<FileInfo>("--input", "Current API JSON file") { IsRequired = true };
        currentOption.AddAlias("-i");
        var formatOption = new Option<string>("--format", () => "json", "Output format (json or markdown)");
        formatOption.AddAlias("-f");

        compareCommand.AddOption(baselineOption);
        compareCommand.AddOption(currentOption);
        compareCommand.AddOption(formatOption);
        compareCommand.SetHandler(CompareHandler, baselineOption, currentOption, formatOption);

        rootCommand.AddCommand(extractCommand);
        rootCommand.AddCommand(compareCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task ExtractHandler(FileInfo input, FileInfo? output)
    {
        var content = await File.ReadAllTextAsync(input.FullName);
        var api = GdScriptParser.ExtractApi(content, input.Name);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(api, options);

        if (output != null)
        {
            await File.WriteAllTextAsync(output.FullName, json);
            Console.WriteLine($"API extracted to {output.FullName}");
        }
        else
        {
            Console.WriteLine(json);
        }
    }

    private static async Task<int> CompareHandler(FileInfo baseline, FileInfo current, string format)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var baselineContent = await File.ReadAllTextAsync(baseline.FullName);
        var currentContent = await File.ReadAllTextAsync(current.FullName);

        var baselineApi = JsonSerializer.Deserialize<ApiSnapshot>(baselineContent, options)!;
        var currentApi = JsonSerializer.Deserialize<ApiSnapshot>(currentContent, options)!;

        var changes = ApiComparer.Compare(baselineApi, currentApi);
        var hasChanges = changes.HasChanges();

        if (format.Equals("markdown", StringComparison.OrdinalIgnoreCase))
        {
            if (hasChanges)
            {
                Console.WriteLine(changes.ToMarkdown());
            }
            else
            {
                Console.WriteLine("No API changes detected.");
            }
        }
        else
        {
            var changesJson = JsonSerializer.Serialize(changes, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });
            Console.WriteLine(changesJson);
        }

        return hasChanges ? 1 : 0;
    }
}

public record ApiSnapshot
{
    public string SourceFile { get; init; } = "";
    public List<SignalInfo> Signals { get; init; } = [];
    public List<PropertyInfo> Properties { get; init; } = [];
    public List<FunctionInfo> PublicFunctions { get; init; } = [];
}

public record SignalInfo
{
    public string Name { get; init; } = "";
    public List<string> Args { get; init; } = [];
}

public record PropertyInfo
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "Variant";
}

public record FunctionInfo
{
    public string Name { get; init; } = "";
    public List<ArgumentInfo> Args { get; init; } = [];
    public string ReturnType { get; init; } = "void";
}

public record ArgumentInfo
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "Variant";
    public bool HasDefault { get; init; }
}

public record ApiChanges
{
    public List<FunctionInfo> AddedFunctions { get; init; } = [];
    public List<FunctionInfo> RemovedFunctions { get; init; } = [];
    public List<ModifiedFunction> ModifiedFunctions { get; init; } = [];
    public List<SignalInfo> AddedSignals { get; init; } = [];
    public List<SignalInfo> RemovedSignals { get; init; } = [];
    public List<ModifiedSignal> ModifiedSignals { get; init; } = [];
    public List<PropertyInfo> AddedProperties { get; init; } = [];
    public List<PropertyInfo> RemovedProperties { get; init; } = [];
    public List<ModifiedProperty> ModifiedProperties { get; init; } = [];

    public bool HasChanges() =>
        AddedFunctions.Count > 0 || RemovedFunctions.Count > 0 || ModifiedFunctions.Count > 0 ||
        AddedSignals.Count > 0 || RemovedSignals.Count > 0 || ModifiedSignals.Count > 0 ||
        AddedProperties.Count > 0 || RemovedProperties.Count > 0 || ModifiedProperties.Count > 0;

    public string ToMarkdown()
    {
        var lines = new List<string> { "## API Changes Detected", "" };

        if (AddedFunctions.Count > 0)
        {
            lines.Add("### Added Functions");
            foreach (var f in AddedFunctions)
            {
                var args = string.Join(", ", f.Args.Select(a => $"{a.Name}: {a.Type}"));
                lines.Add($"- `{f.Name}({args}) -> {f.ReturnType}`");
            }
            lines.Add("");
        }

        if (RemovedFunctions.Count > 0)
        {
            lines.Add("### Removed Functions");
            foreach (var f in RemovedFunctions)
            {
                var args = string.Join(", ", f.Args.Select(a => $"{a.Name}: {a.Type}"));
                lines.Add($"- `{f.Name}({args}) -> {f.ReturnType}`");
            }
            lines.Add("");
        }

        if (ModifiedFunctions.Count > 0)
        {
            lines.Add("### Modified Functions");
            foreach (var m in ModifiedFunctions)
            {
                var oldArgs = string.Join(", ", m.Old.Args.Select(a => $"{a.Name}: {a.Type}"));
                var newArgs = string.Join(", ", m.New.Args.Select(a => $"{a.Name}: {a.Type}"));
                lines.Add($"- `{m.Name}`");
                lines.Add($"  - Old: `({oldArgs}) -> {m.Old.ReturnType}`");
                lines.Add($"  - New: `({newArgs}) -> {m.New.ReturnType}`");
            }
            lines.Add("");
        }

        if (AddedSignals.Count > 0)
        {
            lines.Add("### Added Signals");
            foreach (var s in AddedSignals)
            {
                var args = string.Join(", ", s.Args);
                lines.Add($"- `signal {s.Name}({args})`");
            }
            lines.Add("");
        }

        if (RemovedSignals.Count > 0)
        {
            lines.Add("### Removed Signals");
            foreach (var s in RemovedSignals)
            {
                var args = string.Join(", ", s.Args);
                lines.Add($"- `signal {s.Name}({args})`");
            }
            lines.Add("");
        }

        if (AddedProperties.Count > 0)
        {
            lines.Add("### Added Properties");
            foreach (var p in AddedProperties)
            {
                lines.Add($"- `var {p.Name}: {p.Type}`");
            }
            lines.Add("");
        }

        if (RemovedProperties.Count > 0)
        {
            lines.Add("### Removed Properties");
            foreach (var p in RemovedProperties)
            {
                lines.Add($"- `var {p.Name}: {p.Type}`");
            }
            lines.Add("");
        }

        return string.Join("\n", lines);
    }
}

public record ModifiedFunction
{
    public string Name { get; init; } = "";
    public FunctionInfo Old { get; init; } = new();
    public FunctionInfo New { get; init; } = new();
}

public record ModifiedSignal
{
    public string Name { get; init; } = "";
    public SignalInfo Old { get; init; } = new();
    public SignalInfo New { get; init; } = new();
}

public record ModifiedProperty
{
    public string Name { get; init; } = "";
    public PropertyInfo Old { get; init; } = new();
    public PropertyInfo New { get; init; } = new();
}

public static class GdScriptParser
{
    private static readonly Regex FunctionPattern = new(
        @"^func\s+(\w+)\s*\(([^)]*)\)\s*(?:->\s*(\w+))?\s*:",
        RegexOptions.Compiled);

    private static readonly Regex SignalPattern = new(
        @"^signal\s+(\w+)(?:\s*\(([^)]*)\))?",
        RegexOptions.Compiled);

    private static readonly Regex PropertyPattern = new(
        @"^var\s+(\w+)\s*(?::\s*(\w+))?",
        RegexOptions.Compiled);

    public static ApiSnapshot ExtractApi(string content, string fileName)
    {
        var lines = content.Split('\n');
        var api = new ApiSnapshot { SourceFile = fileName };

        var signals = new List<SignalInfo>();
        var properties = new List<PropertyInfo>();
        var publicFunctions = new List<FunctionInfo>();

        var inPublicSection = false;

        foreach (var line in lines)
        {
            var stripped = line.Trim();

            // Class-level declarations start at column 0 (no leading whitespace)
            var isClassLevel = line.Length > 0 && !char.IsWhiteSpace(line[0]) && stripped.Length > 0;

            // Track PUBLIC INTERFACE section
            if (line.Contains("# *** PUBLIC INTERFACE"))
            {
                inPublicSection = true;
                continue;
            }
            if (line.Contains("# *** PRIVATE"))
            {
                inPublicSection = false;
                continue;
            }

            // Parse signals (always at class level)
            if (isClassLevel && stripped.StartsWith("signal "))
            {
                var signalInfo = ParseSignal(stripped);
                if (signalInfo != null)
                {
                    signals.Add(signalInfo);
                }
                continue;
            }

            // Parse public properties at class level only
            if (isClassLevel && !inPublicSection && stripped.StartsWith("var ") && !stripped.StartsWith("var _"))
            {
                var propInfo = ParseProperty(stripped);
                if (propInfo != null)
                {
                    properties.Add(propInfo);
                }
                continue;
            }

            // Parse public functions (in PUBLIC INTERFACE section, at class level)
            if (isClassLevel && inPublicSection && stripped.StartsWith("func "))
            {
                var funcInfo = ParseFunction(stripped);
                if (funcInfo != null)
                {
                    publicFunctions.Add(funcInfo);
                }
            }
        }

        return api with
        {
            Signals = signals,
            Properties = properties,
            PublicFunctions = publicFunctions
        };
    }

    private static FunctionInfo? ParseFunction(string line)
    {
        var match = FunctionPattern.Match(line);
        if (!match.Success) return null;

        var name = match.Groups[1].Value;
        var argsStr = match.Groups[2].Value.Trim();
        var returnType = match.Groups[3].Success ? match.Groups[3].Value : "void";

        var args = new List<ArgumentInfo>();
        if (!string.IsNullOrEmpty(argsStr))
        {
            var argParts = SplitArguments(argsStr);
            foreach (var arg in argParts)
            {
                var argInfo = ParseArgument(arg.Trim());
                if (argInfo != null)
                {
                    args.Add(argInfo);
                }
            }
        }

        return new FunctionInfo
        {
            Name = name,
            Args = args,
            ReturnType = returnType
        };
    }

    private static List<string> SplitArguments(string argsStr)
    {
        var parts = new List<string>();
        var depth = 0;
        var current = "";

        foreach (var c in argsStr)
        {
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                if (!string.IsNullOrWhiteSpace(current))
                {
                    parts.Add(current.Trim());
                }
                current = "";
                continue;
            }

            current += c;
        }

        if (!string.IsNullOrWhiteSpace(current))
        {
            parts.Add(current.Trim());
        }

        return parts;
    }

    private static ArgumentInfo? ParseArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return null;

        var hasDefault = arg.Contains('=');
        if (hasDefault)
        {
            arg = arg.Split('=')[0].Trim();
        }

        string name;
        var type = "Variant";

        if (arg.Contains(':'))
        {
            var parts = arg.Split(':');
            name = parts[0].Trim();
            type = parts[1].Trim();
        }
        else
        {
            name = arg.Trim();
        }

        return new ArgumentInfo
        {
            Name = name,
            Type = type,
            HasDefault = hasDefault
        };
    }

    private static SignalInfo? ParseSignal(string line)
    {
        var match = SignalPattern.Match(line);
        if (!match.Success) return null;

        var name = match.Groups[1].Value;
        var argsStr = match.Groups[2].Success ? match.Groups[2].Value : "";

        var args = new List<string>();
        if (!string.IsNullOrWhiteSpace(argsStr))
        {
            foreach (var arg in argsStr.Split(','))
            {
                var trimmed = arg.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    args.Add(trimmed);
                }
            }
        }

        return new SignalInfo
        {
            Name = name,
            Args = args
        };
    }

    private static PropertyInfo? ParseProperty(string line)
    {
        var match = PropertyPattern.Match(line);
        if (!match.Success) return null;

        var name = match.Groups[1].Value;
        if (name.StartsWith('_')) return null;

        var type = match.Groups[2].Success ? match.Groups[2].Value : "Variant";

        return new PropertyInfo
        {
            Name = name,
            Type = type
        };
    }
}

public static class ApiComparer
{
    public static ApiChanges Compare(ApiSnapshot baseline, ApiSnapshot current)
    {
        var baselineFuncs = baseline.PublicFunctions.ToDictionary(f => f.Name);
        var currentFuncs = current.PublicFunctions.ToDictionary(f => f.Name);

        var addedFunctions = new List<FunctionInfo>();
        var removedFunctions = new List<FunctionInfo>();
        var modifiedFunctions = new List<ModifiedFunction>();

        foreach (var (name, func) in currentFuncs)
        {
            if (!baselineFuncs.TryGetValue(name, out var baselineFunc))
            {
                addedFunctions.Add(func);
            }
            else if (!FunctionsEqual(baselineFunc, func))
            {
                modifiedFunctions.Add(new ModifiedFunction
                {
                    Name = name,
                    Old = baselineFunc,
                    New = func
                });
            }
        }

        foreach (var (name, func) in baselineFuncs)
        {
            if (!currentFuncs.ContainsKey(name))
            {
                removedFunctions.Add(func);
            }
        }

        // Compare signals
        var baselineSignals = baseline.Signals.ToDictionary(s => s.Name);
        var currentSignals = current.Signals.ToDictionary(s => s.Name);

        var addedSignals = new List<SignalInfo>();
        var removedSignals = new List<SignalInfo>();
        var modifiedSignals = new List<ModifiedSignal>();

        foreach (var (name, signal) in currentSignals)
        {
            if (!baselineSignals.TryGetValue(name, out var baselineSignal))
            {
                addedSignals.Add(signal);
            }
            else if (!SignalsEqual(baselineSignal, signal))
            {
                modifiedSignals.Add(new ModifiedSignal
                {
                    Name = name,
                    Old = baselineSignal,
                    New = signal
                });
            }
        }

        foreach (var (name, signal) in baselineSignals)
        {
            if (!currentSignals.ContainsKey(name))
            {
                removedSignals.Add(signal);
            }
        }

        // Compare properties
        var baselineProps = baseline.Properties.ToDictionary(p => p.Name);
        var currentProps = current.Properties.ToDictionary(p => p.Name);

        var addedProperties = new List<PropertyInfo>();
        var removedProperties = new List<PropertyInfo>();
        var modifiedProperties = new List<ModifiedProperty>();

        foreach (var (name, prop) in currentProps)
        {
            if (!baselineProps.TryGetValue(name, out var baselineProp))
            {
                addedProperties.Add(prop);
            }
            else if (baselineProp.Type != prop.Type)
            {
                modifiedProperties.Add(new ModifiedProperty
                {
                    Name = name,
                    Old = baselineProp,
                    New = prop
                });
            }
        }

        foreach (var (name, prop) in baselineProps)
        {
            if (!currentProps.ContainsKey(name))
            {
                removedProperties.Add(prop);
            }
        }

        return new ApiChanges
        {
            AddedFunctions = addedFunctions,
            RemovedFunctions = removedFunctions,
            ModifiedFunctions = modifiedFunctions,
            AddedSignals = addedSignals,
            RemovedSignals = removedSignals,
            ModifiedSignals = modifiedSignals,
            AddedProperties = addedProperties,
            RemovedProperties = removedProperties,
            ModifiedProperties = modifiedProperties
        };
    }

    private static bool FunctionsEqual(FunctionInfo a, FunctionInfo b)
    {
        if (a.ReturnType != b.ReturnType) return false;
        if (a.Args.Count != b.Args.Count) return false;

        for (var i = 0; i < a.Args.Count; i++)
        {
            if (a.Args[i].Name != b.Args[i].Name ||
                a.Args[i].Type != b.Args[i].Type ||
                a.Args[i].HasDefault != b.Args[i].HasDefault)
            {
                return false;
            }
        }

        return true;
    }

    private static bool SignalsEqual(SignalInfo a, SignalInfo b)
    {
        if (a.Args.Count != b.Args.Count) return false;

        for (var i = 0; i < a.Args.Count; i++)
        {
            if (a.Args[i] != b.Args[i]) return false;
        }

        return true;
    }
}
