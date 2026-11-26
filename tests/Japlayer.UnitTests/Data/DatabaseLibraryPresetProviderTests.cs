using Japlayer.Data.Context;
using Japlayer.Data.Entities;
using Japlayer.Data.Models;
using Japlayer.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.Data
{
    [TestClass]
    public class DatabaseLibraryPresetProviderTests : IDisposable
    {
        private readonly SqliteConnection _sqliteConnection;
        private readonly DbContextOptions<DatabaseContext> _options;
        private readonly DatabaseLibraryPresetProvider _presetProvider;

        public DatabaseLibraryPresetProviderTests()
        {
            _sqliteConnection = new SqliteConnection("Data Source=:memory:");
            _sqliteConnection.Open();

            _options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlite(_sqliteConnection)
                .Options;

            using var context = new DatabaseContext(_options);
            context.Database.Migrate();

            _presetProvider = new DatabaseLibraryPresetProvider(new TestDbContextFactory(_options));
        }

        public void Dispose() => _sqliteConnection.Dispose();

        [TestMethod]
        public async Task CreateGetUpdateDelete_PersistsAcrossContexts_AndReflectsFacetRenames()
        {
            // Arrange: seed facet lookup values
            await using (var seedContext = new DatabaseContext(_options))
            {
                seedContext.UserTags.AddRange(new UserTag { Name = "VR" }, new UserTag { Name = "2D" });
                seedContext.MediaGenres.Add(new MediaGenre { Name = "Action" });
                seedContext.MediaPeople.Add(new MediaPerson { Name = "Actor Old" });
                seedContext.MediaStudios.Add(new MediaStudio { Name = "Studio One" });
                seedContext.MediaSeries.Add(new MediaSeries { Name = "Series A" });
                await seedContext.SaveChangesAsync();
            }

            var model = new LibraryPresetModel
            {
                Name = "+VR, −2D • Rating ↓",
                IsCustomName = false,
                SearchText = "cyber",
                SortField = "Rating",
                IsSortDescending = true,
                RatingMin = 3,
                RatingMax = 5,
                RuntimeMin = 30,
                RuntimeMax = 120,
                Tags = new LibraryPresetFacetSelection { Included = ["VR"], Excluded = ["2D"] },
                Genres = new LibraryPresetFacetSelection { Included = ["Action"] },
                Cast = new LibraryPresetFacetSelection { Included = ["Actor Old"] },
                Studios = new LibraryPresetFacetSelection { Excluded = ["Studio One"] },
                Series = new LibraryPresetFacetSelection { Included = ["Series A"] }
            };

            // Act 1: create and verify from fresh read
            var created = await _presetProvider.CreatePresetAsync(model);
            Assert.IsTrue(created.Id > 0);

            // Act 2: rename facet values directly in SQLite (simulating external metadata/tag rename)
            await using (var renameContext = new DatabaseContext(_options))
            {
                (await renameContext.UserTags.FirstAsync(tag => tag.Name == "VR")).Name = "Virtual Reality";
                (await renameContext.MediaGenres.FirstAsync(genre => genre.Name == "Action")).Name = "Sci-Fi Action";
                (await renameContext.MediaPeople.FirstAsync(person => person.Name == "Actor Old")).Name = "Actor New";
                await renameContext.SaveChangesAsync();
            }

            var loaded = (await _presetProvider.GetPresetsAsync()).Single();
            Assert.AreEqual("cyber", loaded.SearchText);
            Assert.AreEqual("Rating", loaded.SortField);
            Assert.IsTrue(loaded.IsSortDescending);
            Assert.AreEqual(3, loaded.RatingMin);
            Assert.AreEqual(120, loaded.RuntimeMax);
            CollectionAssert.AreEqual(new[] { "Virtual Reality" }, loaded.Tags.Included.ToList());
            CollectionAssert.AreEqual(new[] { "2D" }, loaded.Tags.Excluded.ToList());
            CollectionAssert.AreEqual(new[] { "Sci-Fi Action" }, loaded.Genres.Included.ToList());
            CollectionAssert.AreEqual(new[] { "Actor New" }, loaded.Cast.Included.ToList());
            CollectionAssert.AreEqual(new[] { "Studio One" }, loaded.Studios.Excluded.ToList());
            CollectionAssert.AreEqual(new[] { "Series A" }, loaded.Series.Included.ToList());

            // Act 3: update preset with custom name and fewer facets
            loaded.Name = "My Custom View";
            loaded.IsCustomName = true;
            loaded.Tags = new LibraryPresetFacetSelection { Included = ["Virtual Reality"] };
            loaded.Genres = new LibraryPresetFacetSelection();
            await _presetProvider.UpdatePresetAsync(loaded);

            var updated = (await _presetProvider.GetPresetsAsync()).Single();
            Assert.AreEqual("My Custom View", updated.Name);
            Assert.IsTrue(updated.IsCustomName);
            Assert.AreEqual(0, updated.Genres.Included.Count);
            CollectionAssert.AreEqual(new[] { "Virtual Reality" }, updated.Tags.Included.ToList());

            // Act 4: deleting an unused UserTag cascades without FK violation
            await using (var deleteTagContext = new DatabaseContext(_options))
            {
                var tag = await deleteTagContext.UserTags.FirstAsync(entry => entry.Name == "Virtual Reality");
                deleteTagContext.UserTags.Remove(tag);
                await deleteTagContext.SaveChangesAsync();
            }

            var afterTagDelete = (await _presetProvider.GetPresetsAsync()).Single();
            Assert.AreEqual(0, afterTagDelete.Tags.Included.Count);

            // Act 5: delete preset cascades all remaining join rows
            await _presetProvider.DeletePresetAsync(updated.Id);
            Assert.AreEqual(0, (await _presetProvider.GetPresetsAsync()).Count);
            await using var verifyContext = new DatabaseContext(_options);
            Assert.AreEqual(0, await verifyContext.LibraryPresetPeople.CountAsync());
            Assert.AreEqual(0, await verifyContext.LibraryPresetStudios.CountAsync());
            Assert.AreEqual(0, await verifyContext.LibraryPresetSeries.CountAsync());
        }

        [TestMethod]
        public async Task AddLibraryPresetsMigration_DeduplicatesCaseVariantUserTags_AndEnforcesUniqueNocaseIndex()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlite(connection)
                .Options;

            await using var context = new DatabaseContext(options);
            var migrator = context.GetService<IMigrator>();

            // Migrate up to the previous migration before UserTags_name unique index existed
            await migrator.MigrateAsync("20261126191731_AddMediaLocationDurationSeconds");

            // Seed mixed-case duplicate UserTags linked to the same media and a second media
            await context.Database.ExecuteSqlRawAsync("""
                INSERT INTO Media (mediaId) VALUES ('M1'), ('M2');
                INSERT INTO UserTags (id, name) VALUES (1, 'VR'), (2, 'vr');
                INSERT INTO UserTagsRelationship (mediaId, userTagId) VALUES ('M1', 1), ('M1', 2), ('M2', 2);
                """);

            // Act: migrate to latest (AddLibraryPresets)
            await migrator.MigrateAsync();

            // Assert: duplicates merged cleanly onto canonical tag id 1
            var tags = await context.UserTags.AsNoTracking().ToListAsync();
            Assert.AreEqual(1, tags.Count);
            Assert.AreEqual("VR", tags[0].Name);

            var media1 = await context.Media.Include(m => m.UserTags).FirstAsync(m => m.MediaId == "M1");
            var media2 = await context.Media.Include(m => m.UserTags).FirstAsync(m => m.MediaId == "M2");
            Assert.AreEqual("VR", media1.UserTags.Single().Name);
            Assert.AreEqual("VR", media2.UserTags.Single().Name);

            // Unique NOCASE index rejects inserting "vR"
            context.UserTags.Add(new UserTag { Name = "vR" });
            await Assert.ThrowsExceptionAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }
}
