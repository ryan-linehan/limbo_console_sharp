using Microsoft.CodeAnalysis;

namespace Limbo.Console.Generator.AutoCompletion
{
    internal sealed class AutoCompleteDefinition
    {
        public AutoCompleteDefinition(string sourceMethod, int argIndex, Location location)
        {
            SourceMethod = sourceMethod;
            ArgIndex = argIndex;
            Location = location;
        }

        public readonly string SourceMethod;
        public readonly int ArgIndex;
        public readonly Location Location;
    }
}