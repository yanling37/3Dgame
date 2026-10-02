import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import {chromium} from 'playwright';
import {serve,ROOT} from './server.mjs';
import {MODES,LABELS} from './brush.js';
const args=process.argv.slice(2),outName=args.includes('--out')?args[args.indexOf('--out')+1]:'cloud-results';
if(!outName||path.basename(outName)!==outName)throw new Error('out must be a directory name inside this lab');
const out=path.join(ROOT,outName);if(fs.existsSync(path.join(out,'requests.jsonl')))throw new Error('Existing results; use a fresh output name');
fs.mkdirSync(out,{recursive:true});
const save=(p,x)=>{fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,JSON.stringify(x,null,2));};
const image=(p,url)=>{fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,Buffer.from(url.split(',')[1],'base64'));};
const data=JSON.parse(fs.readFileSync(path.join(ROOT,'cases.json'),'utf8'));
const quick=args.includes('--quick'),sizes=quick?[320]:[320,640],seeds=quick?[17]:data.seeds;
const env={time_utc:new Date().toISOString(),node:process.version,os:os.platform(),release:os.release(),arch:os.arch(),
  cpu:os.cpus()[0]?.model,logical_cores:os.cpus().length,system_ram_bytes:os.totalmem(),
  package_versions:JSON.parse(fs.readFileSync(path.join(ROOT,'package.json'),'utf8')).dependencies,
  input_sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(ROOT,'cases.json'))).digest('hex'),
  gpu_required:false,model_inference:false,real_sensor_pressure_samples:0,browser_peak_memory_bytes:null};
save(path.join(out,'config.json'),{modes:MODES,sizes,seeds,base_width_logical_px:3,
  playback_ms:1200,input_policy:'identical frozen XY/pen order; no case-specific geometry replacement',
  synthetic_pressure:true,manual_scores:null,quick});
const {server,url}=await serve();let browser;
const rows=[],animations=[],checks=[];
try{
  browser=await chromium.launch({headless:true});env.browser=browser.version();save(path.join(out,'environment.json'),env);
  const page=await browser.newPage({viewport:{width:700,height:760},deviceScaleFactor:1});
  const errors=[];page.on('pageerror',e=>errors.push(String(e)));
  await page.route('**/*',r=>r.request().url().startsWith(url)?r.continue():r.abort());
  await page.goto(url+'runner.html');await page.waitForFunction(()=>window.ready);
  // Same baseline path for warmup; these calls are never counted in measurements.
  for(const mode of MODES)await page.evaluate(r=>window.brushLab.show(r),{strokes:data.cases[0].strokes,mode,size:320,seed:17});
  for(const item of data.cases){
    for(const size of sizes)for(const seed of seeds)for(const mode of MODES){
      const stem=`${item.id}_${mode}_s${seed}_${size}`,row={case_id:item.id,mode,seed,size,status:'error',manual_scores:null};
      try{
        const r=await page.evaluate(r=>window.brushLab.show(r),{strokes:item.strokes,mode,seed,size});
        const {png,svg,...metrics}=r;Object.assign(row,metrics);
        Object.assign(row,await page.evaluate(s=>window.brushLab.pixelStats(s),['T01','T10'].includes(item.id)));
        row.status=row.dark_pixels?'success':'blank';
        image(path.join(out,'png',stem+'.png'),png);
        fs.mkdirSync(path.join(out,'svg'),{recursive:true});fs.writeFileSync(path.join(out,'svg',stem+'.svg'),svg);
      }catch(e){row.error=String(e);}
      rows.push(row);fs.appendFileSync(path.join(out,'requests.jsonl'),JSON.stringify(row)+'\n');
    }
    console.log(JSON.stringify({phase:'static',case_id:item.id,done:rows.length}));
  }
  for(const item of data.cases){
    const r=await page.evaluate(r=>window.brushLab.animate(r),{strokes:item.strokes,seed:17,size:320,duration:1200});
    const {png,...metrics}=r;animations.push({case_id:item.id,...metrics});image(path.join(out,'animation-final',item.id+'.png'),png);
    const check=await page.evaluate(r=>window.brushLab.verifyGeometry(r),{strokes:item.strokes,seed:17});
    const {snapshots,...results}=check;checks.push({case_id:item.id,...results});
    if(['T03','T05','T06','C01'].includes(item.id))for(const s of snapshots)image(path.join(out,'frames',`${item.id}_${s.progress}.png`),s.png);
    console.log(JSON.stringify({phase:'animation',case_id:item.id,first_ms:metrics.first_content_ms,complete_ms:metrics.complete_visible_ms,
      lost_ink:results.lost_dark_pixels,svg_mismatch:results.svg_raster_dark_mismatch_pixels}));
  }
  if(errors.length)throw new Error('browser errors: '+errors.join(';'));
  await browser.close();browser=null;
  const cold=[];
  for(let i=0;i<3;i++){
    const t=performance.now();const b=await chromium.launch({headless:true}),p=await b.newPage();
    await p.goto(url+'runner.html');await p.waitForFunction(()=>window.ready);
    await p.evaluate(r=>window.brushLab.show(r),{strokes:data.cases[1].strokes,mode:'pressure-arc',seed:17,size:320});
    cold.push({browser_start_to_static_visible_ms:performance.now()-t});await b.close();
  }
  save(path.join(out,'cold.json'),cold);save(path.join(out,'animations.json'),animations);save(path.join(out,'geometry-checks.json'),checks);
  const pct=(v,p)=>v.length?[...v].sort((a,b)=>a-b)[Math.ceil(v.length*p)-1]:null;
  const modes=[];
  for(const size of sizes)for(const mode of MODES){const r=rows.filter(r=>r.size===size&&r.mode===mode),good=r.filter(r=>r.status==='success');
    modes.push({mode,size,requests:r.length,success:good.length,blank:r.filter(r=>r.status==='blank').length,error:r.filter(r=>r.status==='error').length,
      build_p50_ms:pct(good.map(r=>r.build_ms),.5),build_p95_ms:pct(good.map(r=>r.build_ms),.95),visible_p95_ms:pct(good.map(r=>r.complete_visible_ms),.95),
      border_touching_images:good.filter(r=>r.border_dark_pixels>0).length});}
  const summary={scope:'rendering only, no language model or semantic benchmark',static_requests:rows.length,brush_variants:modes,
    animation_requests:animations.length,animation_first_p95_ms:pct(animations.map(r=>r.first_content_ms).filter(x=>x!==null),.95),
    animation_complete_p95_ms:pct(animations.map(r=>r.complete_visible_ms),.95),animation_frame_gap_p95_ms:pct(animations.map(r=>r.max_frame_gap_ms),.95),
    retained_ink_failures:checks.filter(r=>r.lost_dark_pixels>0).map(r=>r.case_id),
    strong_ink_loss_cases:checks.filter(r=>r.strong_lost_pixels>0).map(r=>r.case_id),
    retention_policy:'raw loss uses gray 128; diagnostic strong loss uses before<96 after>160; raw failures are retained',
    svg_export_mismatch_cases:checks.filter(r=>r.svg_raster_dark_mismatch_pixels>0).map(r=>r.case_id),manual_scores:null};
  save(path.join(out,'summary.json'),summary);
  save(path.join(out,'node-memory.json'),{node_max_rss_kb:process.resourceUsage().maxRSS,browser_peak_memory_bytes:null});
  console.log(JSON.stringify(summary));
}finally{if(browser)await browser.close();await new Promise(r=>server.close(r));}
