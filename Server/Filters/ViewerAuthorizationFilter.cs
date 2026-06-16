using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Pronetsys.Server.Services;

namespace Pronetsys.Server.Filters;

internal class ViewerAuthorizationFilter(
    IDataService _dataService,
    IOtpProvider _otpProvider,
    IRemoteControlSessionCache _sessionCache) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (await IsAuthorized(context))
        {
            return;
        }

        context.Result = new RedirectResult("/Account/Login");
    }

    private async Task<bool> IsAuthorized(AuthorizationFilterContext context)
    {
        var settings = await _dataService.GetSettings();
        if (!settings.RemoteControlRequiresAuthentication)
        {
            return true;
        }

        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            return true;
        }

        // Unauthenticated viewers may pass with a one-time password, but only when it
        // was minted for the exact device of the session they're requesting. This
        // prevents an OTP issued for one device/org from acting as a generic pass
        // into the viewer for a different session (cross-tenant skeleton key).
        var query = context.HttpContext.Request.Query;
        if (query.TryGetValue("otp", out var otp) &&
            query.TryGetValue("sessionId", out var sessionId) &&
            _sessionCache.TryGetValue($"{sessionId}", out var session) &&
            _otpProvider.OtpMatchesDevice($"{otp}", session.DeviceId))
        {
            return true;
        }

        return false;
    }
}
