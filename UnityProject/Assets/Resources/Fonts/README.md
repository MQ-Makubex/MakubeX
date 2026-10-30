# MakubeX WebGL 中文字体

`Chinese.ttf` 是 **MakubeX UI SC Regular**，由明确使用 SIL Open Font License 1.1 的 Noto Sans CJK SC Regular 和 Noto Sans Symbols 字形合法子集化并组合。浏览器不需要访问玩家电脑上的中文系统字体。

本轮产物：131,724 字节（约 129 KiB），600 个 Unicode 码位，601 个字形。包含当前全部 C# 源文件和已确认第一章原文中的可打印字符，以及完整可打印 ASCII；`間久部`、马克贝斯、源水、设置、八章名称、`✦`、`✓` 均已验证。

## 来源与许可

未复制 macOS 的苹方、黑体或其他专有系统字体。使用本机 IntelliJ IDEA 自带 Android layoutlib 中的两个开源 Noto 文件：

| 字体 | 本机原始文件 | 版本与许可 |
|---|---|---|
| Noto Sans CJK SC Regular | `/Applications/IntelliJ IDEA.app/Contents/plugins/android/lib/layoutlib/data/fonts/NotoSansCJK-Regular.ttc`，集合索引 2 | 1.005；字体内 name 13 明确 SIL OFL 1.1 |
| Noto Sans Symbols | `/Applications/IntelliJ IDEA.app/Contents/plugins/android/lib/layoutlib/data/fonts/NotoSansSymbols-Regular-Subsetted.ttf` | 1.09 uh；字体内 name 13 明确 SIL OFL 1.1 |

- Noto CJK 官方项目：[notofonts/noto-cjk](https://github.com/notofonts/noto-cjk)。
- Noto 符号字体官方项目：[notofonts/symbols](https://github.com/notofonts/symbols)。
- 完整 OFL 许可证：`LICENSE.txt`，从 [Noto 官方字体库](https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/LICENSE) 获取。
- 两份原始版权声明：`COPYRIGHT.txt`；在生成字体的 name 0 中也已保留。
- 字体内部家族名已更改为 MakubeX UI SC，避免将改动后的字体冒充原版 Noto。
- 字形删减、一个符号字形组合和轮廓格式转换均继续使用 OFL 1.1；请随再分发版本保留版权与许可证。

## 字体处理

中文与 ASCII 使用 Noto Sans CJK SC。只有 CJK 原文件不包含的 `✦`（U+2726）来自同为 OFL 的 Noto Sans Symbols。CFF 曲线以最大误差 1/1000 em 转为 TrueType 曲线，适配 Unity 动态字体；沿用 SC 原字面和行距指标。

不在 Resources 中携带 18.7 MB 的原始 TTC 或整份符号字体。`Coverage.json` 记录原文件及产物的 SHA-256、实际输入文件、字形数量和缺字检查；`Characters.txt` 是本次覆盖字符清单。

## 重建

需要 Python 3 与 `fonttools`。本轮工具临时安装在 `/private/tmp/makubex-fonts-download/tools`，没有修改系统 Python，也不是运行游戏的依赖。

在工程根目录执行：

```sh
python3 Assets/Resources/Fonts/rebuild_subset.py \
  --cjk '/Applications/IntelliJ IDEA.app/Contents/plugins/android/lib/layoutlib/data/fonts/NotoSansCJK-Regular.ttc' \
  --symbols '/Applications/IntelliJ IDEA.app/Contents/plugins/android/lib/layoutlib/data/fonts/NotoSansSymbols-Regular-Subsetted.ttf'
```

可以通过 `--cjk` / `--symbols` 指向相同许可的对应 Noto 原文件；TTC 中 SC 字面的索引用 `--font-number` 指定。脚本先检查字体内的 OFL 声明，再检查源文件和生成字体的全部码位，缺字时失败退出。

增加新的 UI 文案或对白后应重建，再由 Unity 重新导入。当前子集覆盖第一阶段内容，不是完整中文输入字体或后七章对白字体。

## 验证边界

fontTools 重开产物并检查：600/600 个所需码位有映射；FreeType/Pillow 成功加载并绘制原对白、设置、章节名与符号，已视觉检查。此字体工作没有启动 Unity 或浏览器；Unity 导入、WebGL 构建和浏览器实测由主开发流程验证。
