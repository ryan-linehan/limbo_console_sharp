using System;

namespace Limbo.Console.Sharp
{
    /// <summary>
    /// Defines an autocomplete source for a ConsoleCommand parameter
    /// </summary>
    /// <remarks>
    /// Placed on the ConsoleCommand function
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = true)]
    public sealed class AutoCompleteAttribute : Attribute {

        /// <summary>
        /// The name of the method providing the autocomplete suggestions
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// Inline array of autocomplete values
        /// </summary>
        public string[] InlineValues { get; }

        /// <summary>
        /// The index of the parameter in the AutoComplete function's signature.
        /// </summary>
        public int ArgumentIndex { get; }

        /// <summary>
        /// Creates an autocomplete attribute with a method name
        /// </summary>
        /// <param name="methodName">The name of the method that returns autocomplete suggestions</param>
        /// <param name="argumentIndex">The parameter index (0-based)</param>
        public AutoCompleteAttribute(string methodName, int argumentIndex = 0)
        {
            MethodName = methodName;
            ArgumentIndex = argumentIndex;
        }

        /// <summary>
        /// Creates an autocomplete attribute with inline values
        /// </summary>
        /// <param name="inlineValues">Array of string values for autocomplete</param>
        /// <param name="argumentIndex">The parameter index (0-based)</param>
        public AutoCompleteAttribute(string[] inlineValues, int argumentIndex = 0)
        {
            InlineValues = inlineValues;
            ArgumentIndex = argumentIndex;
        }
    }
}