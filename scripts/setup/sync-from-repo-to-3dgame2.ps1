# Copy game content into a Hub-created URP project, then push to GitHub.
# Run in PowerShell on your PC.
#
# Example:
#   powershell -ExecutionPolicy Bypass -File scripts/setup/sync-from-repo-to-3dgame2.ps1
#   powershell -ExecutionPolicy Bypass -File scripts/setup/sync-from-repo-to-3dgame2.ps1 -Dest "D:\MyProject\3dgame2"

param(
    [string]$Dest = "D:\MyProject\3dgame2",
    [string]$RemoteUrl = "https://github.com/yanling37/3Dgame.git",
    [string]$Branch = "cursor/unity-blender-requirements-55cc",
    [switch]$SkipPush
)

$ErrorActionPreference = "Stop"
$Src = Resolve-Path (Join-Path $PSScriptRoot "..\..")

if (-not (Test-Path $Dest)) {
    throw "Destination not found: $Dest  (先用 Unity Hub 建好 3D URP 项目)"
}
if (-not (Test-Path (Join-Path $Dest "Assets"))) {
    throw "Not a Unity project (missing Assets): $Dest"
}

Write-Host "Source repo: $Src" -ForegroundColor Cyan
Write-Host "Dest project: $Dest" -ForegroundColor Cyan

function Copy-Tree($Relative) {
    $from = Join-Path $Src $Relative
    $to = Join-Path $Dest $Relative
    if (-not (Test-Path $from)) {
        Write-Host "[SKIP] missing $Relative" -ForegroundColor Yellow
        return
    }
    New-Item -ItemType Directory -Force -Path $to | Out-Null
    robocopy $from $to /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    Write-Host "[OK] $Relative"
}

# Game content
Copy-Tree "Assets\Scripts"
Copy-Tree "Assets\Scenes"
Copy-Tree "Assets\Art"
Copy-Tree "Assets\Input"
Copy-Tree "Assets\Prefabs"
Copy-Tree "ArtSource"
Copy-Tree "docs"
Copy-Tree "scripts"

# Root docs / ignore rules (keep Dest ProjectSettings from Hub — more reliable)
$rootFiles = @("README.md", ".gitignore", ".gitattributes")
foreach ($f in $rootFiles) {
    $from = Join-Path $Src $f
    if (Test-Path $from) {
        Copy-Item $from (Join-Path $Dest $f) -Force
        Write-Host "[OK] $f"
    }
}

# Ensure Unity ignore rules exist
$gitignore = Join-Path $Dest ".gitignore"
if (-not (Test-Path $gitignore)) {
    Copy-Item (Join-Path $Src ".gitignore") $gitignore -Force
}

Write-Host ""
Write-Host "Copy done. Dest keeps its own ProjectSettings/Packages from Hub URP template." -ForegroundColor Green

if ($SkipPush) {
    Write-Host "SkipPush set — not touching git remote."
    exit 0
}

Set-Location $Dest

if (-not (Test-Path (Join-Path $Dest ".git"))) {
    git init
    git checkout -b $Branch
    git remote add origin $RemoteUrl
} else {
    $existing = git remote get-url origin 2>$null
    if (-not $existing) {
        git remote add origin $RemoteUrl
    }
}

git lfs install 2>$null
git add -A
git status --short | Select-Object -First 40

$msg = "Sync Hub URP project 3dgame2 with game assets and docs"
git commit -m $msg 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Nothing new to commit or commit failed — continue push if branch exists." -ForegroundColor Yellow
}

git push -u origin $Branch
Write-Host ""
Write-Host "Pushed to $RemoteUrl branch $Branch" -ForegroundColor Green
Write-Host "Next: Unity Hub Open -> $Dest"
