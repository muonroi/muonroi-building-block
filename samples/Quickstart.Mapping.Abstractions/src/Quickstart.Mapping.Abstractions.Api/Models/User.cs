namespace Quickstart.Mapping.Abstractions.Api.Models;
public class User {
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
public class UserDto : IMapFrom<User> {
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}