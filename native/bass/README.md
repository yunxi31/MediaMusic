# BASS 原生引擎 DLL

这些原生 DLL **不通过 NuGet 分发**（授权原因），必须手动下载后放入本目录。
`.csproj` 已配置 `<None Include="native\bass\*.dll" CopyToOutputDirectory="PreserveNewest" />`，
构建时会自动复制到输出目录。

## 需要下载的 DLL

| DLL | 用途 | 对应 PRD 格式 |
|---|---|---|
| `bass.dll` | 核心引擎：MP3 / WAV / OGG | MP3 / WAV / OGG |
| `bassmix.dll` | 混音器：0ms gapless / crossfade | §2.1 无缝播放 |
| `bass_fx.dll` | DSP / EQ（BASS_BFX_PEAKEQ） | §2.1 多段均衡器 |
| `bassflac.dll` | FLAC 插件 | §2.1 FLAC |
| `bass_ape.dll` | APE/MAC 插件 | §2.1 APE |
| `bass_aac.dll` | AAC/MP4/M4A 插件 | §2.1 AAC |

## 下载来源

官方站点：https://www.un4seen.com/ （BASS / BASSmix / BASS_FX / 各插件分别下载 Windows x64 版本）

## 授权提醒

- BASS 对**非商业用途免费**；商用需向 un4seen 购买授权。
- MediaMusic 的分发性质（商用 / 非商用）需在正式发布前由项目所有者确认。

## 骨架阶段说明

骨架阶段 `Audio/BassEngine.cs` 的 `Init()` 已做容错：若这些 DLL 缺失，仅记录告警日志，
应用仍可正常启动（音频功能不可用），不会崩溃。放入 DLL 后重启即可启用音频。
