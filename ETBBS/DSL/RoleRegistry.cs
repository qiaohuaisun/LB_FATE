namespace ETBBS;

/// <summary>
/// Central registry for all loaded role definitions.
/// Provides lookup by ID and batch loading from directories.
/// </summary>
public sealed class RoleRegistry
{
    private readonly Dictionary<string, RoleDefinition> _byId = new(StringComparer.Ordinal);

    /// <summary>
    /// Loads all .lbr files from a directory into this registry.
    /// </summary>
    /// <param name="directory">Directory path to search.</param>
    /// <param name="recursive">If true, search subdirectories recursively.</param>
    /// <returns>This registry (for fluent chaining).</returns>
    /// <exception cref="FormatException">Thrown if any file fails to parse.</exception>
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

    /// <summary>
    /// Removes all registered roles.
    /// </summary>
    public void Clear() => _byId.Clear();

    /// <summary>
    /// Adds or updates a role definition in the registry.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if role ID is empty.</exception>
    public void AddOrUpdate(RoleDefinition role)
    {
        if (string.IsNullOrWhiteSpace(role.Id))
            throw new ArgumentException("Role id must not be empty", nameof(role));
        _byId[role.Id] = role;
    }

    /// <summary>
    /// Attempts to retrieve a role by ID.
    /// </summary>
    /// <returns>True if the role exists, false otherwise.</returns>
    public bool TryGet(string id, out RoleDefinition role) => _byId.TryGetValue(id, out role!);

    /// <summary>
    /// Gets a role by ID.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown if the role doesn't exist.</exception>
    public RoleDefinition Get(string id)
        => _byId.TryGetValue(id, out var role) ? role : throw new KeyNotFoundException($"Role '{id}' not found");

    /// <summary>
    /// Enumerates all registered role definitions.
    /// </summary>
    public IEnumerable<RoleDefinition> All() => _byId.Values;

    /// <summary>
    /// Enumerates all registered role IDs.
    /// </summary>
    public IEnumerable<string> Ids() => _byId.Keys;

    /// <summary>
    /// Enumerates all (RoleId, SkillName) pairs across all roles.
    /// Useful for populating AI skill lists or generating documentation.
    /// </summary>
    public IEnumerable<(string Id, string Skill)> AllSkills()
        => _byId.Values.SelectMany(r => r.Skills.Select(s => (r.Id, s.Name)));
}
