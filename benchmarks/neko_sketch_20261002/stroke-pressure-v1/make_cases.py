import hashlib,json,math
from pathlib import Path
root=Path(__file__).resolve().parent
cases=[]
def add(cid,title,points,closed=False):
    cases.append({'id':cid,'title':title,'source':'authored geometric test path, no real pressure or time',
                  'strokes':[{'points':points,'closed':closed}]})
add('T01','长直线',[[30,160],[290,160]])
add('T02','弯曲线',[[30+260*i/60,160+60*math.sin(2*math.pi*i/60)] for i in range(61)])
add('T03','闭合圆',[[160+85*math.cos(2*math.pi*i/64),160+85*math.sin(2*math.pi*i/64)] for i in range(65)],True)
add('T04','尖锐转角',[[40,260],[40,50],[160,200],[275,50],[275,260]])
add('T05','自交折线',[[45,45],[275,275],[45,275],[275,45]])
add('T06','八字回环',[[160+105*math.sin(2*math.pi*i/120),160+95*math.sin(4*math.pi*i/120)] for i in range(121)])
add('T07','点',[[160,160]])
add('T08','短笔',[[156,160],[164,160]])
add('T09','重复点',[[160,160],[160,160],[160,160]])
add('T10','不均匀采样直线',[[30,160],[33,160],[39,160],[120,160],[260,160],[266,160],[290,160]])
for cid,source,title in [('C01','E02','拥抱'),('C02','S04','送花'),('C03','E05','感谢'),('C04','S02','伸展')]:
    p=root.parent/'composition-v3/formal-greedy/strokes'/f'{source}_s17.json'
    strokes=json.loads(p.read_text(encoding='utf-8'))
    # Freeze only geometry. All four styles see exactly the same paths and pen order.
    cases.append({'id':cid,'title':title,'source':'prior measured composition-v3 '+source+' seed17, frozen geometry',
                  'source_sha256':hashlib.sha256(p.read_bytes()).hexdigest(),
                  'strokes':[{'points':s['points'],'closed':s['closed']} for s in strokes]})
(root/'cases.json').write_text(json.dumps({'logical_size':320,'pressure_source':'none in input; profiles are synthesized by the brush',
                                          'seeds':[17,29,43],'cases':cases},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'cases':len(cases),'real_sensor_pressure_samples':0,'model_calls_now':0}))
