using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace SqlDataCleanup;

/// <summary>
/// Represents a table dependency with foreign key and primary key tables.
/// </summary>
class TableDepend
{
    /// <summary>
    /// Gets or sets the foreign key table name.
    /// </summary>
    [Column("FK_Table")] public string FkTable { get; set; } = default!;

    /// <summary>
    /// Gets or sets the primary key table name.
    /// </summary>
    [Column("PK_Table")] public string PkTable { get; set; } = default!;
}

/// <summary>
/// Represents information about a database table.
/// </summary>
public class TableInfo
{
    private readonly HashSet<TableInfo> _listPkTables = new();
    private int _weight = 0;

    /// <summary>
    /// Gets or sets the table name.
    /// </summary>
    [Column("TABLE_NAME")] public string TableName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the schema of the table.
    /// </summary>
    [Column("TABLE_SCHEMA")] public string Schema { get; set; } = default!;

    /// <summary>
    /// Gets the weight of the table, used for sorting based on dependencies.
    /// </summary>
    public int Weaight => _weight;

    /// <summary>
    /// Increases the weight of the table and recursively increases the weight of dependent tables.
    /// </summary>
    private void InCreaseWeight()
    {
        _weight += 1;
        foreach (var table in _listPkTables)
            table.InCreaseWeight();
    }

    /// <summary>
    /// Adds a primary key table to the list of dependent tables and increases its weight.
    /// </summary>
    /// <param name="table">The primary key table to add.</param>
    public void AddPkTable(TableInfo table)
    {
        if (_listPkTables.Add(table))
            table.InCreaseWeight();
    }
}

/// <summary>
/// Represents a job to clean up the database by deleting old records.
/// </summary>
/// <param name="name">The name of the cleanup job.</param>
/// <param name="connectionString">The connection string to the database.</param>
/// <param name="dbConfig">The database configuration.</param>
public class DbCleanupJob
{
    private readonly string name;
    private readonly string connectionString;
    private readonly DbConfig dbConfig;

    public DbCleanupJob(string name, string connectionString, DbConfig dbConfig)
    {
        this.name = name;
        this.connectionString = connectionString;
        this.dbConfig = dbConfig;
    }

    /// <summary>
    /// Creates a new instance of the database context.
    /// </summary>
    /// <returns>A new <see cref="DbContext"/> instance.</returns>
    private DbContext CreateDbContext()
    {
        var db = new DbContext(new DbContextOptionsBuilder().UseSqlServer(connectionString).Options);
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        return db;
    }

    /// <summary>
    /// Asynchronously retrieves the list of tables from the database.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of tables.</returns>
    private async Task<IEnumerable<TableInfo>> GetTablesAsync()
    {
        Console.WriteLine($"{name}: Reading all tables...");

        await using var db = CreateDbContext();
        var tables = await db.Database
            .SqlQuery<TableInfo>(
                $"SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA <> 'sys' and TABLE_TYPE= 'BASE TABLE'")
            .AsNoTracking().ToListAsync();

        // Only take the whitelist tables
        return tables.Where(s =>
            dbConfig.Tables.Keys.Contains(s.TableName, StringComparer.InvariantCultureIgnoreCase));
    }

    /// <summary>
    /// Asynchronously sorts the tables based on their dependencies.
    /// </summary>
    /// <param name="tables">The list of tables to sort.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the sorted list of tables.</returns>
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

            // These tables may be in the excluded list
            if (pkTable is null || fkTable is null) continue;
            fkTable.AddPkTable(pkTable);
        }

        return tables.OrderBy(t => t.Weaight);
    }

    /// <summary>
    /// Builds the SQL delete query for a specific table based on the table configuration.
    /// </summary>
    /// <param name="table">The name of the table.</param>
    /// <param name="tbConfig">The table configuration.</param>
    /// <returns>The SQL delete query string.</returns>
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

    /// <summary>
    /// Asynchronously deletes old records from a specific table based on the table configuration.
    /// </summary>
    /// <param name="table">The name of the table.</param>
    /// <param name="tbConfig">The table configuration.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Asynchronously runs the database cleanup job.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
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