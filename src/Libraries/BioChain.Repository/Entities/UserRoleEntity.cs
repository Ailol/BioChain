namespace BioChain.Repository.Entities;

public class UserRoleEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
