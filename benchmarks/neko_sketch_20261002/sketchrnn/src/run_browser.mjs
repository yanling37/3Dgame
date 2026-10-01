import http from "node:http";
import fs from "node:fs";
import path from "node:path";
import puppeteer from "puppeteer-core";

const args = process.argv.slice(2);
function arg(name, fallback = null) {
  const i = args.indexOf(name);
  if (i < 0) return fallback;
  return args[i + 1];
}

const root = path.resolve(arg("--root"));
const jobsPath = path.resolve(arg("--jobs"));
const outPath = path.resolve(arg("--out"));
const spec = JSON.parse(fs.readFileSync(jobsPath, "utf8"));
const chromePath = spec.chrome || process.env.CHROME_PATH || "/usr/local/bin/google-chrome";

function contentType(file) {
  if (file.endsWith(".html")) return "text/html; charset=utf-8";
  if (file.endsWith(".js")) return "text/javascript; charset=utf-8";
  if (file.endsWith(".json")) return "application/json";
  return "application/octet-stream";
}

function resolveUrl(urlPath) {
  const clean = decodeURIComponent(urlPath.split("?")[0]);
  let rel;
  if (clean === "/" || clean === "/index.html") rel = path.join("web", "index.html");
  else if (clean.startsWith("/weights/")) rel = path.join("weights", clean.slice("/weights/".length));
  else rel = path.join("web", clean.replace(/^\/+/, ""));
  const full = path.resolve(root, rel);
  if (!full.startsWith(root + path.sep) && full !== root) return null;
  return full;
}

function startServer() {
  const server = http.createServer((req, res) => {
    const full = resolveUrl(req.url || "/");
    if (!full || !fs.existsSync(full) || !fs.statSync(full).isFile()) {
      res.writeHead(404);
      res.end("not found");
      return;
    }
    res.writeHead(200, { "content-type": contentType(full), "cache-control": "no-store" });
    fs.createReadStream(full).pipe(res);
  });
  return new Promise((resolve) => {
    server.listen(0, "127.0.0.1", () => resolve(server));
  });
}

function readStatus(pid) {
  try {
    const text = fs.readFileSync(`/proc/${pid}/status`, "utf8");
    const grab = (key) => {
      const m = text.match(new RegExp(key + ":\\s+(\\d+)"));
      return m ? Number(m[1]) : null;
    };
    return { vm_rss_kb: grab("VmRSS"), vm_hwm_kb: grab("VmHWM") };
  } catch {
    return null;
  }
}

function writePng(dataUrl, file) {
  if (!dataUrl) return;
  const comma = dataUrl.indexOf(",");
  const buf = Buffer.from(dataUrl.slice(comma + 1), "base64");
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, buf);
}

async function attachPage(browser, url, offline) {
  const page = await browser.newPage();
  const blocked = [];
  const pageErrors = [];
  page.on("pageerror", (err) => pageErrors.push(String(err)));
  if (offline) {
    await page.setRequestInterception(true);
    page.on("request", (req) => {
      let host = "";
      try {
        host = new URL(req.url()).hostname;
      } catch {
        host = "";
      }
      if (host === "127.0.0.1" || host === "localhost") req.continue();
      else {
        blocked.push(req.url());
        req.abort("blockedbyclient");
      }
    });
  }
  await page.goto(url, { waitUntil: "load", timeout: 60000 });
  await page.waitForFunction("window.__eval && window.__eval.ready === true", { timeout: 30000 });
  return { page, blocked, pageErrors };
}

async function launch() {
  const t0 = process.hrtime.bigint();
  const browser = await puppeteer.launch({
    executablePath: chromePath,
    headless: true,
    args: [
      "--no-sandbox",
      "--disable-dev-shm-usage",
      "--enable-precise-memory-info",
      "--window-size=800,800",
    ],
    defaultViewport: { width: 800, height: 800 },
  });
  const launchMs = Number(process.hrtime.bigint() - t0) / 1e6;
  return { browser, launchMs };
}

function slim(result) {
  if (!result) return result;
  const copy = { ...result };
  delete copy.png_data_url;
  delete copy.stroke5;
  delete copy.polylines;
  return copy;
}

async function runJobsOnPage(page, jobs, outDir, blocked, pageErrors) {
  const results = [];
  for (const job of jobs) {
    const started = process.hrtime.bigint();
    try {
      if (job.op === "backend") {
        const backend = await page.evaluate((name) => window.__eval.ensureBackend(name), job.backend);
        results.push({ id: job.id, op: job.op, ok: true, backend });
      } else if (job.op === "probe") {
        const probe = await page.evaluate(() => window.__eval.probe());
        results.push({ id: job.id, op: job.op, ok: true, probe });
      } else if (job.op === "load") {
        const loaded = await page.evaluate(
          (payload) => window.__eval.loadModel(payload.category, payload.weightUrl || null),
          { category: job.category, weightUrl: job.weight_url || null }
        );
        results.push({ id: job.id, op: job.op, ok: true, ...loaded });
      } else if (job.op === "generate") {
        let loadInfo = null;
        if (job.include_load) {
          loadInfo = await page.evaluate(
            (payload) => window.__eval.loadModel(payload.category, payload.weightUrl || null),
            { category: job.category, weightUrl: job.weight_url || null }
          );
        }
        const generated = await page.evaluate((payload) => window.__eval.generate(payload), {
          category: job.category,
          weightUrl: job.weight_url || null,
          seed: job.seed,
          temperature: job.temperature,
          pixelFactor: job.pixel_factor,
          maxPoints: job.max_points,
          maxStrokes: job.max_strokes,
          timeoutMs: job.timeout_ms,
          strokeWidth: job.stroke_width,
          sampler: job.sampler || "seeded",
        });
        const pngPath = path.join(outDir, `${job.id}.png`);
        writePng(generated.png_data_url, pngPath);
        const rawPath = path.join(outDir, `${job.id}.json`);
        fs.writeFileSync(
          rawPath,
          JSON.stringify(
            {
              id: job.id,
              stroke5: generated.stroke5,
              polylines: generated.polylines,
              timings_ms: generated.timings_ms,
              status: generated.status,
            },
            null,
            0
          )
        );
        let spot = null;
        if (job.spotcheck) {
          const shot = path.join(outDir, "spotcheck", `${job.id}.png`);
          fs.mkdirSync(path.dirname(shot), { recursive: true });
          const el = await page.$("#preview");
          await el.screenshot({ path: shot });
          spot = shot;
        }
        results.push({
          id: job.id,
          op: job.op,
          ok: true,
          case_id: job.case_id || null,
          group: job.group || null,
          category: job.category,
          seed: job.seed,
          warmup: !!job.warmup,
          seedcheck: !!job.seedcheck,
          node_wait_ms: Number(process.hrtime.bigint() - started) / 1e6,
          png: pngPath,
          raw: rawPath,
          spotcheck: spot,
          result: slim(generated),
          load_info: loadInfo,
        });
      } else if (job.op === "ping") {
        const ping = await page.evaluate(async (target) => {
          try {
            const response = await fetch(target);
            return { ok: true, status: response.status };
          } catch (err) {
            return { ok: false, error: String(err) };
          }
        }, job.url);
        results.push({ id: job.id, op: job.op, ok: true, ping, blocked: blocked.slice() });
      } else {
        results.push({ id: job.id, op: job.op, ok: false, error: "unknown op" });
      }
    } catch (err) {
      results.push({
        id: job.id,
        op: job.op,
        ok: false,
        case_id: job.case_id || null,
        seed: job.seed || null,
        error: String(err && err.stack ? err.stack : err),
        page_errors: pageErrors.slice(),
      });
    }
  }
  return { results, blocked: blocked.slice(), page_errors: pageErrors.slice() };
}

async function main() {
  const server = await startServer();
  const port = server.address().port;
  const url = `http://127.0.0.1:${port}/`;
  const outDir = path.resolve(spec.out_dir);
  fs.mkdirSync(outDir, { recursive: true });
  const summary = {
    url,
    chrome_path: chromePath,
    offline: !!spec.offline,
    cold: !!spec.cold,
    sessions: [],
  };
  try {
      if (spec.cold) {
      for (const job of spec.jobs) {
        const t0 = process.hrtime.bigint();
        const { browser } = await launch();
        try {
          const { page, blocked, pageErrors } = await attachPage(browser, url, !!spec.offline);
          const readyMs = Number(process.hrtime.bigint() - t0) / 1e6;
          await page.evaluate((name) => window.__eval.ensureBackend(name), spec.backend);
          const ran = await runJobsOnPage(page, [job], outDir, blocked, pageErrors);
          const proc = browser.process();
          summary.sessions.push({
            browser_launch_ms: readyMs,
            chrome_version: await browser.version(),
            pid_status: proc ? readStatus(proc.pid) : null,
            node_status: readStatus(process.pid),
            ...ran,
          });
        } finally {
          await browser.close();
        }
      }
    } else {
      const t0 = process.hrtime.bigint();
      const { browser } = await launch();
      try {
        const { page, blocked, pageErrors } = await attachPage(browser, url, !!spec.offline);
        summary.chrome_version = await browser.version();
        summary.browser_launch_ms = Number(process.hrtime.bigint() - t0) / 1e6;
        await page.evaluate((name) => window.__eval.ensureBackend(name), spec.backend);
        const ran = await runJobsOnPage(page, spec.jobs, outDir, blocked, pageErrors);
        const proc = browser.process();
        summary.sessions.push({
          browser_launch_ms: summary.browser_launch_ms,
          pid_status: proc ? readStatus(proc.pid) : null,
          node_status: readStatus(process.pid),
          ...ran,
        });
      } finally {
        await browser.close();
      }
    }
  } finally {
    server.close();
  }
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, JSON.stringify(summary));
  const failed = summary.sessions.flatMap((s) => s.results).filter((r) => r.ok === false);
  console.log(JSON.stringify({ out: outPath, sessions: summary.sessions.length, failed: failed.length }));
  if (failed.length && spec.fail_on_error) process.exit(2);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
