"""Daily TRELLIS.2 GPU EC2 status mail.

EventBridge Scheduler (00:00 Asia/Shanghai) -> this Lambda -> SNS email.

Never stops or starts instances.
"""

from __future__ import annotations

import json
import os
import time
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

import boto3
from botocore.exceptions import ClientError

SHANGHAI = ZoneInfo("Asia/Shanghai")
FALLBACK_HOURLY_USD = 2.699  # ap-northeast-1 Linux g6e.xlarge On-Demand fallback


def _env(name: str, default: str | None = None) -> str:
    value = os.environ.get(name, default)
    if value is None or value == "":
        raise RuntimeError(f"missing env {name}")
    return value


def _tags(inst: dict) -> dict[str, str]:
    return {t["Key"]: t["Value"] for t in inst.get("Tags", [])}


def _fmt_duration(seconds: float) -> str:
    seconds = max(0, int(seconds))
    hours, rem = divmod(seconds, 3600)
    minutes, secs = divmod(rem, 60)
    return f"{hours}小时{minutes}分钟{secs}秒"


def lookup_hourly_price(instance_type: str, region_name: str) -> tuple[float, str]:
    """Return (usd_per_hour, source). Fail closed to a labeled fallback."""
    location = {
        "ap-northeast-1": "Asia Pacific (Tokyo)",
        "us-east-1": "US East (N. Virginia)",
    }.get(region_name)
    if not location:
        return FALLBACK_HOURLY_USD, f"fallback ${FALLBACK_HOURLY_USD}/h（未知 Region 定价）"
    try:
        pricing = boto3.client("pricing", region_name="us-east-1")
        resp = pricing.get_products(
            ServiceCode="AmazonEC2",
            Filters=[
                {"Type": "TERM_MATCH", "Field": "instanceType", "Value": instance_type},
                {"Type": "TERM_MATCH", "Field": "location", "Value": location},
                {"Type": "TERM_MATCH", "Field": "operatingSystem", "Value": "Linux"},
                {"Type": "TERM_MATCH", "Field": "tenancy", "Value": "Shared"},
                {"Type": "TERM_MATCH", "Field": "capacitystatus", "Value": "Used"},
                {"Type": "TERM_MATCH", "Field": "preInstalledSw", "Value": "NA"},
                {"Type": "TERM_MATCH", "Field": "licenseModel", "Value": "No License required"},
            ],
            MaxResults=5,
        )
        for raw in resp.get("PriceList", []):
            data = json.loads(raw)
            terms = data.get("terms", {}).get("OnDemand", {})
            for term in terms.values():
                for dim in term.get("priceDimensions", {}).values():
                    usd = dim.get("pricePerUnit", {}).get("USD")
                    if usd is None:
                        continue
                    rate = float(usd)
                    if rate <= 0:
                        continue
                    return rate, f"AWS Price List ${rate:.4f}/h"
    except Exception:
        pass
    return FALLBACK_HOURLY_USD, f"fallback ${FALLBACK_HOURLY_USD}/h（Price List 不可用）"


def try_gpu_usage(region: str, instance_id: str, state: str) -> str | None:
    """Only return GPU text when SSM is actually healthy and nvidia-smi succeeds."""
    if state != "running":
        return None
    try:
        ssm = boto3.client("ssm", region_name=region)
        info = ssm.describe_instance_information(
            Filters=[{"Key": "InstanceIds", "Values": [instance_id]}]
        )
        listed = info.get("InstanceInformationList") or []
        if not listed:
            return None
        ping = listed[0].get("PingStatus")
        if ping != "Online":
            return None
        sent = ssm.send_command(
            InstanceIds=[instance_id],
            DocumentName="AWS-RunShellScript",
            Comment="trellis2-daily-gpu-status",
            TimeoutSeconds=30,
            Parameters={
                "commands": [
                    "nvidia-smi --query-gpu=name,utilization.gpu,memory.used,memory.total,temperature.gpu,power.draw --format=csv,noheader"
                ]
            },
        )
        command_id = sent["Command"]["CommandId"]
        deadline = time.time() + 25
        while time.time() < deadline:
            time.sleep(2)
            inv = ssm.get_command_invocation(CommandId=command_id, InstanceId=instance_id)
            status = inv.get("Status")
            if status in ("Pending", "InProgress", "Delayed"):
                continue
            if status == "Success":
                text = (inv.get("StandardOutputContent") or "").strip()
                return text or None
            return None
    except Exception:
        return None
    return None


def try_credit_used(now_utc: datetime) -> str | None:
    """Monthly Credit applied, not remaining balance. Omit if not reliable."""
    start = now_utc.replace(day=1, hour=0, minute=0, second=0, microsecond=0)
    end = now_utc + timedelta(days=1)
    try:
        ce = boto3.client("ce", region_name="us-east-1")
        resp = ce.get_cost_and_usage(
            TimePeriod={
                "Start": start.date().isoformat(),
                "End": end.date().isoformat(),
            },
            Granularity="MONTHLY",
            Metrics=["UnblendedCost"],
            Filter={"Dimensions": {"Key": "RECORD_TYPE", "Values": ["Credit"]}},
        )
        rows = resp.get("ResultsByTime") or []
        if not rows:
            return None
        amount = rows[0]["Total"]["UnblendedCost"]["Amount"]
        unit = rows[0]["Total"]["UnblendedCost"]["Unit"]
        return f"本月已抵扣 Credit {amount} {unit}（这是已使用额度，不是剩余余额）"
    except Exception:
        return None


def describe_ebs(ec2, instance_id: str) -> str:
    vols = ec2.describe_volumes(
        Filters=[{"Name": "attachment.instance-id", "Values": [instance_id]}]
    ).get("Volumes", [])
    if not vols:
        return "未查到已挂载 EBS"
    parts = []
    for v in vols:
        attach = (v.get("Attachments") or [{}])[0]
        parts.append(
            "{id} {size}GiB {vtype} {state} 设备={dev} 删除保护随实例={del_on_term}".format(
                id=v.get("VolumeId"),
                size=v.get("Size"),
                vtype=v.get("VolumeType"),
                state=v.get("State"),
                dev=attach.get("Device", "-"),
                del_on_term=attach.get("DeleteOnTermination"),
            )
        )
    return "；".join(parts)


def handler(event, context):
    region = os.environ.get("AWS_REGION") or os.environ.get("AWS_DEFAULT_REGION") or "ap-northeast-1"
    instance_id = _env("INSTANCE_ID")
    topic_arn = _env("TOPIC_ARN")

    ec2 = boto3.client("ec2", region_name=region)
    sns = boto3.client("sns", region_name=region)
    now_utc = datetime.now(timezone.utc)
    now_cn = now_utc.astimezone(SHANGHAI)

    desc = ec2.describe_instances(InstanceIds=[instance_id])
    reservations = desc.get("Reservations") or []
    if not reservations or not reservations[0].get("Instances"):
        raise RuntimeError(f"instance not found: {instance_id}")
    inst = reservations[0]["Instances"][0]

    state = inst["State"]["Name"]
    name = _tags(inst).get("Name", "（未设置 Name 标签）")
    instance_type = inst.get("InstanceType", "-")
    public_ip = inst.get("PublicIpAddress", "无（实例停止时通常没有公网 IP）")
    az = (inst.get("Placement") or {}).get("AvailabilityZone", "-")
    launch = inst.get("LaunchTime")

    gpu_line = None
    gpu = try_gpu_usage(region, instance_id, state)
    if gpu:
        gpu_line = f"GPU 使用情况：{gpu}"

    rate, rate_src = lookup_hourly_price(instance_type, region)

    if state == "running" and launch is not None:
        if launch.tzinfo is None:
            launch = launch.replace(tzinfo=timezone.utc)
        hours = (now_utc - launch).total_seconds() / 3600.0
        runtime = _fmt_duration((now_utc - launch).total_seconds())
        session_cost = hours * rate
        cost_line = (
            f"估算 EC2 GPU 费用：本次开机约 ${session_cost:.2f} "
            f"（{hours:.2f} 小时 × ${rate:.4f}/h，{rate_src}）"
        )
        warn = "⚠️ GPU 服务器当前正在运行，如果今天不再使用 TRELLIS.2，请停止实例。"
        suggest = "建议停止服务器（本邮件不会自动停止）。"
    else:
        runtime = "当前未运行"
        cost_line = "估算 EC2 GPU 费用：实例已停止，本次无实例小时费（EBS 仍可能按月计费）。"
        warn = "🟢 GPU 服务器当前已停止。"
        suggest = "无需停止（已经停止）。本邮件不会自动停止实例。"

    credit = try_credit_used(now_utc)
    ebs = describe_ebs(ec2, instance_id)

    lines = [
        "TRELLIS.2 GPU 服务器每日状态（实时查询）",
        f"查询时间（北京时间）：{now_cn.strftime('%Y-%m-%d %H:%M:%S %Z')}",
        "",
        f"EC2 当前状态：{state}",
        f"实例名称：{name}",
        f"Instance ID：{instance_id}",
        f"实例类型：{instance_type}",
        "GPU：NVIDIA L40S",
        f"公网 IP：{public_ip}",
        f"可用区：{az}",
        f"当前运行时长：{runtime}",
        cost_line,
        f"EBS 信息：{ebs}",
        f"是否建议停止服务器：{suggest}",
    ]
    if gpu_line:
        # Insert GPU usage after the GPU model line.
        gpu_idx = lines.index("GPU：NVIDIA L40S") + 1
        lines.insert(gpu_idx, gpu_line)
    if credit:
        cost_idx = next(i for i, line in enumerate(lines) if line.startswith("估算 EC2 GPU 费用"))
        lines.insert(cost_idx + 1, f"AWS Credit：{credit}")

    lines.extend(["", warn, "", "本邮件由 EventBridge Scheduler → Lambda → SNS 发送，不会自动停止实例。"])
    body = "\n".join(lines)

    subject = f"[TRELLIS.2] GPU服务器 {state} {instance_id}"
    sns.publish(TopicArn=topic_arn, Subject=subject[:100], Message=body)
    return {"ok": True, "state": state, "instance_id": instance_id}
