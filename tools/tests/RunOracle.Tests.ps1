#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

<#
    run-oracle.ps1 prints a verdict line and its exit code is what CI and the driver read, so a RED
    that exits 0 would read as green. This drives the recorder-failure path with a stand-in `python`
    that always fails (the real failure seen on 2026-10-20: the Python first on PATH had no `regex`
    module), without recording or running anything. The consumer is never reached.
#>

BeforeAll {
    $script:ScriptPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'run-oracle.ps1'
}

Describe 'run-oracle.ps1 verdict and exit code' {
    It 'exits non-zero when it prints RED because the recorder failed' -Skip:(-not $IsWindows) {
        $fake = Join-Path ([System.IO.Path]::GetTempPath()) "fake-python-$([guid]::NewGuid().ToString('n'))"
        New-Item -ItemType Directory -Path $fake | Out-Null
        Set-Content -LiteralPath (Join-Path $fake 'python.cmd') -Value "@echo off`r`nexit /b 7`r`n"
        try {
            $env:PATH = "$fake$([System.IO.Path]::PathSeparator)$env:PATH"
            $output = & pwsh -NoProfile -File $ScriptPath -Seeds 7 -Count 1 2>&1 | Out-String
            $exit = $LASTEXITCODE
        }
        finally {
            $env:PATH = $env:PATH.Substring($fake.Length + 1)
            Remove-Item -LiteralPath $fake -Recurse -Force -ErrorAction SilentlyContinue
        }
        $output | Should -Match 'Oracle: RED - the recorder failed \(exit 7\)'
        $exit | Should -Not -Be 0
    }
}
