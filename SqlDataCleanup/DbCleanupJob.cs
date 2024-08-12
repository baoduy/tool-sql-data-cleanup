using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace SqlDataCleanup;

class TableDepend
{
    [Column("FK_Table")] public string FkTable { get; set; } = default!;
    [Column("PK_Table")] public string PkTable { get; set; } = default!;
}

public class TableInfo
{
    private readonly HashSet<TableInfo> _listPkTables = [];
    private int _weight = 0;
    [Column("TABLE_NAME")] public string TableName { get; set; } = default!;
    [Column("TABLE_SCHEMA")] public string Schema { get; set; } = default!;

    public int Weaight => _weight;
    //public IEnumerable<TableInfo> PkTables => _listPkTables;

    private void InCreaseWeight()
    {
        _weight += 1;
        foreach (var table in _listPkTables)
            table.InCreaseWeight();
    }

    public void AddPkTable(TableInfo table)
    {
        if (_listPkTables.Add(table))
            table.InCreaseWeight();
    }
}

public class DbCleanupJob(string name, string connectionString, DateTime beforeDate, DbConfig config)
{
    private DbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder().UseSqlServer(connectionString).Options);

    private async Task<IEnumerable<TableInfo>> GetTablesAsync()
    {
        Console.WriteLine($"${name}: Reading all tables...");

        await using var db = CreateDbContext();
        var tables = await db.Database
            .SqlQuery<TableInfo>(
                $"SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA <> 'sys' and TABLE_TYPE= 'BASE TABLE'")
            .AsNoTracking().ToListAsync();

        return tables.Where(s =>
            !config.ExcludeTables.Contains(s.TableName, StringComparer.InvariantCultureIgnoreCase));
    }

    private async Task<IEnumerable<TableInfo>> SortTableReferences(List<TableInfo> tables)
    {
        Console.WriteLine($"${name}: Ordering tables based on the dependencies...");

        await using var db = CreateDbContext();
        var tablesDepends = await db.Database
            .SqlQuery<TableDepend>($@"
                SELECT DISTINCT
    FK_Table = FK.TABLE_NAME, 
    PK_Table = PK.TABLE_NAME
FROM 
    INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS C
    INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS FK
    ON C.CONSTRAINT_NAME = FK.CONSTRAINT_NAME
        INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS PK
    ON C.UNIQUE_CONSTRAINT_NAME = PK.CONSTRAINT_NAME")
            .AsNoTracking().ToListAsync();

        foreach (var tableDepend in tablesDepends)
        {
            var pkTable = tables.FirstOrDefault(t =>
                t.TableName.Equals(tableDepend.PkTable, StringComparison.CurrentCultureIgnoreCase));
            var fkTable = tables.FirstOrDefault(t =>
                t.TableName.Equals(tableDepend.FkTable, StringComparison.CurrentCultureIgnoreCase));

            //These tables may be in the excluded list
            if (pkTable is null || fkTable is null) continue;
            fkTable.AddPkTable(pkTable);
        }

        return tables.OrderBy(t => t.Weaight);
    }

    private string BuildDeleteQuery(string table)
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
            await using var db = CreateDbContext();
            var rowsAffected = await db.Database.ExecuteSqlRawAsync(BuildDeleteQuery(table),
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