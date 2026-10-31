const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const zlib=require('node:zlib');
// This runs the real launch controller against a small event/HTTP fixture, not a browser.
function fixture({failManifest=false,holdUnity=false,fetchChunk}={}){
  const elements=new Map();
  function element(id){
    if(!elements.has(id))elements.set(id,{id,style:{},dataset:{},hidden:['load-panel','error'].includes(id),disabled:false,value:0,textContent:'',listeners:{},addEventListener(n,fn){this.listeners[n]=fn;},focus(){},});
    return elements.get(id);
  }
  const fetched=[],scripts=[],messages=[],progress=[];let created=0,quit=0,finish;
  const windowEvents={};
  const raw=Buffer.from('MakubeX test resource');const packed=zlib.gzipSync(raw);
  const entry={parts:[{path:'chunk.bin',bytes:packed.length}],bytes:raw.length,compressedBytes:packed.length,mime:'application/octet-stream'};
  const document={body:{dataset:{state:'idle'}},pointerLockElement:null,listeners:{},addEventListener(n,fn){this.listeners[n]=fn;},getElementById:element,createElement(){return {};},head:{appendChild(script){scripts.push(script.src);queueMicrotask(()=>script.onload());}}};
  const window={MakubeXBuild:{loaderUrl:'loader.js',resourcesUrl:'resources.json'},WebAssembly,DecompressionStream,devicePixelRatio:1,location:{reload(){}},addEventListener(n,fn){windowEvents[n]=fn;},async createUnityInstance(canvas,config,notify){created++;notify(.5);notify(1);if(holdUnity)await new Promise(resolve=>finish=resolve);return {SendMessage(...args){messages.push(args);},async Quit(){quit++;}};}};
  const context={window,document,console:{error(){}},AbortController,Blob,Response,DecompressionStream,URL,setTimeout,clearTimeout,fetch:async(url,options)=>{
    fetched.push(url);if(options.signal.aborted)throw new Error('aborted');
    if(url==='resources.json')return new Response(failManifest?'unavailable':JSON.stringify({resources:{data:entry,wasm:entry}}),{status:failManifest?503:200});
    return fetchChunk?fetchChunk({url,options,packed,attempt:fetched.filter(x=>x===url).length}):new Response(packed);
  }};
  Object.defineProperty(element('progress'),'value',{set(v){progress.push(v);},get(){return progress.at(-1)||0;}});
  vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../dist/launch.js'),'utf8'),context);
  return {window,document,element,fetched,scripts,messages,progress,windowEvents,created:()=>created,quit:()=>quit,finish:()=>finish?.(),click:()=>element('start').listeners.click()};
}
async function until(predicate){for(let i=0;i<100&&!predicate();i++)await new Promise(r=>setTimeout(r,5));assert.ok(predicate(),'timed out');}
test('entry waits for Start; one click enters Unity and requests map once',async()=>{
  const f=fixture();assert.deepEqual(f.fetched,[]);assert.deepEqual(f.scripts,[]);
  assert.equal(f.document.body.dataset.state,'idle');assert.equal(f.element('load-panel').hidden,true);
  const loading=f.click();assert.equal(f.element('load-panel').hidden,false);
  await f.click();await loading;
  assert.equal(f.created(),1);assert.deepEqual(f.messages,[['MakubeX App','StartFromWeb']]);
  assert.equal(f.document.body.dataset.state,'player');assert.equal(f.element('cover').hidden,true);
  assert.ok(f.progress.every((n,i)=>i===0||n>=f.progress[i-1]));assert.equal(f.progress.at(-1),1);
});
test('HTTP failure stays on cover with a retry and does not start Unity',async()=>{
  const f=fixture({failManifest:true});await f.click();
  assert.equal(f.created(),0);assert.equal(f.document.body.dataset.state,'error');
  assert.equal(f.element('cover').hidden,false);assert.equal(f.element('error').hidden,false);
  assert.match(f.element('error-message').textContent,/资源清单/);
});
test('leaving during Unity initialization quits the late instance without starting map',async()=>{
  const f=fixture({holdUnity:true});const loading=f.click();await until(()=>f.created()===1);
  f.windowEvents.pagehide();f.finish();await loading;
  assert.equal(f.quit(),1);assert.deepEqual(f.messages,[]);
});
test('leaving a running player quits that instance',async()=>{
  const f=fixture();await f.click();f.windowEvents.pagehide();
  await until(()=>f.quit()===1);
});
test('a temporary HTTP 5xx is retried and starts the game',async()=>{
  const f=fixture({fetchChunk:({packed,attempt})=>attempt===1?new Response('unavailable',{status:503}):new Response(packed)});
  await f.click();assert.equal(f.fetched.filter(x=>x==='chunk.bin').length,3);
  assert.equal(f.created(),1);assert.equal(f.document.body.dataset.state,'player');
});
test('a broken partial stream rolls back its bytes before retrying',async()=>{
  const f=fixture({fetchChunk:({packed,attempt})=>{
    if(attempt!==1)return new Response(packed);
    let reads=0;
    return {ok:true,body:{getReader(){return {async read(){if(reads++===0)return {done:false,value:packed.subarray(0,10)};throw new TypeError('network interrupted');}}}}};
  }});
  await f.click();
  const maximumPartial=.86*10/(2*zlib.gzipSync(Buffer.from('MakubeX test resource')).length);
  assert.ok(f.progress.some(value=>Math.abs(value-maximumPartial)<1e-10));
  assert.ok(f.progress.some((value,i)=>i>0&&value<f.progress[i-1]),'failed bytes are removed from progress');
  assert.equal(f.fetched.filter(x=>x==='chunk.bin').length,3);assert.equal(f.created(),1);
});
test('HTTP 4xx is not retried',async()=>{
  const f=fixture({fetchChunk:()=>new Response('missing',{status:404})});await f.click();
  assert.equal(f.fetched.filter(x=>x==='chunk.bin').length,1);assert.equal(f.created(),0);
  assert.equal(f.document.body.dataset.state,'error');
});
test('repeated server errors stop after three attempts',async()=>{
  const f=fixture({fetchChunk:()=>new Response('unavailable',{status:500})});await f.click();
  assert.equal(f.fetched.filter(x=>x==='chunk.bin').length,3);assert.equal(f.created(),0);
  assert.equal(f.document.body.dataset.state,'error');
});
test('leaving during retry delay cancels the pending retry',async()=>{
  const f=fixture({fetchChunk:()=>new Response('unavailable',{status:503})});
  const loading=f.click();await until(()=>f.fetched.includes('chunk.bin'));
  f.windowEvents.pagehide();await loading;await new Promise(resolve=>setTimeout(resolve,300));
  assert.equal(f.fetched.filter(x=>x==='chunk.bin').length,1);assert.equal(f.created(),0);
  assert.notEqual(f.document.body.dataset.state,'error');
});
test('leaving during a partial download aborts without retrying',async()=>{
  const f=fixture({fetchChunk:({packed,options})=>{
    let reads=0;
    return {ok:true,body:{getReader(){return {async read(){
      if(reads++===0)return {done:false,value:packed.subarray(0,10)};
      return new Promise((resolve,reject)=>options.signal.addEventListener('abort',()=>reject(new Error('aborted')),{once:true}));
    }}}}};
  }});
  const loading=f.click();await until(()=>f.progress.some(value=>value>0));
  f.windowEvents.pagehide();await loading;
  assert.equal(f.fetched.filter(x=>x==='chunk.bin').length,1);assert.equal(f.created(),0);
  assert.notEqual(f.document.body.dataset.state,'error');
});
