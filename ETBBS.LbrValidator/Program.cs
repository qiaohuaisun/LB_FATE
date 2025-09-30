using ETBBS;
using System.Diagnostics;

namespace ETBBS.LbrValidator;

/// <summary>
/// Command-line tool to validate LBR (role definition) files.
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("  ETBBS LBR Validator - Role File Syntax Checker");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine();

        // Parse command line arguments
        var options = ParseArguments(args);
        if (options == null)
        {
            ShowUsage();
            return 1;
        }

        // Check if it's a single file or directory
        if (File.Exists(options.Directory))
        {
            // Validate single file
            var result = ValidateFile(options.Directory, options);
            Console.WriteLine();
            if (result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ FILE PASSED VALIDATION");
                Console.ResetColor();
                if (options.ShowDetails)
                {
                    Console.WriteLine($"\nRole: {result.RoleName} (id: {result.RoleId})");
                    Console.WriteLine($"Skills: {result.SkillCount}");
                }
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ FILE FAILED VALIDATION");
                Console.WriteLine($"\nError: {result.ErrorMessage}");
                Console.ResetColor();
                return 1;
            }
        }

        // Validate directory
        if (!Directory.Exists(options.Directory))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error: Path not found: {options.Directory}");
            Console.ResetColor();
            return 1;
        }

        // Find all .lbr files
        var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var lbrFiles = Directory.GetFiles(options.Directory, "*.lbr", searchOption)
            .OrderBy(f => f)
            .ToArray();

        if (lbrFiles.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ Warning: No .lbr files found in {options.Directory}");
            Console.ResetColor();
            return 0;
        }

        Console.WriteLine($"Found {lbrFiles.Length} .lbr file(s) to validate{(options.Recursive ? " (recursive)" : "")}");
        Console.WriteLine();

        // Validate each file
        var results = new List<ValidationResult>();
        var stopwatch = Stopwatch.StartNew();

        foreach (var file in lbrFiles)
        {
            var result = ValidateFile(file, options);
            results.Add(result);
        }

        stopwatch.Stop();

        // Print summary
        Console.WriteLine();
        PrintSummary(results, stopwatch.Elapsed);

        // Return exit code
        var hasErrors = results.Any(r => !r.Success);
        return hasErrors ? 1 : 0;
    }

    private static ValidationResult ValidateFile(string filePath, ValidationOptions options)
    {
        var result = new ValidationResult
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            if (options.Verbose)
            {
                Console.Write($"Validating: {GetRelativePath(filePath, options.Directory)} ... ");
            }

            // Try to load the role
            var role = LbrLoader.LoadFromFile(filePath);

            result.Success = true;
            result.RoleName = role.Name;
            result.RoleId = role.Id;
            result.SkillCount = role.Skills.Length;

            if (options.Verbose)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ OK");
                Console.ResetColor();

                if (options.ShowDetails)
                {
                    Console.WriteLine($"  Role: {role.Name} (id: {role.Id})");
                    Console.WriteLine($"  Skills: {role.Skills.Length}");
                    if (role.Tags.Count > 0)
                        Console.WriteLine($"  Tags: {string.Join(", ", role.Tags)}");
                }
            }
            else
            {
                Console.Write(".");
            }
        }
        catch (FormatException ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;

            if (options.Verbose)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ FAILED");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"  Error: {ex.Message}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("X");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Unexpected error: {ex.Message}";

            if (options.Verbose)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ FAILED");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"  Error: {ex.GetType().Name}: {ex.Message}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("X");
                Console.ResetColor();
            }
        }

        return result;
    }

    private static void PrintSummary(List<ValidationResult> results, TimeSpan elapsed)
    {
        Console.WriteLine();
        Console.WriteLine("───────────────────────────────────────────────────────");
        Console.WriteLine("  VALIDATION SUMMARY");
        Console.WriteLine("───────────────────────────────────────────────────────");
        Console.WriteLine();

        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        Console.WriteLine($"Total files:    {results.Count}");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Passed:         {successCount}");
        Console.ResetColor();

        if (failureCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed:         {failureCount}");
            Console.ResetColor();
        }

        Console.WriteLine($"Time elapsed:   {elapsed.TotalSeconds:0.00}s");
        Console.WriteLine();

        // Show failed files
        if (failureCount > 0)
        {
            Console.WriteLine("FAILED FILES:");
            Console.WriteLine();

            foreach (var result in results.Where(r => !r.Success))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ {result.FileName}");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"  {result.ErrorMessage}");
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        // Overall result
        Console.WriteLine();
        if (successCount == results.Count)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ ALL FILES PASSED VALIDATION");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ VALIDATION FAILED ({failureCount} file(s) with errors)");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    private static ValidationOptions? ParseArguments(string[] args)
    {
        var options = new ValidationOptions();

        if (args.Length == 0)
        {
            // Use current directory by default
            options.Directory = Directory.GetCurrentDirectory();
            return options;
        }

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--help" || arg == "-h")
            {
                return null;
            }
            else if (arg == "--recursive" || arg == "-r")
            {
                options.Recursive = true;
            }
            else if (arg == "--verbose" || arg == "-v")
            {
                options.Verbose = true;
            }
            else if (arg == "--details" || arg == "-d")
            {
                options.ShowDetails = true;
                options.Verbose = true; // Details implies verbose
            }
            else if (arg == "--quiet" || arg == "-q")
            {
                options.Verbose = false;
            }
            else if (!arg.StartsWith("-"))
            {
                // Assume it's the directory path
                options.Directory = arg;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Unknown option: {arg}");
                Console.ResetColor();
                return null;
            }
        }

        return options;
    }

    private static void ShowUsage()
    {
        Console.WriteLine("Usage: ETBBS.LbrValidator [path] [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  path                   File or directory to validate (default: current directory)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -r, --recursive        Scan subdirectories recursively");
        Console.WriteLine("  -v, --verbose          Show detailed progress for each file");
        Console.WriteLine("  -d, --details          Show role details (implies --verbose)");
        Console.WriteLine("  -q, --quiet            Minimal output (only summary)");
        Console.WriteLine("  -h, --help             Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ETBBS.LbrValidator");
        Console.WriteLine("  ETBBS.LbrValidator roles");
        Console.WriteLine("  ETBBS.LbrValidator roles -r -v");
        Console.WriteLine("  ETBBS.LbrValidator D:\\path\\to\\roles --details");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0 - All files passed validation");
        Console.WriteLine("  1 - One or more files failed validation or error occurred");
        Console.WriteLine();
    }

    private static string GetRelativePath(string fullPath, string basePath)
    {
        try
        {
            var baseUri = new Uri(Path.GetFullPath(basePath) + Path.DirectorySeparatorChar);
            var fullUri = new Uri(Path.GetFullPath(fullPath));
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
        catch
        {
            return Path.GetFileName(fullPath);
        }
    }
}

class ValidationOptions
{
    public string Directory { get; set; } = ".";
    public bool Recursive { get; set; }
    public bool Verbose { get; set; }
    public bool ShowDetails { get; set; }
}

class ValidationResult
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public bool Success { get; set; }
    public string? RoleName { get; set; }
    public string? RoleId { get; set; }
    public int SkillCount { get; set; }
    public string? ErrorMessage { get; set; }
}
