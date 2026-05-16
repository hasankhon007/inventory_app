using Course.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Course.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
	public DbSet<Inventory> Inventories => Set<Inventory>();
	public DbSet<InventoryCategory> InventoryCategories => Set<InventoryCategory>();
	public DbSet<InventoryFieldDefinition> InventoryFieldDefinitions => Set<InventoryFieldDefinition>();
	public DbSet<InventoryIdElement> InventoryIdElements => Set<InventoryIdElement>();
	public DbSet<InventoryAccess> InventoryAccesses => Set<InventoryAccess>();
	public DbSet<Item> Items => Set<Item>();
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

		builder.Entity<InventoryFieldDefinition>()
			.HasIndex(field => new { field.InventoryId, field.FieldType, field.SlotIndex })
			.IsUnique();

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
			.Property(item => item.NumberValue1)
			.HasPrecision(18, 2);

		builder.Entity<Item>()
			.Property(item => item.NumberValue2)
			.HasPrecision(18, 2);

		builder.Entity<Item>()
			.Property(item => item.NumberValue3)
			.HasPrecision(18, 2);

		builder.Entity<ItemLike>()
			.HasKey(like => new { like.ItemId, like.UserId });

		builder.Entity<ItemLike>()
			.HasIndex(like => new { like.ItemId, like.UserId })
			.IsUnique();
	}
}
