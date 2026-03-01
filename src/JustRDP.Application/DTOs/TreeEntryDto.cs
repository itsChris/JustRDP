using JustRDP.Domain.Enums;

namespace JustRDP.Application.DTOs;

public class TreeEntryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public TreeEntryType EntryType { get; set; }
}
