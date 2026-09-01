<#
    tools/find-lock-holder.ps1 exists because a locked build file rolled back a green S26 commit on
    2026-09-01, and the lock was then misattributed to the IDE for want of a way to ask. A
    diagnostic nobody has watched work is not a diagnostic, so this holds a real lock and checks the
    script names the process holding it.
#>

BeforeAll {
    $script:ScriptPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'find-lock-holder.ps1'
}

Describe 'find-lock-holder.ps1' {
    It 'reports no holder for a file nothing has open' {
        $path = Join-Path ([System.IO.Path]::GetTempPath()) "lock-probe-$([guid]::NewGuid().ToString('n')).tmp"
        'unlocked' | Set-Content -LiteralPath $path
        try {
            $output = & pwsh -NoProfile -File $ScriptPath $path 2>&1 | Out-String
            $output | Should -Match 'No process is holding'
        }
        finally { Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue }
    }

    It 'names the process id and application holding an open file' {
        $path = Join-Path ([System.IO.Path]::GetTempPath()) "lock-probe-$([guid]::NewGuid().ToString('n')).tmp"
        'locked' | Set-Content -LiteralPath $path

        # A real exclusive handle, held by a child process for the length of the query. Holding it
        # from *this* process would work too, but a child is what the script actually meets in the
        # wild, and it proves the report is not somehow reading its own caller.
        $holder = Start-Process -FilePath 'pwsh' -PassThru -WindowStyle Hidden -ArgumentList @(
            '-NoProfile', '-Command',
            "`$fs = [System.IO.File]::Open('$path', 'Open', 'ReadWrite', 'None'); Start-Sleep -Seconds 30; `$fs.Dispose()"
        )
        try {
            # Give the child time to take the handle before asking who holds it.
            $deadline = (Get-Date).AddSeconds(15)
            do {
                Start-Sleep -Milliseconds 200
                $output = & pwsh -NoProfile -File $ScriptPath $path 2>&1 | Out-String
            } while ($output -match 'No process is holding' -and (Get-Date) -lt $deadline)

            $output | Should -Match 'process\(es\) holding'
            $output | Should -Match "pid\s+$($holder.Id)\b"
        }
        finally {
            Stop-Process -Id $holder.Id -Force -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 300
            Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        }
    }

    It 'fails loudly on a path that does not exist, rather than reporting no holder' {
        # The dangerous failure is a typo in the path reading as "nothing is holding it", which
        # sends the reader off to blame the wrong thing again.
        $missing = Join-Path ([System.IO.Path]::GetTempPath()) "lock-probe-missing-$([guid]::NewGuid().ToString('n')).tmp"
        $output = & pwsh -NoProfile -File $ScriptPath $missing 2>&1 | Out-String
        $output | Should -Not -Match 'No process is holding'
        $LASTEXITCODE | Should -Not -Be 0
    }
}
