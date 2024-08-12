using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SqlDataCleanup;

public class DbConfig
{
    public string? PrimaryField { get; set; }
    public string[] ExcludeTables { get; set; } = Enumerable.Empty<string>().ToArray();
    public string[] ConditionFields { get; set; } = Enumerable.Empty<string>().ToArray();
    public int? OlderThanDays { get; set; }
}

public class DbCleanup
{
    public static string Name = "DbCleanup";

    [Required,Range(180,int.MaxValue)]
    public int OlderThanDays { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = default!;

    [Required(AllowEmptyStrings = false)]
    public string PrimaryField { get; set; } = "Id";

    public string[] ExcludeTables { get; set; } = Enumerable.Empty<string>().ToArray();
    public string[] ConditionFields { get; set; } = Enumerable.Empty<string>().ToArray();

    [Required]
    public Dictionary<string, DbConfig> Databases { get; set; } = new();
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

        services.AddOptions<DbCleanup>()
            .BindConfiguration(DbCleanup.Name);

        services.AddSingleton<DbCleanup>(sp=>sp.GetDbCleanupConfig())
            .AddSingleton<SqlCleanupJob>();

        return services.BuildServiceProvider();
    }

    public static DbCleanup GetDbCleanupConfig(this IServiceProvider provider)
        => provider.GetRequiredService<IOptions<DbCleanup>>().Value;
}