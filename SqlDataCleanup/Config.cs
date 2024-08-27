using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SqlDataCleanup;

/// <summary>
/// Base configuration class with shared properties.
/// </summary>
public abstract class SharedConfig
{
    /// <summary>
    /// Gets or sets the primary field name.
    /// </summary>
    public string? PrimaryField { get; set; }

    /// <summary>
    /// Gets or sets the array of condition fields.
    /// </summary>
    public string[] ConditionFields { get; set; } = Enumerable.Empty<string>().ToArray();

    /// <summary>
    /// Gets or sets the number of days to consider data as old.
    /// </summary>
    [Required, Range(30, int.MaxValue)] public int? OlderThanDays { get; set; }
}

/// <summary>
/// Configuration for a specific table.
/// </summary>
public sealed class TableConfig : SharedConfig
{
}

/// <summary>
/// Configuration for a specific database.
/// </summary>
public sealed class DbConfig : SharedConfig
{
    /// <summary>
    /// Gets or sets the dictionary of table configurations.
    /// </summary>
    public Dictionary<string, TableConfig> Tables { get; set; } = new();
}

/// <summary>
/// Main SQL configuration class.
/// </summary>
public sealed class SqlConfig : SharedConfig
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public static string Name = "DbCleanup";

    /// <summary>
    /// Gets or sets the connection string for the database.
    /// </summary>
    [Required(AllowEmptyStrings = false)] public string ConnectionString { get; set; } = default!;

    /// <summary>
    /// Gets or sets the dictionary of database configurations.
    /// </summary>
    [Required] public Dictionary<string, DbConfig> Databases { get; set; } = new();
}

/// <summary>
/// Static class to handle configuration and dependency injection.
/// </summary>
public static class Config
{
    /// <summary>
    /// Sets up the dependency injection container.
    /// </summary>
    /// <returns>A configured <see cref="ServiceProvider"/>.</returns>
    public static ServiceProvider GetDi()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection()
            //.AddLogging()
            .AddSingleton<IConfiguration>(config);

        services.AddOptions<SqlConfig>()
            .BindConfiguration(SqlConfig.Name);

        services.AddSingleton<SqlConfig>(sp => sp.GetDbCleanupConfig())
            .AddSingleton<SqlCleanupJob>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Extension method to get the SQL configuration from the service provider.
    /// </summary>
    /// <param name="provider">The service provider.</param>
    /// <returns>The <see cref="SqlConfig"/> instance.</returns>
    public static SqlConfig GetDbCleanupConfig(this IServiceProvider provider)
        => provider.GetRequiredService<IOptions<SqlConfig>>().Value;
}