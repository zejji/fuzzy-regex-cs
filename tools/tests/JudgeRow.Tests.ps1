<#
    judge-row.py pins this port's answer to a judged oracle row. JudgedDivergenceTests compares it in
    upstream's terms for change positions (D160), so where a DIVERGE block gives the answer in those
    terms on its own line, that line is the one pinned. These drive the tool with --dry-run over a
    made-up sweep stamped with this checkout's src/ tree, so nothing is written.
#>

BeforeAll {
    $script:ToolPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'judge-row.py'
    $script:RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $script:SrcTree = (git -C $script:RepoRoot rev-parse 'HEAD:src').Trim()
    $script:Label = '  port (upstream change positions, D160) '

    function script:Judge([string[]]$portLines) {
        $root = Join-Path ([System.IO.Path]::GetTempPath()) "judge-row-$([guid]::NewGuid().ToString('n'))"
        $sweep = Join-Path $root 'sweep-1'
        New-Item -ItemType Directory -Path $sweep | Out-Null
        $row = '{"generator": "fuzzy", "pattern": "(?r)(?:abc){s<=1}", "flags": 0, "namedLists": {}, "subject": "axc", "operation": "search", "codepointSpan": null, "outcome": {"kind": "nomatch"}}'
        Set-Content -LiteralPath (Join-Path $sweep 'wave.jsonl') -Value @('{"defaultVersion": 8192}', $row)
        $report = @('DIVERGE row 1 (fuzzy) search', '  upstream no match') + $portLines + @('', "port build: src $script:SrcTree")
        Set-Content -LiteralPath (Join-Path $sweep 'report.txt') -Value $report
        $data = Join-Path $root 'judged.jsonl'
        Set-Content -LiteralPath $data -Value '// Pending candidates: none-yet'
        try {
            $out = & python $script:ToolPath --seed 1 --row 1 --cause D160 --witness 'w' --dry-run `
                --sweeps $root --file $data 2>&1
            $LASTEXITCODE | Should -Be 0 -Because ($out -join "`n")
            ((($out -join '') | ConvertFrom-Json)).port
        }
        finally {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
    }
}

Describe 'judge-row.py' {
    It 'pins the port answer in upstream change positions when the block gives one' {
        Judge @('  port     (0,3) live', ($script:Label + '(0,3) upstream terms')) | Should -Be '(0,3) upstream terms'
    }

    It 'pins the port line when the block gives no other' {
        Judge @('  port     (0,3) live') | Should -Be '(0,3) live'
    }
}
