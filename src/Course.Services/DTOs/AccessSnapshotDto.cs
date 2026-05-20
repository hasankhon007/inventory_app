namespace Course.Services.DTOs;

public class AccessSnapshotDto
{
    public bool IsOwner { get; set; }
    public bool CanView { get; set; }
    public bool CanWrite { get; set; }
    public bool CanManage { get; set; }
    public bool CanAddItems { get; set; }
}
