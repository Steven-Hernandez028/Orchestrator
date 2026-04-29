using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Core.Models;
using Orchestrator.Infrastructure.Data;
using Orchestrator.Infrastructure.Data.Repositories;
using Xunit;

namespace Orchestrator.UnitTests.Repositories;

public class ScriptRepositoryTests
{
    private OrchestratorDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrchestratorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrchestratorDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateScript()
    {
        using var context = CreateContext();
        var repo = new ScriptRepository(context);
        var script = new Script
        {
            Name = "Test Script",
            JsonDefinition = "{\"steps\":[]}"
        };

        var result = await repo.CreateAsync(script);

        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Test Script");
        result.Version.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnScript()
    {
        using var context = CreateContext();
        var repo = new ScriptRepository(context);
        var script = new Script { Name = "Test", JsonDefinition = "{}" };
        await repo.CreateAsync(script);

        var result = await repo.GetByIdAsync(script.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
    }

    [Fact]
    public async Task UpdateAsync_ShouldIncrementVersion()
    {
        using var context = CreateContext();
        var repo = new ScriptRepository(context);
        var script = new Script { Name = "Test", JsonDefinition = "{}" };
        await repo.CreateAsync(script);

        script.JsonDefinition = "{\"updated\":true}";
        var result = await repo.UpdateAsync(script);

        result.Version.Should().Be(2);
        result.JsonDefinition.Should().Contain("updated");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveScript()
    {
        using var context = CreateContext();
        var repo = new ScriptRepository(context);
        var script = new Script { Name = "Test", JsonDefinition = "{}" };
        await repo.CreateAsync(script);

        var result = await repo.DeleteAsync(script.Id);

        result.Should().BeTrue();
        var fetched = await repo.GetByIdAsync(script.Id);
        fetched.Should().BeNull();
    }
}
