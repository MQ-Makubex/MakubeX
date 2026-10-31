# v010 网页启动入口

这里保留 v010 的启动页 HTML、样式、启动脚本、资源清单和启动流程测试。启动页等待玩家点击开始后加载 Unity WebGL，并显示下载和初始化进度。

`dist/resources.json` 是当时部署版本的资源清单，引用生成的 WebGL 文件和压缩分块；这些构建产物不包含在此目录中，因此此目录本身不能直接作为完整游戏运行。游戏工程源码位于 `../UnityProject/`。

使用 Node.js 24 运行启动流程测试：

```sh
node --test WebLaunch/tests/launch.test.cjs
```
