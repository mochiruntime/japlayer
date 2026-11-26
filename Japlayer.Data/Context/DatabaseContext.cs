using System.Linq.Expressions;
using Japlayer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Japlayer.Data.Context;

public partial class DatabaseContext : DbContext
{
    public DatabaseContext()
    {
    }

    public DatabaseContext(DbContextOptions<DatabaseContext> options)
        : base(options)
    {
    }

    public virtual DbSet<MediaGenre> MediaGenres { get; set; }

    public virtual DbSet<MediaImage> MediaImages { get; set; }

    public virtual DbSet<MediaLocation> MediaLocations { get; set; }

    public virtual DbSet<MediaMetadata> MediaMetadata { get; set; }

    public virtual DbSet<MediaPerson> MediaPeople { get; set; }

    public virtual DbSet<MediaSeries> MediaSeries { get; set; }

    public virtual DbSet<MediaStudio> MediaStudios { get; set; }

    public virtual DbSet<Media> Media { get; set; }

    public virtual DbSet<UserTag> UserTags { get; set; }

    public virtual DbSet<MediaThumbnail> MediaThumbnails { get; set; }

    public virtual DbSet<MediaHighlight> MediaHighlights { get; set; }

    public virtual DbSet<UserMediaData> UserMediaData { get; set; }

    public virtual DbSet<LibraryPreset> LibraryPresets { get; set; }

    public virtual DbSet<LibraryPresetTag> LibraryPresetTags { get; set; }

    public virtual DbSet<LibraryPresetGenre> LibraryPresetGenres { get; set; }

    public virtual DbSet<LibraryPresetPerson> LibraryPresetPeople { get; set; }

    public virtual DbSet<LibraryPresetStudio> LibraryPresetStudios { get; set; }

    public virtual DbSet<LibraryPresetSeries> LibraryPresetSeries { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaGenre>(entity =>
        {
            entity.HasIndex(e => e.Name, "MediaGenres_name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<MediaImage>(entity =>
        {
            entity.HasKey(e => e.Filepath);

            entity.Property(e => e.Filepath).HasColumnName("filepath");
            entity.Property(e => e.MediaId)
                .UseCollation("NOCASE")
                .HasColumnName("mediaId");
            entity.Property(e => e.Url).HasColumnName("url");

            entity.HasOne(d => d.Media).WithMany(p => p.MediaImages)
                .HasForeignKey(d => d.MediaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MediaLocation>(entity =>
        {
            entity.HasKey(e => new { e.MediaId, e.Scene, e.Hostname, e.Path });

            entity.Property(e => e.MediaId)
                .UseCollation("NOCASE")
                .HasColumnName("mediaId");
            entity.Property(e => e.Scene)
                .HasDefaultValue(1)
                .HasColumnType("INT")
                .HasColumnName("scene");
            entity.Property(e => e.Hostname).HasColumnName("hostname");
            entity.Property(e => e.Path).HasColumnName("path");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("createdAt");
            entity.Property(e => e.DurationSeconds)
                .HasColumnType("INT")
                .HasColumnName("durationSeconds");

            entity.HasOne(d => d.Media).WithMany(p => p.MediaLocations)
                .HasForeignKey(d => d.MediaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MediaThumbnail>(entity =>
        {
            entity.HasKey(e => new { e.MediaId, e.Scene, e.Timestamp });

            entity.Property(e => e.MediaId)
                .UseCollation("NOCASE")
                .HasColumnName("mediaId");
            entity.Property(e => e.Scene)
                .HasColumnType("INT")
                .HasColumnName("scene");
            entity.Property(e => e.Timestamp)
                .HasColumnType("INT")
                .HasColumnName("timestamp");
            entity.Property(e => e.Path).HasColumnName("path");
            entity.Property(e => e.OriginalHostname).HasColumnName("originalHostname");
            entity.Property(e => e.OriginalPath).HasColumnName("originalPath");

            entity.HasOne(d => d.Media).WithMany(p => p.MediaThumbnails)
                .HasForeignKey(d => d.MediaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MediaHighlight>(entity =>
        {
            entity.HasKey(e => new { e.MediaId, e.Scene, e.Timestamp });

            entity.Property(e => e.MediaId)
                .UseCollation("NOCASE")
                .HasColumnName("mediaId");
            entity.Property(e => e.Scene)
                .HasColumnType("INT")
                .HasColumnName("scene");
            entity.Property(e => e.Timestamp)
                .HasColumnType("INT")
                .HasColumnName("timestamp");

            entity.HasOne(d => d.Media).WithMany(p => p.MediaHighlights)
                .HasForeignKey(d => d.MediaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserMediaData>(entity =>
        {
            entity.HasKey(e => e.MediaId);

            entity.Property(e => e.MediaId)
                .UseCollation("NOCASE")
                .HasColumnName("mediaId");
            entity.Property(e => e.Rating)
                .HasColumnType("INT")
                .HasColumnName("rating");
            entity.Property(e => e.TotalPlayTimeSeconds)
                .HasDefaultValue(0L)
                .HasColumnType("INTEGER")
                .HasColumnName("totalPlayTimeSeconds");
            entity.Property(e => e.LastPlayedAt)
                .HasColumnName("lastPlayedAt");

            entity.HasOne(d => d.Media).WithOne(p => p.UserMediaData)
                .HasForeignKey<UserMediaData>(d => d.MediaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MediaMetadata>(entity =>
        {
            entity.HasKey(e => e.MetadataUrl);

            entity.Property(e => e.MetadataUrl).HasColumnName("metadataUrl");
            entity.Property(e => e.ContentId).HasColumnName("contentId");
            entity.Property(e => e.Cover).HasColumnName("cover");
            entity.Property(e => e.MediaId)
                .UseCollation("NOCASE")
                .HasColumnName("mediaId");
            entity.Property(e => e.ReleaseDate).HasColumnName("releaseDate");
            entity.Property(e => e.RuntimeMinutes)
                .HasColumnType("INT")
                .HasColumnName("runtimeMinutes");
            entity.Property(e => e.Thumbnail).HasColumnName("thumbnail");
            entity.Property(e => e.Title).HasColumnName("title");

            entity.HasOne(d => d.CoverNavigation).WithMany(p => p.MediaMetadataCoverNavigations)
                .HasForeignKey(d => d.Cover)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Media).WithMany(p => p.MediaMetadata)
                .HasForeignKey(d => d.MediaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ThumbnailNavigation).WithMany(p => p.MediaMetadataThumbnailNavigations)
                .HasForeignKey(d => d.Thumbnail)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MediaPerson>(entity =>
        {
            entity.HasIndex(e => e.Name, "MediaPeople_name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<MediaSeries>(entity =>
        {
            entity.HasIndex(e => e.Name, "MediaSeries_name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<MediaStudio>(entity =>
        {
            entity.HasIndex(e => e.Name, "MediaStudios_name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<Media>(entity =>
        {
            entity.HasKey(e => e.MediaId);

            entity.Property(e => e.MediaId)
                .UseCollation("NOCASE")
                .HasColumnName("mediaId");

            entity.HasMany(d => d.Genres).WithMany(p => p.Media)
                .UsingEntity<Dictionary<string, object>>(
                    "GenresRelationship",
                    r => r.HasOne<MediaGenre>().WithMany()
                        .HasForeignKey("Genre")
                        .OnDelete(DeleteBehavior.Restrict),
                    l => l.HasOne<Media>().WithMany()
                        .HasForeignKey("MediaId")
                        .OnDelete(DeleteBehavior.Restrict),
                    j =>
                    {
                        j.HasKey("MediaId", "Genre");
                        j.ToTable("GenresRelationship");
                        j.IndexerProperty<string>("MediaId")
                            .UseCollation("NOCASE")
                            .HasColumnName("mediaId");
                        j.IndexerProperty<int>("Genre")
                            .HasColumnType("INT")
                            .HasColumnName("genre");
                    });

            entity.HasMany(d => d.People).WithMany(p => p.Media)
                .UsingEntity<Dictionary<string, object>>(
                    "CastRelationship",
                    r => r.HasOne<MediaPerson>().WithMany()
                        .HasForeignKey("Person")
                        .OnDelete(DeleteBehavior.Restrict),
                    l => l.HasOne<Media>().WithMany()
                        .HasForeignKey("MediaId")
                        .OnDelete(DeleteBehavior.Restrict),
                    j =>
                    {
                        j.HasKey("MediaId", "Person");
                        j.ToTable("CastRelationship");
                        j.IndexerProperty<string>("MediaId")
                            .UseCollation("NOCASE")
                            .HasColumnName("mediaId");
                        j.IndexerProperty<int>("Person")
                            .HasColumnType("INT")
                            .HasColumnName("person");
                    });

            entity.HasMany(d => d.PeopleNavigation).WithMany(p => p.MediaNavigation)
                .UsingEntity<Dictionary<string, object>>(
                    "StaffRelationship",
                    r => r.HasOne<MediaPerson>().WithMany()
                        .HasForeignKey("Person")
                        .OnDelete(DeleteBehavior.Restrict),
                    l => l.HasOne<Media>().WithMany()
                        .HasForeignKey("MediaId")
                        .OnDelete(DeleteBehavior.Restrict),
                    j =>
                    {
                        j.HasKey("MediaId", "Person");
                        j.ToTable("StaffRelationship");
                        j.IndexerProperty<string>("MediaId")
                            .UseCollation("NOCASE")
                            .HasColumnName("mediaId");
                        j.IndexerProperty<int>("Person")
                            .HasColumnType("INT")
                            .HasColumnName("person");
                    });

            entity.HasMany(d => d.Series).WithMany(p => p.Media)
                .UsingEntity<Dictionary<string, object>>(
                    "SeriesRelationship",
                    r => r.HasOne<MediaSeries>().WithMany()
                        .HasForeignKey("Series")
                        .OnDelete(DeleteBehavior.Restrict),
                    l => l.HasOne<Media>().WithMany()
                        .HasForeignKey("MediaId")
                        .OnDelete(DeleteBehavior.Restrict),
                    j =>
                    {
                        j.HasKey("MediaId", "Series");
                        j.ToTable("SeriesRelationship");
                        j.IndexerProperty<string>("MediaId")
                            .UseCollation("NOCASE")
                            .HasColumnName("mediaId");
                        j.IndexerProperty<int>("Series")
                            .HasColumnType("INT")
                            .HasColumnName("series");
                    });

            entity.HasMany(d => d.Studios).WithMany(p => p.Media)
                .UsingEntity<Dictionary<string, object>>(
                    "StudiosRelationship",
                    r => r.HasOne<MediaStudio>().WithMany()
                        .HasForeignKey("Studio")
                        .OnDelete(DeleteBehavior.Restrict),
                    l => l.HasOne<Media>().WithMany()
                        .HasForeignKey("MediaId")
                        .OnDelete(DeleteBehavior.Restrict),
                    j =>
                    {
                        j.HasKey("MediaId", "Studio");
                        j.ToTable("StudiosRelationship");
                        j.IndexerProperty<string>("MediaId")
                            .UseCollation("NOCASE")
                            .HasColumnName("mediaId");
                        j.IndexerProperty<int>("Studio")
                            .HasColumnType("INT")
                            .HasColumnName("studio");
                    });

            entity.HasMany(d => d.UserTags).WithMany(p => p.Media)
                .UsingEntity<Dictionary<string, object>>(
                    "UserTagsRelationship",
                    r => r.HasOne<UserTag>().WithMany()
                        .HasForeignKey("UserTagId")
                        .OnDelete(DeleteBehavior.Restrict),
                    l => l.HasOne<Media>().WithMany()
                        .HasForeignKey("MediaId")
                        .OnDelete(DeleteBehavior.Restrict),
                    j =>
                    {
                        j.HasKey("MediaId", "UserTagId");
                        j.ToTable("UserTagsRelationship");
                        j.IndexerProperty<string>("MediaId")
                            .UseCollation("NOCASE")
                            .HasColumnName("mediaId");
                        j.IndexerProperty<int>("UserTagId")
                            .HasColumnType("INT")
                            .HasColumnName("userTagId");
                    });
        });

        modelBuilder.Entity<UserTag>(entity =>
        {
            entity.HasIndex(e => e.Name, "UserTags_name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .UseCollation("NOCASE")
                .HasColumnName("name");
        });

        modelBuilder.Entity<LibraryPreset>(entity =>
        {
            entity.HasIndex(e => e.Name, "LibraryPresets_name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .UseCollation("NOCASE")
                .HasColumnName("name");
            entity.Property(e => e.IsCustomName).HasColumnName("isCustomName");
            entity.Property(e => e.SearchText).HasColumnName("searchText");
            entity.Property(e => e.SortField).HasColumnName("sortField");
            entity.Property(e => e.IsSortDescending).HasColumnName("isSortDescending");
            entity.Property(e => e.RatingMin).HasColumnName("ratingMin");
            entity.Property(e => e.RatingMax).HasColumnName("ratingMax");
            entity.Property(e => e.RuntimeMin).HasColumnName("runtimeMin");
            entity.Property(e => e.RuntimeMax).HasColumnName("runtimeMax");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
        });

        ConfigurePresetJoin(
            modelBuilder,
            "LibraryPresetTags",
            "userTagId",
            preset => preset.Tags,
            join => join.LibraryPreset,
            join => join.UserTag,
            join => new { join.LibraryPresetId, join.UserTagId },
            join => join.UserTagId);

        ConfigurePresetJoin(
            modelBuilder,
            "LibraryPresetGenres",
            "genreId",
            preset => preset.Genres,
            join => join.LibraryPreset,
            join => join.Genre,
            join => new { join.LibraryPresetId, join.GenreId },
            join => join.GenreId);

        ConfigurePresetJoin(
            modelBuilder,
            "LibraryPresetPeople",
            "personId",
            preset => preset.People,
            join => join.LibraryPreset,
            join => join.Person,
            join => new { join.LibraryPresetId, join.PersonId },
            join => join.PersonId);

        ConfigurePresetJoin(
            modelBuilder,
            "LibraryPresetStudios",
            "studioId",
            preset => preset.Studios,
            join => join.LibraryPreset,
            join => join.Studio,
            join => new { join.LibraryPresetId, join.StudioId },
            join => join.StudioId);

        ConfigurePresetJoin(
            modelBuilder,
            "LibraryPresetSeries",
            "seriesId",
            preset => preset.Series,
            join => join.LibraryPreset,
            join => join.Series,
            join => new { join.LibraryPresetId, join.SeriesId },
            join => join.SeriesId);

        OnModelCreatingPartial(modelBuilder);
    }

    private static void ConfigurePresetJoin<TJoin, TTarget>(
        ModelBuilder modelBuilder,
        string tableName,
        string targetColumnName,
        Expression<Func<LibraryPreset, IEnumerable<TJoin>?>> presetNavigation,
        Expression<Func<TJoin, LibraryPreset?>> joinToPreset,
        Expression<Func<TJoin, TTarget?>> joinToTarget,
        Expression<Func<TJoin, object?>> keyExpression,
        Expression<Func<TJoin, int>> targetIdProperty)
        where TJoin : class
        where TTarget : class
    {
        modelBuilder.Entity<TJoin>(entity =>
        {
            entity.ToTable(tableName);
            entity.HasKey(keyExpression);

            entity.Property<int>("LibraryPresetId").HasColumnName("libraryPresetId");
            entity.Property(targetIdProperty).HasColumnName(targetColumnName);
            entity.Property<bool>("IsIncluded").HasColumnName("isIncluded");

            entity.HasOne(joinToPreset)
                .WithMany(presetNavigation)
                .HasForeignKey("LibraryPresetId")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(joinToTarget)
                .WithMany()
                .HasForeignKey(((MemberExpression)targetIdProperty.Body).Member.Name)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
