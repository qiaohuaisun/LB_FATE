using System.Collections.Immutable;

namespace ETBBS;

public sealed class SkillRegistry
{
    private readonly Dictionary<string, Skill> _skills = new(StringComparer.Ordinal);

    public void Register(Skill skill) => _skills[skill.Metadata.Name] = skill;

    public bool TryGetSkill(string name, out Skill skill) => _skills.TryGetValue(name, out skill!);

    public SkillMetadata? GetSkill(string name) => _skills.TryGetValue(name, out var s) ? s.Metadata : null;

    public IEnumerable<SkillMetadata> All() => _skills.Values.Select(s => s.Metadata);
}
 
