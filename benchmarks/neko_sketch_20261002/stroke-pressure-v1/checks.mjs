import assert from 'node:assert/strict';
import fs from 'node:fs';
import {prepare,resample,widthsFor,toSvg} from './brush.js';
const data=JSON.parse(fs.readFileSync(new URL('./cases.json',import.meta.url),'utf8'));
for(const item of data.cases){
  for(const mode of ['constant','pfh-auto','pressure-arc']){
    const g=prepare(item.strokes,mode,17);assert(g.length>0);
    for(const s of g){assert(s.d.length>0);assert(!/NaN|Infinity/.test(s.d));
      if(s.widths)assert(s.widths.every(w=>Number.isFinite(w)&&w>.8&&w<5));}
    assert(toSvg(g).startsWith('<svg xmlns='));
  }
}
const line=prepare(data.cases[0].strokes,'pressure-arc',17)[0];
assert(Math.max(...line.widths)/Math.min(...line.widths)>2);
const dense=prepare(data.cases.find(x=>x.id==='T10').strokes,'pressure-arc',17)[0];
assert.deepEqual(line.widths,dense.widths);
for(let i=0;i<line.points.length;i++)assert(Math.hypot(line.points[i][0]-dense.points[i][0],line.points[i][1]-dense.points[i][1])<1e-6);
const circle=prepare(data.cases.find(x=>x.id==='T03').strokes,'pressure-arc',17)[0];
assert(Math.abs(circle.widths[0]-circle.widths.at(-1))<1e-10);
assert.deepEqual(prepare(data.cases[1].strokes,'pressure-arc',17),prepare(data.cases[1].strokes,'pressure-arc',17));
assert.notDeepEqual(widthsFor(resample(data.cases[1].strokes[0]),17,0),widthsFor(resample(data.cases[1].strokes[0]),29,0));
for(const invalid of [[],[[NaN,1]],[[2,Infinity]],[[1,2,3]]])assert.throws(()=>prepare([{points:invalid}],'pressure-arc'));
assert.throws(()=>prepare([{points:[[1,1]]}],'unknown'));
console.log(JSON.stringify({geometric_cases:data.cases.length,finite_outlines:true,single_stroke_width_variation:true,
  density_invariance:true,closed_seam_width_match:true,seed_determinism:true,invalid_input_rejection:true,real_pressure_samples:0}));
