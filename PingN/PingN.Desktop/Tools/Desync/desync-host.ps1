param(
    [Parameter(Mandatory = $true)]
    [string]$RequestPath
)

$ErrorActionPreference = 'Stop'
$request = Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json
$pipe = $null
$reader = $null
$writer = $null
$engine = $null
$stdoutTask = $null
$stderrTask = $null
$exitCode = 1
$stdoutWriter = $null
$stderrWriter = $null

try {
    $pipe = [System.IO.Pipes.NamedPipeServerStream]::new(
        $request.PipeName,
        [System.IO.Pipes.PipeDirection]::InOut,
        1,
        [System.IO.Pipes.PipeTransmissionMode]::Byte,
        [System.IO.Pipes.PipeOptions]::None
    )
    if (-not $pipe.WaitForConnectionAsync().Wait([TimeSpan]::FromSeconds(30))) {
        throw 'PingN did not connect to the Desync control pipe.'
    }
    $reader = [System.IO.StreamReader]::new($pipe, [System.Text.Encoding]::UTF8, $false, 1024, $true)
    $writer = [System.IO.StreamWriter]::new($pipe, [System.Text.UTF8Encoding]::new($false), 1024, $true)
    $writer.AutoFlush = $true

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = [string]$request.ExecutablePath
    $startInfo.Arguments = [string]$request.Arguments
    $startInfo.WorkingDirectory = [string]$request.WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $engine = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $engine) { throw 'Could not start winws2.exe.' }
    $stdoutWriter = [System.IO.StreamWriter]::new([string]$request.StdoutPath, $false, [System.Text.UTF8Encoding]::new($false))
    $stderrWriter = [System.IO.StreamWriter]::new([string]$request.StderrPath, $false, [System.Text.UTF8Encoding]::new($false))
    $stdoutWriter.AutoFlush = $true
    $stderrWriter.AutoFlush = $true
    $stdoutTask = $engine.StandardOutput.ReadLineAsync()
    $stderrTask = $engine.StandardError.ReadLineAsync()
    Start-Sleep -Milliseconds 600
    if ($engine.HasExited) {
        $writer.WriteLine("ERROR:winws2 exited with code $($engine.ExitCode).")
        $exitCode = $engine.ExitCode
    } else {
        $writer.WriteLine('READY')
        $readTask = $reader.ReadLineAsync()
        while (-not $engine.HasExited) {
            if ($null -ne $stdoutTask -and $stdoutTask.IsCompleted) {
                $line = $stdoutTask.GetAwaiter().GetResult()
                if ($null -ne $line) { $stdoutWriter.WriteLine($line); $stdoutTask = $engine.StandardOutput.ReadLineAsync() }
                else { $stdoutTask = $null }
            }
            if ($null -ne $stderrTask -and $stderrTask.IsCompleted) {
                $line = $stderrTask.GetAwaiter().GetResult()
                if ($null -ne $line) { $stderrWriter.WriteLine($line); $stderrTask = $engine.StandardError.ReadLineAsync() }
                else { $stderrTask = $null }
            }
            if ($readTask.Wait(200)) {
                if ($readTask.Result -eq 'STOP') {
                    $engine.Kill()
                    $engine.WaitForExit()
                    $writer.WriteLine('STOPPED')
                    $exitCode = 0
                    break
                }
                if ($null -eq $readTask.Result) {
                    $engine.Kill()
                    $engine.WaitForExit()
                    $exitCode = 0
                    break
                }
                $readTask = $reader.ReadLineAsync()
            }
        }
        if ($engine.HasExited -and $exitCode -ne 0) {
            $writer.WriteLine("EXIT:$($engine.ExitCode)")
            $exitCode = $engine.ExitCode
        }
    }
}
catch {
    if ($null -ne $writer) {
        try { $writer.WriteLine("ERROR:$($_.Exception.Message)") } catch {}
    }
    $exitCode = 1
}
finally {
    if ($null -ne $engine) {
        try {
            if (-not $engine.HasExited) {
                $engine.Kill()
                $engine.WaitForExit()
            }
        } catch {}
        try {
            if ($null -ne $stdoutTask) {
                $line = $stdoutTask.GetAwaiter().GetResult()
                if ($null -ne $line) { $stdoutWriter.WriteLine($line) }
            }
            $stdoutWriter.Write($engine.StandardOutput.ReadToEnd())
        } catch {}
        try {
            if ($null -ne $stderrTask) {
                $line = $stderrTask.GetAwaiter().GetResult()
                if ($null -ne $line) { $stderrWriter.WriteLine($line) }
            }
            $stderrWriter.Write($engine.StandardError.ReadToEnd())
        } catch {}
        $engine.Dispose()
    }
    if ($null -ne $stdoutWriter) { $stdoutWriter.Dispose() }
    if ($null -ne $stderrWriter) { $stderrWriter.Dispose() }
    if ($null -ne $writer) { $writer.Dispose() }
    if ($null -ne $reader) { $reader.Dispose() }
    if ($null -ne $pipe) { $pipe.Dispose() }
}

exit $exitCode
