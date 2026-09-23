using System.Security.Principal;
using PCDoctor.Core.Interfaces;

namespace PCDoctor.Diagnostics.Services;

public sealed class PrivilegeService : IPrivilegeService
{
    public PrivilegeService()
    {
        using var identity = WindowsIdentity.GetCurrent();
        CurrentUser = identity.Name;
        IsAdministrator = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public bool IsAdministrator { get; }
    public string CurrentUser { get; }
}
