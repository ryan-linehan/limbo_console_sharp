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
        /// The index of the parameter in the AutoComplete function's signature.
        /// </summary>
        public int ArgumentIndex { get; }
        
        public AutoCompleteAttribute(string methodName, int argumentIndex = 0)
        {
            (MethodName, ArgumentIndex) = (methodName, argumentIndex);
        }
    }
}