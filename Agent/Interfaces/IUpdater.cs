using System.Threading.Tasks;

namespace Pronetsys.Agent.Interfaces;

public interface IUpdater
{
    Task BeginChecking();
    Task CheckForUpdates();
    Task InstallLatestVersion();
}