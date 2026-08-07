# Unity + Blender local setup for Windows (run in PowerShell)
# This script checks tooling and prints install steps. It does not silently download Unity.

$ErrorActionPreference = "Continue"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $Root

Write-Host "== 3Dgame Windows env check ==" -ForegroundColor Cyan
Write-Host "Repo: $Root"

function Test-Cmd($Name, $Command) {
    try {
        $null = Invoke-Expression $Command 2>$null
        Write-Host "[OK] $Name" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "[MISSING] $Name" -ForegroundColor Yellow
        return $false
    }
}

$ok = 0
if (Test-Cmd "git" "git --version") { $ok++ }
if (Test-Cmd "git-lfs" "git lfs version") { $ok++ }

$blender = Get-Command blender -ErrorAction SilentlyContinue
if ($blender) {
    Write-Host "[OK] blender" -ForegroundColor Green
    $ok++
} else {
    Write-Host "[MISSING] blender  -> https://www.blender.org/download/ (4.2 LTS)" -ForegroundColor Yellow
}

$unityHubPaths = @(
    "$env:ProgramFiles\Unity Hub\Unity Hub.exe",
    "$env:LOCALAPPDATA\Programs\Unity Hub\Unity Hub.exe"
)
$hub = $unityHubPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($hub) {
    Write-Host "[OK] Unity Hub ($hub)" -ForegroundColor Green
    $ok++
} else {
    Write-Host "[MISSING] Unity Hub -> https://unity.com/download" -ForegroundColor Yellow
}

$editorHint = "C:\Program Files\Unity\Hub\Editor\6000.0.50f1\Editor\Unity.exe"
if (Test-Path $editorHint) {
    Write-Host "[OK] Unity Editor 6000.0.50f1" -ForegroundColor Green
    $ok++
} else {
    Write-Host "[MISSING] Unity Editor 6000.0.50f1 (install via Hub, match ProjectSettings/ProjectVersion.txt)" -ForegroundColor Yellow
}

if (Test-Path "ProjectSettings\ProjectVersion.txt") {
    Write-Host "[OK] Unity project scaffold present" -ForegroundColor Green
    $ok++
}

Write-Host ""
Write-Host "Manual install checklist:" -ForegroundColor Cyan
Write-Host " 1. Install Unity Hub + login (Personal license OK)"
Write-Host " 2. Install Editor version 6000.0.50f1 with modules:"
Write-Host "      - Microsoft Visual Studio Community / IDE support"
Write-Host "      - Windows Build Support (IL2CPP)"
Write-Host " 3. Install Blender 4.2 LTS"
Write-Host " 4. In Unity Hub: Open -> select this repo folder ($Root)"
Write-Host " 5. Let Unity import packages (URP / Input System / Cinemachine / ProBuilder)"
Write-Host " 6. If prompted by Input System, restart Editor"
Write-Host " 7. Open Assets/Scenes/Level_01.unity"
Write-Host ""
Write-Host "Git LFS tip: git lfs install"
Write-Host "Docs: docs/开发环境.md"
Write-Host "Checks roughly OK count: $ok"
