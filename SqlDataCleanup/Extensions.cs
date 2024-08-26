namespace SqlDataCleanup;

public static class Extensions
{
    public static T PreparingConfig<T>(this T me, SharedConfig from) where T : SharedConfig
    {
        me.OlderThanDays ??= from.OlderThanDays;
        me.PrimaryField ??= from.PrimaryField;
        me.ConditionFields = me.ConditionFields.Any()
            ? me.ConditionFields
            : from.ConditionFields;

        return me;
    }
}