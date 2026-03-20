using System.Security.Claims;

namespace LittleChat.Modules.Admin.API;

public interface IAdminAuthorizationService
{
    bool IsAdmin(ClaimsPrincipal user);
}
