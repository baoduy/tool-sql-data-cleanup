namespace SqlDataCleanup;

/// <summary>
/// Represents a job to clean up multiple SQL databases based on the provided configuration.
/// </summary>
public class SqlCleanupJob
{
    private readonly SqlConfig sqlConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlCleanupJob"/> class.
    /// </summary>
    /// <param name="sqlConfig">The SQL configuration containing database connection details and cleanup settings.</param>
    public SqlCleanupJob(SqlConfig sqlConfig)
    {
        this.sqlConfig = sqlConfig;
    }

    /// <summary>
    /// Asynchronously runs the SQL cleanup job for all configured databases.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RunAsync()
    {
        Console.WriteLine(
            $"Running SQL Cleanup Job for dbs:\n {string.Join("\n\t", sqlConfig.Databases.Keys)}");

        foreach (var dbConfig in sqlConfig.Databases)
        {
            var conn = sqlConfig.ConnectionString.Replace("[DbName]", dbConfig.Key);
            var db = dbConfig.Value.PreparingConfig(sqlConfig);

            var dbCleanup = new DbCleanupJob(dbConfig.Key, conn, db);
            await dbCleanup.RunAsync();
        }

        Console.WriteLine("Finished SQL Cleanup Job");
    }
}