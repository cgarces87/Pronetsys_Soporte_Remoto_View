using System.ComponentModel.DataAnnotations;

namespace Pronetsys.Shared.Entities;

public class KeyValueRecord
{
    [Key]
    public Guid Key { get; set; }
    public string? Value { get; set; }
}
