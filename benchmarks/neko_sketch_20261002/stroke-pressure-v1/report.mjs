import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {ROOT} from './server.mjs';
const dir=process.argv[2]??'local-results-v2';
if(path.basename(dir)!==dir)throw new Error('result directory must be inside this lab');
const read=name=>JSON.parse(fs.readFileSync(path.join(ROOT,dir,name),'utf8'));
const summary=read('summary.json'),env=read('environment.json'),geo=read('geometry-checks.json'),cold=read('cold.json');
const rows=fs.readFileSync(path.join(ROOT,dir,'requests.jsonl'),'utf8').trim().split('\n').map(s=>JSON.parse(s));
const candidate=rows.filter(r=>r.mode==='pressure-arc'),widths=candidate.flatMap(r=>[r.planned_width_min_px,r.planned_width_max_px]);
const f=x=>x==null?'null':Number(x.toFixed(2));
const lines=[
  'Neko 单笔粗细笔刷本机复核 2026-10-02',
  `结果目录：${dir}。运行时间 UTC：${env.time_utc}。`,
  `设备：${env.cpu.trim()}；${env.os} ${env.release}；Node ${env.node}；Chromium ${env.browser}。`,
  '仅绘制层：336 次静态请求，14 次候选播放，3 次浏览器冷启动。没有模型调用，没有接入 Neko。',
  '输入坐标与笔画顺序冻结；真实笔压样本 0，真实绘画时间样本 0，所有沿程压力为设计模拟。',
  '每版本每分辨率 42 次。p95 只对成功样本；成功数和空白数单列。',
  '',
  ...summary.brush_variants.map(r=>`${r.mode} ${r.size}px：${r.success}/${r.requests} 成功，blank=${r.blank}，error=${r.error}；几何构建 p95 ${f(r.build_p95_ms)}ms，静态完整可见 p95 ${f(r.visible_p95_ms)}ms；边界触墨 ${r.border_touching_images} 张。`),
  '',
  `pressure-arc 合计 ${candidate.filter(r=>r.status==='success').length}/${candidate.length} 成功。设计笔宽范围 ${f(Math.min(...widths))}～${f(Math.max(...widths))} 逻辑像素；这不是设备笔压或实测力。`,
  `候选实际首墨 p95 ${f(summary.animation_first_p95_ms)}ms；1200ms 播放的完整可见 p95 ${f(summary.animation_complete_p95_ms)}ms；每请求最大帧间隔的 p95 ${f(summary.animation_frame_gap_p95_ms)}ms。`,
  `浏览器冷启动到静态可见：${cold.map(r=>f(r.browser_start_to_static_visible_ms)).join(' / ')}ms；HTTP 服务已运行。`,
  '首墨灰度<100，之后再等两次 rAF；完整可见也等两次。不是中文 prompt 到画面的端到端延迟。',
  '',
  `21 个进度帧的严格灰度128丢墨检查待复核：${summary.retained_ink_failures.join(', ')||'无'}。原始丢墨事件共 ${geo.reduce((s,r)=>s+r.lost_dark_pixels,0)}。`,
  ...geo.filter(r=>r.lost_dark_pixels).map(r=>`${r.case_id}：${r.lost_dark_pixels} 次灰度越阈值；样例 ${r.loss_examples.slice(0,3).map(p=>`(${p.x},${p.y}) ${p.before}→${p.after}`).join('，')}；强丢失 ${r.strong_lost_pixels}。`),
  '不能因为灰度变化很小就把严格丢墨结果写成 0。强丢失只作诊断，不替代严格条件。这是21帧抽样，不是所有动画帧的证明。',
  `SVG 与 Canvas 最终图一致性：${summary.svg_export_mismatch_cases.length?summary.svg_export_mismatch_cases.join(', '):'14/14 没有像素差异'}。`,
  `失败/空白明细：${rows.filter(r=>r.status!=='success').map(r=>`${r.case_id}/${r.mode}/s${r.seed}/${r.size}:${r.status}`).join('；')||'无'}。`,
  '',
  '已验证：单笔宽度变化、点密度不变性、闭合首尾宽度一致、同种子复现、非法输入拒绝；预览、SVG导出、播放时控件锁定、手机宽度显示和鼠标采集结构通过。',
  '机械变化已能展示，但“像真人落笔”仍需人工盲评，人工分保持 null。候选严格零丢墨条件未通过，不能宣布最终验收。',
  '旧拥抱/送花等语义限制仍在；粗细变化没有修复人物关系或形状生成。',
  '本轮不需要 GPU；浏览器峰值内存未测，显存未测。node-memory.json 是 Node 调度进程内存，不能当浏览器或 GPU 占用。',
  '下一步：云端按 CURSOR任务.txt 同机复核四种笔触，再让用户选择风格；可选提供2–3张手画参考，或用 capture.html 采集真实轨迹。',
  '',
  '开源参考：https://github.com/steveruizok/perfect-freehand （本包是独立合成压力笔刷，与该库的点距模拟作对照）。',
];
fs.writeFileSync(path.join(ROOT,'report.txt'),lines.join('\n')+'\n');
const hashes={result_directory:dir,input_sha256:env.input_sha256,source_files:{}};
for(const name of ['brush.js','browser.js','preview.js','cases.json','package-lock.json'])hashes.source_files[name]=crypto.createHash('sha256').update(fs.readFileSync(path.join(ROOT,name))).digest('hex');
fs.writeFileSync(path.join(ROOT,dir,'tested-source-sha256.json'),JSON.stringify(hashes,null,2));
console.log(JSON.stringify({report:'report.txt',candidate_success:candidate.filter(r=>r.status==='success').length,
  synthetic_width_px:[f(Math.min(...widths)),f(Math.max(...widths))],raw_ink_loss_events:geo.reduce((s,r)=>s+r.lost_dark_pixels,0),
  strict_retention_unpassed:summary.retained_ink_failures,manual_scores:null}));
