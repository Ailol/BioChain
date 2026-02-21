namespace NeuroGateway.Repository.Roles;

// Application-level role names — vendor-agnostic, no dependency on any IdP
public static class AppRole
{
    public const string Work = "work";
    public const string Private = "private";
    public const string Both = "both";
    public const string Worker = "worker";
    public const string Admin = "admin";

    public static readonly string[] All = [Work, Private, Both, Worker, Admin];

    public static bool IsValid(string role) => All.Contains(role);

    // Expand composite roles into their constituent effective permissions
    public static HashSet<string> ExpandEffective(IEnumerable<string> roles)
    {
        var effective = new HashSet<string>(roles);
        if (effective.Contains(Both))
        {
            effective.Add(Work);
            effective.Add(Private);
        }
        if (effective.Contains(Admin))
        {
            effective.Add(Work);
            effective.Add(Private);
            effective.Add(Both);
            effective.Add(Worker);
        }
        return effective;
    }
}
