using Muonroi.Governance.Abstractions.License;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Muonroi.Governance.License;

/// <summary>
/// Provides methods to detect various forms of tampering, including debuggers, 
/// hardware breakpoints, and method hooks.
/// </summary>
public sealed class AntiTamperDetector(LicenseConfigs configs)
{
    private readonly bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Performs a full anti-tamper check based on configuration.
    /// </summary>
    /// <returns>True if tampering is detected; otherwise, false.</returns>
    public bool DetectTampering()
    {
        if (IsDebuggerAttached()) return true;
        if (IsProfilerAttached()) return true;
        
        if (_isWindows && configs.EnableHardwareBreakpointDetection && !Environment.Is64BitProcess)
        {
            if (CheckHardwareBreakpoints()) return true;
        }

        return false;
    }

    private bool IsDebuggerAttached()
    {
        // Managed debugger check
        if (Debugger.IsAttached) return true;

        // Native debugger check (Windows only)
        if (_isWindows && IsDebuggerPresent()) return true;

        return false;
    }

    /// <summary>
    /// Detects if a managed or native profiler is attached via environment variables.
    /// Works on all platforms and architectures including 64-bit.
    /// </summary>
    private static bool IsProfilerAttached()
    {
        // .NET Framework and .NET Core profiler attach signal
        string? corProfiling = Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING");
        if (corProfiling == "1") return true;

        // .NET Core / .NET 5+ profiler attach signal
        string? coreclrProfiling = Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING");
        if (coreclrProfiling == "1") return true;

        return false;
    }

    private bool CheckHardwareBreakpoints()
    {
        if (!_isWindows) return false;
        if (Environment.Is64BitProcess) return false;

        try
        {
            // DR0-DR3 are used for hardware breakpoints.
            // If any of these registers are non-zero, a hardware breakpoint is set.
            THREAD_CONTEXT context = new()
            {
                ContextFlags = CONTEXT_DEBUG_REGISTERS
            };

            nint hThread = GetCurrentThread();
            if (GetThreadContext(hThread, ref context))
            {
                if (context.Dr0 != 0 || context.Dr1 != 0 || context.Dr2 != 0 || context.Dr3 != 0)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore errors in detection to avoid false positives/crashes
        }

        return false;
    }

    /// <summary>
    /// Checks if a specific method has been hooked (e.g., via JMP or INT3).
    /// </summary>
    public static bool IsMethodHooked(MethodBase method)
    {
        if (method == null) return false;

        try
        {
            nint handle = method.MethodHandle.GetFunctionPointer();
            byte firstByte = Marshal.ReadByte(handle);

            // 0xE9 = JMP (common for Detours/Harmony)
            // 0xCC = INT3 (software breakpoint)
            return firstByte == 0xE9 || firstByte == 0xCC;
        }
        catch
        {
            return false;
        }
    }

    #region P/Invoke

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    private static extern bool IsDebuggerPresent();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetThreadContext(IntPtr hThread, ref THREAD_CONTEXT lpContext);

    private const uint CONTEXT_DEBUG_REGISTERS = 0x00010010;

    [StructLayout(LayoutKind.Sequential)]
    private struct THREAD_CONTEXT
    {
        public uint ContextFlags;
        public uint Dr0;
        public uint Dr1;
        public uint Dr2;
        public uint Dr3;
        public uint Dr6;
        public uint Dr7;
        // Other fields omitted for brevity as we only need debug registers
    }

    #endregion
}
