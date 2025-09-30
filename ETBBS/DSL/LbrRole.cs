using System.Collections.Immutable;
using System.Text;

namespace ETBBS;

/// <summary>
/// Immutable definition of a character role/class loaded from an LBR file.
/// Contains initial stats, tags, and skill definitions.
/// </summary>
public sealed class RoleDefinition
{
    /// <summary>Unique role identifier (e.g., "warrior", "mage").</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Display name for the role.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional description text.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Initial unit variables (e.g., hp, mp, atk).</summary>
    public ImmutableDictionary<string, object> Vars { get; init; } = ImmutableDictionary<string, object>.Empty;

    /// <summary>Initial tags for units of this role.</summary>
    public ImmutableHashSet<string> Tags { get; init; } = ImmutableHashSet<string>.Empty;

    /// <summary>Skills defined for this role.</summary>
    public ImmutableArray<RoleSkill> Skills { get; init; } = ImmutableArray<RoleSkill>.Empty;
}

/// <summary>
/// Represents a single skill within a role definition.
/// </summary>
public sealed class RoleSkill
{
    /// <summary>Skill name/identifier.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Original DSL script text.</summary>
    public string Script { get; init; } = string.Empty;

    /// <summary>Pre-compiled skill ready for execution.</summary>
    public Skill Compiled { get; init; } = null!;
}

/// <summary>
/// Loads role definitions from .lbr files using the LBR format.
/// LBR Format:
/// <code>
/// role "Warrior" id "warrior" {
///   description "A mighty melee fighter"
///   vars { "hp" = 100 "atk" = 15 }
///   tags { "melee" "tank" }
///   skills {
///     skill "Slash" { damage self 10; }
///   }
/// }
/// </code>
/// </summary>
public static class LbrLoader
{
    /// <summary>
    /// Loads a role definition from a file path.
    /// </summary>
    /// <exception cref="FormatException">Thrown if parsing fails.</exception>
    public static RoleDefinition LoadFromFile(string path)
        => Load(System.IO.File.ReadAllText(path));

    /// <summary>
    /// Loads all .lbr files from a directory.
    /// </summary>
    public static IEnumerable<RoleDefinition> LoadFromDirectory(string dir)
        => System.IO.Directory.EnumerateFiles(dir, "*.lbr").Select(LoadFromFile);

    /// <summary>
    /// Parses a role definition from LBR format text.
    /// </summary>
    /// <exception cref="FormatException">Thrown if parsing fails.</exception>
    public static RoleDefinition Load(string text)
    {
        var p = new RoleParser(text);
        return p.Parse();
    }
}

internal sealed class RoleParser
{
    private readonly string _src;
    private int _pos;

    public RoleParser(string src) { _src = src; _pos = 0; }

    public RoleDefinition Parse()
    {
        SkipWs();
        Require("role");
        var name = ParseString();
        Require("id");
        var id = ParseString();
        var vars = new Dictionary<string, object>();
        var tags = new HashSet<string>();
        var skills = new List<RoleSkill>();
        string description = string.Empty;
        SkipWs(); Expect('{'); SkipWs();
        while (!Eof() && Peek() != '}')
        {
            if (Try("description") || Try("desc"))
            {
                var d = ParseString(); description = d; SkipOptSep();
                continue;
            }
            if (Try("vars"))
            {
                Expect('{'); SkipWs();
                while (!Eof() && Peek() != '}')
                {
                    var key = ParseString(); Expect('='); var val = ParseValue();
                    vars[key] = val; SkipOptSep();
                }
                Expect('}'); SkipWs();
                continue;
            }
            if (Try("tags"))
            {
                Expect('{'); SkipWs();
                while (!Eof() && Peek() != '}')
                {
                    var tag = ParseString(); tags.Add(tag); SkipOptSep();
                }
                Expect('}'); SkipWs();
                continue;
            }
            if (Try("skills"))
            {
                Expect('{'); SkipWs();
                while (!Eof() && Peek() != '}')
                {
                    Require("skill"); var sname = ParseString();
                    var body = ParseBlockText(); // capture inner skill DSL
                    var compiled = TextDsl.FromTextUsingGlobals(sname, body);
                    skills.Add(new RoleSkill { Name = sname, Script = body, Compiled = compiled });
                    SkipOptSep();
                }
                Expect('}'); SkipWs();
                continue;
            }
            throw Error("unknown section in role");
        }
        Expect('}');
        return new RoleDefinition
        {
            Id = id,
            Name = name,
            Description = description,
            Vars = vars.ToImmutableDictionary(),
            Tags = tags.ToImmutableHashSet(),
            Skills = skills.ToImmutableArray()
        };
    }

    private string ParseBlockText()
    {
        SkipWs(); Expect('{');
        var start = _pos; int depth = 1;
        while (!Eof() && depth > 0)
        {
            var c = Next();
            if (c == '"')
            {
                // skip string
                while (!Eof()) { var d = Next(); if (d == '"') break; if (d == '\\' && !Eof()) Next(); }
            }
            else if (c == '#')
            {
                // skip to end of line for hash comments
                while (!Eof() && Peek() != '\n') _pos++;
            }
            else if (c == '/' && !Eof())
            {
                // support // line comments and /* block comments */ inside blocks
                var n = Peek();
                if (n == '/') { while (!Eof() && Peek() != '\n') _pos++; }
                else if (n == '*')
                {
                    // consume '*'
                    _pos++;
                    while (!Eof())
                    {
                        if (Peek() == '*')
                        {
                            _pos++;
                            if (!Eof() && Peek() == '/') { _pos++; break; }
                        }
                        else
                        {
                            _pos++;
                        }
                    }
                }
            }
            else if (c == '{') depth++;
            else if (c == '}') depth--;
        }
        var end = _pos - 1; // position at the '}'
        var inner = _src.Substring(start, end - start);
        return inner.Trim();
    }

    private void SkipOptSep()
    {
        SkipWs(); if (!Eof() && (Peek() == ';' || Peek() == ',')) { _pos++; }
        SkipWs();
    }

    private object ParseValue()
    {
        SkipWs();
        if (Peek() == '"') return ParseString();
        // number or bool
        if (Try("true")) return true;
        if (Try("false")) return false;
        return ParseNumber();
    }

    private double ParseNumber()
    {
        SkipWs(); int start = _pos; bool seenDot = false;
        if (!Eof() && (Peek() == '-' || Peek() == '+')) _pos++;
        if (Eof() || !char.IsDigit(Peek())) throw Error("number expected");
        while (!Eof() && char.IsDigit(Peek())) _pos++;
        if (!Eof() && Peek() == '.') { seenDot = true; _pos++; while (!Eof() && char.IsDigit(Peek())) _pos++; }
        var s = _src.Substring(start, _pos - start);
        if (!double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
            throw Error("invalid number");
        if (!seenDot && d == Math.Truncate(d)) return (int)d; // return int where possible
        return d;
    }

    private string ParseString()
    {
        SkipWs(); Expect('"'); var sb = new StringBuilder();
        while (!Eof())
        {
            var c = Next();
            if (c == '"') break;
            if (c == '\\' && !Eof())
            {
                var n = Next();
                sb.Append(n switch { '\\' => '\\', '"' => '"', 'n' => '\n', 't' => '\t', _ => n });
            }
            else sb.Append(c);
        }
        // Check if we've reached the end of file without finding a closing quote
        if (Eof())
        {
            throw Error("unclosed string literal");
        }
        return sb.ToString();
    }

    private void SkipWs()
    {
        while (!Eof())
        {
            if (char.IsWhiteSpace(Peek())) { _pos++; continue; }
            // comments: '# ...', '// ...', and '/* ... */'
            if (Peek() == '#') { while (!Eof() && Peek() != '\n') _pos++; continue; }
            if (Peek() == '/' && _pos + 1 < _src.Length)
            {
                var n = _src[_pos + 1];
                if (n == '/') { _pos += 2; while (!Eof() && Peek() != '\n') _pos++; continue; }
                if (n == '*')
                {
                    _pos += 2;
                    while (!Eof())
                    {
                        if (Peek() == '*')
                        {
                            _pos++;
                            if (!Eof() && Peek() == '/') { _pos++; break; }
                        }
                        else { _pos++; }
                    }
                    continue;
                }
            }
            break;
        }
    }

    private bool Try(string kw)
    {
        SkipWs(); var save = _pos;
        if (TryConsume(kw))
        {
            if (Eof() || !char.IsLetterOrDigit(Peek())) return true;
        }
        _pos = save; return false;
    }

    private void Require(string kw)
    {
        if (!Try(kw)) throw Error($"keyword '{kw}' expected");
    }

    private bool TryConsume(string text)
    {
        SkipWs();
        if (_pos + text.Length > _src.Length) return false;
        for (int i = 0; i < text.Length; i++)
        {
            if (char.ToLowerInvariant(_src[_pos + i]) != char.ToLowerInvariant(text[i])) return false;
        }
        _pos += text.Length; return true;
    }

    private void Expect(char ch)
    {
        SkipWs(); if (Eof() || Peek() != ch) throw Error($"'{ch}' expected"); _pos++;
    }
    private char Peek() => _src[_pos];
    private char Next() => _src[_pos++];
    private bool Eof() => _pos >= _src.Length;
    private Exception Error(string msg) => new FormatException($"LBR parse error at {_pos}: {msg}");
}

/// <summary>
/// Factory for creating units from role definitions.
/// </summary>
public static class UnitFactory
{
    /// <summary>
    /// Adds a new unit to the world state with initial stats/tags from a role.
    /// </summary>
    /// <param name="s">Current world state.</param>
    /// <param name="id">Unique unit ID.</param>
    /// <param name="role">Role definition to instantiate.</param>
    /// <returns>Updated world state with the new unit.</returns>
    public static WorldState AddUnit(WorldState s, string id, RoleDefinition role)
    {
        return WorldStateOps.WithUnit(s, id, u => new UnitState(role.Vars, role.Tags));
    }
}
