Neko 单笔粗细笔刷实验 2026-10-02

目的：一条笔画内部由轻到重再提笔，表达自然落笔轻重。
这一轮评估绘制层；没有接入 Neko，没有语言模型调用，没有真实笔压，没有人工质量评分。

快速看效果：Node.js >=20，运行 node server.mjs 50435，浏览器打开 http://127.0.0.1:50435/。
预览 bundle 已包含在包里，启动预览不需要重新安装依赖。
brush-comparison.png 是固定几何的四版本静态对照；profile 图展示候选第一笔的设计宽度。
capture.html 是可选的本地原始轨迹采集页。数据导出后不会自动接入候选。

正式云端测试：按 CURSOR任务.txt 执行。只需 Node/npm/Chromium/CPU，无 GPU 或模型权重。
依赖与版本冻结在 package-lock.json。许可证见 licenses/。
cases.json 为冻结样本。make_cases.py 是本机来源构建脚本，需要旧 composition-v3 数据，不是云端运行前置条件。

pressure-arc 核心：brush.js。绘制变宽的填充轮廓；不依赖 SVG stroke-width 沿路径变化。
原始输入没有 time/pressure，因此沿程压力只是设计曲线，不是真人手部动力学模型。
独立的 perfect-freehand 对照通过点距模拟压力，不能把该点距当作实测速度。

local-results-v2/：最终本机全矩阵，不能冒充云端结果；report.txt 记录结论和待复核项。
local-results/：开发前一版本，仅保存在本机，不放入交付包，不混入正式结果。
qa/：浏览器界面、SVG 导出和鼠标采集功能检查；自动鼠标样本不是人工风格数据。
