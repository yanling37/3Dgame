#!/usr/bin/env bash
set -euo pipefail
# Linux/macOS deploy for EventBridge Scheduler -> Lambda -> SNS.
# Does not stop EC2. Does not print AWS keys.

REGION=ap-northeast-1
INSTANCE_ID=i-0dd866f52ad65195a
EMAIL=1815486243@qq.com
LAMBDA_NAME=trellis2-daily-gpu-status
TOPIC_NAME=trellis2-daily-gpu-status
SCHEDULE_NAME=trellis2-daily-gpu-status
LAMBDA_ROLE_NAME=trellis2-daily-gpu-status-lambda
SCHEDULER_ROLE_NAME=trellis2-daily-gpu-status-scheduler
ROOT="$(cd "$(dirname "$0")" && pwd)"

echo "=== check AWS identity ==="
ACCOUNT="$(aws sts get-caller-identity --query Account --output text)"
echo "Account=${ACCOUNT}"
echo "Region=${REGION}"

TOPIC_ARN="$(aws --region "$REGION" sns list-topics --query "Topics[?ends_with(TopicArn, ':${TOPIC_NAME}')].TopicArn | [0]" --output text)"
if [[ -z "$TOPIC_ARN" || "$TOPIC_ARN" == "None" ]]; then
  TOPIC_ARN="$(aws --region "$REGION" sns create-topic --name "$TOPIC_NAME" --query TopicArn --output text)"
fi
echo "TopicArn=${TOPIC_ARN}"

EXISTING_SUB="$(aws --region "$REGION" sns list-subscriptions-by-topic --topic-arn "$TOPIC_ARN" --query "Subscriptions[?Endpoint=='${EMAIL}' && Protocol=='email'].SubscriptionArn | [0]" --output text || true)"
if [[ -z "$EXISTING_SUB" || "$EXISTING_SUB" == "None" ]]; then
  aws --region "$REGION" sns subscribe --topic-arn "$TOPIC_ARN" --protocol email --notification-endpoint "$EMAIL" >/dev/null
  echo "Created pending SNS subscription for ${EMAIL}"
else
  echo "Subscription already present: ${EXISTING_SUB}"
fi

ensure_role() {
  local name="$1" trust="$2"
  if ! aws iam get-role --role-name "$name" >/dev/null 2>&1; then
    aws iam create-role --role-name "$name" --assume-role-policy-document "file://${trust}" >/dev/null
  fi
  aws iam get-role --role-name "$name" --query Role.Arn --output text
}

LAMBDA_ROLE_ARN="$(ensure_role "$LAMBDA_ROLE_NAME" "${ROOT}/trust-lambda.json")"
SCHEDULER_ROLE_ARN="$(ensure_role "$SCHEDULER_ROLE_NAME" "${ROOT}/trust-scheduler.json")"
aws iam put-role-policy --role-name "$LAMBDA_ROLE_NAME" --policy-name inline --policy-document "file://${ROOT}/iam-lambda-policy.json" >/dev/null

cat > /tmp/trellis2-sched-policy.json <<EOF
{"Version":"2012-10-17","Statement":[{"Sid":"InvokeStatusLambda","Effect":"Allow","Action":"lambda:InvokeFunction","Resource":"arn:aws:lambda:${REGION}:${ACCOUNT}:function:${LAMBDA_NAME}"}]}
EOF
aws iam put-role-policy --role-name "$SCHEDULER_ROLE_NAME" --policy-name inline --policy-document file:///tmp/trellis2-sched-policy.json >/dev/null

echo "Waiting 10s for IAM role propagation..."
sleep 10

ZIP="${ROOT}/function.zip"
rm -f "$ZIP"
(cd "$ROOT" && zip -q "$ZIP" lambda_function.py)

ENV_VARS="Variables={INSTANCE_ID=${INSTANCE_ID},TOPIC_ARN=${TOPIC_ARN}}"
if aws --region "$REGION" lambda get-function --function-name "$LAMBDA_NAME" >/dev/null 2>&1; then
  aws --region "$REGION" lambda update-function-code --function-name "$LAMBDA_NAME" --zip-file "fileb://${ZIP}" >/dev/null
  aws --region "$REGION" lambda wait function-updated --function-name "$LAMBDA_NAME"
  aws --region "$REGION" lambda update-function-configuration --function-name "$LAMBDA_NAME" --environment "$ENV_VARS" --timeout 60 --memory-size 256 >/dev/null
else
  aws --region "$REGION" lambda create-function \
    --function-name "$LAMBDA_NAME" \
    --runtime python3.12 \
    --handler lambda_function.handler \
    --role "$LAMBDA_ROLE_ARN" \
    --timeout 60 \
    --memory-size 256 \
    --zip-file "fileb://${ZIP}" \
    --environment "$ENV_VARS" >/dev/null
fi
FN_ARN="$(aws --region "$REGION" lambda get-function --function-name "$LAMBDA_NAME" --query Configuration.FunctionArn --output text)"
echo "FunctionArn=${FN_ARN}"

aws --region "$REGION" lambda add-permission \
  --function-name "$LAMBDA_NAME" \
  --statement-id AllowEventBridgeScheduler \
  --action lambda:InvokeFunction \
  --principal scheduler.amazonaws.com \
  --source-arn "arn:aws:scheduler:${REGION}:${ACCOUNT}:schedule/default/${SCHEDULE_NAME}" >/dev/null 2>&1 || true

TARGET="Arn=${FN_ARN},RoleArn=${SCHEDULER_ROLE_ARN}"
if aws --region "$REGION" scheduler get-schedule --name "$SCHEDULE_NAME" --group-name default >/dev/null 2>&1; then
  aws --region "$REGION" scheduler update-schedule \
    --name "$SCHEDULE_NAME" --group-name default \
    --schedule-expression "cron(0 0 * * ? *)" \
    --schedule-expression-timezone "Asia/Shanghai" \
    --flexible-time-window Mode=OFF --state ENABLED --target "$TARGET" >/dev/null
else
  aws --region "$REGION" scheduler create-schedule \
    --name "$SCHEDULE_NAME" --group-name default \
    --schedule-expression "cron(0 0 * * ? *)" \
    --schedule-expression-timezone "Asia/Shanghai" \
    --flexible-time-window Mode=OFF --state ENABLED --target "$TARGET" >/dev/null
fi

echo "=== invoke Lambda once ==="
echo '{}' > "${ROOT}/payload.json"
aws --region "$REGION" lambda invoke --function-name "$LAMBDA_NAME" --cli-binary-format raw-in-base64-out --payload "file://${ROOT}/payload.json" "${ROOT}/invoke-out.json" >/dev/null
cat "${ROOT}/invoke-out.json"
echo
echo DEPLOY_OK
echo "请打开 1815486243@qq.com（含垃圾箱）查找 AWS Notification - Subscription Confirmation 并点击确认。"
echo "确认后告诉我，再触发一次测试发送。本部署不会停止 EC2。"
