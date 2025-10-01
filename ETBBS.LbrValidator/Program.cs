using System.Diagnostics;

namespace ETBBS.LbrValidator;

/// <summary>
/// Command-line tool to validate LBR (role definition) files.
/// </summary>
class Program
{
    static int Main(string[] args)
    {
        // Parse command line arguments
        var options = ParseArguments(args);
        if (options == null)
        {
            ShowUsage();
            return 1;
        }

        var loc = new Localizer(options.Language);

        if (!options.Json)
        {
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine($"  {loc.Title}");
            Console.WriteLine("═══════════════════════════════════════════════════════");
            Console.WriteLine();
        }

        // Check if it's a single file or directory
        if (File.Exists(options.Directory))
        {
            // Validate single file
            var result = ValidateFile(options.Directory, options);
            if (options.Json)
            {
                var payload = JsonPrinter.Single(result);
                Console.WriteLine(payload);
                return result.Success ? 0 : 1;
            }
            else
            {
                Console.WriteLine();
                if (result.Success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ {loc.FilePassed}");
                    Console.ResetColor();
                    if (options.ShowDetails)
                    {
                        Console.WriteLine($"\n{loc.RoleLabel}: {result.RoleName} (id: {result.RoleId})");
                        Console.WriteLine($"{loc.SkillsLabel}: {result.SkillCount}");
                        if (result.Warnings.Count > 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            foreach (var w in result.Warnings) Console.WriteLine($"• {w}");
                            Console.ResetColor();
                        }
                    }
                    return 0;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ {loc.FileFailed}");
                    Console.WriteLine($"\n{loc.ErrorPrefix}: {result.ErrorMessage}");
                    if (result.Warnings.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        foreach (var w in result.Warnings) Console.WriteLine($"• {w}");
                        Console.ResetColor();
                    }
                    Console.ResetColor();
                    return 1;
                }
            }
        }

        // Validate directory
        if (!Directory.Exists(options.Directory))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ {loc.ErrorPrefix}: {loc.PathNotFound(options.Directory)}");
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
            if (options.Json)
            {
                Console.WriteLine(JsonPrinter.Empty(options.Directory));
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠ {loc.NoFilesFound(options.Directory)}");
                Console.ResetColor();
                return 0;
            }
        }

        if (!options.Json)
        {
            Console.WriteLine(loc.FoundFiles(lbrFiles.Length, options.Recursive));
            Console.WriteLine();
        }

        // Validate each file
        var results = new List<ValidationResult>();
        var stopwatch = Stopwatch.StartNew();

        foreach (var file in lbrFiles)
        {
            var result = ValidateFile(file, options);
            results.Add(result);
        }

        // Duplicate role id detection across successfully parsed files
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in results.Where(r => r.Success && !string.IsNullOrWhiteSpace(r.RoleId)))
        {
            if (r.RoleId is null) continue;
            if (!map.TryGetValue(r.RoleId, out var firstPath))
            {
                map[r.RoleId] = r.FilePath;
            }
            else
            {
                r.Success = false;
                r.ErrorMessage = $"Duplicate role id '{r.RoleId}' also defined in '{Path.GetFileName(firstPath)}'";
            }
        }

        stopwatch.Stop();

        // Emit results
        if (options.Json)
        {
            Console.WriteLine(JsonPrinter.Batch(results, stopwatch.Elapsed));
        }
        else
        {
            Console.WriteLine();
            PrintSummary(results, stopwatch.Elapsed, loc);
        }

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
                if (!options.Json)
                    Console.Write($"Validating: {GetRelativePath(filePath, options.Directory)} ... ");
            }

            // Try to load the role
            var role = LbrLoader.LoadFromFile(filePath);

            result.Success = true;
            result.RoleName = role.Name;
            result.RoleId = role.Id;
            result.SkillCount = role.Skills.Length;

            // Semantic checks (warnings)
            if (string.IsNullOrWhiteSpace(role.Name))
                result.Warnings.Add("Empty role name");
            if (string.IsNullOrWhiteSpace(role.Id))
                result.Warnings.Add("Empty role id");
            var dupSkills = role.Skills
                .GroupBy(s => s.Name, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();
            if (dupSkills.Length > 0)
                result.Warnings.Add($"Duplicate skill names: {string.Join(", ", dupSkills)}");

            // DSL static analysis warnings per skill
            foreach (var s in role.Skills)
            {
                var warns = TextDsl.AnalyzeText(s.Script);
                foreach (var w in warns)
                    result.Warnings.Add($"Skill '{s.Name}': {w}");
            }

            if (options.Verbose)
            {
                if (!options.Json)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ OK");
                    Console.ResetColor();
                }

                if (options.ShowDetails)
                {
                    if (!options.Json)
                    {
                        Console.WriteLine($"  Role: {role.Name} (id: {role.Id})");
                        Console.WriteLine($"  Skills: {role.Skills.Length}");
                        if (role.Tags.Count > 0)
                            Console.WriteLine($"  Tags: {string.Join(", ", role.Tags)}");
                        if (result.Warnings.Count > 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            foreach (var w in result.Warnings) Console.WriteLine($"  ! {w}");
                            Console.ResetColor();
                        }
                    }
                }
            }
            else
            {
                if (!options.Json)
                    Console.Write(".");
            }
        }
        catch (FormatException ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;

            if (options.Verbose)
            {
                if (!options.Json)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ FAILED");
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"  Error: {ex.Message}");
                    Console.ResetColor();
                }
            }
            else
            {
                if (!options.Json)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("X");
                    Console.ResetColor();
                }
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Unexpected error: {ex.Message}";

            if (options.Verbose)
            {
                if (!options.Json)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ FAILED");
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"  Error: {ex.GetType().Name}: {ex.Message}");
                    Console.ResetColor();
                }
            }
            else
            {
                if (!options.Json)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("X");
                    Console.ResetColor();
                }
            }
        }

        return result;
    }

    private static void PrintSummary(List<ValidationResult> results, TimeSpan elapsed, Localizer loc)
    {
        Console.WriteLine();
        Console.WriteLine("───────────────────────────────────────────────────────");
        Console.WriteLine($"  {loc.Summary}");
        Console.WriteLine("───────────────────────────────────────────────────────");
        Console.WriteLine();

        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        Console.WriteLine($"{loc.TotalFilesLabel}:    {results.Count}");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"{loc.PassedLabel}:         {successCount}");
        Console.ResetColor();

        if (failureCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{loc.FailedLabel}:         {failureCount}");
            Console.ResetColor();
        }

        Console.WriteLine($"{loc.TimeElapsedLabel}:   {elapsed.TotalSeconds:0.00}s");
        Console.WriteLine();

        // Show failed files
        if (failureCount > 0)
        {
            Console.WriteLine(loc.FailedFilesHeader + ":");
            Console.WriteLine();

            foreach (var result in results.Where(r => !r.Success))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ {result.FileName}");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"  {result.ErrorMessage}");
                if (result.Warnings.Count > 0)
                {
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    foreach (var w in result.Warnings) Console.WriteLine($"  ! {w}");
                }
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        // Overall result
        Console.WriteLine();
        if (successCount == results.Count)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ {loc.AllPassed}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ {loc.ValidationFailed(failureCount)}");
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
            else if (arg == "--json")
            {
                options.Json = true;
            }
            else if (arg.StartsWith("--lang="))
            {
                var lang = arg.Substring("--lang=".Length);
                options.Language = (lang.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) || lang.Equals("zh", StringComparison.OrdinalIgnoreCase)) ? "zh-CN" : "en";
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
        Console.WriteLine("      --json             Output results as JSON only");
        Console.WriteLine("      --lang=<en|zh-CN>  Localize messages (default: en)");
        Console.WriteLine("  -h, --help             Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ETBBS.LbrValidator");
        Console.WriteLine("  ETBBS.LbrValidator roles");
        Console.WriteLine("  ETBBS.LbrValidator roles -r -v");
        Console.WriteLine("  ETBBS.LbrValidator D:\\path\\to\\roles --details");
        Console.WriteLine("  ETBBS.LbrValidator roles --json");
        Console.WriteLine("  ETBBS.LbrValidator roles --lang=zh-CN");
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
    public bool Json { get; set; }
    public string Language { get; set; } = "en";
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
    public List<string> Warnings { get; } = new();
}

static class JsonPrinter
{
    public static string Single(ValidationResult r)
    {
        var payload = new
        {
            file = r.FileName,
            path = r.FilePath,
            success = r.Success,
            error = r.ErrorMessage,
            role = r.RoleName,
            id = r.RoleId,
            skills = r.SkillCount,
            warnings = r.Warnings.ToArray()
        };
        return System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    public static string Batch(List<ValidationResult> results, TimeSpan elapsed)
    {
        var payload = new
        {
            summary = new
            {
                total = results.Count,
                passed = results.Count(r => r.Success),
                failed = results.Count(r => !r.Success),
                seconds = elapsed.TotalSeconds
            },
            results = results.Select(r => new
            {
                file = r.FileName,
                path = r.FilePath,
                success = r.Success,
                error = r.ErrorMessage,
                role = r.RoleName,
                id = r.RoleId,
                skills = r.SkillCount,
                warnings = r.Warnings.ToArray()
            }).ToArray()
        };
        return System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    public static string Empty(string dir)
    {
        var payload = new { summary = new { total = 0, passed = 0, failed = 0, seconds = 0.0 }, results = Array.Empty<object>(), directory = dir };
        return System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }
}

sealed class Localizer
{
    private readonly string _lang;
    public Localizer(string lang) { _lang = lang; }
    public string Title => _lang.StartsWith("zh") ? "ETBBS LBR 验证器 - 角色文件语法检查器" : "ETBBS LBR Validator - Role File Syntax Checker";
    public string FilePassed => _lang.StartsWith("zh") ? "文件验证通过" : "FILE PASSED VALIDATION";
    public string FileFailed => _lang.StartsWith("zh") ? "文件验证失败" : "FILE FAILED VALIDATION";
    public string ErrorPrefix => _lang.StartsWith("zh") ? "错误" : "Error";
    public string Summary => _lang.StartsWith("zh") ? "验证汇总" : "VALIDATION SUMMARY";
    public string TotalFilesLabel => _lang.StartsWith("zh") ? "总文件数" : "Total files";
    public string PassedLabel => _lang.StartsWith("zh") ? "通过" : "Passed";
    public string FailedLabel => _lang.StartsWith("zh") ? "失败" : "Failed";
    public string TimeElapsedLabel => _lang.StartsWith("zh") ? "耗时" : "Time elapsed";
    public string FailedFilesHeader => _lang.StartsWith("zh") ? "失败文件" : "FAILED FILES";
    public string AllPassed => _lang.StartsWith("zh") ? "所有文件均通过验证" : "ALL FILES PASSED VALIDATION";
    public string RoleLabel => _lang.StartsWith("zh") ? "角色" : "Role";
    public string SkillsLabel => _lang.StartsWith("zh") ? "技能数" : "Skills";
    public string FoundFiles(int n, bool recursive)
        => _lang.StartsWith("zh")
           ? $"找到 {n} 个 .lbr 文件待验证{(recursive ? "（递归）" : "")}"
           : $"Found {n} .lbr file(s) to validate{(recursive ? " (recursive)" : "")}";
    public string NoFilesFound(string dir)
        => _lang.StartsWith("zh") ? $"未在 {dir} 中找到 .lbr 文件" : $"No .lbr files found in {dir}";
    public string PathNotFound(string path)
        => _lang.StartsWith("zh") ? $"路径不存在: {path}" : $"Path not found: {path}";
    public string ValidationFailed(int fail)
        => _lang.StartsWith("zh") ? $"验证失败（{fail} 个文件存在错误）" : $"VALIDATION FAILED ({fail} file(s) with errors)";
}
