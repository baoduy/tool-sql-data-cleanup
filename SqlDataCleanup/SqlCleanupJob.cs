using Microsoft.EntityFrameworkCore;

namespace SqlDataCleanup;

public class SqlCleanupJob(DbCleanup config)
{
    private DbConfig PreparingConfig(DbConfig dbConfig)
    {
        dbConfig.ExcludeTables = config.ExcludeTables.Union(dbConfig.ExcludeTables).ToArray();
        dbConfig.OlderThanDays ??= config.OlderThanDays;
        dbConfig.ConditionFields = dbConfig.ConditionFields.Any()
            ? dbConfig.ConditionFields
            : config.ConditionFields;

        return dbConfig;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("Running SQL Cleanup Job");

        var beforeDate = DateTime.Today.AddDays(config.OlderThanDays * -1);
        foreach (var dbConfig in config.Databases)
        {
            var conn = config.ConnectionString.Replace("[DbName]", dbConfig.Key);
            var db = PreparingConfig(dbConfig.Value);

            await using var dbCleanup = new DbCleanupJob(dbConfig.Key, conn, beforeDate, db);
            await dbCleanup.RunAsync();
        }

        Console.WriteLine("Finished SQL Cleanup Job");
    }
}