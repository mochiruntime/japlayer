using System;
using System.Collections.Generic;
using Japlayer.Data.Models;
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
            entity.Property(e => e.Uuid)
                .HasColumnType("BINARY(16)")
                .HasColumnName("uuid");
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
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
