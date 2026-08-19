#!/usr/bin/env bash
set -euo pipefail
REGION=ap-northeast-1
LAMBDA_NAME=trellis2-daily-gpu-status
ROOT="$(cd "$(dirname "$0")" && pwd)"
echo '{}' > "${ROOT}/payload.json"
aws --region "$REGION" lambda invoke --function-name "$LAMBDA_NAME" --cli-binary-format raw-in-base64-out --payload "file://${ROOT}/payload.json" "${ROOT}/invoke-out.json"
cat "${ROOT}/invoke-out.json"
echo
