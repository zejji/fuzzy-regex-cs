<#
.SYNOPSIS
    What the built-in Regex.CacheSize actually does, measured rather than quoted.

.DESCRIPTION
    S59 copies Regex.CacheSize's shape, so its default, its setter validation and its
    trimming behaviour have to be observed on this runtime, not read off a docs page.
    The public API cannot say how many entries the cache holds, so the live count is read
    by reflection out of System.Text.RegularExpressions.RegexCache - internal, and only
    ever inspected here.

    Run: pwsh -NoProfile -File tools/probes/bcl-regex-cachesize.ps1
#>

$ErrorActionPreference = 'Stop'

$cacheType = [System.Text.RegularExpressions.Regex].Assembly.GetType(
    'System.Text.RegularExpressions.RegexCache'
)

Write-Output "runtime: $([System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription)"
Write-Output "RegexCache type found: $($null -ne $cacheType)"

$flags = [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Static
$fields = $cacheType.GetFields($flags)
Write-Output "RegexCache static fields: $(($fields | ForEach-Object { $_.Name }) -join ', ')"

function Get-LiveCount {
    # The dictionary is the authoritative count of cached entries; the list is the MRU order.
    $dictField = $cacheType.GetFields(
        [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Static
    ) | Where-Object { $_.Name -like '*ictionary*' } | Select-Object -First 1
    $listField = $cacheType.GetFields(
        [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Static
    ) | Where-Object { $_.Name -like '*List*' } | Select-Object -First 1

    $dict = if ($dictField) { $dictField.GetValue($null) } else { $null }
    $list = if ($listField) { $listField.GetValue($null) } else { $null }

    $dictCount = if ($null -eq $dict) { 'null' } else { $dict.Count }
    $listCount = if ($null -eq $list) { 'null' } else { $list.Count }
    "dict=$dictCount list=$listCount"
}

Write-Output ''
Write-Output "1. default CacheSize: $([System.Text.RegularExpressions.Regex]::CacheSize)"

# Fill past the default bound with distinct static-call patterns.
foreach ($i in 1..20) {
    [void][System.Text.RegularExpressions.Regex]::IsMatch('subject', "p$i")
}
Write-Output "2. after 20 distinct static patterns at CacheSize=$([System.Text.RegularExpressions.Regex]::CacheSize): $(Get-LiveCount)"

[System.Text.RegularExpressions.Regex]::CacheSize = 5
Write-Output "3. immediately after CacheSize = 5 (no further calls): $(Get-LiveCount)"

[System.Text.RegularExpressions.Regex]::CacheSize = 0
Write-Output "4. immediately after CacheSize = 0 (no further calls): $(Get-LiveCount)"

[void][System.Text.RegularExpressions.Regex]::IsMatch('subject', 'q1')
Write-Output "5. after one static call while CacheSize = 0: $(Get-LiveCount)"

[System.Text.RegularExpressions.Regex]::CacheSize = 15
try {
    [System.Text.RegularExpressions.Regex]::CacheSize = -1
    Write-Output '6. CacheSize = -1 did NOT throw'
}
catch {
    $inner = $_.Exception
    if ($inner -is [System.Management.Automation.SetValueInvocationException] -and $inner.InnerException) {
        $inner = $inner.InnerException
    }
    Write-Output "6. CacheSize = -1 threw: $($inner.GetType().FullName): $($inner.Message)"
}
Write-Output "7. CacheSize after the failed set: $([System.Text.RegularExpressions.Regex]::CacheSize)"
