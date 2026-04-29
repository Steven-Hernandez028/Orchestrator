using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Api;
using Orchestrator.Infrastructure.Data;

namespace Orchestrator.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all DbContext related services
            var dbContextDescriptors = services.Where(x =>
                x.ServiceType == typeof(DbContextOptions<OrchestratorDbContext>) ||
                (x.ServiceType.IsGenericType &&
                 x.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>) &&
                 x.ServiceType.GetGenericArguments()[0] == typeof(OrchestratorDbContext)))
                .ToList();

            foreach (var descriptor in dbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            // Add fresh DbContext with in-memory database
            services.AddDbContext<OrchestratorDbContext>(options =>
            {
                options.UseInMemoryDatabase($"InMemoryTestDb_{Guid.NewGuid():N}");
            });
        });

        builder.UseEnvironment("Testing");
    }
}
