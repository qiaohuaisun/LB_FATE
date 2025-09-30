using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ETBBS;

/// <summary>
/// Provides logging functionality for the ETBBS engine.
/// Uses Microsoft.Extensions.Logging abstractions for flexible logger integration.
/// </summary>
public static class ETBBSLog
{
    private static ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    /// <summary>
    /// Configures the logger factory for the entire ETBBS engine.
    /// Must be called before using any ETBBS functionality to enable logging.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use. Pass null to disable logging.</param>
    public static void Configure(ILoggerFactory? loggerFactory)
    {
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    /// <summary>
    /// Creates a logger for the specified category.
    /// </summary>
    /// <typeparam name="T">The type whose name is used as the category.</typeparam>
    /// <returns>An ILogger instance for the specified category.</returns>
    public static ILogger<T> CreateLogger<T>() => _loggerFactory.CreateLogger<T>();

    /// <summary>
    /// Creates a logger for the specified category name.
    /// </summary>
    /// <param name="categoryName">The category name for messages produced by the logger.</param>
    /// <returns>An ILogger instance for the specified category.</returns>
    public static ILogger CreateLogger(string categoryName) => _loggerFactory.CreateLogger(categoryName);
}

/// <summary>
/// Provides structured logging for action execution with timing information.
/// </summary>
public sealed class ActionExecutionLogger
{
    private readonly ILogger _logger;
    private readonly string _actionName;
    private readonly DateTime _startTime;

    private ActionExecutionLogger(ILogger logger, string actionName)
    {
        _logger = logger;
        _actionName = actionName;
        _startTime = DateTime.UtcNow;

        _logger.LogDebug("Executing action: {ActionName}", _actionName);
    }

    /// <summary>
    /// Starts timing an action execution.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="actionName">The name of the action being executed.</param>
    /// <returns>An ActionExecutionLogger instance. Call Complete() when done.</returns>
    public static ActionExecutionLogger Start(ILogger logger, string actionName)
        => new(logger, actionName);

    /// <summary>
    /// Logs successful completion with timing information.
    /// </summary>
    public void Complete()
    {
        var elapsed = DateTime.UtcNow - _startTime;
        _logger.LogDebug("Action completed: {ActionName} in {ElapsedMs}ms", _actionName, elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Logs failure with exception and timing information.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    public void Fail(Exception exception)
    {
        var elapsed = DateTime.UtcNow - _startTime;
        _logger.LogError(exception, "Action failed: {ActionName} after {ElapsedMs}ms", _actionName, elapsed.TotalMilliseconds);
    }
}

/// <summary>
/// Provides structured logging for skill execution.
/// </summary>
public sealed class SkillExecutionLogger
{
    private readonly ILogger _logger;
    private readonly string _skillName;
    private readonly string _casterId;
    private readonly DateTime _startTime;

    private SkillExecutionLogger(ILogger logger, string skillName, string casterId)
    {
        _logger = logger;
        _skillName = skillName;
        _casterId = casterId;
        _startTime = DateTime.UtcNow;

        _logger.LogInformation("Skill execution started: {SkillName} by {CasterId}", _skillName, _casterId);
    }

    /// <summary>
    /// Starts timing a skill execution.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <param name="skillName">The name of the skill being executed.</param>
    /// <param name="casterId">The ID of the unit casting the skill.</param>
    /// <returns>A SkillExecutionLogger instance. Call Complete() when done.</returns>
    public static SkillExecutionLogger Start(ILogger logger, string skillName, string casterId)
        => new(logger, skillName, casterId);

    /// <summary>
    /// Logs successful completion with timing and result information.
    /// </summary>
    /// <param name="actionsExecuted">Number of atomic actions executed.</param>
    /// <param name="unitsAffected">Number of units affected.</param>
    public void Complete(int actionsExecuted, int unitsAffected)
    {
        var elapsed = DateTime.UtcNow - _startTime;
        _logger.LogInformation(
            "Skill execution completed: {SkillName} by {CasterId} in {ElapsedMs}ms - {ActionsExecuted} actions, {UnitsAffected} units affected",
            _skillName, _casterId, elapsed.TotalMilliseconds, actionsExecuted, unitsAffected);
    }

    /// <summary>
    /// Logs skill validation failure.
    /// </summary>
    /// <param name="reason">The reason for validation failure.</param>
    public void ValidationFailed(string reason)
    {
        _logger.LogWarning("Skill validation failed: {SkillName} by {CasterId} - {Reason}", _skillName, _casterId, reason);
    }

    /// <summary>
    /// Logs skill execution failure.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    public void Fail(Exception exception)
    {
        var elapsed = DateTime.UtcNow - _startTime;
        _logger.LogError(exception, "Skill execution failed: {SkillName} by {CasterId} after {ElapsedMs}ms",
            _skillName, _casterId, elapsed.TotalMilliseconds);
    }
}