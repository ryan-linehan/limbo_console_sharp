#!/usr/bin/env python3
"""
Extract public API signatures from limbo_console.gd

This script parses the GDScript file and extracts:
- Public functions (between '# *** PUBLIC INTERFACE' and '# *** PRIVATE')
- Signals
- Public properties (variables without _ prefix)

Output: JSON file with API signatures for comparison
"""

import re
import json
import sys
from pathlib import Path
from typing import Optional


def parse_function_signature(line: str) -> Optional[dict]:
    """Parse a GDScript function definition line."""
    # Match: func name(args) -> return_type:
    # or: func name(args):
    pattern = r'^func\s+(\w+)\s*\(([^)]*)\)\s*(?:->\s*(\w+))?\s*:'
    match = re.match(pattern, line.strip())
    if not match:
        return None

    name = match.group(1)
    args_str = match.group(2).strip()
    return_type = match.group(3) or "void"

    # Parse arguments
    args = []
    if args_str:
        # Split by comma, but handle default values with commas in them
        arg_parts = []
        depth = 0
        current = ""
        for char in args_str:
            if char in '([{':
                depth += 1
            elif char in ')]}':
                depth -= 1
            elif char == ',' and depth == 0:
                arg_parts.append(current.strip())
                current = ""
                continue
            current += char
        if current.strip():
            arg_parts.append(current.strip())

        for arg in arg_parts:
            arg = arg.strip()
            if not arg:
                continue

            # Parse: name: Type = default or name = default or name: Type or name
            arg_info = {"name": "", "type": "Variant", "has_default": False}

            # Check for default value
            if '=' in arg:
                arg_info["has_default"] = True
                arg = arg.split('=')[0].strip()

            # Check for type annotation
            if ':' in arg:
                parts = arg.split(':')
                arg_info["name"] = parts[0].strip()
                arg_info["type"] = parts[1].strip()
            else:
                arg_info["name"] = arg.strip()

            args.append(arg_info)

    return {
        "name": name,
        "args": args,
        "return_type": return_type
    }


def parse_signal(line: str) -> Optional[dict]:
    """Parse a GDScript signal definition."""
    # Match: signal name(args) or signal name
    pattern = r'^signal\s+(\w+)(?:\s*\(([^)]*)\))?'
    match = re.match(pattern, line.strip())
    if not match:
        return None

    name = match.group(1)
    args_str = match.group(2) or ""

    args = []
    if args_str.strip():
        for arg in args_str.split(','):
            arg = arg.strip()
            if arg:
                args.append(arg)

    return {
        "name": name,
        "args": args
    }


def parse_property(line: str) -> Optional[dict]:
    """Parse a GDScript public property (var without _ prefix)."""
    # Match: var name: Type = value or var name = value or var name: Type
    pattern = r'^var\s+(\w+)\s*(?::\s*(\w+))?\s*(?:=|:)'
    match = re.match(pattern, line.strip())
    if not match:
        return None

    name = match.group(1)
    # Skip private variables (starting with _)
    if name.startswith('_'):
        return None

    var_type = match.group(2) or "Variant"

    return {
        "name": name,
        "type": var_type
    }


def extract_api(gd_file_path: str) -> dict:
    """Extract the public API from a GDScript file."""
    with open(gd_file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    lines = content.split('\n')

    api = {
        "source_file": str(Path(gd_file_path).name),
        "signals": [],
        "properties": [],
        "public_functions": []
    }

    in_public_section = False
    in_function = False

    for i, line in enumerate(lines):
        stripped = line.strip()

        # Detect if we're entering/leaving a function (indentation-based)
        # Class-level declarations start at column 0 (no leading whitespace)
        is_class_level = line and not line[0].isspace() and stripped

        # Track PUBLIC INTERFACE section
        if '# *** PUBLIC INTERFACE' in line:
            in_public_section = True
            continue
        if '# *** PRIVATE' in line:
            in_public_section = False
            continue

        # Parse signals (always at class level)
        if is_class_level and stripped.startswith('signal '):
            signal_info = parse_signal(stripped)
            if signal_info:
                api["signals"].append(signal_info)
            continue

        # Parse public properties at class level only (no indentation, not private)
        # Must be before PUBLIC INTERFACE section and at class level
        if is_class_level and not in_public_section and stripped.startswith('var ') and not stripped.startswith('var _'):
            prop_info = parse_property(stripped)
            if prop_info:
                api["properties"].append(prop_info)
            continue

        # Parse public functions (in PUBLIC INTERFACE section, at class level)
        if is_class_level and in_public_section and stripped.startswith('func '):
            func_info = parse_function_signature(stripped)
            if func_info:
                api["public_functions"].append(func_info)

    return api


def compare_apis(baseline: dict, current: dict) -> dict:
    """Compare two API snapshots and return differences."""
    changes = {
        "added_functions": [],
        "removed_functions": [],
        "modified_functions": [],
        "added_signals": [],
        "removed_signals": [],
        "modified_signals": [],
        "added_properties": [],
        "removed_properties": [],
        "modified_properties": []
    }

    # Compare functions
    baseline_funcs = {f["name"]: f for f in baseline.get("public_functions", [])}
    current_funcs = {f["name"]: f for f in current.get("public_functions", [])}

    for name in current_funcs:
        if name not in baseline_funcs:
            changes["added_functions"].append(current_funcs[name])
        elif current_funcs[name] != baseline_funcs[name]:
            changes["modified_functions"].append({
                "name": name,
                "old": baseline_funcs[name],
                "new": current_funcs[name]
            })

    for name in baseline_funcs:
        if name not in current_funcs:
            changes["removed_functions"].append(baseline_funcs[name])

    # Compare signals
    baseline_signals = {s["name"]: s for s in baseline.get("signals", [])}
    current_signals = {s["name"]: s for s in current.get("signals", [])}

    for name in current_signals:
        if name not in baseline_signals:
            changes["added_signals"].append(current_signals[name])
        elif current_signals[name] != baseline_signals[name]:
            changes["modified_signals"].append({
                "name": name,
                "old": baseline_signals[name],
                "new": current_signals[name]
            })

    for name in baseline_signals:
        if name not in current_signals:
            changes["removed_signals"].append(baseline_signals[name])

    # Compare properties
    baseline_props = {p["name"]: p for p in baseline.get("properties", [])}
    current_props = {p["name"]: p for p in current.get("properties", [])}

    for name in current_props:
        if name not in baseline_props:
            changes["added_properties"].append(current_props[name])
        elif current_props[name] != baseline_props[name]:
            changes["modified_properties"].append({
                "name": name,
                "old": baseline_props[name],
                "new": current_props[name]
            })

    for name in baseline_props:
        if name not in current_props:
            changes["removed_properties"].append(baseline_props[name])

    return changes


def has_changes(changes: dict) -> bool:
    """Check if there are any API changes."""
    return any(len(v) > 0 for v in changes.values())


def format_changes_markdown(changes: dict) -> str:
    """Format API changes as markdown for GitHub issues."""
    lines = ["## API Changes Detected\n"]

    if changes["added_functions"]:
        lines.append("### Added Functions")
        for f in changes["added_functions"]:
            args = ", ".join(f"{a['name']}: {a['type']}" for a in f["args"])
            lines.append(f"- `{f['name']}({args}) -> {f['return_type']}`")
        lines.append("")

    if changes["removed_functions"]:
        lines.append("### Removed Functions")
        for f in changes["removed_functions"]:
            args = ", ".join(f"{a['name']}: {a['type']}" for a in f["args"])
            lines.append(f"- `{f['name']}({args}) -> {f['return_type']}`")
        lines.append("")

    if changes["modified_functions"]:
        lines.append("### Modified Functions")
        for m in changes["modified_functions"]:
            old_args = ", ".join(f"{a['name']}: {a['type']}" for a in m["old"]["args"])
            new_args = ", ".join(f"{a['name']}: {a['type']}" for a in m["new"]["args"])
            lines.append(f"- `{m['name']}`")
            lines.append(f"  - Old: `({old_args}) -> {m['old']['return_type']}`")
            lines.append(f"  - New: `({new_args}) -> {m['new']['return_type']}`")
        lines.append("")

    if changes["added_signals"]:
        lines.append("### Added Signals")
        for s in changes["added_signals"]:
            args = ", ".join(s["args"]) if s["args"] else ""
            lines.append(f"- `signal {s['name']}({args})`")
        lines.append("")

    if changes["removed_signals"]:
        lines.append("### Removed Signals")
        for s in changes["removed_signals"]:
            args = ", ".join(s["args"]) if s["args"] else ""
            lines.append(f"- `signal {s['name']}({args})`")
        lines.append("")

    if changes["added_properties"]:
        lines.append("### Added Properties")
        for p in changes["added_properties"]:
            lines.append(f"- `var {p['name']}: {p['type']}`")
        lines.append("")

    if changes["removed_properties"]:
        lines.append("### Removed Properties")
        for p in changes["removed_properties"]:
            lines.append(f"- `var {p['name']}: {p['type']}`")
        lines.append("")

    return "\n".join(lines)


def main():
    import argparse

    parser = argparse.ArgumentParser(description="Extract and compare limbo_console API signatures")
    parser.add_argument("command", choices=["extract", "compare"], help="Command to run")
    parser.add_argument("--input", "-i", help="Input GDScript file path")
    parser.add_argument("--output", "-o", help="Output JSON file path")
    parser.add_argument("--baseline", "-b", help="Baseline JSON file for comparison")
    parser.add_argument("--format", "-f", choices=["json", "markdown"], default="json", help="Output format for compare")

    args = parser.parse_args()

    if args.command == "extract":
        if not args.input:
            print("Error: --input is required for extract command", file=sys.stderr)
            sys.exit(1)

        api = extract_api(args.input)
        output = json.dumps(api, indent=2)

        if args.output:
            with open(args.output, 'w', encoding='utf-8') as f:
                f.write(output)
            print(f"API extracted to {args.output}")
        else:
            print(output)

    elif args.command == "compare":
        if not args.input or not args.baseline:
            print("Error: --input and --baseline are required for compare command", file=sys.stderr)
            sys.exit(1)

        with open(args.baseline, 'r', encoding='utf-8') as f:
            baseline = json.load(f)

        with open(args.input, 'r', encoding='utf-8') as f:
            current = json.load(f)

        changes = compare_apis(baseline, current)

        if args.format == "markdown":
            if has_changes(changes):
                print(format_changes_markdown(changes))
                sys.exit(1)  # Exit with error to indicate changes detected
            else:
                print("No API changes detected.")
                sys.exit(0)
        else:
            print(json.dumps(changes, indent=2))
            sys.exit(1 if has_changes(changes) else 0)


if __name__ == "__main__":
    main()
