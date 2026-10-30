mergeInto(LibraryManager.library, {
  $MakubeXFullscreen: {
    canvas: null,
    handler: null,
    area: null,
    pending: false,
    denied: false,
    fullscreenElement: function () {
      return document.fullscreenElement || document.webkitFullscreenElement || null;
    },
    active: function () {
      var element = this.fullscreenElement();
      return !!element && (element === this.canvas || element.contains(this.canvas));
    },
    supported: function () {
      var canvas = this.canvas || Module['canvas'];
      return !!canvas && !!(canvas.requestFullscreen || canvas.webkitRequestFullscreen);
    },
    ensureCanvas: function () {
      var canvas = Module['canvas'];
      if (!canvas || canvas === this.canvas) return;
      if (this.canvas && this.handler) this.canvas.removeEventListener('pointerdown', this.handler, true);
      this.canvas = canvas;
      this.handler = function (event) { MakubeXFullscreen.onPointerDown(event); };
      canvas.addEventListener('pointerdown', this.handler, true);
    },
    onPointerDown: function (event) {
      var area = this.area;
      if (!area || this.pending || event.button !== 0) return;
      var bounds = this.canvas.getBoundingClientRect();
      if (bounds.width <= 0 || bounds.height <= 0) return;
      var x = (event.clientX - bounds.left) / bounds.width;
      var y = 1 - (event.clientY - bounds.top) / bounds.height;
      if (x < area.left || x > area.right || y < area.bottom || y > area.top) return;
      var target = this.active() ? document : this.canvas;
      var request = this.active()
        ? (document.exitFullscreen || document.webkitExitFullscreen)
        : (this.canvas.requestFullscreen || this.canvas.webkitRequestFullscreen);
      if (!request) { this.denied = true; return; }
      this.pending = true;
      this.denied = false;
      var state = this;
      try {
        // This call runs synchronously inside the actual DOM input event, before Unity's loop.
        var result = request.call(target);
        Promise.resolve(result).then(function () {
          state.pending = false;
        }, function () {
          // A denied user gesture / permissions policy is recoverable. Never leak a rejected
          // Promise into Unity's alert handler or claim the canvas entered fullscreen.
          state.pending = false;
          state.denied = true;
        });
      } catch (error) {
        state.pending = false;
        state.denied = true;
      }
    }
  },
  MakubeX_SetFullscreenArea__deps: ['$MakubeXFullscreen'],
  MakubeX_SetFullscreenArea: function (left, bottom, right, top, enabled) {
    MakubeXFullscreen.ensureCanvas();
    MakubeXFullscreen.area = enabled ? { left: left, bottom: bottom, right: right, top: top } : null;
  },
  MakubeX_GetFullscreenState__deps: ['$MakubeXFullscreen'],
  MakubeX_GetFullscreenState: function () {
    MakubeXFullscreen.ensureCanvas();
    if (MakubeXFullscreen.active()) return 1;
    if (!MakubeXFullscreen.supported()) return -1;
    if (MakubeXFullscreen.pending) return 2;
    return MakubeXFullscreen.denied ? -2 : 0;
  }
});
