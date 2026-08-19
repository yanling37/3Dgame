# TRELLIS.2 GPU 服务器每日状态邮件

架构：

```text
EventBridge Scheduler（每天 00:00 Asia/Shanghai）
  → Lambda `trellis2-daily-gpu-status`
  → SNS Topic `trellis2-daily-gpu-status`
  → 1815486243@qq.com
```

Lambda **实时**调用 `DescribeInstances`，**不会**停止或启动 EC2。IAM 策略里没有 `ec2:StopInstances` / `ec2:TerminateInstances`。

## 邮件内容

固定包含：

- EC2 当前状态
- 实例名称（Name 标签）
- Instance ID
- 实例类型（g6e.xlarge）
- GPU：NVIDIA L40S
- 公网 IP
- 当前运行时长
- 估算 EC2 GPU 费用（优先 AWS Price List，失败则使用东京 On-Demand fallback $2.699/h）
- EBS 信息
- 是否建议停止服务器

仅在能可靠获取时才显示：

- GPU 使用情况（需要实例在线且 SSM Online，并能跑通 `nvidia-smi`）
- AWS Credit（Cost Explorer 本月已抵扣 Credit；**不是**剩余余额。剩余额度 API 不可靠，因此不显示）

状态文案：

- 运行中：`⚠️ GPU 服务器当前正在运行，如果今天不再使用 TRELLIS.2，请停止实例。`
- 已停止：`🟢 GPU 服务器当前已停止。`

## 当前限制（很重要）

这台 EC2 `i-0dd866f52ad65195a` **没有 IAM Instance Profile**。因此：

- Cloud Agent / 服务器自己 **无法** 调用 AWS API 创建 SNS/Lambda
- 部署必须用你本机已登录的 AWS CLI（或 AWS Console）
- GPU 使用情况在实例未绑定 `AmazonSSMManagedInstanceCore` 之前，邮件会省略该行（符合“不能可靠获取就不显示”）

部署 **不要** 把 AWS Access Key 写进仓库或贴到对话里。

## 本机部署

需要：AWS CLI v2，账号对 `ap-northeast-1` 有 IAM / Lambda / SNS / Scheduler 权限。

Windows PowerShell：

```powershell
cd scripts\aws-gpu-status-mail
aws sts get-caller-identity
.\deploy.ps1
```

Linux / macOS：

```bash
cd scripts/aws-gpu-status-mail
aws sts get-caller-identity
chmod +x deploy.sh send-test.sh
./deploy.sh
```

## SNS 邮箱确认

首次订阅后，AWS 会给 `1815486243@qq.com` 发送确认信（也可能在垃圾箱）。

主题类似：`AWS Notification - Subscription Confirmation`

必须点击邮件里的 **Confirm subscription**，否则以后 Lambda 调用成功也收不到信。

确认后再测一次：

```powershell
.\send-test.ps1
```

或：

```bash
./send-test.sh
```

## 资源名

| 资源 | 名称 |
|---|---|
| Lambda | `trellis2-daily-gpu-status` |
| SNS Topic | `trellis2-daily-gpu-status` |
| Scheduler | `trellis2-daily-gpu-status` |
| Lambda Role | `trellis2-daily-gpu-status-lambda` |
| Scheduler Role | `trellis2-daily-gpu-status-scheduler` |
| 时区 | `Asia/Shanghai` |
| Cron | `cron(0 0 * * ? *)`（每天 00:00） |
| Region | `ap-northeast-1` |
| Instance | `i-0dd866f52ad65195a` |

## 可选：让 GPU 使用情况可查询

给该 EC2 附加 IAM 实例配置文件，至少包含：

- `AmazonSSMManagedInstanceCore`

不要把 Stop/Terminate 权限交给这封状态邮件的 Lambda 角色。
