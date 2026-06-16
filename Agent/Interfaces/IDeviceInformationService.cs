using Pronetsys.Shared.Dtos;
using System.Threading.Tasks;

namespace Pronetsys.Agent.Interfaces;

public interface IDeviceInformationService
{
    Task<DeviceClientDto> CreateDevice(string deviceId, string orgId);
}
