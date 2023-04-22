using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace RecoilStarter
{
    public enum PROCESS_INFORMATION_CLASS : int
    {
        ProcessBasicInformation = 0,
        ProcessQuotaLimits,
        ProcessIoCounters,
        ProcessVmCounters,
        ProcessTimes,
        ProcessBasePriority,
        ProcessRaisePriority,
        ProcessDebugPort,
        ProcessExceptionPort,
        ProcessAccessToken,
        ProcessLdtInformation,
        ProcessLdtSize,
        ProcessDefaultHardErrorMode,
        ProcessIoPortHandlers,
        ProcessPooledUsageAndLimits,
        ProcessWorkingSetWatch,
        ProcessUserModeIOPL,
        ProcessEnableAlignmentFaultFixup,
        ProcessPriorityClass,
        ProcessWx86Information,
        ProcessHandleCount,
        ProcessAffinityMask,
        ProcessPriorityBoost,
        ProcessDeviceMap,
        ProcessSessionInformation,
        ProcessForegroundInformation,
        ProcessWow64Information,
        ProcessImageFileName,
        ProcessLUIDDeviceMapsEnabled,
        ProcessBreakOnTermination,
        ProcessDebugObjectHandle,
        ProcessDebugFlags,
        ProcessHandleTracing,
        ProcessIoPriority,
        ProcessExecuteFlags,
        ProcessResourceManagement,
        ProcessCookie,
        ProcessImageInformation,
        ProcessCycleTime,
        ProcessPagePriority,
        ProcessInstrumentationCallback,
        ProcessThreadStackAllocation,
        ProcessWorkingSetWatchEx,
        ProcessImageFileNameWin32,
        ProcessImageFileMapping,
        ProcessAffinityUpdateMode,
        ProcessMemoryAllocationMode,
        MaxProcessInfoClass
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_BASIC_INFORMATION
    {
        public int ExitStatus;
        public int PebBaseAddress;
        public int AffinityMask;
        public int BasePriority;
        public int UniqueProcessId;
        public int InheritedFromUniqueProcessId;
        public int Size
        {
            get { return 24; }
        }
    };

    public enum IOPriority
    {
        Critical = 4,
        High = 3,
        Low = 1,
        Normal = 2,
        VeryLow = 0,
    };

    class Win32
    {
        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtSetInformationProcess(int processHandle, PROCESS_INFORMATION_CLASS processInformationClass, ref int processInformation, uint processInformationLength);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtQueryInformationProcess(int processHandle, PROCESS_INFORMATION_CLASS processInformationClass, ref int processInformation, int processInformationLength, ref int returnLength);
    }
}
