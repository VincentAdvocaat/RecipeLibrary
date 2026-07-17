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
    public DbSet<IngredientTranslation> IngredientTranslations => Set<IngredientTranslation>();
    public DbSet<IngredientTranslationAlias> IngredientTranslationAliases => Set<IngredientTranslationAlias>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<IngredientTag> IngredientTags => Set<IngredientTag>();
    public DbSet<IngredientMatchLog> IngredientMatchLogs => Set<IngredientMatchLog>();
    public DbSet<ShoppingListGroup> ShoppingListGroups => Set<ShoppingListGroup>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();
    public DbSet<ShoppingListItemSource> ShoppingListItemSources => Set<ShoppingListItemSource>();
    public DbSet<PantryItem> PantryItems => Set<PantryItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var recipeTitleConverter = new ValueConverter<RecipeTitle, string>(
            v => v.Value,
            v => new RecipeTitle(v));

        var quantityConverter = new ValueConverter<Quantity?, decimal?>(
            v => v.HasValue ? v.Value.Value : null,
            v => v.HasValue ? new Quantity(v.Value) : null);

        var nullableUnitConverter = new ValueConverter<Unit?, string?>(
            v => v.HasValue ? v.Value.ToString() : null,
            v => string.IsNullOrWhiteSpace(v) ? null : Enum.Parse<Unit>(v));

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
                .HasPrecision(18, 3)
                .IsRequired(false);

            b.Property(x => x.Unit)
                .HasConversion(nullableUnitConverter)
                .HasMaxLength(32)
                .IsRequired(false);

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

            b.Property(x => x.CatalogKey)
                .HasMaxLength(200);

            b.HasIndex(x => x.CatalogKey)
                .IsUnique()
                .HasFilter("[CatalogKey] IS NOT NULL");

            b.Property(x => x.CreatedAt)
                .IsRequired();
        });

        modelBuilder.Entity<IngredientTranslation>(b =>
        {
            b.HasKey(x => x.Id);
            b.ToTable("IngredientTranslations");

            b.Property(x => x.LanguageCode)
                .HasMaxLength(35)
                .IsRequired();

            b.Property(x => x.DisplayName)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(x => x.NormalizedDisplayName)
                .HasMaxLength(200)
                .IsRequired();

            b.HasIndex(x => new { x.IngredientId, x.LanguageCode })
                .IsUnique();

            // Non-unique monitoring/lookup index — catalog curation owns duplicate policy.
            b.HasIndex(x => new { x.LanguageCode, x.NormalizedDisplayName });

            b.HasOne(x => x.Ingredient)
                .WithMany(x => x.Translations)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IngredientTranslationAlias>(b =>
        {
            b.HasKey(x => x.Id);
            b.ToTable("IngredientTranslationAliases");

            b.Property(x => x.Alias)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(x => x.NormalizedAlias)
                .HasMaxLength(200)
                .IsRequired();

            // Non-unique monitoring/lookup index — catalog curation owns duplicate policy.
            b.HasIndex(x => x.NormalizedAlias);

            b.HasOne(x => x.Translation)
                .WithMany(x => x.Aliases)
                .HasForeignKey(x => x.IngredientTranslationId)
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

        modelBuilder.Entity<ShoppingListGroup>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.OwnerUserId)
                .HasMaxLength(256);

            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();

            b.HasMany(x => x.Lists)
                .WithOne(x => x.Group)
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShoppingList>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.Name)
                .HasMaxLength(100)
                .IsRequired();

            b.Property(x => x.StoreOrder).IsRequired();
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();

            b.HasIndex(x => new { x.GroupId, x.StoreOrder })
                .IsUnique();

            b.HasMany(x => x.Items)
                .WithOne(x => x.ShoppingList)
                .HasForeignKey(x => x.ShoppingListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShoppingListItem>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.DisplayName)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(x => x.Preparation)
                .HasMaxLength(200);

            b.Property(x => x.Quantity)
                .HasConversion(quantityConverter)
                .HasPrecision(18, 3)
                .IsRequired(false);

            b.Property(x => x.Unit)
                .HasConversion(nullableUnitConverter)
                .HasMaxLength(32)
                .IsRequired(false);

            b.HasMany(x => x.Sources)
                .WithOne(x => x.Item)
                .HasForeignKey(x => x.ShoppingListItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShoppingListItemSource>(b =>
        {
            b.HasKey(x => new { x.ShoppingListItemId, x.RecipeId });

            b.Property(x => x.RecipeTitle)
                .HasMaxLength(200)
                .IsRequired();
        });

        modelBuilder.Entity<PantryItem>(b =>
        {
            b.HasKey(x => x.Id);

            b.Property(x => x.OwnerUserId)
                .HasMaxLength(256)
                .IsRequired();

            b.Property(x => x.DisplayName)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.UpdatedAt).IsRequired();

            b.HasIndex(x => x.OwnerUserId);

            b.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.CanonicalIngredientId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

