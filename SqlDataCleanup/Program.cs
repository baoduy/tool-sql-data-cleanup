// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.DependencyInjection;
using SqlDataCleanup;

await using var provider = Config.GetDi();
var job = provider.GetRequiredService<SqlCleanupJob>();
await job.RunAsync();