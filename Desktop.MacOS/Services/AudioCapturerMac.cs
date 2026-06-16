using Pronetsys.Desktop.Shared.Abstractions;

namespace Pronetsys.Desktop.MacOS.Services;

public class AudioCapturerMac : IAudioCapturer
{
#pragma warning disable CS0067
    public event EventHandler<byte[]>? AudioSampleReady;
#pragma warning restore

    public void ToggleAudio(bool toggleOn)
    {
        // Not implemented.  Audio capture on macOS requires a virtual audio
        // device (e.g. CoreAudio tap / ScreenCaptureKit audio); not yet ported.
    }
}
