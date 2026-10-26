# Shared bounded Docker invocation; no shell interpolation or native error forwarding.
function Invoke-OwnedProcess {
    param([string[]]$ProcessArguments, [string]$InputDocument = '', [int]$TimeoutSeconds = 180)
    $start = [System.Diagnostics.ProcessStartInfo]::new('docker')
    $start.UseShellExecute = $false
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardInputEncoding = [System.Text.UTF8Encoding]::new($false)
    foreach ($argument in $ProcessArguments) { $start.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $start
    $started = $false
    $output = $null
    $errors = $null
    try {
        if (-not $process.Start()) { throw 'Could not start the Docker command.' }
        $started = $true
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.Write($InputDocument)
        $process.StandardInput.Close()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            throw 'Docker command timed out. The run-owned initializer must be stopped before another attempt.'
        }
        # Native stderr can contain connection details; never forward it to the console.
        $null = $errors.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw 'Docker or initialization failed. Stop this disposable demo and inspect restricted diagnostics.' }
        return $output.GetAwaiter().GetResult().Trim()
    }
    finally {
        if ($started) {
            if (-not $process.HasExited) { $process.Kill($true) }
            $null = $process.WaitForExit(5000)
            foreach ($read in @($output, $errors)) {
                if ($null -ne $read) { try { $null = $read.GetAwaiter().GetResult() } catch { } }
            }
        }
        $process.Dispose()
    }
}

