using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TrafficStatistics.Core.Models;

namespace TrafficStatistics.Core.Services;

/// <summary>
/// Resolves process IDs to <see cref="ProcessInfo"/> instances with caching.
/// Handles processes that have already exited and periodically cleans up stale entries.
/// </summary>
public sealed class ProcessInfoService : IDisposable
{
    private readonly ConcurrentDictionary<int, ProcessInfo> _cache = new();
    private readonly Timer _cleanupTimer;

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS_PROCESS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
        public uint dwProcessId;
        public uint dwServiceFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ENUM_SERVICE_STATUS_PROCESS
    {
        public IntPtr lpServiceName;
        public IntPtr lpDisplayName;
        public SERVICE_STATUS_PROCESS ServiceStatusProcess;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool EnumServicesStatusEx(
        IntPtr hSCManager,
        int InfoLevel,
        int dwServiceType,
        int dwServiceState,
        IntPtr lpServices,
        int cbBufSize,
        out int pcbBytesNeeded,
        out int lpServicesReturned,
        ref int lpResumeHandle,
        string? pszGroupName);

    [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenSCManager(
        string? lpMachineName,
        string? lpDatabaseName,
        int dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr hSCObject);

    private const int SC_MANAGER_ENUMERATE_SERVICE = 0x0004;
    private const int SC_ENUM_PROCESS_INFO = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessInfoService"/> class.
    /// A background timer cleans up stale entries every 5 minutes.
    /// </summary>
    public ProcessInfoService()
    {
        _cleanupTimer = new Timer(
            CleanupStaleEntries,
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Resolves a PID to a <see cref="ProcessInfo"/>.
    /// Returns a cached entry if available; otherwise queries the OS.
    /// Returns <c>null</c> if the process has already exited and is not cached.
    /// </summary>
    /// <param name="pid">The process identifier to look up.</param>
    /// <returns>Process information, or <c>null</c> if unavailable.</returns>
    public ProcessInfo? GetProcessInfo(int pid)
    {
        if (_cache.TryGetValue(pid, out var cached))
        {
            if (cached.Name == "svchost.exe")
            {
                lock (_svcLock)
                {
                    if (_svchostServices.TryGetValue(pid, out var services) && 
                        !string.IsNullOrEmpty(services))
                    {
                        var newInfo = cached with { Name = $"svchost.exe ({services})" };
                        _cache[pid] = newInfo;
                        return newInfo;
                    }
                }
            }
            return cached;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            var processName = process.ProcessName;

            if (processName.Equals("svchost", StringComparison.OrdinalIgnoreCase))
            {
                UpdateSvchostServices();
                lock (_svcLock)
                {
                    if (_svchostServices.TryGetValue(pid, out var services) && 
                        !string.IsNullOrEmpty(services))
                    {
                        processName = $"svchost.exe ({services})";
                    }
                    else
                    {
                        processName = "svchost.exe";
                    }
                }
            }
            else if (!processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                processName += ".exe";
            }

            var info = new ProcessInfo(
                Pid: pid,
                Name: processName,
                Path: GetProcessPath(process),
                StartTime: GetProcessStartTime(process));

            _cache.TryAdd(pid, info);
            return info;
        }
        catch (ArgumentException)
        {
            // Process has already exited.
            return null;
        }
        catch (InvalidOperationException)
        {
            // Process has already exited between GetProcessById and accessing properties.
            return null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }

    private static string? GetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            // Access denied or 32/64-bit mismatch.
            return null;
        }
    }

    private static DateTime GetProcessStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private readonly object _svcLock = new();
    private readonly Dictionary<int, string> _svchostServices = new();
    private DateTime _lastSvcUpdate = DateTime.MinValue;

    private void UpdateSvchostServices()
    {
        lock (_svcLock)
        {
            if ((DateTime.UtcNow - _lastSvcUpdate).TotalSeconds < 15)
            {
                return;
            }
            _lastSvcUpdate = DateTime.UtcNow;
        }

        try
        {
            var tempSvc = GetServicesByPid();
            lock (_svcLock)
            {
                _svchostServices.Clear();
                foreach (var kvp in tempSvc)
                {
                    _svchostServices[kvp.Key] = string.Join(", ", kvp.Value);
                }
            }
        }
        catch
        {
            // Ignore failures
        }
    }

    private Dictionary<int, List<string>> GetServicesByPid()
    {
        var result = new Dictionary<int, List<string>>();
        IntPtr hScm = OpenSCManager(null, null, SC_MANAGER_ENUMERATE_SERVICE);
        if (hScm == IntPtr.Zero) return result;

        try
        {
            int bytesNeeded = 0;
            int servicesReturned = 0;
            int resumeHandle = 0;

            // First call to get the buffer size
            bool success = EnumServicesStatusEx(
                hScm,
                SC_ENUM_PROCESS_INFO,
                0x30, // SERVICE_WIN32 (SERVICE_WIN32_OWN_PROCESS | SERVICE_WIN32_SHARE_PROCESS)
                0x03, // SERVICE_STATE_ALL
                IntPtr.Zero,
                0,
                out bytesNeeded,
                out servicesReturned,
                ref resumeHandle,
                null);

            int err = Marshal.GetLastWin32Error();
            if (!success && (err == 234 || bytesNeeded > 0)) // ERROR_MORE_DATA or buffer size query
            {
                IntPtr buffer = Marshal.AllocHGlobal(bytesNeeded);
                try
                {
                    success = EnumServicesStatusEx(
                        hScm,
                        SC_ENUM_PROCESS_INFO,
                        0x30,
                        0x03,
                        buffer,
                        bytesNeeded,
                        out bytesNeeded,
                        out servicesReturned,
                        ref resumeHandle,
                        null);

                    if (success)
                    {
                        int structSize = Marshal.SizeOf<ENUM_SERVICE_STATUS_PROCESS>();
                        IntPtr current = buffer;
                        for (int i = 0; i < servicesReturned; i++)
                        {
                            var status = Marshal.PtrToStructure<ENUM_SERVICE_STATUS_PROCESS>(current);
                            int pid = (int)status.ServiceStatusProcess.dwProcessId;
                            if (pid > 0)
                            {
                                string? serviceName = Marshal.PtrToStringUni(status.lpServiceName);
                                if (!string.IsNullOrEmpty(serviceName))
                                {
                                    if (!result.TryGetValue(pid, out var list))
                                    {
                                        list = new List<string>();
                                        result[pid] = list;
                                    }
                                    list.Add(serviceName);
                                }
                            }
                            current = IntPtr.Add(current, structSize);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        catch
        {
            // Ignore
        }
        finally
        {
            CloseServiceHandle(hScm);
        }

        return result;
    }

    private void CleanupStaleEntries(object? state)
    {
        foreach (var (pid, _) in _cache)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                // Process still alive — keep the entry.
            }
            catch (ArgumentException)
            {
                // Process no longer exists — remove stale entry.
                _cache.TryRemove(pid, out _);
            }
        }
    }
}
