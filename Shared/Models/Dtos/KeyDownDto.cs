using System.Runtime.Serialization;

namespace Pronetsys.Shared.Models.Dtos;

[DataContract]
public class KeyDownDto
{
    [DataMember(Name = "Key")]
    public string Key { get; set; } = string.Empty;
}
