import {prepare,raster,toSvg,profileStats,MODES,LABELS} from './brush.js';
import {sketch} from './node_modules/sketchling/dist/core/sketch.js';
import {mountRenderable} from './node_modules/sketchling/dist/render/renderer.js';
const stage=document.getElementById('stage'),afterPaint=()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));
let instance=null;
function clear(){if(instance)instance.timeline.kill();instance=null;stage.replaceChildren();}
function canvas(size){const c=document.createElement('canvas');c.width=size;c.height=size;c.style.width='320px';c.style.maxWidth='100%';stage.append(c);return c;}
async function svgCanvas(svg,size){
  const c=canvas(size),url=URL.createObjectURL(new Blob([svg],{type:'image/svg+xml'}));
  try{const image=new Image();image.src=url;await image.decode();c.getContext('2d').drawImage(image,0,0,size,size);}finally{URL.revokeObjectURL(url);}
  return c;
}
function pixels(c){return [...c.getContext('2d').getImageData(0,0,c.width,c.height).data];}
window.brushLab={
  async show(request){
    clear();const begin=performance.now(),{strokes,mode,seed=17,size=320,baseWidth=3}=request;
    if(!MODES.includes(mode))throw new Error('unknown brush');
    let svg,stats,buildMs;
    if(mode==='sketchling'){
      const scene=sketch.scene({width:320,height:320,background:'#fff',look:'ink',seed:String(seed)});
      strokes.forEach(s=>scene.add((s.closed?sketch.loop:sketch.stroke)(s.points,
        {color:'#000',weight:baseWidth,looseness:.13,energy:'calm',smooth:true})));
      instance=mountRenderable(scene.serialize(),stage);instance.seekTo(instance.totalDuration()+.01);
      const copy=instance.svg.cloneNode(true);copy.setAttribute('xmlns','http://www.w3.org/2000/svg');
      copy.setAttribute('width',String(size));copy.setAttribute('height',String(size));svg=copy.outerHTML;
      buildMs=performance.now()-begin;
      stage.replaceChildren();await svgCanvas(svg,size);
      stats={planned_width_min_px:baseWidth,planned_width_max_px:baseWidth,profile_sources:['whole-stroke design width; rough geometry may overlap'],centerline_points:strokes.reduce((a,s)=>a+s.points.length,0),svg_bytes:svg.length};
    }else{
      const geometry=prepare(strokes,mode,seed,baseWidth);buildMs=performance.now()-begin;
      const c=canvas(size);raster(c,geometry);svg=toSvg(geometry,size);stats=profileStats(geometry);
      // Verify that the independent SVG export and canvas use the same filled geometry.
    }
    await afterPaint();const complete=performance.now()-begin,c=stage.querySelector('canvas');
    return {build_ms:buildMs,complete_visible_ms:complete,...stats,svg,png:c.toDataURL('image/png')};
  },
  async animate(request){
    clear();const begin=performance.now(),{strokes,seed=17,size=320,baseWidth=3,duration=1200}=request;
    const geometry=prepare(strokes,'pressure-arc',seed,baseWidth),build=performance.now()-begin,c=canvas(size);
    raster(c,geometry,0);
    const t=performance.now();let frames=0,maxGap=0,last=t,first=null,firstWait=null;
    await new Promise(resolve=>{function frame(now){maxGap=Math.max(maxGap,now-last);last=now;frames++;
      raster(c,geometry,Math.min(1,(now-t)/duration));
      if(!firstWait){const data=c.getContext('2d').getImageData(0,0,size,size).data;
        if(data.some((v,i)=>i%4===0&&v<100))firstWait=afterPaint().then(()=>{first=performance.now()-begin;});}
      if(now-t>=duration)resolve();else requestAnimationFrame(frame);}requestAnimationFrame(frame);});
    if(firstWait)await firstWait;
    await afterPaint();return {build_ms:build,first_content_ms:first,complete_visible_ms:performance.now()-begin,
      actual_playback_ms:performance.now()-t,frames,max_frame_gap_ms:maxGap,png:c.toDataURL('image/png')};
  },
  frame(request){clear();const c=canvas(request.size??320),geometry=prepare(request.strokes,'pressure-arc',request.seed??17,request.baseWidth??3);raster(c,geometry,request.progress);return pixels(c);},
  async exportPixels(request){
    clear();const geometry=prepare(request.strokes,'pressure-arc',request.seed??17,request.baseWidth??3),c=await svgCanvas(toSvg(geometry,request.size??320),request.size??320);return pixels(c);
  },
  widths(request){return prepare(request.strokes,'pressure-arc',request.seed??17,request.baseWidth??3).map(g=>({closed:g.closed,length:g.length,widths:g.widths}));}
  ,pixelStats(straight=false){
    const c=stage.querySelector('canvas'),ctx=c.getContext('2d'),d=ctx.getImageData(0,0,c.width,c.height).data;
    let dark=0,border=0;const widths=[];
    for(let x=0;x<c.width;x++){let count=0;for(let y=0;y<c.height;y++)if(d[(y*c.width+x)*4]<128){dark++;count++;if(!x||!y||x===c.width-1||y===c.height-1)border++;}
      if(straight&&x>c.width*40/320&&x<c.width*280/320)widths.push(count/(c.width/320));}
    widths.sort((a,b)=>a-b);return {dark_pixels:dark,border_dark_pixels:border,
      raster_straight_width_p10_px:widths.length?widths[Math.floor(widths.length*.1)]:null,
      raster_straight_width_p90_px:widths.length?widths[Math.floor(widths.length*.9)]:null};
  },
  async verifyGeometry(request){
    clear();const g=prepare(request.strokes,'pressure-arc',request.seed??17,3),c=canvas(320),snapshots=[];
    let prev=null,lost=0,strongLost=0,maxBrighten=0;const counts=[],lossExamples=[];
    const progressValues=Array.from({length:21},(_,i)=>i/20);
    for(const progress of progressValues){
      raster(c,g,progress);const d=c.getContext('2d').getImageData(0,0,320,320).data;
      let count=0;for(let i=0;i<d.length;i+=4){if(d[i]<128)count++;
        if(prev){maxBrighten=Math.max(maxBrighten,d[i]-prev[i]);
          if(prev[i]<96&&d[i]>160)strongLost++;
          if(prev[i]<128&&d[i]>=128){lost++;if(lossExamples.length<10)lossExamples.push({x:(i/4)%320,y:Math.floor(i/4/320),before:prev[i],after:d[i],progress});}
        }}
      counts.push(count);prev=new Uint8ClampedArray(d);
      if([.1,.25,.5,.75,1].includes(progress))snapshots.push({progress,png:c.toDataURL('image/png')});
    }
    const exported=await svgCanvas(toSvg(g),320),svg=exported.getContext('2d').getImageData(0,0,320,320).data;
    let mismatch=0,maxDelta=0;for(let i=0;i<prev.length;i+=4){if((prev[i]<128)!==(svg[i]<128))mismatch++;maxDelta=Math.max(maxDelta,Math.abs(prev[i]-svg[i]));}
    return {checked_progress:progressValues,lost_dark_pixels:lost,loss_examples:lossExamples,
            strong_lost_pixels:strongLost,max_brighten_channel_delta:maxBrighten,
            progress_dark_pixels:counts,svg_raster_dark_mismatch_pixels:mismatch,
            svg_raster_max_channel_delta:maxDelta,snapshots};
  }
};
window.ready=true;
async function setup(){
  if(!document.getElementById('case'))return;
  const data=await fetch('cases.json').then(r=>r.json());
  const select=document.getElementById('case');data.cases.forEach(c=>select.add(new Option(c.title,c.id)));
  select.value='T02';
  const mode=document.getElementById('mode');MODES.forEach(m=>mode.add(new Option(LABELS[m],m)));mode.value='pressure-arc';
  async function render(){const item=data.cases.find(c=>c.id===select.value),width=Number(document.getElementById('width').value);
    const result=await window.brushLab.show({strokes:item.strokes,mode:mode.value,baseWidth:width});
    document.getElementById('summary').textContent=`几何构建 ${result.build_ms.toFixed(1)} ms；设计宽度 ${result.planned_width_min_px?.toFixed(2)??'未直接导出'}～${result.planned_width_max_px?.toFixed(2)??'未直接导出'} px`;
    document.getElementById('replay').disabled=mode.value!=='pressure-arc';
    const chart=document.getElementById('profile'),ctx=chart.getContext('2d');ctx.clearRect(0,0,chart.width,chart.height);
    ctx.strokeStyle='#999';ctx.beginPath();ctx.moveTo(36,10);ctx.lineTo(36,116);ctx.lineTo(310,116);ctx.stroke();
    ctx.fillStyle='#333';ctx.font='12px sans-serif';ctx.fillText('笔宽(px)',0,10);ctx.fillText('0',24,119);ctx.fillText('6',24,15);ctx.fillText('沿单笔路径 0% → 100%',105,140);
    if(mode.value==='pressure-arc'){
      const w=window.brushLab.widths({strokes:item.strokes,baseWidth:width})[0].widths;
      ctx.strokeStyle='#171717';ctx.beginPath();w.forEach((v,i)=>{const x=36+274*i/Math.max(1,w.length-1),y=116-106*v/6;i?ctx.lineTo(x,y):ctx.moveTo(x,y);});ctx.stroke();
    }
  }
  let busy=false;
  async function exclusive(fn){if(busy)return;busy=true;const controls=[...document.querySelectorAll('#controls select,#controls input,#controls button')];
    controls.forEach(c=>c.disabled=true);try{await fn();}catch(e){document.getElementById('summary').textContent=e.message;}
    finally{busy=false;controls.forEach(c=>c.disabled=false);document.getElementById('replay').disabled=mode.value!=='pressure-arc';}}
  select.onchange=mode.onchange=()=>exclusive(render);document.getElementById('width').oninput=()=>exclusive(render);
  document.getElementById('replay').onclick=()=>exclusive(()=>window.brushLab.animate({strokes:data.cases.find(c=>c.id===select.value).strokes,baseWidth:Number(document.getElementById('width').value)}));
  document.getElementById('download').onclick=()=>exclusive(async()=>{const r=await window.brushLab.show({strokes:data.cases.find(c=>c.id===select.value).strokes,mode:mode.value,baseWidth:Number(document.getElementById('width').value)});const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([r.svg],{type:'image/svg+xml'}));a.download='variable-stroke.svg';a.click();setTimeout(()=>URL.revokeObjectURL(a.href),1000);});
  await exclusive(render);window.uiReady=true;
}
setup().catch(e=>{document.getElementById('summary').textContent=e.message;throw e;});
