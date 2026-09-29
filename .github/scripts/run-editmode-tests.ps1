# Runs the ModelLibrary.Tests Edit Mode suite.
# Unity must already be activated in Unity Hub on this machine.
# Do not pass -quit with -runTests; the test runner exits on its own.
# Compiler warnings are not failed here. Phase 5E owns that gate.

param(
    [Parameter(Mandatory = $true)]
    [string] $UnityVersion,

    [string] $PackageRoot,

    [string] $ProjectPath,

    [string] $ResultsPath,

    [string] $LogPath
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
    $PackageRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

if ([string]::IsNullOrWhiteSpace($ResultsPath)) {
    $ResultsPath = Join-Path $env:TEMP ("ModelLibrary-" + $UnityVersion + "-results.xml")
}

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $env:TEMP ("ModelLibrary-" + $UnityVersion + "-editor.log")
}

$editor = Join-Path $env:ProgramFiles (Join-Path 'Unity\Hub\Editor' (Join-Path $UnityVersion 'Editor\Unity.exe'))
if (-not (Test-Path -LiteralPath $editor)) {
    Write-Output ("Unity " + $UnityVersion + " is not installed at " + $editor + ". Activate that Editor in Unity Hub on the self-hosted runner.")
    exit 3
}

$createdProject = $false
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $env:TEMP ("ModelsLibraryCI_" + $UnityVersion)
    if (Test-Path -LiteralPath $ProjectPath) {
        Remove-Item -LiteralPath $ProjectPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path (Join-Path $ProjectPath 'ProjectSettings') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $ProjectPath 'Assets') -Force | Out-Null
    $versionFile = Join-Path $ProjectPath (Join-Path 'ProjectSettings' 'ProjectVersion.txt')
    Set-Content -LiteralPath $versionFile -Value ("m_EditorVersion: " + $UnityVersion) -Encoding ascii
    $packageLink = Join-Path $ProjectPath (Join-Path 'Assets' 'ModelLibrary')
    New-Item -ItemType Junction -Path $packageLink -Target $PackageRoot | Out-Null
    $createdProject = $true
}

Write-Output ("Unity: " + $editor)
Write-Output ("Project: " + $ProjectPath)
Write-Output ("Results: " + $ResultsPath)
Write-Output ("Log: " + $LogPath)
Write-Output ("CreatedProject: " + $createdProject)

$argumentList = @(
    '-batchmode',
    '-nographics',
    '-projectPath', $ProjectPath,
    '-runTests',
    '-testPlatform', 'EditMode',
    '-assemblyNames', 'ModelLibrary.Tests',
    '-testResults', $ResultsPath,
    '-logFile', $LogPath
)

$process = Start-Process -FilePath $editor -ArgumentList $argumentList -Wait -PassThru
Write-Output ("EXIT " + $process.ExitCode)
exit $process.ExitCode
