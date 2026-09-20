<#
.SYNOPSIS
    Names the processes holding a file open, using the Windows Restart Manager.

.DESCRIPTION
    "The process cannot access the file ... because it is being used by another process" does not
    say which process, and guessing is expensive. Measured 2026-09-01: a locked
    `src/FuzzyRegex/obj/Debug/net10.0/FuzzyRegex.sourcelink.json` made every build fail, which made
    the ratchet RED with no test report, which made the S26 driver roll back a commit that was
    perfectly green. The commit had to be recovered from a tag and re-verified in a worktree.

    The lock was then attributed to Rider on circumstantial grounds - its ReSharper hosts were in
    the process list - and the owner restarted the IDE for nothing. This script named the real
    holders in one call: `VBCSCompiler` and `csc`, the Roslyn compiler server. Killing those two
    released it immediately.

    The clue had already been printed and skimmed past. `dotnet build-server shutdown` had said:

        MSBuild server shut down successfully.
        VB/C# compiler server failed to shut down: The shutdown command failed:

    So: run `dotnet build-server shutdown` first, because it is free, and READ BOTH LINES - a server
    that reports a failed shutdown is the first suspect, not noise. If the lock survives, run this
    rather than forming a theory.

    Uses the Restart Manager API (rstrtmgr.dll), which is what the "this file is open in..." dialogs
    use. Read-only: it names holders and never closes, kills or restarts anything.

.PARAMETER Path
    The file to ask about. Relative paths are resolved against the current directory.

.EXAMPLE
    tools/find-lock-holder.ps1 src/FuzzyRegex/obj/Debug/net10.0/FuzzyRegex.sourcelink.json

.EXAMPLE
    # The usual sequence when a build says a file is in use:
    dotnet build-server shutdown          # free, and read BOTH lines of its output
    tools/find-lock-holder.ps1 <the path the error named>
    # then stop only what it names. Compiler and build servers are safe to stop; they restart on demand.
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Path)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolved = (Resolve-Path -LiteralPath $Path).Path

# The Restart Manager is a Windows API. Elsewhere the interop below compiles but every call fails
# with an error that names nothing useful (CI on ubuntu and macos, 2026-09-20), so stop here.
if (-not $IsWindows) {
    throw "find-lock-holder.ps1 asks the Windows Restart Manager and cannot run on $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription). Use lsof or fuser here."
}

# The interop lives in a uniquely-named type so that running this twice in one PowerShell session
# does not fail on "type already exists".
$typeName = "RmLockProbe_$([guid]::NewGuid().ToString('N'))"
$source = @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public static class $typeName {
    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME { public uint dwLowDateTime; public uint dwHighDateTime; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RM_UNIQUE_PROCESS { public int dwProcessId; public FILETIME ProcessStartTime; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RM_PROCESS_INFO {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string strServiceShortName;
        public int ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)] public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    static extern int RmStartSession(out uint handle, int flags, string sessionKey);

    [DllImport("rstrtmgr.dll")]
    static extern int RmEndSession(uint handle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    static extern int RmRegisterResources(uint handle, uint nFiles, string[] files,
        uint nApplications, IntPtr applications, uint nServices, string[] services);

    [DllImport("rstrtmgr.dll")]
    static extern int RmGetList(uint handle, out uint nProcInfoNeeded, ref uint nProcInfo,
        [In, Out] RM_PROCESS_INFO[] rgAffectedApps, ref uint rebootReasons);

    public static List<int[]> Ids(string path, out List<string> names) {
        var ids = new List<int[]>();
        names = new List<string>();
        uint handle;
        if (RmStartSession(out handle, 0, Guid.NewGuid().ToString()) != 0) { return ids; }
        try {
            if (RmRegisterResources(handle, 1, new[] { path }, 0, IntPtr.Zero, 0, null) != 0) { return ids; }
            uint needed = 0, count = 0, reason = 0;
            RmGetList(handle, out needed, ref count, null, ref reason);
            if (needed == 0) { return ids; }
            count = needed;
            var info = new RM_PROCESS_INFO[count];
            if (RmGetList(handle, out needed, ref count, info, ref reason) != 0) { return ids; }
            for (int i = 0; i < count; i++) {
                ids.Add(new[] { info[i].Process.dwProcessId });
                names.Add(info[i].strAppName);
            }
        } finally { RmEndSession(handle); }
        return ids;
    }
}
"@

# -PassThru returns the compiled type. Looking it up by name afterwards does not work: the type
# lives in a generated assembly that [type]::GetType cannot resolve from a bare name.
$probe = @(Add-Type -TypeDefinition $source -Language CSharp -PassThru)[0]

$names = $null
$ids = $probe::Ids($resolved, [ref]$names)

if ($ids.Count -eq 0) {
    Write-Host "No process is holding $resolved." -ForegroundColor Green
    return
}

Write-Host "$($ids.Count) process(es) holding $resolved" -ForegroundColor Yellow
for ($i = 0; $i -lt $ids.Count; $i++) {
    Write-Host ("  pid {0,-8} {1}" -f $ids[$i][0], $names[$i]) -ForegroundColor DarkYellow
}
Write-Host "Stop only what you recognise. Compiler and build servers restart on demand; an IDE does not." -ForegroundColor DarkGray
