# Installs com.models-library from the repository-root Git URL into a new project,
# saves project settings, restarts the Editor, reinstalls the package, and checks
# that the saved value is still there.
#
# The project is created at C:\UnityGit\CleanGitInstall4C. Unity 6000.6.0f1
# rejected the same layout under AppData\Local\Temp and prefixed the current
# directory onto that path.
# Tag v1.0.12 is not created. The URL is the repository root, which resolves to
# the default branch. Do not add ?path=Assets/ModelLibrary.
# Unity Hub on this machine holds the license. Do not pass -quit with -executeMethod.

param(
    [string] $UnityVersion = '6000.6.0f1',
    [string] $GitUrl = 'https://github.com/PierreGac/Unity-ModelsLibrary.git',
    [string] $ProjectPath = 'C:\UnityGit\CleanGitInstall4C',
    [string] $LogDirectory = 'C:\UnityGit\CleanGitInstall4C-logs'
)

$ErrorActionPreference = 'Stop'
$Sentinel = 'CleanGitInstall_4C_sentinel'
$SubfolderQuery = '?path=Assets/ModelLibrary'

$editor = Join-Path $env:ProgramFiles (Join-Path 'Unity\Hub\Editor' (Join-Path $UnityVersion 'Editor\Unity.exe'))
if (-not (Test-Path -LiteralPath $editor)) {
    Write-Output ("Unity " + $UnityVersion + " is not installed at " + $editor)
    exit 3
}

if ($GitUrl.Contains($SubfolderQuery)) {
    Write-Output ("Refusing Git URL that contains " + $SubfolderQuery)
    exit 4
}

if (Test-Path -LiteralPath $ProjectPath) {
    Remove-Item -LiteralPath $ProjectPath -Recurse -Force
}

New-Item -ItemType Directory -Path (Join-Path $ProjectPath 'ProjectSettings') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $ProjectPath 'Packages') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $ProjectPath 'Assets') -Force | Out-Null
New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null

$versionFile = Join-Path $ProjectPath (Join-Path 'ProjectSettings' 'ProjectVersion.txt')
Set-Content -LiteralPath $versionFile -Value ("m_EditorVersion: " + $UnityVersion) -Encoding ascii

$manifestPath = Join-Path $ProjectPath (Join-Path 'Packages' 'manifest.json')
$manifest = @"
{
  "dependencies": {
    "com.models-library": "$GitUrl",
    "com.unity.modules.imgui": "1.0.0",
    "com.unity.modules.jsonserialize": "1.0.0",
    "com.unity.modules.ui": "1.0.0",
    "com.unity.modules.uielements": "1.0.0",
    "com.unity.modules.unitywebrequest": "1.0.0"
  }
}
"@
Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding ascii

function Start-UnityEditor {
    param(
        [string] $LogName,
        [string[]] $ExtraArguments
    )

    $logPath = Join-Path $LogDirectory $LogName
    $argumentList = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $ProjectPath,
        '-logFile', $logPath
    )
    if ($ExtraArguments -ne $null) {
        $argumentList += $ExtraArguments
    }

    Write-Output ("Unity " + $LogName)
    $process = Start-Process -FilePath $editor -ArgumentList $argumentList -Wait -PassThru
    Write-Output ("EXIT " + $process.ExitCode + " " + $logPath)
    if ($process.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $logPath) {
            Get-Content -LiteralPath $logPath -Tail 40
        }
        exit $process.ExitCode
    }
}

function Assert-SentinelFile {
    param([string] $Stage)

    $settingsPath = Join-Path $ProjectPath (Join-Path 'ProjectSettings' 'ModelLibrarySettings.json')
    if (-not (Test-Path -LiteralPath $settingsPath)) {
        Write-Output ($Stage + " did not leave ProjectSettings/ModelLibrarySettings.json")
        exit 5
    }

    $settingsJson = Get-Content -LiteralPath $settingsPath -Raw
    if ($settingsJson.Contains($Sentinel) -eq $false) {
        Write-Output ($Stage + " lost the saved repository root")
        exit 5
    }
}

Start-UnityEditor -LogName '01-resolve.log' -ExtraArguments @('-quit')

$lockPath = Join-Path $ProjectPath (Join-Path 'Packages' 'packages-lock.json')
if (-not (Test-Path -LiteralPath $lockPath)) {
    Write-Output 'Package resolution did not write Packages/packages-lock.json'
    exit 6
}

$lockJson = Get-Content -LiteralPath $lockPath -Raw
if ($lockJson.Contains($GitUrl) -eq $false) {
    Write-Output 'packages-lock.json does not record the root Git URL'
    exit 6
}

if ($lockJson.Contains($SubfolderQuery)) {
    Write-Output 'packages-lock.json contains the old subfolder query'
    exit 6
}

$packageCache = Join-Path $ProjectPath (Join-Path 'Library' 'PackageCache')
$resolvedList = @(Get-ChildItem -LiteralPath $packageCache -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name.StartsWith('com.models-library@') })
if ($resolvedList.Count -lt 1) {
    Write-Output 'The Git package was not copied into Library/PackageCache'
    exit 6
}

$resolvedRoot = $resolvedList[0].FullName
if (-not (Test-Path -LiteralPath (Join-Path $resolvedRoot 'package.json'))) {
    Write-Output 'The resolved package has no root package.json'
    exit 6
}

$editorDir = Join-Path $ProjectPath (Join-Path 'Assets' 'Editor')
New-Item -ItemType Directory -Path $editorDir -Force | Out-Null
$probePath = Join-Path $editorDir 'CleanGitInstallProbe.cs'
$probe = @"
using System;
using System.IO;
using ModelLibrary.Editor.Settings;
using UnityEditor;
using UnityEngine;

public static class CleanGitInstallProbe
{
    private const string SENTINEL = "$Sentinel";

    public static void SaveSentinel()
    {
        try
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            settings.repositoryRoot = SENTINEL;
            settings.SaveProjectCopy();
            if (PackageContainsSentinel())
            {
                Debug.LogError("The resolved package contains the project settings value.");
                EditorApplication.Exit(4);
                return;
            }

            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError(exception.ToString());
            EditorApplication.Exit(1);
        }
    }

    public static void AssertAfterRestart()
    {
        try
        {
            ModelLibrarySettings settings = ModelLibrarySettings.GetOrCreate();
            if (settings.repositoryRoot != SENTINEL)
            {
                Debug.LogError("Project settings did not survive reload. repositoryRoot=" + settings.repositoryRoot);
                EditorApplication.Exit(5);
                return;
            }

            if (PackageContainsSentinel())
            {
                Debug.LogError("The resolved package contains the project settings value.");
                EditorApplication.Exit(4);
                return;
            }

            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError(exception.ToString());
            EditorApplication.Exit(1);
        }
    }

    private static bool PackageContainsSentinel()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string cache = Path.Combine(projectRoot, "Library", "PackageCache");
        if (!Directory.Exists(cache))
        {
            return false;
        }

        string[] directories = Directory.GetDirectories(cache, "com.models-library@*");
        for (int i = 0; i < directories.Length; i++)
        {
            string[] files = Directory.GetFiles(directories[i], "*", SearchOption.AllDirectories);
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string extension = Path.GetExtension(files[fileIndex]).ToLowerInvariant();
                if (extension != ".json" && extension != ".cs" && extension != ".asset" && extension != ".md" && extension != ".txt" && extension != ".yml" && extension != ".yaml" && extension != ".asmdef" && extension != ".meta")
                {
                    continue;
                }

                string text = File.ReadAllText(files[fileIndex]);
                if (text.Contains(SENTINEL))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
"@
Set-Content -LiteralPath $probePath -Value $probe -Encoding UTF8

Start-UnityEditor -LogName '02-save.log' -ExtraArguments @('-executeMethod', 'CleanGitInstallProbe.SaveSentinel')
Assert-SentinelFile -Stage 'Save'

Start-UnityEditor -LogName '03-restart.log' -ExtraArguments @('-executeMethod', 'CleanGitInstallProbe.AssertAfterRestart')
Assert-SentinelFile -Stage 'Restart'

Get-ChildItem -LiteralPath $packageCache -Directory | Where-Object { $_.Name.StartsWith('com.models-library@') } | ForEach-Object {
    Remove-Item -LiteralPath $_.FullName -Recurse -Force
}

Start-UnityEditor -LogName '04-reinstall.log' -ExtraArguments @('-quit')
Start-UnityEditor -LogName '05-after-reinstall.log' -ExtraArguments @('-executeMethod', 'CleanGitInstallProbe.AssertAfterRestart')
Assert-SentinelFile -Stage 'Reinstall'

Write-Output ('Clean Git install passed. Project: ' + $ProjectPath)
exit 0
