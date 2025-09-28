using System.Collections.Immutable;

namespace ETBBS;

public sealed class RoleRegistry
{
    private readonly Dictionary<string, RoleDefinition> _byId = new(StringComparer.Ordinal);

    public RoleRegistry LoadDirectory(string directory, bool recursive = false)
    {
        var option = recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly;
        foreach (var path in System.IO.Directory.EnumerateFiles(directory, "*.lbr", option))
        {
            try
            {
                var role = LbrLoader.LoadFromFile(path);
                if (string.IsNullOrWhiteSpace(role.Id))
                    throw new FormatException($"Role at '{path}' has empty id.");
                _byId[role.Id] = role;
            }
            catch (FormatException ex)
            {
                throw new FormatException($"Failed to parse role file '{path}': {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new FormatException($"Failed to load role file '{path}': {ex.Message}", ex);
            }
        }
        return this;
    }

    public void Clear() => _byId.Clear();

    public void AddOrUpdate(RoleDefinition role)
    {
        if (string.IsNullOrWhiteSpace(role.Id))
            throw new ArgumentException("Role id must not be empty", nameof(role));
        _byId[role.Id] = role;
    }

    public bool TryGet(string id, out RoleDefinition role) => _byId.TryGetValue(id, out role!);

    public RoleDefinition Get(string id)
        => _byId.TryGetValue(id, out var role) ? role : throw new KeyNotFoundException($"Role '{id}' not found");

    public IEnumerable<RoleDefinition> All() => _byId.Values;

    public IEnumerable<string> Ids() => _byId.Keys;

    public IEnumerable<(string Id, string Skill)> AllSkills()
        => _byId.Values.SelectMany(r => r.Skills.Select(s => (r.Id, s.Name)));
}
