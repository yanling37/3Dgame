import { getStroke } from 'perfect-freehand';
export const MODES=['constant','sketchling','pfh-auto','pressure-arc'];
export const LABELS={'constant':'等宽对照','sketchling':'现有 Sketchling','pfh-auto':'距离模拟压力','pressure-arc':'沿程压力曲线'};
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
const dist=(a,b)=>Math.hypot(a[0]-b[0],a[1]-b[1]);
const smooth=t=>{t=clamp(t,0,1);return t*t*(3-2*t);};
const fmt=n=>Number(n.toFixed(3));

export function resample(stroke,step=1.5){
  if(!Array.isArray(stroke.points)||!stroke.points.length)throw new Error('empty stroke');
  const points=[];
  for(const p of stroke.points){
    if(!Array.isArray(p)||p.length!==2||p.some(x=>!Number.isFinite(x)||Math.abs(x)>10000))throw new Error('invalid XY');
    if(!points.length||dist(p,points.at(-1))>1e-6)points.push([...p]);
  }
  if(stroke.closed&&points.length>1&&dist(points[0],points.at(-1))>1e-6)points.push([...points[0]]);
  const lengths=[0];for(let i=1;i<points.length;i++)lengths.push(lengths.at(-1)+dist(points[i-1],points[i]));
  const total=lengths.at(-1);
  if(!total)return {points:[points[0]],length:0,positions:[0],closed:false};
  const count=Math.ceil(total/step),sampled=[],positions=[];let j=1;
  for(let i=0;i<=count;i++){
    const s=total*i/count;while(j<lengths.length-1&&lengths[j]<s)j++;
    const u=(s-lengths[j-1])/(lengths[j]-lengths[j-1]);
    sampled.push(points[j-1].map((x,k)=>x+(points[j][k]-x)*u));positions.push(s);
  }
  return {points:sampled,length:total,positions,closed:!!stroke.closed};
}

export function widthsFor(path,seed,index,baseWidth=3){
  const n=path.points.length,phase=((seed*0.61803398875+index*.38196601125)%1)*Math.PI*2;
  if(n===1)return [baseWidth];
  const shortFactor=Math.min(1,path.length/35);
  return path.positions.map((s,i)=>{
    const u=s/path.length;
    let pressure;
    if(path.closed){
      // Periodic pressure: no artificial start/end notch where a circle closes.
      pressure=.60+.14*Math.sin(2*Math.PI*u+phase)+.045*Math.cos(4*Math.PI*u+phase/2);
    }else{
      const center=.50+.08*Math.sin(phase);
      const body=.59+.13*Math.exp(-Math.pow((u-center)/.23,2))+.04*Math.sin(Math.PI*u+phase);
      const contact=smooth(s/Math.min(18,path.length*.18));
      const release=smooth((path.length-s)/Math.min(24,path.length*.24));
      pressure=.08+(body-.08)*contact*release;
    }
    pressure=.60+(clamp(pressure,.06,.86)-.60)*shortFactor;
    return baseWidth*(.25+1.25*pressure);
  });
}

function circle(x,y,r){
  x=fmt(x);y=fmt(y);r=fmt(r);
  return `M ${fmt(x+r)} ${y} A ${r} ${r} 0 1 1 ${fmt(x-r)} ${y} A ${r} ${r} 0 1 1 ${fmt(x+r)} ${y} Z `;
}
function patch(a,b,ra,rb){
  const d=dist(a,b);if(d<1e-8)return circle(...b,rb);
  const nx=-(b[1]-a[1])/d,ny=(b[0]-a[0])/d;
  // Consistent positive winding for quad and circles prevents overlap holes.
  const q=[[a[0]+nx*ra,a[1]+ny*ra],[a[0]-nx*ra,a[1]-ny*ra],
           [b[0]-nx*rb,b[1]-ny*rb],[b[0]+nx*rb,b[1]+ny*rb]];
  return q.map((p,i)=>`${i?'L':'M'} ${fmt(p[0])} ${fmt(p[1])}`).join(' ')+' Z '+circle(...b,rb);
}
export function prepare(strokes,mode='pressure-arc',seed=17,baseWidth=3){
  if(!MODES.includes(mode))throw new Error('unknown brush');
  if(!Number.isFinite(baseWidth)||baseWidth<.5||baseWidth>12)throw new Error('invalid base width');
  if(!Number.isInteger(seed))throw new Error('invalid seed');
  return strokes.map((stroke,index)=>{
    const path=resample(stroke),widths=mode==='pressure-arc'?widthsFor(path,seed,index,baseWidth):path.points.map(()=>baseWidth);
    if(mode==='pfh-auto'){
      // This library uses point distance, not measured drawing speed.
      const points=stroke.points.filter((p,i,a)=>i===0||dist(p,a[i-1])>1e-6);
      const outline=getStroke(points,{size:baseWidth,thinning:.65,smoothing:.5,streamline:0,
                                      simulatePressure:true,last:true,start:{taper:0},end:{taper:0}});
      if(outline.some(p=>p.some(v=>!Number.isFinite(v))))throw new Error('non-finite outline');
      const d=outline.map((p,i)=>`${i?'L':'M'} ${fmt(p[0])} ${fmt(p[1])}`).join(' ')+' Z';
      return {...path,widths:null,parts:[d],d,pressure_source:'distance simulation, no timestamps'};
    }
    const parts=[circle(...path.points[0],widths[0]/2)];
    for(let i=1;i<path.points.length;i++)parts.push(patch(path.points[i-1],path.points[i],widths[i-1]/2,widths[i]/2));
    return {...path,widths,parts,d:parts.join(''),pressure_source:mode==='pressure-arc'?'authored synthetic arclength profile':'constant design width'};
  });
}
export function toSvg(geometry,size=320){
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 320 320"><rect width="320" height="320" fill="white"/>`+
    geometry.map(s=>`<path d="${s.d}" fill="black" fill-rule="nonzero"/>`).join('')+'</svg>';
}

export function raster(canvas,geometry,progress=1){
  const ctx=canvas.getContext('2d'),scale=canvas.width/320;
  ctx.setTransform(1,0,0,1,0,0);ctx.fillStyle='white';ctx.fillRect(0,0,canvas.width,canvas.height);
  ctx.setTransform(scale,0,0,scale,0,0);ctx.fillStyle='black';
  const duration=geometry.map(g=>Math.max(20,g.length)),total=duration.reduce((a,b)=>a+b,0);
  let time=clamp(progress,0,1)*total;
  geometry.forEach((g,i)=>{
    if(time<=0)return;
    const u=clamp(time/duration[i],0,1);time-=duration[i];
    // An initial 1.5px segment makes light contact visible on the first frame.
    const end=u>=1?g.parts.length:Math.min(g.parts.length,Math.max(2,Math.floor(smooth(u)*g.parts.length)));
    const d=end===g.parts.length?g.d:g.parts.slice(0,end).join('');
    ctx.fill(new Path2D(d),'nonzero');
  });
}

export function profileStats(geometry){
  const all=geometry.flatMap(g=>g.widths??[]);
  return {planned_width_min_px:all.length?Math.min(...all):null,planned_width_max_px:all.length?Math.max(...all):null,
          centerline_points:geometry.reduce((a,g)=>a+g.points.length,0),
          svg_bytes:toSvg(geometry).length,
          profile_sources:[...new Set(geometry.map(g=>g.pressure_source))]};
}
