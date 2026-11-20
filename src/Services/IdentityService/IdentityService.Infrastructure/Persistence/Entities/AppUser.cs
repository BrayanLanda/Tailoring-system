using Microsoft.AspNetCore.Identity;

namespace IdentityService.Infrastructure.Persistence.Entities;

public sealed class AppUser : IdentityUser<Guid>
{
    public Guid? TailorProfileId { get; set; }
}

public sealed class AppRole : IdentityRole<Guid>
{
    public const string Admin = "Admin";
    public const string Tailor = "Tailor";
    public const string Receptionist = "Receptionist";

    public AppRole() { }
    public AppRole(string roleName) : base(roleName) { }
}
