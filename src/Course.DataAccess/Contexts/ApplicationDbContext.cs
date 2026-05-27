using Course.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Course.DataAccess.Contexts;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<InventoryCategory> InventoryCategories => Set<InventoryCategory>();
    public DbSet<CustomField> CustomFields => Set<CustomField>();
    public DbSet<InventoryIdElement> InventoryIdElements => Set<InventoryIdElement>();
    public DbSet<InventoryAccess> InventoryAccesses => Set<InventoryAccess>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemFieldValue> ItemFieldValues => Set<ItemFieldValue>();
    public DbSet<ItemLike> ItemLikes => Set<ItemLike>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<InventoryTag> InventoryTags => Set<InventoryTag>();
    public DbSet<DiscussionPost> DiscussionPosts => Set<DiscussionPost>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<InventoryCategory>()
            .HasIndex(category => category.Name)
            .IsUnique();

        // CustomField configuration
        builder.Entity<CustomField>(entity =>
        {
            entity.HasIndex(field => new { field.InventoryId, field.Name })
                .HasFilter("\"IsDeleted\" = false")
                .IsUnique();

            entity.HasOne(field => field.Inventory)
                .WithMany(inventory => inventory.CustomFields)
                .HasForeignKey(field => field.InventoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(field => !field.IsDeleted);
        });

        // ItemFieldValue configuration
        builder.Entity<ItemFieldValue>(entity =>
        {
            entity.HasIndex(value => new { value.ItemId, value.CustomFieldId })
                .IsUnique();

            entity.HasOne(value => value.Item)
                .WithMany(item => item.FieldValues)
                .HasForeignKey(value => value.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(value => value.CustomField)
                .WithMany(field => field.FieldValues)
                .HasForeignKey(value => value.CustomFieldId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(value => value.Value)
                .HasMaxLength(4000);
        });

        builder.Entity<InventoryIdElement>()
            .HasIndex(element => new { element.InventoryId, element.SortOrder });

        builder.Entity<InventoryAccess>()
            .HasKey(access => new { access.InventoryId, access.UserId });

        builder.Entity<InventoryAccess>()
            .HasIndex(access => new { access.InventoryId, access.UserId })
            .IsUnique();

        builder.Entity<InventoryTag>()
            .HasKey(tag => new { tag.InventoryId, tag.TagId });

        builder.Entity<Tag>()
            .HasIndex(tag => tag.Name)
            .IsUnique();

        builder.Entity<Item>()
            .HasIndex(item => new { item.InventoryId, item.CustomId })
            .IsUnique();

        builder.Entity<Item>()
            .Property(item => item.Price)
            .HasPrecision(18, 2);

        builder.Entity<ItemLike>()
            .HasKey(like => new { like.ItemId, like.UserId });

        builder.Entity<ItemLike>()
            .HasIndex(like => new { like.ItemId, like.UserId })
            .IsUnique();
    }
}