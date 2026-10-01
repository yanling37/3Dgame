/* Browser-side SketchRNN evaluator.
   Uses @magenta/sketch 0.2.0 (window.ms) and times paints with performance.now().
   The seeded sampler follows sketch_support.js sampleSoftmax / birandn / gaussRandom.
   Official model.sample() is only used for the parity check. */
(function () {
  const models = new Map();
  const ORIGIN = [160, 160];
  const LOGICAL = 320;
  const PREVIEW = 512;

  function mulberry32(seed) {
    let a = seed >>> 0;
    return function () {
      a = (a + 0x6d2b79f5) | 0;
      let t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }

  function makeGauss(rng) {
    let returnV = false;
    let vVal = 0;
    function gauss() {
      if (returnV) {
        returnV = false;
        return vVal;
      }
      const u = 2 * rng() - 1;
      const v = 2 * rng() - 1;
      const r = u * u + v * v;
      if (r === 0 || r > 1) return gauss();
      const c = Math.sqrt((-2 * Math.log(r)) / r);
      vVal = v * c;
      returnV = true;
      return u * c;
    }
    return gauss;
  }

  function sampleSoftmax(arr, rng) {
    const x = rng();
    let acc = 0;
    for (let i = 0; i < arr.length; i++) {
      acc += arr[i];
      if (acc >= x) return i;
    }
    return -1;
  }

  function seededSample(pdf, scaleFactor, rng, gauss) {
    const idx = sampleSoftmax(pdf.pi, rng);
    const penIdx = sampleSoftmax(pdf.pen, rng);
    if (idx < 0 || penIdx < 0 || idx >= pdf.muX.length) {
      throw new Error("sample index failed idx=" + idx + " pen=" + penIdx);
    }
    const pen = [0, 0, 0];
    pen[penIdx] = 1;
    const mu1 = pdf.muX[idx];
    const mu2 = pdf.muY[idx];
    const sigma1 = pdf.sigmaX[idx];
    const sigma2 = pdf.sigmaY[idx];
    const corr = pdf.corr[idx];
    const z1 = gauss();
    const z2 = gauss();
    const x = Math.sqrt(1 - corr * corr) * sigma1 * z1 + corr * sigma1 * z2 + mu1;
    const y = sigma2 * z2 + mu2;
    return [x * scaleFactor, y * scaleFactor, pen[0], pen[1], pen[2]];
  }

  function nextPaint() {
    return new Promise((resolve) => {
      requestAnimationFrame(() => requestAnimationFrame(resolve));
    });
  }

  function paint(polylines, strokeWidth) {
    const canvas = document.getElementById("preview");
    const ctx = canvas.getContext("2d");
    const scale = PREVIEW / LOGICAL;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.fillStyle = "#ffffff";
    ctx.fillRect(0, 0, PREVIEW, PREVIEW);
    ctx.lineWidth = strokeWidth * scale;
    ctx.lineCap = "round";
    ctx.lineJoin = "round";
    ctx.strokeStyle = "#000000";
    ctx.beginPath();
    for (const poly of polylines) {
      if (!poly || poly.length < 2) continue;
      ctx.moveTo(poly[0][0] * scale, poly[0][1] * scale);
      for (let i = 1; i < poly.length; i++) {
        ctx.lineTo(poly[i][0] * scale, poly[i][1] * scale);
      }
    }
    ctx.stroke();
  }

  function inkStats() {
    const canvas = document.getElementById("preview");
    const ctx = canvas.getContext("2d");
    const img = ctx.getImageData(0, 0, PREVIEW, PREVIEW).data;
    let ink = 0;
    for (let i = 0; i < img.length; i += 16) {
      if (img[i] < 250 || img[i + 1] < 250 || img[i + 2] < 250) ink++;
    }
    return { ink_samples: ink, sample_stride: 4 };
  }

  function webglInfo() {
    try {
      const c = document.createElement("canvas");
      const gl = c.getContext("webgl") || c.getContext("experimental-webgl");
      if (!gl) return { available: false };
      const dbg = gl.getExtension("WEBGL_debug_renderer_info");
      const info = {
        available: true,
        vendor: gl.getParameter(gl.VENDOR),
        renderer: gl.getParameter(gl.RENDERER),
      };
      if (dbg) {
        info.unmasked_vendor = gl.getParameter(dbg.UNMASKED_VENDOR_WEBGL);
        info.unmasked_renderer = gl.getParameter(dbg.UNMASKED_RENDERER_WEBGL);
      }
      return info;
    } catch (err) {
      return { available: false, error: String(err) };
    }
  }

  async function probe() {
    const tf = window.ms.tf;
    const logs = [];
    const wrap = (level, original) => (...args) => {
      logs.push(level + ": " + args.map(String).join(" "));
      return original.apply(console, args);
    };
    console.warn = wrap("warn", console.warn);
    console.error = wrap("error", console.error);
    const backends = [];
    for (const name of ["cpu", "webgl"]) {
      try {
        const ok = await tf.setBackend(name);
        await tf.ready();
        backends.push({
          name,
          ok: !!ok && tf.getBackend() === name,
          active: tf.getBackend(),
          logs: logs.splice(0, logs.length),
        });
      } catch (err) {
        backends.push({ name, ok: false, error: String(err), logs: logs.splice(0, logs.length) });
      }
    }
    let version = null;
    try {
      version = tf.version;
    } catch (err) {
      version = String(err);
    }
    return {
      tf_backend_after_probe: tf.getBackend(),
      tf_version: version,
      backends,
      webgl: webglInfo(),
      has_sketchrnn: typeof window.ms.SketchRNN === "function",
    };
  }

  async function ensureBackend(name) {
    const tf = window.ms.tf;
    if (tf.getBackend() === name) return tf.getBackend();
    const ok = await tf.setBackend(name);
    await tf.ready();
    if (!ok || tf.getBackend() !== name) {
      throw new Error("backend " + name + " not active (got " + tf.getBackend() + ")");
    }
    return tf.getBackend();
  }

  async function loadModel(category, weightUrl) {
    const key = weightUrl || category;
    if (models.has(key)) {
      const model = models.get(key);
      return {
        category,
        cached: true,
        load_ms: 0,
        num_units: model.numUnits,
        info: model.info,
        weight_url: weightUrl || null,
      };
    }
    const t0 = performance.now();
    const url = weightUrl || "/weights/" + category + ".gen.json";
    const model = new window.ms.SketchRNN(url);
    await model.initialize();
    models.set(key, model);
    const loadMs = performance.now() - t0;
    let sigmaYLength = null;
    try {
      const state = model.update(model.zeroInput(), model.zeroState());
      const pdf = model.getPDF(state, 0.65);
      sigmaYLength = pdf.sigmaY.length;
    } catch (err) {
      sigmaYLength = String(err);
    }
    return {
      category,
      cached: false,
      load_ms: loadMs,
      num_units: model.numUnits,
      info: model.info,
      sigma_y_length: sigmaYLength,
      scale_factor_at_default_pixel: model.scaleFactor,
    };
  }

  async function generate(opts) {
    const model = models.get(opts.weightUrl || opts.category);
    if (!model) throw new Error("model not loaded: " + opts.category);
    const strokeWidth = opts.strokeWidth == null ? 2.5 : opts.strokeWidth;
    const maxPoints = opts.maxPoints || 512;
    const maxStrokes = opts.maxStrokes || 32;
    const timeoutMs = opts.timeoutMs || 30000;
    const temperature = opts.temperature;
    model.setPixelFactor(opts.pixelFactor);
    const sampler = opts.sampler || "seeded";
    const rng = mulberry32(opts.seed >>> 0);
    const gauss = makeGauss(rng);
    const originalRandom = Math.random;
    if (sampler === "official") Math.random = rng;

    const t0 = performance.now();
    let state = model.zeroState();
    let cursor = model.zeroInput();
    let prev = [1, 0, 0];
    let x = ORIGIN[0];
    let y = ORIGIN[1];
    const raw = [];
    const polylines = [];
    let current = null;
    let strokeCount = 0;
    let firstInkMs = null;
    let firstStrokeCompleteMs = null;
    let firstPaintWaitMs = 0;
    let maxInterPointMs = 0;
    let lastMark = t0;
    let timedOut = false;
    let hitPoints = false;
    let hitStrokes = false;
    let sawEnd = false;
    let sampleError = null;
    let nonFinite = false;

    try {
      while (true) {
        if (raw.length >= maxPoints) {
          hitPoints = true;
          break;
        }
        const elapsed = performance.now() - t0;
        if (elapsed > timeoutMs) {
          timedOut = true;
          break;
        }
        state = model.update(cursor, state);
        const pdf = model.getPDF(state, temperature);
        let stroke;
        if (sampler === "official") stroke = model.sample(pdf);
        else stroke = seededSample(pdf, model.scaleFactor, rng, gauss);
        const now = performance.now();
        const gap = now - lastMark;
        if (raw.length > 0 && gap > maxInterPointMs) maxInterPointMs = gap;
        lastMark = now;
        const dx = stroke[0];
        const dy = stroke[1];
        const pDown = stroke[2];
        const pUp = stroke[3];
        const pEnd = stroke[4];
        if (![dx, dy, pDown, pUp, pEnd].every((v) => Number.isFinite(v))) nonFinite = true;
        raw.push([dx, dy, pDown, pUp, pEnd]);
        if (prev[0] === 1) {
          const nx = x + dx;
          const ny = y + dy;
          if (!current) current = [[x, y]];
          current.push([nx, ny]);
          if (Math.hypot(dx, dy) > 0.01 && firstInkMs === null) {
            paint(polylines.concat([current]), strokeWidth);
            const w0 = performance.now();
            await nextPaint();
            firstPaintWaitMs += performance.now() - w0;
            firstInkMs = performance.now() - t0;
          }
        }
        x += dx;
        y += dy;
        if ((pUp === 1 || pEnd === 1) && current) {
          polylines.push(current);
          current = null;
          strokeCount += 1;
          if (firstStrokeCompleteMs === null) firstStrokeCompleteMs = performance.now() - t0;
        }
        prev = [pDown, pUp, pEnd];
        cursor = [dx, dy, pDown, pUp, pEnd];
        if (pEnd === 1) {
          sawEnd = true;
          break;
        }
        if (strokeCount >= maxStrokes) {
          hitStrokes = true;
          break;
        }
      }
    } catch (err) {
      sampleError = String(err && err.stack ? err.stack : err);
    } finally {
      if (sampler === "official") Math.random = originalRandom;
    }

    if (current) {
      polylines.push(current);
      strokeCount += 1;
      if (firstStrokeCompleteMs === null) firstStrokeCompleteMs = performance.now() - t0;
    }

    const raster0 = performance.now();
    paint(polylines, strokeWidth);
    const rasterMs = performance.now() - raster0;
    const w1 = performance.now();
    await nextPaint();
    const finalPaintWaitMs = performance.now() - w1;
    const completeMs = performance.now() - t0;
    const stats = inkStats();
    const sampleLoopMs = completeMs - finalPaintWaitMs - rasterMs;

    let outside = 0;
    let points = 0;
    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;
    for (const poly of polylines) {
      for (const p of poly) {
        points += 1;
        if (p[0] < minX) minX = p[0];
        if (p[1] < minY) minY = p[1];
        if (p[0] > maxX) maxX = p[0];
        if (p[1] > maxY) maxY = p[1];
        if (p[0] < 0 || p[1] < 0 || p[0] > LOGICAL || p[1] > LOGICAL) outside += 1;
      }
    }
    const bbox = points
      ? { min_x: minX, min_y: minY, max_x: maxX, max_y: maxY, width: maxX - minX, height: maxY - minY }
      : null;

    let status = "success";
    let error = null;
    if (sampleError) {
      status = "runtime_error";
      error = sampleError;
    } else if (timedOut) {
      status = "timeout";
      error = "exceeded " + timeoutMs + "ms";
    } else if (nonFinite || stats.ink_samples === 0) {
      status = "invalid_output";
      error = nonFinite ? "non-finite stroke coordinate" : "no visible ink";
    }

    const canvas = document.getElementById("preview");
    return {
      status,
      error,
      sampler,
      seed: opts.seed,
      temperature,
      pixel_factor: opts.pixelFactor,
      scale_factor: model.scaleFactor,
      num_units: model.numUnits,
      backend: window.ms.tf.getBackend(),
      point_count: raw.length,
      stroke_count: polylines.length,
      saw_pen_end: sawEnd,
      hit_point_limit: hitPoints,
      hit_stroke_limit: hitStrokes,
      non_finite: nonFinite,
      timings_ms: {
        model_generate: sampleLoopMs - firstPaintWaitMs,
        parse_vectorize: rasterMs,
        first_content: firstInkMs,
        first_stroke_complete: firstStrokeCompleteMs,
        complete_visible: completeMs,
        first_paint_wait: firstPaintWaitMs,
        final_paint_wait: finalPaintWaitMs,
        max_inter_point: maxInterPointMs,
      },
      outside_point_fraction: points ? outside / points : null,
      bbox,
      ink: stats,
      js_heap: window.performance && performance.memory
        ? {
            used_js_heap: performance.memory.usedJSHeapSize,
            total_js_heap: performance.memory.totalJSHeapSize,
          }
        : null,
      stroke5: raw,
      polylines,
      png_data_url: status === "runtime_error" ? null : canvas.toDataURL("image/png"),
      stroke_order: "native_generation_order",
    };
  }

  window.__eval = {
    ready: true,
    probe,
    ensureBackend,
    loadModel,
    generate,
    webglInfo,
  };
})();
