using System.Runtime.InteropServices;

namespace Pronetsys.Desktop.Native.Linux;

public class Libc
{
    [DllImport("libc", SetLastError = true)]
    public static extern uint geteuid();
}
