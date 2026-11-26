using Japlayer.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.UnitTests.Data
{
    internal sealed class TestDbContextFactory(DbContextOptions<DatabaseContext> options) : IDbContextFactory<DatabaseContext>
    {
        private readonly DbContextOptions<DatabaseContext> _options = options;

        public DatabaseContext CreateDbContext() => new(_options);

        public Task<DatabaseContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new DatabaseContext(_options));
    }
}
