using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class RecipeDbContext(DbContextOptions<RecipeDbContext> options) : DbContext(options)
{
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Ingredient> RecipeIngredients => Set<Ingredient>();
    public DbSet<InstructionStep> InstructionSteps => Set<InstructionStep>();
    public DbSet<CanonicalIngredient> Ingredients => Set<CanonicalIngredient>();
    public DbSet<IngredientAlias> IngredientAliases => Set<IngredientAlias>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<IngredientTag> IngredientTags => Set<IngredientTag>();
    public DbSet<IngredientMatchLog> IngredientMatchLogs => Set<IngredientMatchLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var recipeTitleConverter = new ValueConverter<RecipeTitle, string>(
            v => v.Value,
            v => new RecipeTitle(v));

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

            b.Property(x => x.PreparationMinutes);
            b.Property(x => x.CookingMinutes);
            b.Property(x => x.Category)
                .HasConversion<int>();
            b.Property(x => x.ImageUrl)
                .HasMaxLength(2000);

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
            b.ToTable("RecipeIngredients");

            b.Property(x => x.Name)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(x => x.Preparation)
                .HasMaxLength(200);

            b.Property(x => x.Quantity)
                .HasConversion(quantityConverter)
                .HasPrecision(18, 3);

            b.Property(x => x.Unit)
                .HasConversion<string>()
                .HasMaxLength(32);

            b.HasOne(x => x.IngredientDefinition)
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.SetNull);
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

        modelBuilder.Entity<CanonicalIngredient>(b =>
        {
            b.HasKey(x => x.Id);
            b.ToTable("Ingredients");

            b.Property(x => x.CanonicalName)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(x => x.NormalizedName)
                .HasMaxLength(200)
                .IsRequired();

            b.HasIndex(x => x.NormalizedName)
                .IsUnique();

            b.Property(x => x.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<IngredientAlias>(b =>
        {
            b.HasKey(x => x.Id);
            b.ToTable("IngredientAliases");

            b.Property(x => x.Alias)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(x => x.NormalizedAlias)
                .HasMaxLength(200)
                .IsRequired();

            b.HasIndex(x => x.NormalizedAlias)
                .IsUnique();

            b.HasOne(x => x.Ingredient)
                .WithMany(x => x.Aliases)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Tag>(b =>
        {
            b.HasKey(x => x.Id);
            b.ToTable("Tags");

            b.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            b.Property(x => x.NormalizedName)
                .HasMaxLength(100)
                .IsRequired();

            b.HasIndex(x => x.NormalizedName)
                .IsUnique();
        });

        modelBuilder.Entity<IngredientTag>(b =>
        {
            b.HasKey(x => new { x.IngredientId, x.TagId });
            b.ToTable("IngredientTags");

            b.HasOne(x => x.Ingredient)
                .WithMany(x => x.IngredientTags)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.Tag)
                .WithMany(x => x.IngredientTags)
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IngredientMatchLog>(b =>
        {
            b.HasKey(x => x.Id);
            b.ToTable("IngredientMatchLogs");

            b.Property(x => x.Input)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(x => x.NormalizedInput)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(x => x.MatchType)
                .HasMaxLength(32)
                .IsRequired();

            b.Property(x => x.Confidence)
                .HasPrecision(5, 4);

            b.Property(x => x.CreatedAt)
                .IsRequired();

            b.HasOne(x => x.MatchedIngredient)
                .WithMany()
                .HasForeignKey(x => x.MatchedIngredientId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

