namespace Course.Domain.Entities;

public class ItemFieldValue
{
    public Guid Id { get; set; }

    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public Guid CustomFieldId { get; set; }
    public CustomField? CustomField { get; set; }

    public string? Value { get; set; }
}
