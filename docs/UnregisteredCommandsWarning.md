# LIMBO1003: RegisterConsoleCommands Not Called Warning

## Overview

The code generator will emit a warning (LIMBO1003) when a class contains methods decorated with `[ConsoleCommand]` attribute but does not call the generated `RegisterConsoleCommands()` method.

## Diagnostic Details

- **ID**: LIMBO1003
- **Severity**: Warning
- **Title**: RegisterConsoleCommands not called
- **Message**: Class '{ClassName}' has [ConsoleCommand] attributes but RegisterConsoleCommands() is not called in any method. Commands will not be registered unless you call this.RegisterConsoleCommands() (typically in _Ready or a similar initialization method).

## Why This Matters

When you use the `[ConsoleCommand]` attribute on methods, the source generator creates a `RegisterConsoleCommands()` method that handles the actual registration of these commands with the Limbo console. However, this generated method won't execute automatically - you must call it explicitly, typically in your initialization code (like `_Ready()` in Godot).

If you forget to call `RegisterConsoleCommands()`, your commands will not be available in the console, even though they're properly decorated with attributes.

## Example - Warning Case

```csharp
public partial class MyGameController : Node2D
{
    public override void _Ready()
    {
        // WARNING: LIMBO1003 will be generated here
        // RegisterConsoleCommands() is NOT called!
    }

    [ConsoleCommand("spawn", "Spawns an enemy")]
    public void SpawnEnemy()
    {
        // This command won't be available in the console
    }
}
```

## Example - Correct Usage

```csharp
public partial class MyGameController : Node2D
{
    public override void _Ready()
    {
        // Correctly calling the generated method
        RegisterConsoleCommands();

        // Now the commands are registered and available
    }

    [ConsoleCommand("spawn", "Spawns an enemy")]
    public void SpawnEnemy()
    {
        // This command will be available in the console
    }
}
```

## How to Fix

1. Find the class that's generating the warning
2. Locate your initialization method (typically `_Ready()` for Godot nodes)
3. Add a call to `this.RegisterConsoleCommands()`
4. If you want to unregister commands when the object is destroyed, call `this.UnregisterConsoleCommands()` in `_ExitTree()` or similar cleanup methods

## Suppressing the Warning

If you have a specific reason for not calling `RegisterConsoleCommands()` immediately (e.g., you're calling it conditionally or at a different time), you can suppress the warning:

```csharp
#pragma warning disable LIMBO1003
public partial class MyGameController : Node2D
{
#pragma warning restore LIMBO1003
    // Your code here
}
```

Or in your `.csproj` file:

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);LIMBO1003</NoWarn>
</PropertyGroup>
```

## Related Diagnostics

- **LIMBO1000**: Must be decorated with [ConsoleCommand]
- **LIMBO1002**: Invalid AutoComplete Attribute Usage
