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
        
        if (_isWindows && configs.EnableHardwareBreakpointDetection)
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

        try
        {
            nint hThread = GetCurrentThread();

            if (Environment.Is64BitProcess)
            {
                return CheckHardwareBreakpoints64(hThread);
            }

            // x86: use the compact struct with uint DR registers
            CONTEXT_X86 context = new()
            {
                ContextFlags = CONTEXT_I386_DEBUG_REGISTERS
            };

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
    /// Checks hardware breakpoints on x64 using the full CONTEXT64 structure (1232 bytes).
    /// DR0-DR3 are 64-bit registers at known offsets within the CONTEXT structure.
    /// AllocHGlobal on x64 Windows returns 16-byte aligned memory (required by CONTEXT64).
    /// </summary>
    private static bool CheckHardwareBreakpoints64(nint hThread)
    {
        const int CONTEXT64_SIZE = 1232;
        const int CONTEXT_FLAGS_OFFSET = 0x30;
        const int DR0_OFFSET = 0x68;

        nint pContext = Marshal.AllocHGlobal(CONTEXT64_SIZE);
        try
        {
            for (int i = 0; i < CONTEXT64_SIZE / IntPtr.Size; i++)
            {
                Marshal.WriteIntPtr(pContext + i * IntPtr.Size, IntPtr.Zero);
            }

            Marshal.WriteInt32(pContext + CONTEXT_FLAGS_OFFSET, unchecked((int)CONTEXT_AMD64_DEBUG_REGISTERS));

            if (GetThreadContextRaw(hThread, pContext))
            {
                long dr0 = Marshal.ReadInt64(pContext + DR0_OFFSET);
                long dr1 = Marshal.ReadInt64(pContext + DR0_OFFSET + 8);
                long dr2 = Marshal.ReadInt64(pContext + DR0_OFFSET + 16);
                long dr3 = Marshal.ReadInt64(pContext + DR0_OFFSET + 24);
                return dr0 != 0 || dr1 != 0 || dr2 != 0 || dr3 != 0;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pContext);
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
    private static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT_X86 lpContext);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetThreadContext")]
    private static extern bool GetThreadContextRaw(IntPtr hThread, IntPtr lpContext);

    // x86: CONTEXT_i386 (0x00010000) | CONTEXT_DEBUG_REGISTERS (0x10)
    private const uint CONTEXT_I386_DEBUG_REGISTERS = 0x00010010;

    // x64: CONTEXT_AMD64 (0x00100000) | CONTEXT_DEBUG_REGISTERS (0x10)
    private const uint CONTEXT_AMD64_DEBUG_REGISTERS = 0x00100010;

    [StructLayout(LayoutKind.Sequential)]
    private struct CONTEXT_X86
    {
        public uint ContextFlags;
        public uint Dr0;
        public uint Dr1;
        public uint Dr2;
        public uint Dr3;
        public uint Dr6;
        public uint Dr7;
    }

    #endregion
}
