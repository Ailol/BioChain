namespace NeuroGateway.Repository;

public interface IUserContext
{
    string UserId { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    bool HasRole(string role);
}
