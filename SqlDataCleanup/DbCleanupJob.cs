using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace SqlDataCleanup;

public class TableInfo
{
    [Column("TABLE_NAME")] public string TableName { get; set; } = default!;
    [Column("TABLE_SCHEMA")] public string Schema { get; set; } = default!;
}

public class DbCleanupJob(string name, string connectionString, DateTime beforeDate, DbConfig config)
{
    private DbContext CreateDbContext() => new(new DbContextOptionsBuilder()
        .UseSqlServer(connectionString, op => op.EnableRetryOnFailure()).Options);


    private async Task<IEnumerable<TableInfo>> GetTablesAsync()
    {
        await using var ctx = CreateDbContext();
        var tables = await ctx.Database
            .SqlQuery<TableInfo>(
                $"SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA <> 'sys' and TABLE_TYPE= 'BASE TABLE'")
            .AsNoTracking().ToListAsync();

        return tables.Where(s =>
            !config.ExcludeTables.Contains(s.TableName, StringComparer.InvariantCultureIgnoreCase));
    }

    private string BuildQuery(string table)
    {
        var fields = string.Join(" AND ", config.ConditionFields.Select(f => $"{f} < @beforeDate"));
        return $"""
                WITH CTE AS (
                    SELECT TOP (1000) * 
                    FROM {table}
                    WHERE {fields}
                )
                DELETE FROM {table} WHERE {config.PrimaryField} IN (SELECT {config.PrimaryField} FROM CTE)
                """;
    }

    private async Task DeleteRecordsAsync(string table)
    {
        Console.WriteLine($"Deleting table {table} before {beforeDate}...");

        var count = 0;
        var hasMoreRows = true;
        while (hasMoreRows)
        {
            await using var ctx = CreateDbContext();
            var rowsAffected =
                await ctx.Database.ExecuteSqlRawAsync(BuildQuery(table),
                    new SqlParameter("@beforeDate", beforeDate));

            hasMoreRows = rowsAffected > 0;
            count += rowsAffected;

            //if (hasMoreRows) await Task.Delay(TimeSpan.FromSeconds(3));
        }

        Console.WriteLine($"Deleted {count} records from {table}.");
    }

    public async Task RunAsync()
    {
        Console.WriteLine($"Running {name} cleanup job...");

        if (config.ConditionFields.Length == 0)
            throw new ArgumentNullException(nameof(config.ConditionFields));

        var tables = await GetTablesAsync();
        foreach (var table in tables)
        {
            try
            {
                await DeleteRecordsAsync($"[{table.Schema}].[{table.TableName}]");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\tFailed to delete table {table.TableName}: {ex.Message}");
            }
        }

        Console.WriteLine($"Finished {name} running cleanup job.");
    }
}