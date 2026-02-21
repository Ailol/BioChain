namespace NeuroGateway.Repository.Entities;

public class UserRoleEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string? Email { get; set; }
    public string Role { get; set; } = ""; // work | private | both | worker | admin
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
