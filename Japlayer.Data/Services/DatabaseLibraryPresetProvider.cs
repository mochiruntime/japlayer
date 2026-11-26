using Japlayer.Data.Context;
using Japlayer.Data.Contracts;
using Japlayer.Data.Entities;
using Japlayer.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.Data.Services
{
    public class DatabaseLibraryPresetProvider(IDbContextFactory<DatabaseContext> contextFactory) : ILibraryPresetProvider
    {
        private readonly IDbContextFactory<DatabaseContext> _contextFactory = contextFactory;

        public async Task<IReadOnlyList<LibraryPresetModel>> GetPresetsAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var rows = await context.LibraryPresets
                .AsNoTracking()
                .AsSplitQuery()
                .OrderBy(preset => preset.CreatedAt)
                .ThenBy(preset => preset.Id)
                .Select(preset => new
                {
                    preset.Id,
                    preset.Name,
                    preset.IsCustomName,
                    preset.SearchText,
                    preset.SortField,
                    preset.IsSortDescending,
                    preset.RatingMin,
                    preset.RatingMax,
                    preset.RuntimeMin,
                    preset.RuntimeMax,
                    preset.CreatedAt,
                    Tags = preset.Tags.Select(entry => new FacetEntryProjection(entry.UserTag.Name, entry.IsIncluded)).ToList(),
                    Genres = preset.Genres.Select(entry => new FacetEntryProjection(entry.Genre.Name, entry.IsIncluded)).ToList(),
                    Cast = preset.People.Select(entry => new FacetEntryProjection(entry.Person.Name, entry.IsIncluded)).ToList(),
                    Studios = preset.Studios.Select(entry => new FacetEntryProjection(entry.Studio.Name, entry.IsIncluded)).ToList(),
                    Series = preset.Series.Select(entry => new FacetEntryProjection(entry.Series.Name, entry.IsIncluded)).ToList()
                })
                .ToListAsync();

            return
            [
                .. rows.Select(row => new LibraryPresetModel
                {
                    Id = row.Id,
                    Name = row.Name,
                    IsCustomName = row.IsCustomName,
                    SearchText = row.SearchText,
                    SortField = row.SortField,
                    IsSortDescending = row.IsSortDescending,
                    RatingMin = row.RatingMin,
                    RatingMax = row.RatingMax,
                    RuntimeMin = row.RuntimeMin,
                    RuntimeMax = row.RuntimeMax,
                    CreatedAt = row.CreatedAt,
                    Tags = ToSelection(row.Tags),
                    Genres = ToSelection(row.Genres),
                    Cast = ToSelection(row.Cast),
                    Studios = ToSelection(row.Studios),
                    Series = ToSelection(row.Series)
                })
            ];
        }

        public async Task<LibraryPresetModel> CreatePresetAsync(LibraryPresetModel preset)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var entity = new LibraryPreset
            {
                CreatedAt = preset.CreatedAt
            };
            CopyScalarProperties(preset, entity);
            await PopulateAllJoinsAsync(context, entity, preset);

            context.LibraryPresets.Add(entity);
            await context.SaveChangesAsync();

            preset.Id = entity.Id;
            return preset;
        }

        public async Task UpdatePresetAsync(LibraryPresetModel preset)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.LibraryPresets
                .AsSplitQuery()
                .Include(entry => entry.Tags)
                .Include(entry => entry.Genres)
                .Include(entry => entry.People)
                .Include(entry => entry.Studios)
                .Include(entry => entry.Series)
                .FirstOrDefaultAsync(entry => entry.Id == preset.Id);

            if (existing == null)
            {
                return;
            }

            CopyScalarProperties(preset, existing);
            await PopulateAllJoinsAsync(context, existing, preset);
            await context.SaveChangesAsync();
        }

        public async Task DeletePresetAsync(int presetId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existing = await context.LibraryPresets.FirstOrDefaultAsync(entry => entry.Id == presetId);
            if (existing != null)
            {
                context.LibraryPresets.Remove(existing);
                await context.SaveChangesAsync();
            }
        }

        private static void CopyScalarProperties(LibraryPresetModel source, LibraryPreset target)
        {
            target.Name = source.Name.Trim();
            target.IsCustomName = source.IsCustomName;
            target.SearchText = source.SearchText;
            target.SortField = source.SortField;
            target.IsSortDescending = source.IsSortDescending;
            target.RatingMin = source.RatingMin;
            target.RatingMax = source.RatingMax;
            target.RuntimeMin = source.RuntimeMin;
            target.RuntimeMax = source.RuntimeMax;
        }

        private static async Task PopulateAllJoinsAsync(DatabaseContext context, LibraryPreset entity, LibraryPresetModel model)
        {
            await PopulateJoinCollectionAsync(
                context.UserTags,
                entity.Tags,
                model.Tags,
                (id, isIncluded) => new LibraryPresetTag { UserTagId = id, IsIncluded = isIncluded });

            await PopulateJoinCollectionAsync(
                context.MediaGenres,
                entity.Genres,
                model.Genres,
                (id, isIncluded) => new LibraryPresetGenre { GenreId = id, IsIncluded = isIncluded });

            await PopulateJoinCollectionAsync(
                context.MediaPeople,
                entity.People,
                model.Cast,
                (id, isIncluded) => new LibraryPresetPerson { PersonId = id, IsIncluded = isIncluded });

            await PopulateJoinCollectionAsync(
                context.MediaStudios,
                entity.Studios,
                model.Studios,
                (id, isIncluded) => new LibraryPresetStudio { StudioId = id, IsIncluded = isIncluded });

            await PopulateJoinCollectionAsync(
                context.MediaSeries,
                entity.Series,
                model.Series,
                (id, isIncluded) => new LibraryPresetSeries { SeriesId = id, IsIncluded = isIncluded });
        }

        private static async Task PopulateJoinCollectionAsync<TTarget, TJoin>(
            IQueryable<TTarget> targetQuery,
            ICollection<TJoin> targetCollection,
            LibraryPresetFacetSelection selection,
            Func<int, bool, TJoin> joinFactory)
            where TTarget : class, INamedEntity
        {
            targetCollection.Clear();

            var includedSet = new HashSet<string>(selection.Included, StringComparer.OrdinalIgnoreCase);
            var excludedSet = new HashSet<string>(selection.Excluded, StringComparer.OrdinalIgnoreCase);
            var allNames = includedSet.Union(excludedSet, StringComparer.OrdinalIgnoreCase).ToList();
            if (allNames.Count == 0)
            {
                return;
            }

            var normalizedNames = allNames.Select(name => name.ToLowerInvariant()).Distinct().ToList();
            var matches = await targetQuery
                .AsNoTracking()
                .Where(entity => normalizedNames.Contains(entity.Name.ToLower()))
                .Select(entity => new { entity.Id, entity.Name })
                .ToListAsync();

            var idByName = matches
                .GroupBy(match => match.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

            var addedIds = new HashSet<int>();
            foreach (var name in includedSet)
            {
                if (idByName.TryGetValue(name, out var id) && addedIds.Add(id))
                {
                    targetCollection.Add(joinFactory(id, true));
                }
            }

            foreach (var name in excludedSet)
            {
                if (idByName.TryGetValue(name, out var id) && addedIds.Add(id))
                {
                    targetCollection.Add(joinFactory(id, false));
                }
            }
        }

        private static LibraryPresetFacetSelection ToSelection(IEnumerable<FacetEntryProjection> entries)
        {
            var included = new List<string>();
            var excluded = new List<string>();

            foreach (var entry in entries)
            {
                if (entry.IsIncluded)
                {
                    included.Add(entry.Name);
                }
                else
                {
                    excluded.Add(entry.Name);
                }
            }

            included.Sort(StringComparer.OrdinalIgnoreCase);
            excluded.Sort(StringComparer.OrdinalIgnoreCase);

            return new LibraryPresetFacetSelection
            {
                Included = included,
                Excluded = excluded
            };
        }

        private sealed record FacetEntryProjection(string Name, bool IsIncluded);
    }
}
