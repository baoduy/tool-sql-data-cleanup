using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SqlDataCleanup;

public abstract class SharedConfig
{
    public string? PrimaryField { get; set; }
    public string[] ConditionFields { get; set; } = Enumerable.Empty<string>().ToArray();

    [Required, Range(30, int.MaxValue)] public int? OlderThanDays { get; set; }
}

public sealed class TableConfig : SharedConfig
{
}

public sealed class DbConfig : SharedConfig
{
    public Dictionary<string, TableConfig> Tables { get; set; } = new();
}

public sealed class SqlConfig : SharedConfig
{
    public static string Name = "DbCleanup";
    
    [Required(AllowEmptyStrings = false)] public string ConnectionString { get; set; } = default!;
    
    [Required] public Dictionary<string, DbConfig> Databases { get; set; } = new();
}

public static class Config
{
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

    public static SqlConfig GetDbCleanupConfig(this IServiceProvider provider)
        => provider.GetRequiredService<IOptions<SqlConfig>>().Value;
}