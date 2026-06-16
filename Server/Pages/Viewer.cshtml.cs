using Pronetsys.Server.Filters;
using Pronetsys.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pronetsys.Server.Services;

namespace Pronetsys.Server.Pages;

[ServiceFilter(typeof(ViewerAuthorizationFilter))]
public class ViewerModel(IDataService _dataService) : PageModel
{
    public string FaviconUrl { get; } = "/images/favicon.png";
    public string LogoUrl { get; set; } = string.Empty;
    public string PageDescription { get; } = "Herramientas de soporte remoto de Pronetsys.";
    public string PageTitle { get; } = "Pronetsys Asistencia Remota";
    public string ThemeUrl { get; private set; } = string.Empty;
    public string UserDisplayName { get; private set; } = string.Empty;

    public async Task OnGet()
    {
        var theme = await GetTheme();

        ThemeUrl = theme switch
        {
            ViewerPageTheme.Dark => "/css/remote-control-dark.css",
            ViewerPageTheme.Light => "/css/remote-control-light.css",
            _ => "/css/remote-control-dark.css"
        };
        UserDisplayName = await GetUserDisplayName();
        LogoUrl = await GetLogoUrl();
    }

    private async Task<string> GetLogoUrl()
    {
        return await GetTheme() == ViewerPageTheme.Dark ?
           "/images/pronetsys-logo-white.png" :
           "/images/pronetsys-logo-color.png";
    }

    private Task<ViewerPageTheme> GetTheme()
    {
        // TODO: Implement light theme in new viewer design.
        return Task.FromResult(ViewerPageTheme.Dark);
        //if (User.Identity.IsAuthenticated)
        //{
        //    var user = _dataService.GetUserByNameWithOrg(User.Identity.Name);

        //    var userTheme = user.UserOptions.Theme switch
        //    {
        //        Theme.Light => ViewerPageTheme.Light,
        //        Theme.Dark => ViewerPageTheme.Dark,
        //        _ => ViewerPageTheme.Dark
        //    };
        //    return Task.FromResult(userTheme);
        //}

        //var appTheme = _appConfig.Theme switch
        //{
        //    Theme.Light => ViewerPageTheme.Light,
        //    Theme.Dark => ViewerPageTheme.Dark,
        //    _ => ViewerPageTheme.Dark
        //};
        //return Task.FromResult(appTheme);
    }

    private async Task<string> GetUserDisplayName()
    {
        if (string.IsNullOrWhiteSpace(User?.Identity?.Name))
        {
            return string.Empty;
        }

        var userResult = await _dataService.GetUserByName(User.Identity.Name);

        if (!userResult.IsSuccess)
        {
            return string.Empty;
        }

        var user = userResult.Value;
        var displayName = user.UserOptions?.DisplayName ?? user.UserName ?? string.Empty;
        return displayName;
    }
}
