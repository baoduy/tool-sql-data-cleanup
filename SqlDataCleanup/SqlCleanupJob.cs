namespace SqlDataCleanup;

public class SqlCleanupJob(SqlConfig sqlConfig)
{
    

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