# Tasks: MediaMusic Backend Completion

> 任务按模块分组，依赖关系靠前排列。每个任务均对应 design.md 的设计章节与 requirements.md 的需求编号。
> 状态图例：`[ ]` 未开始 · `[/]` 进行中 · `[x]` 已完成

---

## 模块 0：数据模型与数据库架构扩展
> 前置条件：无。其他所有模块均依赖此模块。

- [x] **0.1** 新增 `EqBand` 数据模型类（`Data/Models/EqBand.cs`）
  - 属性：`Frequency` (double, 20–20000 Hz)、`Gain` (double, -12..+12 dB)、`Bandwidth` (double, 默认 1.0)
  - 添加完整 XML 文档注释（`<summary>`, `<param>`, `<remarks>`）
  - _需求: Req-4, Req-44, Req-48_

- [x] **0.2** 新增 `SearchResult` 数据模型类（`Data/Models/SearchResult.cs`）
  - 属性：`IReadOnlyList<Track> Tracks`、`IReadOnlyList<Album> Albums`、`IReadOnlyList<Artist> Artists`、`IReadOnlyList<Genre> Genres`
  - 计算属性：`int TotalResults`
  - _需求: Req-31_

- [x] **0.3** 新增 `PlayStatistics` 数据模型类与 `TimeRange` 枚举（`Data/Models/PlayStatistics.cs`）
  - `TimeRange` 枚举值：`Last7Days`、`Last30Days`、`LastYear`、`AllTime`
  - `PlayStatistics` 属性：`TotalPlayCount`、`TotalPlayTimeMs`、`TopGenres` (`Dictionary<string, int>`)
  - 计算属性：`TotalPlayTime` (TimeSpan)
  - _需求: Req-36_

- [x] **0.4** 扩展数据库 Schema，新增 SearchHistory 与 PlayHistory 表（`Data/schema.sql`）
  - 添加 `SearchHistory` 表（Id, SearchTerm, SearchedAt）
  - 添加 `PlayHistory` 表（Id, TrackId FK→Tracks ON DELETE CASCADE, PlayedAt）
  - 添加索引：`IX_SearchHistory_SearchedAt`、`IX_PlayHistory_TrackId`、`IX_PlayHistory_PlayedAt`、`IX_PlayHistory_PlayedAt_TrackId`（复合）
  - 所有语句使用 `CREATE TABLE IF NOT EXISTS` / `CREATE INDEX IF NOT EXISTS`
  - _需求: Req-37_

- [x] **0.5** 在 `DbInitializer.Initialize()` 中执行新 Schema 迁移
  - 确保 SearchHistory / PlayHistory 建表语句幂等执行
  - 添加 INFO 日志确认新表创建成功
  - _需求: Req-37_

---

## 模块 1：BASS 引擎增强（BassEngine）
> 文件：`Audio/BassEngine.cs` · 依赖：无，但需先于音频服务完成。

- [x] **1.1** 实现 `VerifyNativeDlls()` 私有方法
  - 检查 `bass.dll`、`bassmix.dll`、`bass_fx.dll` 是否存在于 `AppContext.BaseDirectory`
  - 缺失时逐一记录 WARNING 并返回 `false`
  - _需求: Req-8_

- [x] **1.2** 在 `Init()` 中调用 `VerifyNativeDlls()` 作为前置检查
  - DLL 缺失时跳过 `Bass.Init()`，`IsAvailable` 保持 `false`
  - 现有 `DllNotFoundException` 捕获已符合设计，确认行为一致即可
  - _需求: Req-8_

- [x] **1.3** 完善 `LoadPlugins()`，添加插件句柄存储
  - 新增 `List<int> _pluginHandles` 字段
  - 插件加载成功时记录 INFO（plugin 名称 + handle）
  - 插件加载失败时记录 WARNING + `Bass.LastError`（继续加载其余插件）
  - _需求: Req-7_

- [x] **1.4** 完善 `Dispose()`，在 `Bass.Free()` 前卸载所有插件
  - 遍历 `_pluginHandles`，调用 `Bass.PluginFree(handle)`
  - 记录 DEBUG 日志确认资源释放
  - _需求: Req-7, Req-40_

---

## 模块 2：音频效果服务（EffectsService）
> 文件：`Audio/EffectsService.cs` · 依赖：模块 1（BassEngine.IsAvailable）

- [x] **2.1** 更新 `FadeInAsync` 签名，添加 `channelHandle`、`targetVolume`、`CancellationToken` 参数
  - 新签名：`Task FadeInAsync(int channelHandle, int durationMs, double targetVolume, CancellationToken ct = default)`
  - 验证 `durationMs` 在 0–5000ms，违反时抛出 `ArgumentOutOfRangeException`
  - `BassEngine.IsAvailable == false` 时直接返回（graceful degradation）
  - _需求: Req-1, Req-44, Req-47_

- [x] **2.2** 实现 `FadeInAsync` 核心逻辑
  - 调用 `Bass.ChannelSlideAttribute(channelHandle, ChannelAttribute.Volume, targetVolume, durationMs)`
  - 检查 `ct.IsCancellationRequested`，捕获 `OperationCanceledException`
  - 记录 DEBUG 日志（channelHandle、duration、targetVolume）
  - _需求: Req-1_

- [x] **2.3** 更新 `FadeOutAsync` 签名，添加 `channelHandle`、`CancellationToken` 参数
  - 新签名：`Task FadeOutAsync(int channelHandle, int durationMs, CancellationToken ct = default)`
  - 验证 `durationMs` 在 0–5000ms；BASS 不可用时直接返回
  - _需求: Req-2, Req-44, Req-47_

- [x] **2.4** 实现 `FadeOutAsync` 核心逻辑
  - 调用 `Bass.ChannelSlideAttribute(channelHandle, ChannelAttribute.Volume, 0, durationMs)`
  - 等待 slide 完成（轮询 `Bass.ChannelGetAttribute` 或 `Task.Delay(durationMs)`）
  - 完成后调用 `Bass.ChannelPause(channelHandle)`；支持取消令牌
  - _需求: Req-2_

- [x] **2.5** 更新 `CrossfadeAsync` 签名，添加所有必要参数
  - 新签名：`Task CrossfadeAsync(int outgoingChannel, int incomingChannel, int durationMs, double targetVolume, CancellationToken ct = default)`
  - 验证 `durationMs` 在 0–5000ms；`CrossfadeMs == 0` 时执行即时切换
  - _需求: Req-3, Req-44_

- [x] **2.6** 实现 `CrossfadeAsync` 核心逻辑
  - 并发启动两个 slide：outgoing volume→0、incoming volume 0→targetVolume
  - 使用 `Task.WhenAll` 等待两个 slide完成
  - 处理 outgoing track 提前结束的边界情况
  - _需求: Req-3_

- [x] **2.7** 为 EffectsService 添加线程安全保护
  - 使用 `SemaphoreSlim` 保护同一 channel 的并发 fade 操作
  - _需求: Req-41_

---

## 模块 3：均衡器服务（EqualizerService）
> 文件：`Audio/EqualizerService.cs` · 依赖：模块 0（EqBand 模型）、模块 1（BassEngine）

- [x] **3.1** EqualizerService 实现 `IDisposable`，添加状态字段
  - 新增 `Dictionary<int, List<int>> _channelHandles`（每个 channel 的 DSP handles）
  - 新增 `object _lock`（线程同步）、`bool _disposed`（防止重复释放）
  - 依赖注入中添加 `EqPresetRepository`
  - _需求: Req-40, Req-41, Req-43_

- [x] **3.2** 更新 `ApplyBands` 签名，添加 `channelHandle` 参数
  - 新签名：`void ApplyBands(int channelHandle, IEnumerable<EqBand> bands)`
  - 在 `lock (_lock)` 内执行全部操作；先调用 `Disable(channelHandle)` 清理旧 handles
  - _需求: Req-4, Req-6, Req-43_

- [x] **3.3** 实现 `ApplyBands` 核心 BASS DSP 逻辑
  - 验证 gain 在 -12.0..+12.0 dB，frequency 在 20–20000 Hz，违反时抛出 `ArgumentOutOfRangeException`
  - 调用 `Bass.ChannelSetFX(channelHandle, EffectType.PeakEQ, 0)` 获取 fxHandle
  - fxHandle == 0 时记录 ERROR + `Bass.LastError` 并跳过该 band
  - 调用 `Bass.FXSetParameters(fxHandle, new PeakEQParameters { fCenter, fGain, fBandwidth = 1.0f, lChannel = FXChannelFlags.All })`
  - 将有效 fxHandle 存入 `_channelHandles[channelHandle]`
  - _需求: Req-4, Req-43, Req-44_

- [x] **3.4** 实现 `ApplyPresetAsync` 方法（替换现有 stub `ApplyPreset`）
  - 新签名：`Task ApplyPresetAsync(int channelHandle, long presetId)`
  - 从 `EqPresetRepository.GetByIdAsync(presetId)` 加载预设
  - `JsonSerializer.Deserialize<EqBand[]>(preset.Bands, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })`
  - 捕获 `JsonException` 并记录 ERROR，不应用任何效果；调用 `ApplyBands(channelHandle, bands)`
  - _需求: Req-5, Req-48_

- [x] **3.5** 更新 `Disable` 方法，添加 `channelHandle` 参数并实现真实逻辑
  - 新签名：`void Disable(int channelHandle)`
  - 在 `lock (_lock)` 内遍历 `_channelHandles[channelHandle]`，调用 `Bass.ChannelRemoveFX`，清空列表
  - channel 不在字典中时静默返回
  - _需求: Req-6, Req-43_

- [x] **3.6** 实现 `Dispose()`，释放所有 channel 的全部 DSP handles
  - 遍历 `_channelHandles`，对每个 channel 调用 `Bass.ChannelRemoveFX`
  - 设置 `_disposed = true`
  - _需求: Req-40_

---

## 模块 4：数据仓储扩展

### 4.1 AlbumRepository 扩展（`Data/Repositories/AlbumRepository.cs`）

- [x] **4.1.1** 实现 `GetAllAsync(CancellationToken ct = default)`
  - SQL：`SELECT * FROM Albums ORDER BY Title ASC LIMIT 100`
  - _需求: Req-17_

- [x] **4.1.2** 实现 `SearchAsync(string searchTerm, CancellationToken ct = default)`
  - SQL：`WHERE NormalizedTitle LIKE @term ORDER BY CASE WHEN NormalizedTitle = @exactTerm THEN 0 ELSE 1 END, Title ASC LIMIT 100`
  - 空/null 搜索词时返回全部专辑（调用 GetAllAsync）
  - _需求: Req-18_

- [x] **4.1.3** 实现 `GetByArtistAsync(long artistId, CancellationToken ct = default)`
  - SQL：`WHERE ArtistId = @artistId ORDER BY Year DESC, Title ASC`（利用 IX_Albums_ArtistId）
  - _需求: Req-19_

- [x] **4.1.4** 实现 `GetTracksAsync(long albumId, CancellationToken ct = default)`
  - SQL：JOIN Tracks + Artists + Albums + Genres，`WHERE t.AlbumId = @albumId ORDER BY t.TrackNo ASC, t.Title ASC`
  - 利用 IX_Tracks_AlbumId；返回完整 Track 对象（含 ArtistName、AlbumTitle、GenreName）
  - _需求: Req-20_

### 4.2 ArtistRepository 扩展（`Data/Repositories/ArtistRepository.cs`）

- [x] **4.2.1** 实现 `GetAllAsync(CancellationToken ct = default)`
  - SQL：`SELECT * FROM Artists ORDER BY Name ASC LIMIT 100`
  - _需求: Req-21_

- [x] **4.2.2** 实现 `SearchAsync(string searchTerm, CancellationToken ct = default)`
  - SQL：`WHERE NormalizedName LIKE @term ORDER BY CASE WHEN NormalizedName = @exactTerm THEN 0 ELSE 1 END, Name ASC LIMIT 100`
  - 空/null 搜索词时返回全部艺术家
  - _需求: Req-22_

- [x] **4.2.3** 实现 `GetAlbumsAsync(long artistId, CancellationToken ct = default)`
  - SQL：`WHERE ArtistId = @artistId ORDER BY Year DESC, Title ASC`
  - _需求: Req-23_

- [x] **4.2.4** 实现 `GetTracksAsync(long artistId, CancellationToken ct = default)`
  - SQL：JOIN Tracks + Artists + Albums + Genres，`WHERE t.ArtistId = @artistId ORDER BY al.Year DESC, al.Title ASC, t.TrackNo ASC`
  - 利用 IX_Tracks_ArtistId
  - _需求: Req-24_

### 4.3 GenreRepository 扩展（`Data/Repositories/GenreRepository.cs`）

- [x] **4.3.1** 实现 `GetAllAsync(CancellationToken ct = default)`
  - SQL：`SELECT * FROM Genres ORDER BY Name ASC`
  - _需求: Req-25_

- [x] **4.3.2** 实现 `GetByIdAsync(long id, CancellationToken ct = default)`
  - SQL：`SELECT * FROM Genres WHERE Id = @id`；不存在时返回 `null`
  - _需求: Req-26_

- [x] **4.3.3** 实现 `GetTracksAsync(long genreId, CancellationToken ct = default)`
  - SQL：JOIN Tracks + Artists + Albums + Genres，`WHERE t.GenreId = @genreId ORDER BY ar.Name ASC, al.Title ASC, t.TrackNo ASC`
  - 利用 IX_Tracks_GenreId
  - _需求: Req-27_

### 4.4 EqPresetRepository 完善（`Data/Repositories/EqPresetRepository.cs`）

- [x] **4.4.1** 实现 `GetByIdAsync(long id, CancellationToken ct = default)`
  - SQL：`SELECT * FROM EqPresets WHERE Id = @id`；不存在时返回 `null`（对齐 design.md 接口规范）
  - _需求: Req-28_

- [x] **4.4.2** 实现 `CreateAsync(string name, IEnumerable<EqBand> bands, CancellationToken ct = default)`
  - 验证 name 非空；同名预设存在时抛出 `InvalidOperationException`
  - 序列化 bands 为 JSON；INSERT IsBuiltIn=0，RETURNING Id
  - _需求: Req-30_

- [x] **4.4.3** 实现 `DeleteAsync(long id, CancellationToken ct = default)`
  - 先查 `IsBuiltIn`；为内置时抛出 `InvalidOperationException("Cannot delete built-in preset")`
  - 不存在时静默返回
  - _需求: Req-29_

---

## 模块 5：平台集成服务

### 5.1 系统托盘服务（`Platform/TrayIconService.cs`）

- [x] **5.1.1** TrayIconService 实现 `IDisposable`，注入 `PlayerService`，添加状态字段
  - 构造函数注入 `PlayerService`；私有字段 `NotifyIcon? _notifyIcon`
  - _需求: Req-9, Req-10_

- [x] **5.1.2** 实现 `Show()` 核心逻辑
  - 创建 `NotifyIcon`，加载应用图标，`Visible = true`
  - 创建 `ContextMenuStrip`：Play/Pause · Next Track · [separator] · Show Window · Settings · [separator] · Exit
  - 注册 `DoubleClick` 事件（恢复主窗口）；`_notifyIcon != null` 时幂等跳过
  - 创建失败时捕获异常记录 ERROR，`_notifyIcon` 保持 null（应用继续运行）
  - _需求: Req-9, Req-45_

- [x] **5.1.3** 实现托盘菜单事件处理器
  - `OnPlayPauseClick`：`_playerService.State.IsPlaying ? Pause() : Resume()`
  - `OnNextClick`：`_playerService.Next()`
  - `OnExitClick`：关闭应用
  - `OnTrayDoubleClick`：恢复/聚焦主窗口
  - _需求: Req-9_

- [x] **5.1.4** 实现 `Hide()` 和 `Dispose()`
  - 注销所有事件处理器，`_notifyIcon.Dispose()`，置空引用；多次调用安全（null guard）
  - _需求: Req-10, Req-40_

### 5.2 辅助窗口管理（`Platform/WindowManager.cs`）

- [x] **5.2.1** 添加依赖与状态字段
  - 构造函数注入：`IServiceProvider`、`ClickThroughService`、`WindowDragService`
  - 私有字段：`PhotinoBlazorApp? _miniPlayerWindow`、`PhotinoBlazorApp? _lyricsWindow`、`IntPtr _miniPlayerHandle`、`IntPtr _lyricsHandle`、`object _windowLock`
  - _需求: Req-11, Req-12_

- [x] **5.2.2** 实现 `ShowMiniPlayer()`
  - `lock (_windowLock)`：已打开时 BringWindowToFront 返回
  - 创建 PhotinoBlazorAppBuilder，共享 singleton 服务；设置 SetChromeless/SetTopmost/SetSize(320,120)
  - _需求: Req-11_

- [x] **5.2.3** 实现 `ShowDesktopLyrics()`
  - `lock (_windowLock)`：已打开时 BringWindowToFront 返回
  - 设置 SetChromeless/SetTransparent/SetTopmost；创建后调用 `_clickThroughService.Enable(handle)`
  - _需求: Req-12_

- [x] **5.2.4** 实现 `CloseMiniPlayer()` 和 `CloseDesktopLyrics()`
  - Dispose PhotinoBlazorApp 实例，清空引用和句柄；未打开时静默返回
  - _需求: Req-13_

### 5.3 WindowDragService 与 ClickThroughService 完善

- [x] **5.3.1** 完善 `WindowDragService.StartDrag()`：补齐错误处理
  - `hwnd == IntPtr.Zero` 时记录 WARNING 并返回
  - ReleaseCapture / SendMessage 包裹在 try-catch，失败时记录 WARNING
  - _需求: Req-14, Req-45_

- [x] **5.3.2** 完善 `ClickThroughService.Enable()`：添加 GetWindowLong 返回值校验
  - 返回值为 0 时调用 `Marshal.GetLastWin32Error()` 并记录 WARNING
  - 整体包裹在 try-catch
  - _需求: Req-15, Req-45_

- [x] **5.3.3** 确认 `ClickThroughService.Disable()` 正确保留 `WS_EX_LAYERED`、移除 `WS_EX_TRANSPARENT`
  - _需求: Req-16_

---

## 模块 6：搜索服务（SearchService）
> 新建文件：`Services/SearchService.cs` · 依赖：模块 0（SearchResult + SearchHistory 表）、模块 4（各 Repository 扩展）

- [x] **6.1** 创建 `SearchService` 类骨架
  - 注入：`TrackRepository`、`AlbumRepository`、`ArtistRepository`、`GenreRepository`、`DbConnectionFactory`、`ILogger<SearchService>`
  - 字段：`SemaphoreSlim _suggestionLock = new(1,1)`、`CancellationTokenSource? _suggestionCts`
  - _需求: Req-31, Req-38_

- [x] **6.2** 实现 `SearchAllAsync(string searchTerm, CancellationToken ct = default)`
  - 空白词时返回空 `SearchResult`
  - `Task.WhenAll` 并发查询四个仓储（Track/Album/Artist/Genre）
  - 对 Track/Album/Artist 应用相关性排序，每类限 20 条；Genre 限 20 条
  - 异步 fire-and-forget 保存搜索历史（`_ = SaveSearchAsync(searchTerm, ct)`）
  - _需求: Req-31, Req-42_

- [x] **6.3** 实现相关性排序算法 `ApplyRelevanceRanking<T>`（私有方法）
  - 精确匹配 +100 · 以词开头 +50 · 包含 +25
  - Track 额外：`(int)Math.Log10(track.PlayCount + 1) * 5`；7天内播放 +10
  - 按得分降序排列
  - _需求: Req-49_

- [x] **6.4** 实现 `GetSuggestionsAsync(string partialTerm, CancellationToken ct = default)`（含 300ms 防抖）
  - 少于 2 字符时返回空列表
  - 取消前一个 `_suggestionCts`，创建新 CTS；`Task.Delay(300, linkedCt)` 防抖
  - `_suggestionLock.WaitAsync` 串行化；SQL UNION：Tracks(5) + Albums(3) + Artists(2)，共最多 10 条
  - 捕获 `OperationCanceledException` 返回空列表
  - _需求: Req-32_

- [x] **6.5** 实现搜索历史管理方法
  - `SaveSearchAsync`：INSERT 记录，然后 DELETE 超出 100 条的旧记录
  - `GetRecentSearchesAsync`：SELECT DISTINCT SearchTerm ORDER BY SearchedAt DESC LIMIT 10
  - `ClearHistoryAsync`：DELETE FROM SearchHistory
  - _需求: Req-33_

- [x] **6.6** 在 `ServiceRegistration.Register()` 中注册 `SearchService` 为 Singleton
  - _需求: Req-38_

---

## 模块 7：播放历史服务（PlayHistoryService）
> 新建文件：`Services/PlayHistoryService.cs` · 依赖：模块 0（PlayStatistics + PlayHistory 表）、模块 4（TrackRepository）

- [x] **7.1** 创建 `PlayHistoryService` 类骨架
  - 注入：`DbConnectionFactory`、`TrackRepository`、`ILogger<PlayHistoryService>`
  - 字段：`ConcurrentDictionary<long, DateTime> _recentPlays`
  - _需求: Req-34, Req-38, Req-50_

- [x] **7.2** 实现 `RecordPlayAsync(long trackId, CancellationToken ct = default)`（含去重）
  - `trackId <= 0` 时直接返回
  - 查 `_recentPlays`：60s 内重复时跳过并记录 DEBUG
  - 更新缓存，调用 `CleanupCache()`，异步插入 PlayHistory
  - 数据库失败时记录 ERROR 但不中断播放
  - _需求: Req-34, Req-50_

- [x] **7.3** 实现 `CleanupCache()` 私有方法
  - 移除 `_recentPlays` 中早于 `DateTime.Now.AddMinutes(-2)` 的条目
  - _需求: Req-50_

- [x] **7.4** 实现 `GetRecentlyPlayedAsync(int limit = 50, CancellationToken ct = default)`
  - SQL：JOIN PlayHistory + Tracks + Artists + Albums + Genres，按 Track 去重（GROUP BY t.Id），取 MAX(PlayedAt)，ORDER BY PlayedAt DESC LIMIT @limit
  - _需求: Req-35_

- [x] **7.5** 实现 `GetTopTracksAsync(TimeRange range, int limit = 20, CancellationToken ct = default)`
  - 私有 `GetDateFilter(TimeRange range)` 返回 ISO 时间字符串
  - SQL：COUNT 聚合，WHERE PlayedAt >= @dateFilter，ORDER BY COUNT DESC, MAX(PlayedAt) DESC，LIMIT @limit
  - _需求: Req-36_

- [x] **7.6** 实现 `GetStatisticsAsync(TimeRange range, CancellationToken ct = default)`
  - 三个查询：总播放次数 COUNT(*)、总时长 SUM(t.DurationMs)、Top 5 流派（按 COUNT 排序）
  - 返回 `PlayStatistics` 对象
  - _需求: Req-36_

- [x] **7.7** 在 `PlayerService` 中集成 `PlayHistoryService.RecordPlayAsync`
  - 播放进度达到 80% 或曲目自然结束时调用（异步 fire-and-forget，不阻塞播放线程）
  - 与现有 `IncrementPlayCountAsync` 调用协同（同一触发点）
  - _需求: Req-34_

- [x] **7.8** 在 `ServiceRegistration.Register()` 中注册 `PlayHistoryService` 为 Singleton
  - _需求: Req-38_

---

## 模块 8：横切关注点（Cross-Cutting）

- [x] **8.1** 为所有新增公共类 and 方法添加完整 XML 文档注释
  - 每个 public 类/方法：`<summary>` + `<param>` + `<returns>` + `<exception>`（如适用）
  - 覆盖范围：EffectsService、EqualizerService、所有 Repository 新方法、SearchService、PlayHistoryService
  - _需求: Req-46_

- [x] **8.2** 为所有 Repository 新方法添加数据库异常处理
  - SQLITE_BUSY 时等待 100ms 后重试一次
  - 其他异常记录 ERROR 并返回空集合（不崩溃 UI）
  - _需求: Req-39, Req-42_

- [x] **8.3** 审查所有单例服务可变状态的线程安全性
  - `EqualizerService._channelHandles`：使用 `lock` 保护 ✓
  - `WindowManager._miniPlayerWindow`：使用 `lock` 保护 ✓
  - `PlayHistoryService._recentPlays`：使用 `ConcurrentDictionary` ✓
  - `SearchService._suggestionLock`：正确保护并发建议请求 ✓
  - _需求: Req-41_

- [x] **8.4** 验证所有异步数据库方法向 Dapper 传递 `CancellationToken`
  - 所有 `conn.QueryAsync` / `conn.ExecuteAsync` 均含 `cancellationToken: ct` 参数
  - _需求: Req-47_

- [x] **8.5** Win32 依赖服务添加非 Windows 平台检测（优雅降级）
  - `TrayIconService.Show()`、`WindowDragService.StartDrag()`、`ClickThroughService.Enable()` 开头检查 `OperatingSystem.IsWindows()`
  - 非 Windows 时记录 WARNING 并直接返回
  - _需求: Req-45_

---

## 验收检查清单

- [x] `dotnet build` 零警告零错误
- [x] `DbInitializer` 幂等运行，SearchHistory 和 PlayHistory 表正确创建
- [x] `EffectsService` 在 BASS 不可用时不崩溃，graceful degradation 正常
- [x] `EqualizerService` 应用/移除 EQ 后无 DSP handle 泄漏
- [x] `AlbumRepository`、`ArtistRepository`、`GenreRepository` 所有新方法返回正确排序结果
- [x] `EqPresetRepository.DeleteAsync` 对内置预设抛出 `InvalidOperationException`
- [x] `SearchService.SearchAllAsync` 并发执行（Task.WhenAll）并按相关性排序
- [x] `PlayHistoryService.RecordPlayAsync` 60 秒内重复调用只记录一次
- [x] 所有平台服务（TrayIconService、WindowDragService、ClickThroughService）在句柄无效时不崩溃
- [x] 所有新增公共 API 具有完整 XML 文档注释
