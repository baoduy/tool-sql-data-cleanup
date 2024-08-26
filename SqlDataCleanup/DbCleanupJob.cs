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

public class DbCleanupJob(string name, string connectionString, DbConfig dbConfig)
{
    private DbContext CreateDbContext()
    {
        var db = new DbContext(new DbContextOptionsBuilder().UseSqlServer(connectionString).Options);
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        return db;
    }

    private async Task<IEnumerable<TableInfo>> GetTablesAsync()
    {
        Console.WriteLine($"{name}: Reading all tables...");

        await using var db = CreateDbContext();
        var tables = await db.Database
            .SqlQuery<TableInfo>(
                $"SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA <> 'sys' and TABLE_TYPE= 'BASE TABLE'")
            .AsNoTracking().ToListAsync();

        //Only take the whitelist tables
        return tables.Where(s =>
            dbConfig.Tables.Keys.Contains(s.TableName, StringComparer.InvariantCultureIgnoreCase));
    }

    private async Task<IEnumerable<TableInfo>> SortTableReferences(List<TableInfo> tables)
    {
        Console.WriteLine($"{name}: Ordering tables based on the dependencies...");

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

    private string BuildDeleteQuery(string table, TableConfig tbConfig)
    {
        var fields = string.Join(" AND ", tbConfig.ConditionFields.Select(f => $"{f} < @beforeDate"));
        return $"""
                WITH CTE AS (
                    SELECT TOP (1000) * 
                    FROM {table}
                    WHERE {fields}
                )
                DELETE FROM {table} WHERE {tbConfig.PrimaryField} IN (SELECT {tbConfig.PrimaryField} FROM CTE)
                """;
    }

    private async Task DeleteRecordsAsync(string table, TableConfig tbConfig)
    {
        var beforeDate = DateTime.Today.AddDays((tbConfig.OlderThanDays ?? 365) * -1);
        Console.WriteLine($"Deleting table {table} before {beforeDate}...");

        var count = 0;
        var hasMoreRows = true;
        while (hasMoreRows)
        {
            await using var db = CreateDbContext();
            var query = BuildDeleteQuery(table, tbConfig);
            var rowsAffected = await db.Database.ExecuteSqlRawAsync(query, new SqlParameter("@beforeDate", beforeDate));

            hasMoreRows = rowsAffected > 0;
            count += rowsAffected;
            
            Console.WriteLine($"\tDeleted 1000 records from {table}.");
        }

        Console.WriteLine($"Total Deleted {count} records from {table}.");
    }

    public async Task RunAsync()
    {
        Console.WriteLine($"Running {name} cleanup job...");

        var tables = await GetTablesAsync();
        var shortedTables = await SortTableReferences(tables.ToList());

        foreach (var table in shortedTables)
        {
            try
            {
                var tbConfig = dbConfig.Tables[table.TableName];
                tbConfig.PreparingConfig(dbConfig);
                await DeleteRecordsAsync($"[{table.Schema}].[{table.TableName}]", tbConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\tFailed to delete table {table.TableName}: {ex.Message}");
            }
        }

        Console.WriteLine($"Finished {name} running cleanup job.");
    }
}