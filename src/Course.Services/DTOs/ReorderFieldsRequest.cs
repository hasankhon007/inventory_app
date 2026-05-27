namespace Course.Services.DTOs;

public class ReorderFieldsRequest
{
    public List<Guid> FieldIds { get; set; } = new();
}
