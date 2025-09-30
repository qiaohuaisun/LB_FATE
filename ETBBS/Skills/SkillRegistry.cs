namespace ETBBS;

/// <summary>
/// Central registry for all skills in the game.
/// Provides lookup by name and enumeration of all registered skills.
/// </summary>
public sealed class SkillRegistry
{
    private readonly Dictionary<string, Skill> _skills = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a skill (replaces any existing skill with the same name).
    /// </summary>
    /// <param name="skill">The skill to register.</param>
    public void Register(Skill skill) => _skills[skill.Metadata.Name] = skill;

    /// <summary>
    /// Attempts to retrieve a skill by name.
    /// </summary>
    /// <param name="name">The skill name.</param>
    /// <param name="skill">The retrieved skill (if found).</param>
    /// <returns>True if the skill exists, false otherwise.</returns>
    public bool TryGetSkill(string name, out Skill skill) => _skills.TryGetValue(name, out skill!);

    /// <summary>
    /// Gets skill metadata by name (returns null if not found).
    /// </summary>
    /// <param name="name">The skill name.</param>
    /// <returns>The skill's metadata, or null if not registered.</returns>
    public SkillMetadata? GetSkill(string name) => _skills.TryGetValue(name, out var s) ? s.Metadata : null;

    /// <summary>
    /// Enumerates metadata for all registered skills.
    /// </summary>
    /// <returns>A sequence of all skill metadata.</returns>
    public IEnumerable<SkillMetadata> All() => _skills.Values.Select(s => s.Metadata);
}

