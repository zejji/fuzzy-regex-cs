<#
    tools/check-release-tag.ps1 is the first thing the release workflow runs on a pushed tag. A tag
    that does not name the version Directory.Build.props would pack must stop the release before
    anything is built, because nuget.org never lets a version number be used twice.

    The tests write their own small props files, so they do not change when the real version is
    bumped.
#>

BeforeAll {
    $script:ScriptPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'check-release-tag.ps1'

    function New-Props([string]$Body) {
        $path = Join-Path ([System.IO.Path]::GetTempPath()) "props-probe-$([guid]::NewGuid().ToString('n')).props"
        Set-Content -LiteralPath $path -Value "<Project>$Body</Project>"
        return $path
    }
}

Describe 'check-release-tag.ps1' {
    AfterEach {
        if ($script:Props) { Remove-Item -LiteralPath $script:Props -Force -ErrorAction SilentlyContinue }
    }

    It 'prints the version when the tag is v plus VersionPrefix' {
        $script:Props = New-Props '<PropertyGroup><VersionPrefix>1.0.0</VersionPrefix></PropertyGroup>'

        & $ScriptPath -Tag 'v1.0.0' -PropsPath $Props | Should -Be '1.0.0'
    }

    It 'finds VersionPrefix in a later PropertyGroup' {
        $script:Props = New-Props '<PropertyGroup><Nullable>enable</Nullable></PropertyGroup><PropertyGroup><VersionPrefix>1.2.3</VersionPrefix></PropertyGroup>'

        & $ScriptPath -Tag 'v1.2.3' -PropsPath $Props | Should -Be '1.2.3'
    }

    It 'rejects a tag that names a different version' {
        $script:Props = New-Props '<PropertyGroup><VersionPrefix>1.0.0</VersionPrefix></PropertyGroup>'

        { & $ScriptPath -Tag 'v1.0.1' -PropsPath $Props } | Should -Throw '*v1.0.1*expected v1.0.0*'
    }

    It 'rejects a tag without the v prefix' {
        $script:Props = New-Props '<PropertyGroup><VersionPrefix>1.0.0</VersionPrefix></PropertyGroup>'

        { & $ScriptPath -Tag '1.0.0' -PropsPath $Props } | Should -Throw '*expected v1.0.0*'
    }

    It 'rejects a pre-release tag, which the workflow would otherwise pack as the plain version' {
        $script:Props = New-Props '<PropertyGroup><VersionPrefix>1.0.0</VersionPrefix></PropertyGroup>'

        { & $ScriptPath -Tag 'v1.0.0-rc.1' -PropsPath $Props } | Should -Throw '*expected v1.0.0*'
    }

    It 'fails when the props file has no VersionPrefix' {
        $script:Props = New-Props '<PropertyGroup><Nullable>enable</Nullable></PropertyGroup>'

        { & $ScriptPath -Tag 'v1.0.0' -PropsPath $Props } | Should -Throw '*VersionPrefix*'
    }
}
