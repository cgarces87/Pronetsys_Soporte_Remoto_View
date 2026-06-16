using System.Runtime.Versioning;
using System.Security.Principal;

namespace Pronetsys.Shared.Services;

[SupportedOSPlatform("windows")]
public class ElevationDetectorWin : IElevationDetector
{
    public bool IsElevated()
    {
       return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
