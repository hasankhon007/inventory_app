using System.ComponentModel.DataAnnotations;

namespace Course.Domain.Entities;

public class Tag
{
    public int Id { get; set; }

    [Required]
    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    public ICollection<InventoryTag> Inventories { get; set; } = new List<InventoryTag>();
}
