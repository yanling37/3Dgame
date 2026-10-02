import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
export const ROOT=path.dirname(fileURLToPath(import.meta.url));
export async function serve(port=0){
  const server=http.createServer((req,res)=>{
    let p;try{p=decodeURIComponent(new URL(req.url,'http://localhost').pathname);}catch{res.writeHead(400);res.end();return;}
    if(p==='/')p='/index.html';const full=path.resolve(ROOT,'.'+p),relative=path.relative(ROOT,full);
    if(relative.startsWith('..')||relative.split(path.sep).includes('node_modules')||relative.endsWith('.log')){res.writeHead(404);res.end();return;}
    const types={'.html':'text/html; charset=utf-8','.js':'text/javascript','.json':'application/json','.svg':'image/svg+xml','.png':'image/png','.txt':'text/plain; charset=utf-8'};
    if(!types[path.extname(full)]||!fs.existsSync(full)||!fs.statSync(full).isFile()){res.writeHead(404);res.end();return;}
    res.writeHead(200,{'Content-Type':types[path.extname(full)]});fs.createReadStream(full).pipe(res);
  });
  await new Promise(resolve=>server.listen(port,'127.0.0.1',resolve));
  return {server,url:`http://127.0.0.1:${server.address().port}/`};
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  const {url}=await serve(Number(process.argv[2]??50435));console.log(url);
}
