using Godot;
using Limbo.Console.Sharp;
using System;
using System.Linq;

/// <summary>
/// Demo class to show how to use the LimboConsole wrapper
/// </summary>
public partial class Demo : Node2D
{
    public override void _Ready()
    {
        // Registering a command with no arguments
        LimboConsole.RegisterCommand(new Callable(this, MethodName.StartGameCommand), "game start", "Starts the game");
        // Registering a command with a arguments
        LimboConsole.RegisterCommand(new Callable(this, MethodName.StopGameCommand), "game stop", "Stops the game (add a string for options!)");

        // Register commands to start a demo of subscribing to the toggle signal of the console
        LimboConsole.RegisterCommand(new Callable(this, MethodName.StartDemoToggledSignal), "toggle_signal start", "Starts the demoing toggled signal");
        LimboConsole.RegisterCommand(new Callable(this, MethodName.StopDemoToggledSignal), "toggle_signal stop", "Stops the demoing toggled signal");

        // Registering a command with an argument that has an auto-complete source
        LimboConsole.RegisterCommand(new Callable(this, MethodName.ABC), "abc", "Prints A, B, or C based on the argument (autocomplete source)");
        // Arguments with auto-complete sources index starts at 1 (max is 5 currently)
        LimboConsole.AddArgumentAutocompleteSource("abc", 1, Callable.From(() => new string[] { "a", "b", "c" }));

        LimboConsole.RegisterCommand(new Callable(this, MethodName.AddCallableCommands), "add_callable_commands", "adds the commands that show the callable registration");
        LimboConsole.RegisterCommand(new Callable(this, MethodName.RemoveCallableCommands), "remove_callable_commands", "removes the commands that show the callable registration");

        // Commands with attributes will only get registered if the RegisterConsoleCommands method is used
        // from the source generator
        // NOTE: If this method isn't showing up make sure have used an attribute in the class
        // and that you have built the project to generate the code (sometimes IDE intellisense will do this for you but not always)
        RegisterConsoleCommands();
        // NOTE: C# does not support bind and unbind, use lambdas instead:
        // see https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_differences.html#callable        
    }

    [ConsoleCommand("AttributeCommand", "A command registered via attributes using source gen!")]
    public void AttributeConsoleCommand()
    {
        LimboConsole.Info("AttributeCommand executed!");
    }

    [ConsoleCommand(description: "A command registered via attributes using source gen!")]
    [AutoComplete(nameof(Colors))]
    public void AttributeConsoleCommandWithArg(string arg1)
    {
        LimboConsole.Info("AttributeCommandWithArg executed with arg: " + arg1);
    }

#pragma warning disable LIMBO1002 // Invalid AutoComplete Attribute Usage
    /// <summary>
    /// An example of a command ignoring an error but the source generator will still genrate code that compiles
    /// It just skips over this method when trying to create the <see cref="RegisterConsoleCommands"/>
    /// </summary>
    /// <param name="numbers"></param>
    /// <param name="colors"></param>
    [ConsoleCommand]
    [AutoComplete(nameof(Numbers))]
    public void ColorsAndNumbers([AutoComplete(nameof(Numbers))] int numbers,
                                 [AutoComplete(nameof(Colors))] string colors
                                )
    {
        LimboConsole.Info("ColorsAndNumbers command executed with number: " + numbers);
        LimboConsole.Info("ColorsAndNumbers command executed with color: " + colors);
    }
#pragma warning restore LIMBO1002 // Invalid AutoComplete Attribute Usage


    [ConsoleCommand]
    public void SecondParameterAutoCompleted(int numbers, [AutoComplete(nameof(Colors))] string colors)
    {
        LimboConsole.Info("ColorsAndNumbers command executed with number: " + numbers);
        LimboConsole.Info("ColorsAndNumbers command executed with color: " + colors);
    }

    public string[] Colors()
    {
        return new[] { "red", "green", "blue" };
    }

    public int[] Numbers()
    {
        return new[] { 1, 2, 3, 4, 5 };
    }

    /// <summary>
    /// Register commands via callables - currently registered this way to show that it works but the errors are rather annoying
    /// so the user must explicity turn them on to see them in this demo project
    /// NOTE: currently there is a bug in godot that will throw an error but they still work and register
    ///       see (https://github.com/limbonaut/limboLimboConsole/issues/60)
    /// </summary>
    private void AddCallableCommands()
    {
        // Register in-line lamda
        LimboConsole.RegisterCommand(Callable.From(() => LimboConsole.PrintLine("Hello World!")),
                                                         "hello_callable",
                                                         "Prints Hello World from a command registered by a callable");
        // Register an in-line lambda with a callable argument
        LimboConsole.RegisterCommand(Callable.From((string val) => LimboConsole.PrintLine("Hello World! Val: " + val)),
                                                               "hello_callable_arg",
                                                               "Prints Hello World with a value after it");

        // Register a callable with a callable argument and an auto-complete source
        var validOptions = new[] { "a", "b", "c" };
        LimboConsole.RegisterCommand(Callable.From((string val) =>
                                    {
                                        LimboConsole.PrintLine("Hello World! Val: " + (validOptions.Contains(val) ? val : "Invalid"));
                                    }),
                                    "hello_callable_abc",
                                    "Prints Hello World and the value if it is a, b, or c");
        LimboConsole.AddArgumentAutocompleteSource("hello_callable_abc", 1, Callable.From(() => validOptions));
    }

    /// <summary>
    /// Removes the callable commands 
    /// </summary>
    private void RemoveCallableCommands()
    {
        LimboConsole.UnregisterCommand("hello_callable");
        LimboConsole.UnregisterCommand("hello_callable_arg");
        LimboConsole.UnregisterCommand("hello_callable_abc");
    }

    /// <summary>
    /// Start game command
    /// </summary>
    private void StartGameCommand() => LimboConsole.PrintLine("Game started!");

    /// <summary>
    /// Stop game command
    /// </summary>
    /// <param name="options"></param>
    private void StopGameCommand(string options)
    {
        LimboConsole.PrintLine("Game stopped! Option: " + options);
    }

    /// <summary>
    /// Prints A, B, or C based on the argument
    /// </summary>
    /// <param name="aOrBOrC"></param>

    private void ABC(string aOrBOrC)
    {
        switch (aOrBOrC)
        {
            case "a":
                LimboConsole.PrintLine("A");
                break;
            case "b":
                LimboConsole.PrintLine("B");
                break;
            case "c":
                LimboConsole.PrintLine("C");
                break;
            default:
                LimboConsole.PrintLine("Invalid option");
                break;
        }
    }

    private bool _isToggleConnected = false;
    /// <summary>
    /// Starts listening to the toggled signal if not already listening
    /// </summary>
    private void StartDemoToggledSignal()
    {
        if (_isToggleConnected)
            return;
        _isToggleConnected = true;
        LimboConsole.ConnectToggled(new Callable(this, MethodName.OnConsoleToggled));
    }
    /// <summary>
    /// Stops listening to the toggled signal if already listening
    /// </summary>
    private void StopDemoToggledSignal()
    {
        if (!_isToggleConnected)
            return;
        _isToggleConnected = false;
        LimboConsole.DisconnectToggled(new Callable(this, MethodName.OnConsoleToggled));
    }

    /// <summary>
    /// Prints a line to the console when the toggled signal is emitted
    /// </summary>
    /// <param name="isShown"></param>
    private void OnConsoleToggled(bool isShown)
    {
        LimboConsole.PrintLine("Console toggled: " + isShown);
    }

    override public void _ExitTree()
    {
        // Unregister all commands to avoid memory leaks
        UnregisterConsoleCommands();
        LimboConsole.DisconnectToggled(new Callable(this, MethodName.OnConsoleToggled));
    }
}
