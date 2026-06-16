using Microsoft.AspNetCore.Mvc;
using Pronetsys.Server.Auth;
using Pronetsys.Server.Extensions;
using Pronetsys.Server.Services;
using Pronetsys.Shared.Entities;

namespace Pronetsys.Server.API;

[Route("api/[controller]")]
[ApiController]
public class SavedScriptsController : ControllerBase
{
    private readonly IDataService _dataService;

    public SavedScriptsController(IDataService dataService)
    {
        _dataService = dataService;
    }

    [ServiceFilter(typeof(ExpiringTokenFilter))]
    [HttpGet("{scriptId}")]
    public async Task<ActionResult<SavedScript>> GetScript(Guid scriptId)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return NotFound();
        }

        var result = await _dataService.GetSavedScript(scriptId, orgId);
        if (!result.IsSuccess)
        {
            return NotFound();
        }

        return result.Value;
    }
}
