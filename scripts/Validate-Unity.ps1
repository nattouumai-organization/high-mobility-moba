[CmdletBinding()]
param(
    [ValidateSet('Compile', 'EditMode')]
    [string]$Mode = 'Compile',

    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe',

    [string]$ProjectPath,

    [ValidateRange(30, 3600)]
    [int]$TimeoutSeconds = 600,

    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $PSScriptRoot '..\Game'
}

function Get-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return [System.IO.Path]::GetFullPath($Path)
}

function Test-UnityProcessForProject {
    param([Parameter(Mandatory = $true)][string]$AbsoluteProjectPath)

    try {
        $processes = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction Stop
    }
    catch {
        throw "Unable to inspect running Unity processes: $($_.Exception.Message)"
    }

    foreach ($process in $processes) {
        if ([string]::IsNullOrWhiteSpace($process.CommandLine)) {
            continue
        }

        if ($process.CommandLine.IndexOf($AbsoluteProjectPath, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Test-ValidationLog {
    param(
        [Parameter(Mandatory = $true)][string]$LogPath,
        [Parameter(Mandatory = $true)][ValidateSet('Compile', 'EditMode')][string]$ValidationMode
    )

    if (-not (Test-Path -LiteralPath $LogPath)) {
        return 'Unity log was not created.'
    }

    $log = Get-Content -LiteralPath $LogPath -Raw
    $patterns = @(
        '(?i)error CS\d+',
        '(?i)compilation failed',
        '(?i)fatal error',
        '(?i)license.*(?:license error|activation failed|no valid license|return code 198)',
        '(?i)return code 198'
    )

    foreach ($pattern in $patterns) {
        if ($log -match $pattern) {
            return "Unity log matched failure pattern: $pattern"
        }
    }

    if ($ValidationMode -eq 'Compile' -and $log -notmatch '(?i)Exiting batchmode successfully') {
        return 'Unity did not report a successful batchmode exit.'
    }

    return $null
}

function Test-EditModeResults {
    param([Parameter(Mandatory = $true)][string]$ResultsPath)

    if (-not (Test-Path -LiteralPath $ResultsPath)) {
        return 'EditMode test result XML was not created.'
    }

    try {
        [xml]$results = Get-Content -LiteralPath $ResultsPath -Raw
    }
    catch {
        return "EditMode test result XML could not be parsed: $($_.Exception.Message)"
    }

    $testCases = @($results.SelectNodes('//test-case'))
    if ($testCases.Count -eq 0) {
        return 'EditMode test result XML contains zero test cases.'
    }

    $run = $results.DocumentElement
    if ($run.Name -ne 'test-run' -or $run.result -ne 'Passed') {
        return 'EditMode test run did not pass.'
    }
    foreach ($attribute in @('total', 'passed', 'testcasecount')) {
        if ($run.GetAttribute($attribute) -ne [string]$testCases.Count) {
            return "EditMode result count mismatch: $attribute"
        }
    }
    foreach ($attribute in @('failed', 'inconclusive', 'skipped')) {
        if ($run.GetAttribute($attribute) -ne '0') {
            return "EditMode result contains nonzero or missing $attribute count."
        }
    }
    if (@($results.SelectNodes('//test-suite') | Where-Object { $_.result -ne 'Passed' }).Count -gt 0) {
        return 'An EditMode test suite did not pass (including setup/teardown).'
    }

    $failedCases = @($testCases | Where-Object { $_.result -ne 'Passed' })
    if ($failedCases.Count -gt 0) {
        $names = ($failedCases | ForEach-Object { $_.fullname }) -join ', '
        return "EditMode test cases did not all pass: $names"
    }

    return $null
}

$absoluteUnityPath = Get-AbsolutePath $UnityPath
$absoluteProjectPath = Get-AbsolutePath $ProjectPath

if (-not (Test-Path -LiteralPath $absoluteUnityPath -PathType Leaf)) {
    throw "Unity executable was not found: $absoluteUnityPath"
}

if (-not (Test-Path -LiteralPath $absoluteProjectPath -PathType Container)) {
    throw "Unity project directory was not found: $absoluteProjectPath"
}

$versionFile = Join-Path $absoluteProjectPath 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
    throw "Unity project version file was not found: $versionFile"
}

$projectVersion = Get-Content -LiteralPath $versionFile -Raw
if ($projectVersion -notmatch '(?m)^m_EditorVersion:\s*6000\.5\.4f1\s*$') {
    throw 'The project is not configured for Unity 6000.5.4f1.'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = [System.IO.Path]::GetTempPath()
}
# Always use a fresh child directory, including when an output parent is supplied.
$runId = 'high-mobility-moba-validation-' + [Guid]::NewGuid().ToString('N')
$OutputDirectory = Join-Path (Get-AbsolutePath $OutputDirectory) $runId

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$logPath = Join-Path $OutputDirectory "$Mode.log"
$resultsPath = Join-Path $OutputDirectory 'EditMode.xml'

$mutexName = 'Local\HighMobilityMoba-UnityValidation'
$mutex = New-Object System.Threading.Mutex($false, $mutexName)
$hasMutex = $false
$unityProcess = $null

try {
    try {
        $hasMutex = $mutex.WaitOne(0)
    }
    catch [System.Threading.AbandonedMutexException] {
        $hasMutex = $true
    }

    if (-not $hasMutex) {
        throw 'Another high-mobility-moba Unity validation is already running.'
    }

    if (Test-UnityProcessForProject $absoluteProjectPath) {
        throw "Unity is already running for project: $absoluteProjectPath"
    }

    $arguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath',
        $absoluteProjectPath,
        '-logFile',
        $logPath
    )

    if ($Mode -eq 'EditMode') {
        $arguments += @('-runTests', '-testPlatform', 'editmode', '-testResults', $resultsPath)
    }

    if ($Mode -eq 'Compile') {
        $arguments += '-quit'
    }

    # Start-Process joins ArgumentList into a command line on Windows PowerShell.
    # These arguments are paths or fixed switches; quote each and reject embedded quotes.
    $quotedArguments = foreach ($argument in $arguments) {
        if ($argument.Contains('"')) { throw 'Unity arguments cannot contain quotes.' }
        '"' + $argument.TrimEnd('\') + '"'
    }
    $unityProcess = Start-Process -FilePath $absoluteUnityPath -ArgumentList ($quotedArguments -join ' ') -WindowStyle Hidden -PassThru
    if (-not $unityProcess.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-Process -Id $unityProcess.Id -Force -ErrorAction SilentlyContinue
        throw "Unity validation timed out after $TimeoutSeconds seconds. Log: $logPath"
    }

    if ($unityProcess.ExitCode -ne 0) {
        throw "Unity exited with code $($unityProcess.ExitCode). Log: $logPath"
    }

    $logFailure = Test-ValidationLog $logPath $Mode
    if ($null -ne $logFailure) {
        throw $logFailure
    }

    if ($Mode -eq 'EditMode') {
        $resultsFailure = Test-EditModeResults $resultsPath
        if ($null -ne $resultsFailure) {
            throw $resultsFailure
        }
    }

    Write-Output "Validation passed: $Mode"
    Write-Output "Log: $logPath"
    if ($Mode -eq 'EditMode') {
        Write-Output "Results: $resultsPath"
    }
    exit 0
}
catch {
    Write-Error $_.Exception.Message -ErrorAction Continue
    if (Test-Path -LiteralPath $logPath) {
        Write-Error "Log: $logPath" -ErrorAction Continue
    }
    exit 1
}
finally {
    if ($null -ne $unityProcess -and -not $unityProcess.HasExited) {
        Stop-Process -Id $unityProcess.Id -Force -ErrorAction SilentlyContinue
    }

    if ($hasMutex) {
        $mutex.ReleaseMutex()
    }

    $mutex.Dispose()
}
