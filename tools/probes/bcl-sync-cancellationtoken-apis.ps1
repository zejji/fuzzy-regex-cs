# S51 evidence. Which SYNCHRONOUS (non-Task-returning) BCL APIs take a CancellationToken, and
# where in the signature do they put it? Reflection over the running framework, so the answer is
# the framework's rather than a recollection of it.
#
# Run:  pwsh -File tools/probes/bcl-sync-cancellationtoken-apis.ps1
#
# A method is counted as SYNCHRONOUS when its return type is not Task, Task<T>, ValueTask,
# ValueTask<T> or IAsyncEnumerable<T>. "last" is true when the CancellationToken is the final
# parameter, which is what CA1068 asks for.

$types = @(
    [System.Threading.SemaphoreSlim]
    [System.Threading.ManualResetEventSlim]
    [System.Threading.CountdownEvent]
    [System.Threading.Tasks.Task]
    [System.Threading.Tasks.Parallel]
    [System.Threading.Tasks.ParallelOptions]
    [System.Collections.Concurrent.BlockingCollection[int]]
    [System.Linq.ParallelEnumerable]
    [System.IO.Stream]
    [System.Text.Json.JsonSerializer]
    [System.Text.RegularExpressions.Regex]
    [System.Threading.Channels.Channel]
)

# Matched against Namespace.Name, NOT FullName: FullName is null for an open generic return such
# as ValueTask<TValue>, which silently classified every JsonSerializer.DeserializeAsync overload
# as synchronous on this probe's first run.
$asyncReturns = @(
    'System.Threading.Tasks.Task'
    'System.Threading.Tasks.Task`1'
    'System.Threading.Tasks.ValueTask'
    'System.Threading.Tasks.ValueTask`1'
    'System.Collections.Generic.IAsyncEnumerable`1'
)

$rows = foreach ($type in $types) {
    $methods = $type.GetMethods([System.Reflection.BindingFlags]::Public -bor
        [System.Reflection.BindingFlags]::Instance -bor
        [System.Reflection.BindingFlags]::Static)

    foreach ($method in $methods) {
        $parameters = $method.GetParameters()
        $index = -1
        for ($i = 0; $i -lt $parameters.Length; $i++) {
            if ($parameters[$i].ParameterType -eq [System.Threading.CancellationToken]) { $index = $i }
        }
        if ($index -lt 0) { continue }

        $returnName = "$($method.ReturnType.Namespace).$($method.ReturnType.Name)"
        $isAsync = $asyncReturns -contains $returnName

        [pscustomobject]@{
            Type     = $type.Name
            Method   = $method.Name
            Sync     = -not $isAsync
            Position = "$($index + 1)/$($parameters.Length)"
            Last     = ($index -eq $parameters.Length - 1)
            Optional = $parameters[$index].IsOptional
            Return   = $method.ReturnType.Name
        }
    }
}

Write-Output "=== SYNCHRONOUS BCL methods taking a CancellationToken ==="
$rows | Where-Object Sync | Sort-Object Type, Method | Format-Table -AutoSize | Out-String -Width 160

$sync = @($rows | Where-Object Sync)
$syncLast = @($sync | Where-Object Last)
$syncOptional = @($sync | Where-Object Optional)
Write-Output "sync methods with a token: $($sync.Count)"
Write-Output "  of those, token is the LAST parameter: $($syncLast.Count)"
Write-Output "  of those, token is OPTIONAL (= default): $($syncOptional.Count)"

Write-Output ""
Write-Output "=== Regex: any member taking a TimeSpan matchTimeout ==="
$timeoutMembers = [System.Text.RegularExpressions.Regex].GetMethods([System.Reflection.BindingFlags]::Public -bor
    [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static) |
    Where-Object { $_.GetParameters() | Where-Object { $_.Name -eq 'matchTimeout' } } |
    ForEach-Object {
        $parameters = $_.GetParameters()
        $index = [array]::FindIndex($parameters, [Predicate[System.Reflection.ParameterInfo]] { $args[0].Name -eq 'matchTimeout' })
        [pscustomobject]@{
            Member   = $_.Name
            Static   = $_.IsStatic
            Position = "$($index + 1)/$($parameters.Length)"
            Last     = ($index -eq $parameters.Length - 1)
            Optional = $parameters[$index].IsOptional
        }
    }
$timeoutMembers | Sort-Object Member, Position | Format-Table -AutoSize | Out-String -Width 160
Write-Output "Regex static methods with a matchTimeout: $(@($timeoutMembers).Count)"
Write-Output "Regex INSTANCE methods with a matchTimeout: $(@($timeoutMembers | Where-Object { -not $_.Static }).Count)"
