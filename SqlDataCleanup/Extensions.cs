namespace SqlDataCleanup;

/// <summary>
/// Provides extension methods for configuration objects.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Prepares the configuration by copying values from a shared configuration if they are not already set.
    /// </summary>
    /// <typeparam name="T">The type of the configuration object, which must inherit from <see cref="SharedConfig"/>.</typeparam>
    /// <param name="me">The configuration object to prepare.</param>
    /// <param name="from">The shared configuration object to copy values from.</param>
    /// <returns>The prepared configuration object.</returns>
    public static T PreparingConfig<T>(this T me, SharedConfig from) where T : SharedConfig
    {
        // Set OlderThanDays if not already set
        me.OlderThanDays ??= from.OlderThanDays;

        // Set PrimaryField if not already set
        me.PrimaryField ??= from.PrimaryField;

        // Set ConditionFields if not already set or empty
        me.ConditionFields = me.ConditionFields.Any()
            ? me.ConditionFields
            : from.ConditionFields;

        return me;
    }
}