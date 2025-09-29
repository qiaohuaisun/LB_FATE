using System;
using System.IO;
using System.Linq;
using ETBBS;
using Xunit;

public class RolesSyntaxTests
{
    private static string? FindRepoRolesDir()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var probe = Path.Combine(dir, "roles");
            if (Directory.Exists(probe)) return probe;
            var parent = Directory.GetParent(dir)?.FullName;
            if (string.IsNullOrEmpty(parent)) break;
            dir = parent;
        }
        return null;
    }

    [Fact]
    public void All_Role_Files_Parse_Without_Error()
    {
        var rolesDir = FindRepoRolesDir();
        if (rolesDir is null) return; // skip if not running from repo tree

        var failures = new System.Collections.Generic.List<string>();
        foreach (var file in Directory.EnumerateFiles(rolesDir, "*.lbr", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var text = File.ReadAllText(file);
                var role = LbrLoader.Load(text);
                Assert.False(string.IsNullOrWhiteSpace(role.Id));
                // just touch skills to ensure compiled
                _ = role.Skills.Length;
            }
            catch (Exception ex)
            {
                // Try to locate failing skill by naive extraction
                try
                {
                    var text = File.ReadAllText(file);
                    var skillsStart = text.IndexOf("skills");
                    if (skillsStart >= 0)
                    {
                        var blkStart = text.IndexOf('{', skillsStart);
                        var depth = 1; int i = blkStart + 1;
                        while (i < text.Length && depth > 0)
                        {
                            // find next 'skill "Name" { ... }'
                            var si = text.IndexOf("skill", i, StringComparison.Ordinal);
                            if (si < 0) break;
                            var q1 = text.IndexOf('"', si);
                            var q2 = q1 >= 0 ? text.IndexOf('"', q1 + 1) : -1;
                            var name = (q1 >= 0 && q2 > q1) ? text.Substring(q1 + 1, q2 - q1 - 1) : "<unknown>";
                            var sb = text.IndexOf('{', q2);
                            if (sb < 0) break;
                            var d = 1; int j = sb + 1;
                            while (j < text.Length && d > 0)
                            {
                                if (text[j] == '{') d++; else if (text[j] == '}') d--;
                                j++;
                            }
                            var body = text.Substring(sb + 1, (j - 1) - (sb + 1));
                            try { _ = TextDsl.FromTextUsingGlobals(name, body); }
                            catch (Exception ex2)
                            {
                                var snippet = body.Length > 120 ? body.Substring(0, 120) + "..." : body;
                                failures.Add($"{file} -> skill '{name}': {ex2.Message} | body: {snippet}");
                            }
                            i = j;
                        }
                    }
                    else
                    {
                        failures.Add($"{file}: {ex.Message}");
                    }
                }
                catch
                {
                    failures.Add($"{file}: {ex.Message}");
                }
            }
        }
        if (failures.Count > 0)
            throw new Exception("Role parse failures:\n" + string.Join("\n", failures));
    }
}
