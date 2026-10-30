mergeInto(LibraryManager.library, {
  $MakubeXPlayInput: {page:-1,paused:-1},
  MakubeX_WebStartRequested: function () {
    return window.MakubeXWebStartRequested === true ? 1 : 0;
  },
  MakubeX_SetInputState__deps: ['$MakubeXPlayInput'],
  MakubeX_SetInputState: function(page,paused) {
    if(MakubeXPlayInput.page===page && MakubeXPlayInput.paused===paused)return;
    MakubeXPlayInput.page=page;MakubeXPlayInput.paused=paused;
    var state={page:page,paused:paused!==0};window.MakubeXInputState=state;
    window.dispatchEvent(new CustomEvent('makubex:input-state',{detail:state}));
  },
  MakubeX_IsLookCaptured: function () {
    var canvas=Module['canvas'];
    // Embedded browsers can reject pointer lock. Their ordinary mouse deltas
    // still support click-to-look while the pointer stays over the canvas.
    return document.pointerLockElement === canvas || canvas.dataset.look === 'free' ? 1 : 0;
  }
});
