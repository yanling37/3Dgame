#Requires -Version 5.1
<#
.SYNOPSIS
  Deploy EventBridge Scheduler -> Lambda -> SNS daily GPU status mail.

.NOTES
  Does not stop EC2.
  Does not print AWS keys.
  SNS email subscription requires the mailbox owner to click Confirm.
#>
$ErrorActionPreference = "Stop"

$Region = "ap-northeast-1"
$InstanceId = "i-0dd866f52ad65195a"
$Email = "1815486243@qq.com"
$LambdaName = "trellis2-daily-gpu-status"
$TopicName = "trellis2-daily-gpu-status"
$ScheduleName = "trellis2-daily-gpu-status"
$LambdaRoleName = "trellis2-daily-gpu-status-lambda"
$SchedulerRoleName = "trellis2-daily-gpu-status-scheduler"
$Root = $PSScriptRoot

function Invoke-Aws {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
    & aws --region $Region @Args
    if ($LASTEXITCODE -ne 0) {
        throw "aws failed: aws --region $Region $($Args -join ' ')"
    }
}

Write-Host "=== check AWS identity (keys are not printed) ==="
$ident = aws --region $Region sts get-caller-identity --output json | ConvertFrom-Json
if (-not $ident.Account) { throw "AWS CLI is not logged in. Run aws configure or use an IAM role." }
$Account = $ident.Account
Write-Host "Account=$Account"
Write-Host "Region=$Region"
Write-Host "InstanceId=$InstanceId"
Write-Host "Email=$Email"

Write-Host "=== SNS topic ==="
$TopicArn = aws --region $Region sns list-topics --query "Topics[?ends_with(TopicArn, ':$TopicName')].TopicArn | [0]" --output text
if (-not $TopicArn -or $TopicArn -eq "None") {
    $TopicArn = aws --region $Region sns create-topic --name $TopicName --query TopicArn --output text
}
Write-Host "TopicArn=$TopicArn"

Write-Host "=== SNS email subscription ==="
$subs = aws --region $Region sns list-subscriptions-by-topic --topic-arn $TopicArn --output json | ConvertFrom-Json
$existing = @($subs.Subscriptions | Where-Object { $_.Protocol -eq "email" -and $_.Endpoint -eq $Email })
if ($existing.Count -eq 0) {
    aws --region $Region sns subscribe --topic-arn $TopicArn --protocol email --notification-endpoint $Email --return-subscription-arn | Out-Null
    Write-Host "Created pending SNS subscription for $Email"
} else {
    Write-Host "Subscription already present. Arn=$($existing[0].SubscriptionArn)"
}

Write-Host "=== IAM roles ==="
function Ensure-Role($Name, $TrustFile) {
    $arn = aws iam get-role --role-name $Name --query Role.Arn --output text 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $arn) {
        $arn = aws iam create-role --role-name $Name --assume-role-policy-document "file://$TrustFile" --query Role.Arn --output text
    }
    return $arn
}

$LambdaRoleArn = Ensure-Role $LambdaRoleName (Join-Path $Root "trust-lambda.json")
$SchedulerRoleArn = Ensure-Role $SchedulerRoleName (Join-Path $Root "trust-scheduler.json")
aws iam put-role-policy --role-name $LambdaRoleName --policy-name inline --policy-document "file://$(Join-Path $Root 'iam-lambda-policy.json')" | Out-Null

$schedPolicy = @{
    Version = "2012-10-17"
    Statement = @(@{
        Sid = "InvokeStatusLambda"
        Effect = "Allow"
        Action = "lambda:InvokeFunction"
        Resource = "arn:aws:lambda:${Region}:${Account}:function:${LambdaName}"
    })
} | ConvertTo-Json -Depth 8 -Compress
$tmpSched = Join-Path $env:TEMP "trellis2-sched-policy.json"
[System.IO.File]::WriteAllText($tmpSched, $schedPolicy)
aws iam put-role-policy --role-name $SchedulerRoleName --policy-name inline --policy-document "file://$tmpSched" | Out-Null

Write-Host "Waiting 10s for IAM role propagation..."
Start-Sleep -Seconds 10

Write-Host "=== package Lambda ==="
$zip = Join-Path $Root "function.zip"
$stage = Join-Path $Root "build"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item $stage -ItemType Directory | Out-Null
Copy-Item (Join-Path $Root "lambda_function.py") (Join-Path $stage "lambda_function.py")
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage "lambda_function.py") -DestinationPath $zip -Force

$envVars = "Variables={INSTANCE_ID=$InstanceId,TOPIC_ARN=$TopicArn}"
$fnArn = aws --region $Region lambda get-function --function-name $LambdaName --query Configuration.FunctionArn --output text 2>$null
if ($LASTEXITCODE -ne 0 -or -not $fnArn) {
    Write-Host "Creating Lambda $LambdaName"
    $fnArn = aws --region $Region lambda create-function `
        --function-name $LambdaName `
        --runtime python3.12 `
        --handler lambda_function.handler `
        --role $LambdaRoleArn `
        --timeout 60 `
        --memory-size 256 `
        --zip-file "fileb://$zip" `
        --environment $envVars `
        --query FunctionArn --output text
} else {
    Write-Host "Updating Lambda $LambdaName"
    aws --region $Region lambda update-function-code --function-name $LambdaName --zip-file "fileb://$zip" | Out-Null
    aws --region $Region lambda wait function-updated --function-name $LambdaName
    aws --region $Region lambda update-function-configuration --function-name $LambdaName --environment $envVars --timeout 60 --memory-size 256 | Out-Null
}
Write-Host "FunctionArn=$fnArn"

$permOk = $true
aws --region $Region lambda add-permission `
    --function-name $LambdaName `
    --statement-id AllowEventBridgeScheduler `
    --action lambda:InvokeFunction `
    --principal scheduler.amazonaws.com `
    --source-arn "arn:aws:scheduler:${Region}:${Account}:schedule/default/${ScheduleName}" 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "Lambda permission already exists or could not be added (continuing)." }

Write-Host "=== EventBridge Scheduler 00:00 Asia/Shanghai ==="
$target = "Arn=$fnArn,RoleArn=$SchedulerRoleArn"
$exists = aws --region $Region scheduler get-schedule --name $ScheduleName --group-name default --query Name --output text 2>$null
if ($LASTEXITCODE -ne 0 -or -not $exists -or $exists -eq "None") {
    aws --region $Region scheduler create-schedule `
        --name $ScheduleName `
        --group-name default `
        --schedule-expression "cron(0 0 * * ? *)" `
        --schedule-expression-timezone "Asia/Shanghai" `
        --flexible-time-window "Mode=OFF" `
        --state ENABLED `
        --target $target | Out-Null
} else {
    aws --region $Region scheduler update-schedule `
        --name $ScheduleName `
        --group-name default `
        --schedule-expression "cron(0 0 * * ? *)" `
        --schedule-expression-timezone "Asia/Shanghai" `
        --flexible-time-window "Mode=OFF" `
        --state ENABLED `
        --target $target | Out-Null
}

Write-Host "=== invoke Lambda once (SNS will not deliver until email is confirmed) ==="
$payload = Join-Path $Root "payload.json"
Set-Content -Path $payload -Value "{}" -Encoding ascii
$outfile = Join-Path $Root "invoke-out.json"
aws --region $Region lambda invoke --function-name $LambdaName --cli-binary-format raw-in-base64-out --payload "file://$payload" $outfile | Out-Null
Get-Content $outfile

Write-Host ""
Write-Host "DEPLOY_OK"
Write-Host "请立刻打开邮箱 1815486243@qq.com（含垃圾箱）查找 AWS Notification - Subscription Confirmation，点击 Confirm subscription。"
Write-Host "确认后告诉我，我会再触发一次测试发送。"
Write-Host "本部署不会停止 EC2。"
