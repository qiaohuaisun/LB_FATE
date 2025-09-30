using System;

namespace ETBBS;

/// <summary>
/// Base exception for all ETBBS-related errors.
/// </summary>
public class ETBBSException : Exception
{
    public ETBBSException() { }
    public ETBBSException(string message) : base(message) { }
    public ETBBSException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when skill validation fails (e.g., insufficient MP, out of range, on cooldown).
/// </summary>
public class SkillValidationException : ETBBSException
{
    public string? SkillName { get; }
    public string? Reason { get; }

    public SkillValidationException(string skillName, string reason)
        : base($"Skill '{skillName}' validation failed: {reason}")
    {
        SkillName = skillName;
        Reason = reason;
    }
}

/// <summary>
/// Thrown when DSL parsing fails.
/// </summary>
public class DslParseException : ETBBSException
{
    public int? Line { get; }
    public int? Column { get; }

    public DslParseException(string message) : base(message) { }

    public DslParseException(string message, int line, int column)
        : base($"Parse error at line {line}, column {column}: {message}")
    {
        Line = line;
        Column = column;
    }
}

/// <summary>
/// Thrown when attempting to load a role definition fails.
/// </summary>
public class RoleLoadException : ETBBSException
{
    public string? RoleId { get; }

    public RoleLoadException(string roleId, string message)
        : base($"Failed to load role '{roleId}': {message}")
    {
        RoleId = roleId;
    }

    public RoleLoadException(string roleId, string message, Exception inner)
        : base($"Failed to load role '{roleId}': {message}", inner)
    {
        RoleId = roleId;
    }
}

/// <summary>
/// Thrown when the world state becomes corrupted or inconsistent.
/// </summary>
public class StateCorruptionException : ETBBSException
{
    public StateCorruptionException(string message) : base(message) { }
    public StateCorruptionException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when network operations fail.
/// </summary>
public class NetworkException : ETBBSException
{
    public NetworkException(string message) : base(message) { }
    public NetworkException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when an action cannot be executed.
/// </summary>
public class ActionExecutionException : ETBBSException
{
    public AtomicAction? Action { get; }

    public ActionExecutionException(string message) : base(message) { }

    public ActionExecutionException(AtomicAction action, string message)
        : base($"Failed to execute {action.GetType().Name}: {message}")
    {
        Action = action;
    }

    public ActionExecutionException(AtomicAction action, string message, Exception inner)
        : base($"Failed to execute {action.GetType().Name}: {message}", inner)
    {
        Action = action;
    }
}