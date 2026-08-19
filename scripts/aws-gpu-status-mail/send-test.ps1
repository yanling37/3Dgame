#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$Region = "ap-northeast-1"
$LambdaName = "trellis2-daily-gpu-status"
$Root = $PSScriptRoot
$payload = Join-Path $Root "payload.json"
$outfile = Join-Path $Root "invoke-out.json"
Set-Content -Path $payload -Value "{}" -Encoding ascii
aws --region $Region lambda invoke --function-name $LambdaName --cli-binary-format raw-in-base64-out --payload "file://$payload" $outfile
Get-Content $outfile
Write-Host ""
