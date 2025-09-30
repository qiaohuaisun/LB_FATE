# ETBBS LBR Validator

A command-line tool for validating LBR (role definition) files syntax in the ETBBS game system.

## Overview

The LBR Validator scans directories for `.lbr` files and validates their syntax using the ETBBS core parser. It provides detailed error reporting, statistics, and multiple verbosity levels to help catch syntax errors before runtime.

## Features

- **Recursive Scanning**: Scan subdirectories for `.lbr` files
- **Batch Validation**: Validate multiple files in one run
- **Detailed Reporting**: Show role details including name, ID, skills, and tags
- **Colored Output**: Visual indicators (✓/✗) for success/failure
- **Statistics**: Summary with pass/fail counts and execution time
- **Exit Codes**: CI/CD friendly return codes
- **Multiple Verbosity Levels**: Quiet, normal, verbose, and detailed modes

## Installation

Build the validator tool:

```bash
dotnet build ETBBS.LbrValidator
```

Or publish as a standalone executable:

```bash
dotnet publish ETBBS.LbrValidator -c Release -o publish/validator
```

## Usage

### Basic Syntax

```bash
ETBBS.LbrValidator [directory] [options]
```

### Arguments

- `directory` - Directory to scan for .lbr files (default: current directory)

### Options

- `-r, --recursive` - Scan subdirectories recursively
- `-v, --verbose` - Show detailed progress for each file
- `-d, --details` - Show role details (implies --verbose)
- `-q, --quiet` - Minimal output (only summary)
- `-h, --help` - Show help message

### Examples

**Validate current directory:**
```bash
ETBBS.LbrValidator
```

**Validate specific directory:**
```bash
ETBBS.LbrValidator roles
```

**Recursive scan with verbose output:**
```bash
ETBBS.LbrValidator roles -r -v
```

**Show detailed role information:**
```bash
ETBBS.LbrValidator D:\path\to\roles --details
```

**Quiet mode (summary only):**
```bash
ETBBS.LbrValidator roles -q
```

## Output Examples

### Success (Verbose Mode)

```
═══════════════════════════════════════════════════════
  ETBBS LBR Validator - Role File Syntax Checker
═══════════════════════════════════════════════════════

Found 9 .lbr file(s) to validate

Validating: artoria.lbr ... ✓ OK
Validating: beast_florence.lbr ... ✓ OK
Validating: enkidu.lbr ... ✓ OK
...

───────────────────────────────────────────────────────
  VALIDATION SUMMARY
───────────────────────────────────────────────────────

Total files:    9
Passed:         9
Time elapsed:   0.12s

✓ ALL FILES PASSED VALIDATION
```

### Failure (Verbose Mode)

```
Validating: broken_role.lbr ... ✗ FAILED
  Error: DSL parse error at 15: keyword 'do' expected

───────────────────────────────────────────────────────
  VALIDATION SUMMARY
───────────────────────────────────────────────────────

Total files:    10
Passed:         9
Failed:         1
Time elapsed:   0.15s

FAILED FILES:

✗ broken_role.lbr
  DSL parse error at 15: keyword 'do' expected

✗ VALIDATION FAILED (1 file(s) with errors)
```

### Details Mode

```
Validating: beast_florence.lbr ... ✓ OK
  Role: Beast IV Candidate – Florence (id: beast_florence)
  Skills: 9
  Tags: beast, grand
```

## Exit Codes

- `0` - All files passed validation
- `1` - One or more files failed validation or an error occurred

## Integration with CI/CD

The validator can be integrated into continuous integration pipelines:

### GitHub Actions Example

```yaml
- name: Validate LBR Files
  run: dotnet run --project ETBBS.LbrValidator -- roles -r -v
  continue-on-error: false
```

### Azure DevOps Example

```yaml
- script: dotnet run --project ETBBS.LbrValidator -- $(Build.SourcesDirectory)/roles -r
  displayName: 'Validate Role Files'
  failOnStderr: true
```

## Common Use Cases

### Pre-commit Hook

Validate all role files before committing:

```bash
#!/bin/bash
dotnet run --project ETBBS.LbrValidator -- roles -q
if [ $? -ne 0 ]; then
    echo "LBR validation failed. Please fix syntax errors before committing."
    exit 1
fi
```

### Batch Validation During Development

Quickly check all roles in multiple directories:

```bash
for dir in roles roles_custom roles_test; do
    echo "Validating $dir..."
    ETBBS.LbrValidator $dir -v
done
```

### Find Specific Errors

Use verbose mode to identify which files have issues:

```bash
ETBBS.LbrValidator roles -v 2>&1 | grep "FAILED"
```

## Common Errors

### Syntax Error: Missing 'do' keyword

```
Error: DSL parse error at 80: keyword 'do' expected
```

**Cause**: `for each` statement missing required clauses (e.g., `of caster`, `in range`)

**Fix**: Ensure complete syntax:
```lbr
for each enemies of caster in range 4 of caster do { ... }
```

### File Not Found

```
Error: Directory not found: /path/to/roles
```

**Cause**: Specified directory doesn't exist

**Fix**: Check path and use absolute paths on Windows (e.g., `D:\path\to\roles`)

## Technical Details

- **Parser**: Uses `LbrLoader.LoadFromFile()` from ETBBS core library
- **File Pattern**: Matches `*.lbr` files only
- **Encoding**: UTF-8 with BOM support
- **Performance**: ~50-100ms per file on typical hardware

## Troubleshooting

**Q: Validator shows no files found**

A: Ensure you're in the correct directory and .lbr files exist. Use `-r` for recursive search.

**Q: Getting "cannot access" errors on Windows**

A: Run with elevated permissions or check file/directory access rights.

**Q: Colors not showing in output**

A: Some terminals don't support ANSI colors. Try a different terminal or use `-q` for plain text.

## See Also

- [LBR Syntax Guide](../docs/LBR_SYNTAX.md)
- [Role Creation Tutorial](../docs/ROLE_CREATION.md)
- [ETBBS Core Documentation](../ETBBS/README.md)