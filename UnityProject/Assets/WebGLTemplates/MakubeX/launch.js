/* The approved cover is available before downloading or starting Unity. */
(()=>{
  'use strict';
  const config=window.MakubeXBuild;
  const canvas=document.getElementById('unity-canvas');
  const cover=document.getElementById('cover');
  const start=document.getElementById('start');
  const panel=document.getElementById('load-panel');
  const progress=document.getElementById('progress');
  const percent=document.getElementById('percent');
  const status=document.getElementById('status');
  const error=document.getElementById('error');
  const preparedUrls=[];
  const abort=new AbortController();
  let started=false,exiting=false,instance=null;
  let downloadBytes=0,totalBytes=0,lastProgress=0;
  let playState={page:0,paused:false},requestingLook=false,freeLook=false;
  let wasFullscreen=Boolean(document.fullscreenElement||document.webkitFullscreenElement);

  function endFreeLook(){freeLook=false;canvas.style.cursor='';if(document.pointerLockElement!==canvas)canvas.dataset.look='released';}
  function useFreeLook(reason){
    requestingLook=false;canvas.dataset.lookError=String(reason?.message||reason||'unsupported');
    if(exiting||playState.page!==1||playState.paused)return;
    freeLook=true;canvas.dataset.look='free';canvas.style.cursor='none';
  }

  function forwardEscape(openOnly=false){
    if(exiting||!instance||![1,2].includes(playState.page)||openOnly&&playState.paused)return;
    instance.SendMessage('MakubeX App','BrowserEscapePressed');
  }
  function releaseLook(){
    endFreeLook();
    if(document.pointerLockElement!==canvas)return;
    document.exitPointerLock();
  }
  function pointerChanged(){
    const captured=document.pointerLockElement===canvas;
    canvas.dataset.look=captured?'captured':'released';
    if(captured)delete canvas.dataset.lookError;
    // Losing pointer lock is not necessarily Escape: focus changes and browser
    // restrictions also release it. Only a real menu input should open settings.
  }
  document.addEventListener('pointerlockchange',pointerChanged);
  window.addEventListener('makubex:input-state',event=>{
    playState=event.detail;canvas.dataset.scene=playState.page===1?'chapter':playState.page===2?'map':'loading';
    canvas.dataset.menu=playState.paused?'open':'closed';
    if(playState.page!==1||playState.paused)releaseLook();
  });
  window.addEventListener('keydown',event=>{if(event.key==='Escape'){endFreeLook();forwardEscape();}});
  window.addEventListener('blur',()=>{if(freeLook)endFreeLook();});
  function fullscreenChanged(){
    const fullscreen=Boolean(document.fullscreenElement||document.webkitFullscreenElement);
    if(wasFullscreen&&!fullscreen)forwardEscape(true);
    wasFullscreen=fullscreen;
  }
  document.addEventListener('fullscreenchange',fullscreenChanged);
  document.addEventListener('webkitfullscreenchange',fullscreenChanged);

  function showProgress(value,message){
    lastProgress=Math.max(lastProgress,Math.min(1,value));
    progress.value=lastProgress;
    percent.textContent=Math.floor(lastProgress*100)+'%';
    status.textContent=message;
  }
  function showPlayer(){
    document.body.dataset.state='player';
    cover.hidden=true;canvas.tabIndex=0;canvas.focus();
  }
  function downloadProgress(){
    showProgress(totalBytes ? .86*downloadBytes/totalBytes : 0,
      '正在载入资源 · '+(downloadBytes/1048576).toFixed(1)+' / '+(totalBytes/1048576).toFixed(1)+' MB');
  }
  async function fetchPart(part){
    const response=await fetch(part.path,{signal:abort.signal,cache:'force-cache'});
    if(!response.ok)throw new Error('资源下载失败（'+response.status+'），请检查网络后重试。');
    const reader=response.body.getReader();const chunks=[];let size=0;
    while(true){
      const step=await reader.read();if(step.done)break;
      chunks.push(step.value);size+=step.value.byteLength;
      downloadBytes+=step.value.byteLength;downloadProgress();
    }
    if(size!==part.bytes)throw new Error('资源未下载完整，请重试。');
    return new Blob(chunks);
  }
  async function prepareResource(entry){
    const result=new Array(entry.parts.length);let next=0;
    async function worker(){while(next<entry.parts.length){const i=next++;result[i]=await fetchPart(entry.parts[i]);}}
    await Promise.all(Array.from({length:Math.min(3,entry.parts.length)},worker));
    showProgress(lastProgress,'正在准备游戏资源…');
    const packed=new Blob(result);
    const decoded=await new Response(packed.stream().pipeThrough(new DecompressionStream('gzip'))).blob();
    if(decoded.size!==entry.bytes)throw new Error('资源解压失败，请重试。');
    const url=URL.createObjectURL(new Blob([decoded],{type:entry.mime}));
    preparedUrls.push(url);return url;
  }
  async function loadUnityScript(){
    await new Promise((resolve,reject)=>{
      const script=document.createElement('script');script.src=config.loaderUrl;
      script.onload=resolve;script.onerror=()=>reject(new Error('Unity 启动文件加载失败，请重试。'));
      document.head.appendChild(script);
    });
  }
  async function begin(){
    if(started||exiting)return;
    started=true;start.disabled=true;panel.hidden=false;
    // Visible to Unity in Awake, before it can draw an internal Start cover.
    window.MakubeXWebStartRequested=true;
    document.body.dataset.state='loading';
    showProgress(0,'正在准备资源…');
    try{
      if(!window.WebAssembly)throw new Error('请使用支持 WebAssembly 的电脑浏览器。');
      if(config.resourcesUrl&&!('DecompressionStream' in window))throw new Error('请使用最新版 Chrome、Edge 或 Safari 电脑浏览器试玩。');
      const loaderReady=loadUnityScript();
      loaderReady.catch(()=>{});
      const playerConfig={...config,autoSyncPersistentDataPath:true,devicePixelRatio:Math.min(window.devicePixelRatio||1,1.5)};
      delete playerConfig.loaderUrl;delete playerConfig.resourcesUrl;
      if(config.resourcesUrl){
        const response=await fetch(config.resourcesUrl,{cache:'no-cache',signal:abort.signal});
        if(!response.ok)throw new Error('无法读取游戏资源清单，请重试。');
        const manifest=await response.json();const entries=manifest.resources;
        const artwork=manifest.artwork||[];
        totalBytes=Object.values(entries).reduce((n,e)=>n+e.compressedBytes,0)+artwork.reduce((n,e)=>n+e.bytes,0);
        downloadProgress();
        playerConfig.dataUrl=await prepareResource(entries.data);
        playerConfig.codeUrl=await prepareResource(entries.wasm);
        await Promise.all(artwork.map(fetchPart));
        playerConfig.cacheControl=()=> 'no-store';
      }
      await loaderReady;
      if(exiting)return;
      const engineBase=config.resourcesUrl ? .90 : 0;
      showProgress(engineBase,'正在启动 Unity…');
      instance=await window.createUnityInstance(canvas,playerConfig,value=>{
        showProgress(engineBase+(1-engineBase)*value,'正在启动 Unity…');
        if(value>=1)showPlayer();
      });
      if(exiting){await instance.Quit();return;}
      window.unityInstance=instance;
      showProgress(1,'载入完成');
      showPlayer();
      instance.SendMessage('MakubeX App','StartFromWeb');
      for(const url of preparedUrls)URL.revokeObjectURL(url);
      preparedUrls.length=0;
    }catch(reason){
      if(exiting)return;
      abort.abort();
      document.body.dataset.state='error';cover.hidden=false;panel.hidden=true;error.hidden=false;
      document.getElementById('error-message').textContent=reason instanceof Error?reason.message:String(reason);
      document.getElementById('retry').focus();
      for(const url of preparedUrls)URL.revokeObjectURL(url);
      console.error('MakubeX 启动失败',reason);
    }
  }
  start.addEventListener('click',begin);
  document.getElementById('retry').addEventListener('click',()=>window.location.reload());
  canvas.addEventListener('pointerdown',event=>{
    canvas.focus();
    if(event.button!==0||exiting||!instance||playState.page!==1||playState.paused||requestingLook||freeLook||document.pointerLockElement===canvas)return;
    if(!canvas.requestPointerLock){useFreeLook('unsupported');return;}
    requestingLook=true;
    try {
      Promise.resolve(canvas.requestPointerLock()).then(()=>{
        requestingLook=false;if(playState.page!==1||playState.paused)releaseLook();
      },useFreeLook);
    } catch(reason) {useFreeLook(reason);}
  });
  canvas.addEventListener('pointerleave',()=>{if(freeLook)endFreeLook();});
  canvas.addEventListener('contextmenu',event=>event.preventDefault());
  // Browser-native double clicks remain reliable even if legacy Unity input merges fast clicks.
  canvas.addEventListener('dblclick',event=>{
    if(!instance||exiting||event.button!==0)return;
    const bounds=canvas.getBoundingClientRect();
    const artWidth=Math.min(bounds.width,bounds.height*1672/941);
    const artHeight=artWidth*941/1672;
    const x=(event.clientX-bounds.left-(bounds.width-artWidth)/2)*1672/artWidth;
    const y=(event.clientY-bounds.top-(bounds.height-artHeight)/2)*941/artHeight;
    const nodes=[[281,273],[620,185],[768,405],[1031,220],[1294,405],[851,626],[1158,690],[1519,686]];
    const index=nodes.findIndex(p=>(x-p[0])**2+(y-p[1])**2<=92**2);
    if(index<0)return;
    event.preventDefault();instance.SendMessage('MakubeX App','ActivateChapterFromWeb',String(index));
  });
  window.addEventListener('pagehide',()=>{
    exiting=true;releaseLook();abort.abort();
    for(const url of preparedUrls)URL.revokeObjectURL(url);
    if(instance)instance.Quit().catch(()=>{});
  });
  window.addEventListener('pageshow',event=>{if(event.persisted&&exiting)window.location.reload();});
})();
