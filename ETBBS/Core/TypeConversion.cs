using System.Collections.Immutable;

namespace ETBBS;

/// <summary>
/// Utility class for converting between object types commonly used in game state variables.
/// Centralizes type conversion logic to eliminate duplication and ensure consistency.
/// </summary>
public static class TypeConversion
{
    /// <summary>
    /// Converts an object to an integer, handling common numeric types.
    /// </summary>
    /// <param name="value">The value to convert. Can be int, long, double, float, or null.</param>
    /// <param name="defaultValue">Value to return if conversion fails or value is null.</param>
    /// <returns>The converted integer value.</returns>
    public static int ToInt(object? value, int defaultValue = 0) =>
        value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)Math.Round(d),
            float f => (int)Math.Round(f),
            _ => defaultValue
        };

    /// <summary>
    /// Converts an object to a double, handling common numeric types.
    /// </summary>
    /// <param name="value">The value to convert. Can be double, float, int, long, or null.</param>
    /// <param name="defaultValue">Value to return if conversion fails or value is null.</param>
    /// <returns>The converted double value.</returns>
    public static double ToDouble(object? value, double defaultValue = 0.0) =>
        value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            _ => defaultValue
        };

    /// <summary>
    /// Converts an object to a boolean.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="defaultValue">Value to return if conversion fails or value is null.</param>
    /// <returns>The converted boolean value.</returns>
    public static bool ToBool(object? value, bool defaultValue = false) =>
        value switch
        {
            bool b => b,
            int i => i != 0,
            _ => defaultValue
        };

    /// <summary>
    /// Extension method to safely get a unit variable as a specific type.
    /// </summary>
    /// <typeparam name="T">The target type (int or double).</typeparam>
    /// <param name="unit">The unit to get the variable from.</param>
    /// <param name="key">The variable key.</param>
    /// <param name="defaultValue">Value to return if variable doesn't exist.</param>
    /// <returns>The variable value converted to type T.</returns>
    public static T GetVarAs<T>(this UnitState unit, string key, T defaultValue) where T : struct
    {
        if (!unit.Vars.TryGetValue(key, out var value))
            return defaultValue;

        if (typeof(T) == typeof(int))
            return (T)(object)ToInt(value, (int)(object)defaultValue!);
        if (typeof(T) == typeof(double))
            return (T)(object)ToDouble(value, (double)(object)defaultValue!);
        if (typeof(T) == typeof(bool))
            return (T)(object)ToBool(value, (bool)(object)defaultValue!);

        return defaultValue;
    }

    /// <summary>
    /// Extension method to safely get a unit variable as an integer.
    /// </summary>
    public static int GetIntVar(this UnitState unit, string key, int defaultValue = 0)
    {
        if (!unit.Vars.TryGetValue(key, out var value))
            return defaultValue;
        return ToInt(value, defaultValue);
    }

    /// <summary>
    /// Extension method to safely get a unit variable as a double.
    /// </summary>
    public static double GetDoubleVar(this UnitState unit, string key, double defaultValue = 0.0)
    {
        if (!unit.Vars.TryGetValue(key, out var value))
            return defaultValue;
        return ToDouble(value, defaultValue);
    }

    /// <summary>
    /// Extension method to safely get a unit variable as a boolean.
    /// </summary>
    public static bool GetBoolVar(this UnitState unit, string key, bool defaultValue = false)
    {
        if (!unit.Vars.TryGetValue(key, out var value))
            return defaultValue;
        return ToBool(value, defaultValue);
    }

    /// <summary>
    /// Safely gets a value from a dictionary and converts it to an integer.
    /// </summary>
    public static int GetIntFrom(ImmutableDictionary<string, object> vars, string key, int defaultValue = 0)
    {
        if (!vars.TryGetValue(key, out var value))
            return defaultValue;
        return ToInt(value, defaultValue);
    }

    /// <summary>
    /// Safely gets a value from a dictionary and converts it to a double.
    /// </summary>
    public static double GetDoubleFrom(ImmutableDictionary<string, object> vars, string key, double defaultValue = 0.0)
    {
        if (!vars.TryGetValue(key, out var value))
            return defaultValue;
        return ToDouble(value, defaultValue);
    }

    /// <summary>
    /// Safely gets a value from a dictionary and converts it to a boolean.
    /// </summary>
    public static bool GetBoolFrom(ImmutableDictionary<string, object> vars, string key, bool defaultValue = false)
    {
        if (!vars.TryGetValue(key, out var value))
            return defaultValue;
        return ToBool(value, defaultValue);
    }
}