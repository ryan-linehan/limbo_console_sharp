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
            InlineValues = null;
        }

        public AutoCompleteDefinition(string[] inlineValues, int argIndex, Location location)
        {
            InlineValues = inlineValues;
            ArgIndex = argIndex;
            Location = location;
            SourceMethod = null;
        }

        public readonly string SourceMethod;
        public readonly string[] InlineValues;
        public readonly int ArgIndex;
        public readonly Location Location;

        /// <summary>
        /// Returns true if this definition uses inline values, false if it uses a method name
        /// </summary>
        public bool IsInline => InlineValues != null;
    }
}