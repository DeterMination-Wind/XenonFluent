<div align="center">

<img src="Natsurainko.FluentLauncher/Assets/AppIcon.png" width="128" alt="Xenon-Fluent" />

# Xenon-Fluent

一个基于 [FluentLauncher](https://github.com/Xcube-Studio/Natsurainko.FluentLauncher) 开发的 Fluent 风格 Mindustry 启动器

![Stars](https://img.shields.io/github/stars/DeterMination-Wind/XenonFluent)
![License](https://img.shields.io/badge/license-MIT-yellow)
![Mindustry](https://img.shields.io/badge/game-Mindustry-orange)
![Platform](https://img.shields.io/badge/platform-Windows-blue)

</div>

## ✨ 功能

### 实例与启动
- 管理多个 Mindustry 实例，每个实例完全隔离
  - 独立的 mods / saves / schematics / settings.bin
  - 通过启动时注入 `AppData` / `LOCALAPPDATA` / `TMP` / `TEMP` 环境变量绕过 MSIX 沙箱重定向
- 单 jar 启动管线：`javaw -jar mindustry.jar`，跳过原版 Minecraft 的依赖、账户、参数构造逻辑
- 启动器根目录：`%UserProfile%\Documents\Xenon-Fluent\`，每个实例在 `versions\<id>\`

### 下载
- 4 个 Mindustry 发行源任选其一：
  - **Mindustry** — `Anuken/Mindustry` 官方
  - **MindustryX** — `TinyLake/MindustryX`
  - **CN-ARC-Mindustry** — `BlackDeluxeCat/CN-ARC`
  - **Foo** — `mindustry-antigrief/mindustry-foo-client`
- 下拉一键切换，自动从对应仓库的 GitHub Releases 拉取版本列表
- 资源选择器精准匹配客户端 jar，跳过 `dependencies` / `sources` / `javadoc` / `server`
- 所有 GitHub 资源走 [`gh.tinylake.top`](https://gh.tinylake.top) 镜像加速
- 流式下载带实时速度显示（`12.3 MB / 45.6 MB · 1.23 MB/s`）

### Mod
- 内置 Mindustry mod 浏览器，基于 GitHub topic `mindustry-mod` 搜索
- 一键下载 mod 的最新 release `.jar` 到当前激活实例的隔离 mods 目录
- Mod 信息解析器读取 `mod.json` / `mod.hjson`（容忍 Hjson 注释、不带引号 key、三引号多行串）
  - 显示真实的 displayName / description / version / author

### Java 运行时
- 一键下载 [Eclipse Temurin](https://adoptium.net/) JDK（Adoptium API）
- 自动按系统架构选择 x64 / aarch64
- 解压到 `%LocalAppData%\Xenon-Fluent\Runtimes\` 并注册到 Java 列表

### OOBE
- 4 步引导：语言 → Mindustry 数据目录 → Java → 完成
- 自动扫描候选目录（`%AppData%\Mindustry`、`Documents\Mindustry-data` 等）
- 检测到 `settings.bin` 时显示「已检测到 Mindustry 数据」

### 其他
- 多语言：English / 简体中文 / 繁體中文 / Русский / Українська（继承自上游）
- 主题：浅色 / 深色 / 跟随系统 + 自定义主题色

## 📥 安装

> 暂未提供 MSIX 安装包。请按下面的开发说明从源码构建。

## 🛠️ 开发

### 环境要求
- Windows 10 19041+ / Windows 11
- Visual Studio 2022 + Windows App SDK 工作负载
- **.NET 10 SDK**（上游使用 C# 14 源生成器）
- WinUI 3 / WindowsAppSDK 1.8+

### 构建
```powershell
dotnet build Xenon-Fluent.sln -c Debug -p:Platform=x64 -p:FluentLauncherReleaseChannel=Dev
```

### 注册并运行（MSIX 包形态）
```powershell
Add-AppxPackage -Register "Natsurainko.FluentLauncher\bin\x64\Debug\net9.0-windows10.0.22621.0\AppxManifest.xml" -ForceApplicationShutdown
Start-Process "shell:AppsFolder\26553XcubeStudio.Natsurianko.FluentLauncher.Dev_whpyvkhkm7b2a!App"
```

## 🙏 致谢

- 基于 **[Natsurainko.FluentLauncher](https://github.com/Xcube-Studio/Natsurainko.FluentLauncher)** by [Xcube-Studio](https://github.com/Xcube-Studio)
- <img src="docs/images/credits/Wayzer.jpg" width="20" align="center" /> **Wayzer** — 提供 [`gh.tinylake.top`](https://gh.tinylake.top) GitHub 镜像加速
- Mindustry 来源仓库：
  - [Anuken/Mindustry](https://github.com/Anuken/Mindustry)
  - [TinyLake/MindustryX](https://github.com/TinyLake/MindustryX)
  - [BlackDeluxeCat/CN-ARC](https://github.com/BlackDeluxeCat/CN-ARC)
  - [mindustry-antigrief/mindustry-foo-client](https://github.com/mindustry-antigrief/mindustry-foo-client)
- Java 发行：[Eclipse Temurin](https://adoptium.net/) (Adoptium)

## 📄 许可

本项目延续上游 [FluentLauncher](https://github.com/Xcube-Studio/Natsurainko.FluentLauncher) 的 [MIT 许可](LICENSE)。

> Mindustry® 是 Anuke 的商标。本项目与 Anuken 无关，是社区开发的非官方启动器。
