# SessionStart hook: report every workflow whose latest run on main failed.
#
# The scheduled oracle sweep (.github/workflows/oracle.yml) was red on every run from 2026-09-21 to
# 2026-10-08 and nobody looked, because a scheduled run has no push to watch. This puts the latest
# conclusion of each workflow in front of the session at start, resume and /clear. Silent when all
# are green; never blocks the session (no gh, no network: one line saying so).
$ErrorActionPreference = 'Stop'
try {
    $runs = gh run list --branch main --limit 40 --json workflowName,conclusion,status,event,databaseId,createdAt,headSha |
        ConvertFrom-Json
} catch {
    @{ systemMessage = "ci-status: could not query GitHub ($($_.Exception.Message))" } | ConvertTo-Json -Compress
    exit 0
}
$red = foreach ($group in ($runs | Where-Object status -eq 'completed' | Group-Object workflowName)) {
    $latest = $group.Group | Sort-Object createdAt -Descending | Select-Object -First 1
    if ($latest.conclusion -ne 'success') {
        "$($group.Name): $($latest.conclusion) ($($latest.event), run $($latest.databaseId), " +
            "$($latest.createdAt), $($latest.headSha.Substring(0, 8)))"
    }
}
if (-not $red) { exit 0 }
$text = "RED on main - look before other work (gh run view <id> --log-failed):`n" + ($red -join "`n")
@{
    systemMessage = $text
    hookSpecificOutput = @{ hookEventName = 'SessionStart'; additionalContext = $text }
} | ConvertTo-Json -Compress
