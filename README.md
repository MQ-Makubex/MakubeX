# MakubeX

MakubeX 第一章游戏项目。仓库正在整理，最新版本是 v010。

`UnityProject/` 包含 v010 的两个场景、项目设置、C# 脚本、Shader、场景依赖的模型和材质，以及音频、字体、网页资源。部分脚本沿用早期版本的目录名称，因为 v010 仍然依赖它们。公开副本的构建场景列表只保留 v010，FBX 中的本机路径已脱敏。

整理后的副本已使用 Unity 6000.6.6f1 打开两个场景，检查未发现缺失脚本或场景依赖。WebGL 构建报告为成功，重建后的游戏已在浏览器进入第一章；构建日志仍有 1 条 WebGL Subtarget 预处理异常和 2 条警告。历史版本仍在分阶段整理。

可在 Unity 中使用 `MakubeX → V010 → Build Local WebGL` 从现有 v010 场景构建；旧的“Build Latest Playtest”场景重建菜单仍依赖未包含在此副本中的旧入口场景。`SceneManifest.json` 中的 `source_sha256` 记录的是脱敏前原始 Blender 文件的历史校验值，公开副本的校验值见下方发布附件。

v010 美术场景的 Blender 文件已发布在 [v010 美术源文件](https://github.com/MQ-Makubex/MakubeX/releases/tag/v010-art-source)，包含 SHA-256 校验文件。

历史版本见 [Releases](https://github.com/MQ-Makubex/MakubeX/releases)。早期网页原型的原始目录缺少三张图片，归档中单独附有从 Unity 工程补配的对应原图及说明。

项目当前试玩：<https://makubex.top/>。第一次加载需要下载游戏资源，建议使用电脑浏览器。

`WebLaunch/` 展示 v010 网页入口 HTML、样式、启动脚本及测试；其中资源清单记录已部署构建，生成的 WebGL 资源和分块不包含在该目录中。
