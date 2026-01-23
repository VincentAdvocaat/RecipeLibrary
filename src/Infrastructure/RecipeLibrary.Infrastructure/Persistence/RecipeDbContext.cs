using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class RecipeDbContext(DbContextOptions<RecipeDbContext> options) : DbContext(options)
{
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<InstructionStep> InstructionSteps => Set<InstructionStep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var recipeTitleConverter = new ValueConverter<RecipeTitle, string>(
            v => v.Value,
            v => new RecipeTitle(v));

        var durationConverter = new ValueConverter<Duration, int>(
            v => v.Minutes,
            v => new Duration(v));

        var quantityConverter = new ValueConverter<Quantity, decimal>(
            v => v.Value,
            v => new Quantity(v));

        modelBuilder.Entity<Recipe>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Title)
                .HasConversion(recipeTitleConverter)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(x => x.Description)
                .HasMaxLength(4000);

            b.Property(x => x.Duration)
                .HasConversion(durationConverter);

            b.Property(x => x.Difficulty)
                .HasConversion<int>();

            b.Property(x => x.CreatedAt);
            b.Property(x => x.UpdatedAt);

            b.HasMany(x => x.Ingredients)
                .WithOne()
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.InstructionSteps)
                .WithOne()
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Ingredient>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(x => x.Quantity)
                .HasConversion(quantityConverter)
                .HasPrecision(18, 3);

            b.Property(x => x.Unit)
                .HasConversion<string>()
                .HasMaxLength(32);
        });

        modelBuilder.Entity<InstructionStep>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.StepNumber)
                .IsRequired();

            b.Property(x => x.Text)
                .HasMaxLength(4000)
                .IsRequired();
        });
    }
}

