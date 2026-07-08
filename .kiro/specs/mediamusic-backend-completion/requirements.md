# Requirements Document

## Introduction

本需求文档定义MediaMusic音乐播放器项目中未完成后端功能的规格说明。MediaMusic是一个使用C# Blazor和Photino.NET构建的跨平台桌面音乐播放器，使用BASS音频引擎（带NAudio后备）和SQLite数据库。当前项目已实现核心播放功能和数据持久化层，本文档聚焦于完成音频效果处理、均衡器、数据库查询扩展、平台集成和高级服务功能。

## Glossary

- **System**: MediaMusic应用程序整体
- **BASS_Engine**: BASS音频库的包装器，负责音频设备初始化和插件管理
- **Effects_Service**: 音频效果服务，提供淡入、淡出和交叉淡化功能
- **Equalizer_Service**: 均衡器服务，提供多频段音频均衡处理
- **Album_Repository**: 专辑数据访问层
- **Artist_Repository**: 艺术家数据访问层
- **Genre_Repository**: 流派数据访问层
- **EqPreset_Repository**: 均衡器预设数据访问层
- **Tray_Service**: 系统托盘集成服务
- **Window_Manager**: 辅助窗口管理器（迷你播放器、桌面歌词）
- **Window_Drag_Service**: 无边框窗口拖动服务
- **Click_Through_Service**: 窗口点击穿透服务
- **Search_Service**: 全局搜索服务
- **Play_History_Service**: 播放历史追踪服务
- **BASS_Channel**: BASS音频流句柄
- **DSP_Handle**: 数字信号处理效果句柄
- **Native_DLL**: 原生BASS库文件（bass.dll, bassflac.dll等）
- **DbConnectionFactory**: SQLite数据库连接工厂
- **EQ_Band**: 均衡器频段，包含频率和增益参数
- **NotifyIcon**: Windows系统托盘图标

## Requirements

### Requirement 1: 音频淡入效果

**User Story:** 作为用户，我希望播放开始时有音量淡入效果，这样音频过渡更加平滑自然。

#### Acceptance Criteria


1. WHEN FadeInAsync is called with a duration, THE Effects_Service SHALL use Bass.ChannelSlideAttribute to transition the channel volume from 0 to the target volume over the specified duration
2. THE Effects_Service SHALL validate that the duration is between 0 and 5000 milliseconds
3. IF the BASS engine is not available, THE Effects_Service SHALL complete the task without applying effects
4. THE Effects_Service SHALL return a Task that completes when the fade operation begins (not when it finishes)
5. WHEN the fade is in progress and another playback command is issued, THE Effects_Service SHALL cancel the current fade smoothly

### Requirement 2: 音频淡出效果

**User Story:** 作为用户，我希望暂停或停止时有音量淡出效果，这样避免突然的静音。

#### Acceptance Criteria

1. WHEN FadeOutAsync is called with a duration, THE Effects_Service SHALL use Bass.ChannelSlideAttribute to transition the channel volume to 0 over the specified duration
2. WHEN the fade-out completes, THE Effects_Service SHALL pause or stop the channel
3. THE Effects_Service SHALL validate that the duration is between 0 and 5000 milliseconds
4. IF the BASS engine is not available, THE Effects_Service SHALL complete the task without applying effects
5. THE Effects_Service SHALL return a Task that completes when the channel reaches volume 0

### Requirement 3: 音频交叉淡化

**User Story:** 作为用户，我希望切换歌曲时有交叉淡化效果，这样两首歌能无缝衔接。

#### Acceptance Criteria

1. WHEN CrossfadeAsync is called with a duration, THE Effects_Service SHALL overlap the outgoing track fade-out and incoming track fade-in
2. THE Effects_Service SHALL ensure the outgoing track volume decreases from current to 0 while the incoming track volume increases from 0 to target over the same duration
3. THE Effects_Service SHALL validate that the duration is between 0 and 5000 milliseconds
4. IF CrossfadeMs property is 0, THE Effects_Service SHALL skip crossfade and perform instant track switch
5. THE Effects_Service SHALL handle the case where the outgoing track ends before the crossfade completes

### Requirement 4: 均衡器频段应用

**User Story:** 作为用户，我希望能够调整音频的多个频段增益，这样可以自定义音质。

#### Acceptance Criteria


1. WHEN ApplyBands is called with a collection of EQ_Band objects, THE Equalizer_Service SHALL create a BASS_BFX_PEAKEQ DSP effect for each band using Bass.ChannelSetFX
2. THE Equalizer_Service SHALL set each band's center frequency, gain, and bandwidth using Bass.FXSetParameters with PeakEQParameters
3. THE Equalizer_Service SHALL validate that each band's gain is between -12.0 and +12.0 dB
4. THE Equalizer_Service SHALL support at least 10 frequency bands (32, 64, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 Hz)
5. IF the BASS engine is not available, THE Equalizer_Service SHALL return without applying effects
6. WHEN applying new bands, THE Equalizer_Service SHALL remove existing DSP handles before creating new ones

### Requirement 5: 均衡器预设应用

**User Story:** 作为用户，我希望能够应用预设的均衡器配置，这样可以快速切换不同的音效模式。

#### Acceptance Criteria

1. WHEN ApplyPreset is called with an EqPreset object, THE Equalizer_Service SHALL deserialize the Bands JSON property into EQ_Band array
2. THE Equalizer_Service SHALL call ApplyBands with the deserialized band data
3. IF deserialization fails, THE Equalizer_Service SHALL log an error and return without applying effects
4. THE Equalizer_Service SHALL support both built-in and user-created presets without distinction in application logic

### Requirement 6: 均衡器禁用

**User Story:** 作为用户，我希望能够完全关闭均衡器，这样可以听到原始音质。

#### Acceptance Criteria

1. WHEN Disable is called, THE Equalizer_Service SHALL remove all active BASS DSP effect handles from the current channel
2. THE Equalizer_Service SHALL track DSP handles in a collection to enable batch removal
3. THE Equalizer_Service SHALL reset the channel audio to bypass all EQ processing
4. WHEN no DSP handles are active, THE Equalizer_Service SHALL complete without error

### Requirement 7: BASS音频插件加载

**User Story:** 作为开发者，我希望系统自动加载BASS格式插件，这样可以支持FLAC、APE、AAC等高质量格式。

#### Acceptance Criteria


1. WHEN LoadPlugins is called, THE BASS_Engine SHALL attempt to load bassflac.dll, bass_ape.dll, and bass_aac.dll using Bass.PluginLoad
2. WHEN a plugin loads successfully, THE BASS_Engine SHALL log the plugin name and handle
3. IF a plugin fails to load, THE BASS_Engine SHALL log a warning with the Bass.LastError code but continue loading remaining plugins
4. THE BASS_Engine SHALL only call LoadPlugins after Bass.Init has succeeded
5. THE BASS_Engine SHALL store plugin handles to enable unloading during disposal

### Requirement 8: BASS原生库验证

**User Story:** 作为开发者，我希望启动前验证BASS DLL文件存在，这样可以给用户清晰的错误提示而不是崩溃。

#### Acceptance Criteria

1. WHEN Init is called, THE BASS_Engine SHALL check for the existence of bass.dll, bassmix.dll, and bass_fx.dll in the application directory before calling Bass.Init
2. IF any required Native_DLL is missing, THE BASS_Engine SHALL log a warning message indicating which files are missing and set IsAvailable to false
3. IF all required files exist but Bass.Init fails, THE BASS_Engine SHALL log the Bass.LastError and set IsAvailable to false
4. WHEN IsAvailable is false, THE BASS_Engine SHALL allow the application to continue running using NAudio fallback
5. THE BASS_Engine SHALL catch DllNotFoundException and handle it gracefully by logging and disabling BASS features

### Requirement 9: 系统托盘图标显示

**User Story:** 作为用户，我希望应用最小化到系统托盘，这样可以减少任务栏占用并保持音乐播放。

#### Acceptance Criteria

1. WHEN Show is called, THE Tray_Service SHALL create a NotifyIcon with the MediaMusic application icon
2. THE Tray_Service SHALL attach a context menu with items: "Play/Pause", "Next Track", "Show Window", "Settings", "Exit"
3. WHEN the user clicks "Play/Pause", THE Tray_Service SHALL toggle playback through PlayerService
4. WHEN the user clicks "Next Track", THE Tray_Service SHALL call PlayerService.Next
5. WHEN the user clicks "Exit", THE Tray_Service SHALL close the application
6. WHEN the user double-clicks the tray icon, THE Tray_Service SHALL restore the main window


### Requirement 10: 系统托盘图标隐藏

**User Story:** 作为用户，我希望能够移除系统托盘图标，这样可以完全退出应用。

#### Acceptance Criteria

1. WHEN Hide is called, THE Tray_Service SHALL dispose the NotifyIcon instance
2. THE Tray_Service SHALL unregister all event handlers before disposal
3. THE Tray_Service SHALL release all unmanaged resources associated with the tray icon
4. WHEN Hide is called multiple times, THE Tray_Service SHALL handle it safely without errors

### Requirement 11: 迷你播放器窗口

**User Story:** 作为用户，我希望有一个小型的置顶播放器窗口，这样可以在使用其他应用时控制音乐。

#### Acceptance Criteria

1. WHEN ShowMiniPlayer is called, THE Window_Manager SHALL create a new PhotinoBlazorApp instance with size 320x120 pixels
2. THE Window_Manager SHALL set the window to chromeless (no title bar), topmost (always on top), and load the MiniPlayer Blazor component
3. THE Window_Manager SHALL share the same DI container with the main window so PlayerService state stays synchronized
4. THE Window_Manager SHALL track the mini player window handle to prevent creating duplicates
5. WHEN the mini player is already open, THE Window_Manager SHALL bring it to front instead of creating a new instance

### Requirement 12: 桌面歌词窗口

**User Story:** 作为用户,我希望有一个透明的桌面歌词窗口，这样可以在桌面上看到实时歌词而不遮挡其他内容。

#### Acceptance Criteria

1. WHEN ShowDesktopLyrics is called, THE Window_Manager SHALL create a new PhotinoBlazorApp instance with the DesktopLyrics component
2. THE Window_Manager SHALL set the window to chromeless, transparent, and topmost
3. THE Window_Manager SHALL apply WS_EX_TRANSPARENT and WS_EX_LAYERED extended styles via Click_Through_Service to make the window click-through
4. THE Window_Manager SHALL share PlayerService and LyricsService singletons with the main window for real-time lyric synchronization
5. THE Window_Manager SHALL track the lyrics window handle to prevent creating duplicates


### Requirement 13: 辅助窗口关闭

**User Story:** 作为用户，我希望能够关闭迷你播放器或桌面歌词窗口，这样可以根据需要切换界面模式。

#### Acceptance Criteria

1. WHEN CloseMiniPlayer is called, THE Window_Manager SHALL dispose the mini player PhotinoBlazorApp instance
2. WHEN CloseDesktopLyrics is called, THE Window_Manager SHALL dispose the desktop lyrics PhotinoBlazorApp instance
3. THE Window_Manager SHALL clear the window handle reference after disposal
4. WHEN closing a window that is not open, THE Window_Manager SHALL complete without error

### Requirement 14: 无边框窗口拖动

**User Story:** 作为用户，我希望能够拖动自定义标题栏来移动窗口，这样可以像普通窗口一样操作。

#### Acceptance Criteria

1. WHEN StartDrag is called with a window handle, THE Window_Drag_Service SHALL call Win32Interop.ReleaseCapture to release mouse capture
2. THE Window_Drag_Service SHALL send WM_NCLBUTTONDOWN message with HTCAPTION parameter to the window handle via Win32Interop.SendMessage
3. THE Window_Drag_Service SHALL enable the window to respond to mouse drag immediately
4. THE Window_Drag_Service SHALL work for all chromeless windows (main, mini player, lyrics)
5. IF the window handle is invalid, THE Window_Drag_Service SHALL log a warning and return without crashing

### Requirement 15: 点击穿透启用

**User Story:** 作为用户，我希望桌面歌词窗口不阻挡鼠标点击，这样可以操作窗口下方的内容。

#### Acceptance Criteria

1. WHEN Enable is called with a window handle, THE Click_Through_Service SHALL call Win32Interop.GetWindowLong to retrieve current extended styles
2. THE Click_Through_Service SHALL call Win32Interop.SetWindowLong with WS_EX_LAYERED and WS_EX_TRANSPARENT flags added to enable click-through
3. THE Click_Through_Service SHALL maintain window transparency while making it click-through
4. WHEN applied to the lyrics window, THE Click_Through_Service SHALL allow mouse events to pass through to applications behind it


### Requirement 16: 点击穿透禁用

**User Story:** 作为用户，我希望能够临时禁用歌词窗口的点击穿透，这样可以移动或关闭窗口。

#### Acceptance Criteria

1. WHEN Disable is called with a window handle, THE Click_Through_Service SHALL call Win32Interop.GetWindowLong to retrieve current extended styles
2. THE Click_Through_Service SHALL call Win32Interop.SetWindowLong to remove WS_EX_TRANSPARENT flag while preserving WS_EX_LAYERED
3. THE Click_Through_Service SHALL make the window interactive again so users can drag or close it
4. THE Click_Through_Service SHALL preserve window transparency after disabling click-through

### Requirement 17: 专辑列表查询

**User Story:** 作为用户，我希望浏览所有专辑，这样可以按专辑选择音乐播放。

#### Acceptance Criteria

1. WHEN GetAllAsync is called, THE Album_Repository SHALL query all albums from the Albums table ordered by Title
2. THE Album_Repository SHALL return a collection of Album objects with Id, Title, ArtistId, Year, and CoverPath properties
3. THE Album_Repository SHALL use the existing DbConnectionFactory to create database connections
4. THE Album_Repository SHALL use Dapper for query execution consistent with existing repository patterns
5. WHEN the Albums table is empty, THE Album_Repository SHALL return an empty collection

### Requirement 18: 专辑搜索

**User Story:** 作为用户，我希望按名称搜索专辑，这样可以快速找到目标专辑。

#### Acceptance Criteria

1. WHEN SearchAsync is called with a search term, THE Album_Repository SHALL query albums where Title contains the search term (case-insensitive)
2. THE Album_Repository SHALL use the NormalizedTitle column for efficient searching
3. THE Album_Repository SHALL return results ordered by relevance (exact match first, then partial matches)
4. WHEN the search term is empty or null, THE Album_Repository SHALL return all albums
5. THE Album_Repository SHALL limit results to 100 albums to ensure performance


### Requirement 19: 艺术家专辑查询

**User Story:** 作为用户，我希望查看某个艺术家的所有专辑，这样可以浏览该艺术家的作品集。

#### Acceptance Criteria

1. WHEN GetByArtistAsync is called with an artist ID, THE Album_Repository SHALL query albums where ArtistId matches the given ID
2. THE Album_Repository SHALL return albums ordered by Year descending, then by Title
3. THE Album_Repository SHALL use the IX_Albums_ArtistId index (if exists) for efficient querying
4. WHEN no albums exist for the artist, THE Album_Repository SHALL return an empty collection

### Requirement 20: 专辑曲目查询

**User Story:** 作为用户，我希望查看专辑的所有曲目，这样可以播放整张专辑。

#### Acceptance Criteria

1. WHEN GetTracksAsync is called with an album ID, THE Album_Repository SHALL query tracks from the Tracks table where AlbumId matches
2. THE Album_Repository SHALL return tracks ordered by TrackNo ascending, then by Title
3. THE Album_Repository SHALL use the IX_Tracks_AlbumId index for efficient querying
4. THE Album_Repository SHALL return Track objects with all metadata fields populated
5. WHEN the album has no tracks, THE Album_Repository SHALL return an empty collection

### Requirement 21: 艺术家列表查询

**User Story:** 作为用户，我希望浏览所有艺术家，这样可以按艺术家选择音乐播放。

#### Acceptance Criteria

1. WHEN GetAllAsync is called, THE Artist_Repository SHALL query all artists from the Artists table ordered by Name
2. THE Artist_Repository SHALL return a collection of Artist objects with Id, Name, and CoverPath properties
3. THE Artist_Repository SHALL use the existing DbConnectionFactory to create database connections
4. THE Artist_Repository SHALL use Dapper for query execution consistent with existing repository patterns
5. WHEN the Artists table is empty, THE Artist_Repository SHALL return an empty collection


### Requirement 22: 艺术家搜索

**User Story:** 作为用户，我希望按名称搜索艺术家，这样可以快速找到目标艺术家。

#### Acceptance Criteria

1. WHEN SearchAsync is called with a search term, THE Artist_Repository SHALL query artists where Name contains the search term (case-insensitive)
2. THE Artist_Repository SHALL use the NormalizedName column for efficient searching
3. THE Artist_Repository SHALL return results ordered by relevance (exact match first, then partial matches)
4. WHEN the search term is empty or null, THE Artist_Repository SHALL return all artists
5. THE Artist_Repository SHALL limit results to 100 artists to ensure performance

### Requirement 23: 艺术家专辑关联查询

**User Story:** 作为用户，我希望查看某个艺术家的所有专辑，这样可以探索该艺术家的作品。

#### Acceptance Criteria

1. WHEN GetAlbumsAsync is called with an artist ID, THE Artist_Repository SHALL query albums from the Albums table where ArtistId matches
2. THE Artist_Repository SHALL return albums ordered by Year descending, then by Title
3. WHEN no albums exist for the artist, THE Artist_Repository SHALL return an empty collection
4. THE Artist_Repository SHALL leverage the foreign key relationship between Albums and Artists

### Requirement 24: 艺术家曲目查询

**User Story:** 作为用户，我希望查看某个艺术家的所有曲目，这样可以播放该艺术家的全部作品。

#### Acceptance Criteria

1. WHEN GetTracksAsync is called with an artist ID, THE Artist_Repository SHALL query tracks from the Tracks table where ArtistId matches
2. THE Artist_Repository SHALL return tracks ordered by Year descending, then by Album, then by TrackNo
3. THE Artist_Repository SHALL use the IX_Tracks_ArtistId index for efficient querying
4. WHEN no tracks exist for the artist, THE Artist_Repository SHALL return an empty collection


### Requirement 25: 流派列表查询

**User Story:** 作为用户，我希望浏览所有音乐流派，这样可以按流派筛选音乐。

#### Acceptance Criteria

1. WHEN GetAllAsync is called, THE Genre_Repository SHALL query all genres from the Genres table ordered by Name
2. THE Genre_Repository SHALL return a collection of Genre objects with Id and Name properties
3. THE Genre_Repository SHALL use the existing DbConnectionFactory to create database connections
4. WHEN the Genres table is empty, THE Genre_Repository SHALL return an empty collection

### Requirement 26: 流派详情查询

**User Story:** 作为开发者，我需要按ID获取流派详情，这样可以在UI中显示流派信息。

#### Acceptance Criteria

1. WHEN GetByIdAsync is called with a genre ID, THE Genre_Repository SHALL query the genre from the Genres table where Id matches
2. THE Genre_Repository SHALL return a Genre object with Id and Name properties
3. WHEN the genre ID does not exist, THE Genre_Repository SHALL return null
4. THE Genre_Repository SHALL use parameterized queries to prevent SQL injection

### Requirement 27: 流派曲目查询

**User Story:** 作为用户，我希望查看某个流派的所有曲目，这样可以播放该流派的音乐合集。

#### Acceptance Criteria

1. WHEN GetTracksAsync is called with a genre ID, THE Genre_Repository SHALL query tracks from the Tracks table where GenreId matches
2. THE Genre_Repository SHALL return tracks ordered by Artist, then by Album, then by TrackNo
3. THE Genre_Repository SHALL use the IX_Tracks_GenreId index for efficient querying
4. WHEN no tracks exist for the genre, THE Genre_Repository SHALL return an empty collection

### Requirement 28: 均衡器预设详情查询

**User Story:** 作为用户，我希望按ID加载均衡器预设，这样可以应用保存的音效配置。

#### Acceptance Criteria


1. WHEN GetByIdAsync is called with a preset ID, THE EqPreset_Repository SHALL query the preset from the EqPresets table where Id matches
2. THE EqPreset_Repository SHALL return an EqPreset object with Id, Name, Bands (JSON), IsBuiltIn, and CreatedAt properties
3. WHEN the preset ID does not exist, THE EqPreset_Repository SHALL return null
4. THE EqPreset_Repository SHALL use parameterized queries to prevent SQL injection

### Requirement 29: 均衡器预设删除

**User Story:** 作为用户，我希望删除自定义的均衡器预设，这样可以清理不需要的配置。

#### Acceptance Criteria

1. WHEN DeleteAsync is called with a preset ID, THE EqPreset_Repository SHALL check if IsBuiltIn is true
2. IF the preset is built-in, THE EqPreset_Repository SHALL throw an InvalidOperationException with a descriptive message
3. IF the preset is user-created, THE EqPreset_Repository SHALL delete the record from the EqPresets table
4. WHEN the preset ID does not exist, THE EqPreset_Repository SHALL complete without error
5. THE EqPreset_Repository SHALL return the number of rows affected (0 or 1)

### Requirement 30: 均衡器预设创建

**User Story:** 作为用户，我希望保存当前均衡器设置为新预设，这样可以重复使用喜欢的音效配置。

#### Acceptance Criteria

1. WHEN CreateAsync is called with a name and EQ_Band collection, THE EqPreset_Repository SHALL serialize the bands to JSON
2. THE EqPreset_Repository SHALL insert a new record into the EqPresets table with IsBuiltIn set to 0 (false)
3. THE EqPreset_Repository SHALL validate that the name is not empty and does not conflict with existing presets
4. IF a preset with the same name exists, THE EqPreset_Repository SHALL throw an InvalidOperationException
5. THE EqPreset_Repository SHALL return the ID of the newly created preset

### Requirement 31: 全局搜索功能

**User Story:** 作为用户，我希望能够搜索曲目、专辑、艺术家和流派，这样可以快速找到任何音乐内容。

#### Acceptance Criteria


1. WHEN SearchAllAsync is called with a search term, THE Search_Service SHALL query Track_Repository, Album_Repository, Artist_Repository, and Genre_Repository concurrently
2. THE Search_Service SHALL aggregate results into a unified SearchResult object containing Tracks, Albums, Artists, and Genres collections
3. THE Search_Service SHALL rank results by relevance: exact title/name matches first, then partial matches
4. THE Search_Service SHALL limit each category to 20 results to ensure responsive UI
5. WHEN the search term is empty or whitespace, THE Search_Service SHALL return empty results

### Requirement 32: 搜索建议

**User Story:** 作为用户，我希望在输入搜索词时看到自动完成建议，这样可以更快地找到目标内容。

#### Acceptance Criteria

1. WHEN GetSuggestionsAsync is called with a partial search term (minimum 2 characters), THE Search_Service SHALL return up to 10 matching track titles, album titles, and artist names
2. THE Search_Service SHALL prioritize suggestions based on play count and last played date
3. THE Search_Service SHALL debounce consecutive calls within 300ms to avoid excessive database queries
4. THE Search_Service SHALL return results ordered by relevance and popularity
5. WHEN the search term is less than 2 characters, THE Search_Service SHALL return empty suggestions

### Requirement 33: 搜索历史管理

**User Story:** 作为用户，我希望系统记住我的搜索历史，这样可以快速重复之前的搜索。

#### Acceptance Criteria

1. WHEN a search is performed, THE Search_Service SHALL save the search term to a SearchHistory table with timestamp
2. WHEN GetRecentSearchesAsync is called, THE Search_Service SHALL return the last 10 unique search terms ordered by timestamp descending
3. THE Search_Service SHALL automatically remove duplicate search terms, keeping only the most recent occurrence
4. WHEN ClearHistoryAsync is called, THE Search_Service SHALL delete all search history records
5. THE Search_Service SHALL limit history to 100 entries, automatically removing oldest entries when exceeded


### Requirement 34: 播放历史记录

**User Story:** 作为用户，我希望系统记录我的播放历史，这样可以回顾听过的歌曲。

#### Acceptance Criteria

1. WHEN a track completes playback (reaches 80% duration or ends naturally), THE Play_History_Service SHALL insert a record into a PlayHistory table with TrackId and timestamp
2. THE Play_History_Service SHALL not record the same track more than once per minute to avoid duplicate entries from repeat mode
3. THE Play_History_Service SHALL execute recording asynchronously to avoid blocking playback
4. IF database insertion fails, THE Play_History_Service SHALL log the error but not interrupt playback
5. THE Play_History_Service SHALL integrate with PlayerService.IncrementPlayCountAsync to maintain consistency

### Requirement 35: 最近播放查询

**User Story:** 作为用户，我希望查看最近播放的曲目列表，这样可以重新播放最近听过的歌曲。

#### Acceptance Criteria

1. WHEN GetRecentlyPlayedAsync is called with a limit parameter, THE Play_History_Service SHALL query the PlayHistory table joined with Tracks table
2. THE Play_History_Service SHALL return tracks ordered by PlayedAt timestamp descending
3. THE Play_History_Service SHALL deduplicate tracks, showing only the most recent play for each track
4. THE Play_History_Service SHALL default to 50 tracks when no limit is specified
5. THE Play_History_Service SHALL return Track objects with all metadata fields populated

### Requirement 36: 播放统计分析

**User Story:** 作为用户，我希望看到播放统计数据，这样可以了解我的听歌习惯。

#### Acceptance Criteria

1. WHEN GetTopTracksAsync is called with a time range and limit, THE Play_History_Service SHALL query the most frequently played tracks within the specified period
2. THE Play_History_Service SHALL return tracks ordered by play count descending, then by last played date
3. THE Play_History_Service SHALL support time range filters: Last 7 Days, Last 30 Days, Last Year, All Time
4. WHEN GetTotalPlayTimeAsync is called, THE Play_History_Service SHALL sum the duration of all played tracks from the PlayHistory table
5. THE Play_History_Service SHALL return statistics as a PlayStatistics object containing TotalPlayCount, TotalPlayTimeMs, and TopGenres


### Requirement 37: 数据库架构扩展

**User Story:** 作为开发者，我需要扩展数据库架构以支持搜索历史和播放历史功能，这样新功能可以持久化数据。

#### Acceptance Criteria

1. THE System SHALL create a SearchHistory table with columns: Id (INTEGER PRIMARY KEY), SearchTerm (TEXT), SearchedAt (TEXT)
2. THE System SHALL create a PlayHistory table with columns: Id (INTEGER PRIMARY KEY), TrackId (INTEGER FOREIGN KEY), PlayedAt (TEXT)
3. THE System SHALL create an index IX_PlayHistory_TrackId on PlayHistory(TrackId) for efficient track lookup
4. THE System SHALL create an index IX_PlayHistory_PlayedAt on PlayHistory(PlayedAt) for efficient time-based queries
5. THE System SHALL execute schema migrations automatically during DbInitializer.InitializeAsync
6. THE System SHALL handle the case where tables already exist without errors (CREATE TABLE IF NOT EXISTS)

### Requirement 38: 服务注册完整性

**User Story:** 作为开发者，我需要所有新服务在DI容器中正确注册，这样组件可以通过依赖注入获取服务实例。

#### Acceptance Criteria

1. WHEN ServiceRegistration.Register is called, THE System SHALL register Search_Service as a singleton
2. WHEN ServiceRegistration.Register is called, THE System SHALL register Play_History_Service as a singleton
3. THE System SHALL ensure all repository dependencies are available as scoped services
4. THE System SHALL ensure all singleton services are thread-safe for concurrent access
5. THE System SHALL validate that service registrations do not create circular dependencies

### Requirement 39: 异常处理和日志

**User Story:** 作为开发者，我需要所有服务具有一致的异常处理和日志记录，这样可以诊断和修复问题。

#### Acceptance Criteria

1. WHEN any service method encounters an exception, THE System SHALL log the exception with ERROR level including method name, parameters, and stack trace
2. THE System SHALL use ILogger<T> for structured logging consistent with existing services
3. THE System SHALL not throw exceptions for non-critical failures (e.g., missing BASS DLLs, failed tray icon creation)
4. WHEN database operations fail, THE System SHALL log the SQL query (sanitized) and exception details
5. THE System SHALL log INFO level messages for successful initialization of major subsystems (BASS engine, plugins, windows)


### Requirement 40: 资源释放和清理

**User Story:** 作为开发者，我需要所有服务正确实现资源释放，这样避免内存泄漏和资源耗尽。

#### Acceptance Criteria

1. WHERE a service manages unmanaged resources (BASS handles, NotifyIcon, window handles), THE System SHALL implement IDisposable interface
2. WHEN Dispose is called, THE System SHALL release all DSP handles, channel handles, and Win32 resources
3. THE System SHALL set disposed flags and guard against multiple Dispose calls
4. WHEN application exits, THE System SHALL ensure all singleton services are disposed in reverse dependency order
5. THE System SHALL log resource disposal at DEBUG level for troubleshooting

### Requirement 41: 线程安全要求

**User Story:** 作为开发者，我需要确保单例服务线程安全，这样多个Blazor组件可以并发访问服务。

#### Acceptance Criteria

1. WHERE a service is registered as singleton and has mutable state, THE System SHALL use appropriate synchronization primitives (lock, SemaphoreSlim, ConcurrentDictionary)
2. THE Effects_Service SHALL protect BASS channel operations with locks when fade operations are in progress
3. THE Equalizer_Service SHALL protect DSP handle collections with locks during ApplyBands and Disable operations
4. THE Window_Manager SHALL protect window handle dictionaries with locks during ShowMiniPlayer and CloseMiniPlayer
5. THE Play_History_Service SHALL use async-safe data structures for queuing history records

### Requirement 42: 性能优化要求

**User Story:** 作为用户，我希望所有数据库查询快速响应，这样UI保持流畅不卡顿。

#### Acceptance Criteria

1. THE System SHALL use existing database indexes (IX_Tracks_AlbumId, IX_Tracks_ArtistId, IX_Tracks_GenreId, IX_Tracks_Title) for all queries
2. WHERE full-text search is needed, THE System SHALL use LIKE queries with indexed columns (NormalizedTitle, NormalizedName)
3. THE System SHALL limit result sets to reasonable sizes (50-100 records) by default
4. THE Search_Service SHALL execute concurrent repository queries using Task.WhenAll to minimize total latency
5. THE System SHALL avoid N+1 query problems by using JOINs instead of iterative queries


### Requirement 43: BASS DSP效果链管理

**User Story:** 作为开发者，我需要正确管理BASS DSP效果句柄链，这样可以避免资源泄漏和音频故障。

#### Acceptance Criteria

1. WHEN multiple DSP effects are applied to a channel, THE Equalizer_Service SHALL maintain a collection of active DSP handles
2. WHEN Disable is called, THE Equalizer_Service SHALL iterate through all DSP handles and call Bass.ChannelRemoveFX for each
3. WHEN ApplyBands is called while DSP effects are active, THE Equalizer_Service SHALL remove old effects before creating new ones
4. THE Equalizer_Service SHALL validate that Bass.ChannelSetFX returns a non-zero handle before storing it
5. IF Bass.ChannelSetFX fails, THE Equalizer_Service SHALL log the error with Bass.LastError and skip that band

### Requirement 44: 音频效果参数验证

**User Story:** 作为开发者，我需要验证所有音频效果参数，这样可以防止BASS API调用失败。

#### Acceptance Criteria

1. WHEN FadeInAsync or FadeOutAsync is called, THE Effects_Service SHALL validate duration is between 0 and 5000 milliseconds
2. WHEN ApplyBands is called, THE Equalizer_Service SHALL validate each band's gain is between -12.0 and +12.0 dB
3. WHEN ApplyBands is called, THE Equalizer_Service SHALL validate each band's frequency is between 20 and 20000 Hz
4. IF validation fails, THE System SHALL throw ArgumentOutOfRangeException with descriptive message
5. THE System SHALL sanitize null or empty collections by treating them as no-op requests

### Requirement 45: Win32互操作错误处理

**User Story:** 作为开发者，我需要正确处理Win32 API调用失败，这样平台服务可以优雅降级。

#### Acceptance Criteria

1. WHEN Win32Interop.GetWindowLong returns 0, THE System SHALL call Marshal.GetLastWin32Error and log the error code
2. IF Win32Interop.SendMessage fails, THE Window_Drag_Service SHALL log a warning but not crash the application
3. IF NotifyIcon creation fails, THE Tray_Service SHALL log the exception and set a flag indicating tray is unavailable
4. THE System SHALL wrap all Win32 API calls in try-catch blocks to prevent unhandled exceptions
5. WHEN running on non-Windows platforms, THE System SHALL detect the OS and disable Win32-dependent services


### Requirement 46: XML文档注释完整性

**User Story:** 作为开发者，我需要所有公共API具有XML文档注释，这样IDE可以提供智能提示和文档。

#### Acceptance Criteria

1. THE System SHALL add XML summary comments to all public classes describing their purpose and PRD section reference
2. THE System SHALL add XML summary comments to all public methods describing their behavior and parameters
3. THE System SHALL add XML param comments for all method parameters explaining expected values and constraints
4. THE System SHALL add XML returns comments for all non-void methods describing return values
5. THE System SHALL add XML exception comments for all exceptions that methods can throw

### Requirement 47: 取消令牌支持

**User Story:** 作为开发者，我需要异步方法支持取消令牌，这样长时间运行的操作可以被取消。

#### Acceptance Criteria

1. WHERE an async method performs I/O or long-running operations, THE System SHALL accept a CancellationToken parameter with default value of default
2. THE System SHALL pass CancellationToken to Dapper async methods (QueryAsync, ExecuteAsync)
3. WHEN FadeInAsync or FadeOutAsync is in progress, THE Effects_Service SHALL check CancellationToken.IsCancellationRequested periodically
4. IF cancellation is requested during fade operations, THE Effects_Service SHALL stop the slide and restore immediate volume control
5. THE System SHALL properly handle OperationCanceledException by logging and cleaning up partial state

### Requirement 48: EQ预设序列化格式

**User Story:** 作为开发者，我需要定义清晰的EQ预设JSON格式，这样前后端可以一致地处理预设数据。

#### Acceptance Criteria

1. THE System SHALL serialize EQ_Band objects to JSON array format: [{"Frequency": 60.0, "Gain": 3.0, "Bandwidth": 1.0}, ...]
2. THE System SHALL use System.Text.Json with case-insensitive property matching for deserialization
3. WHEN deserializing preset JSON, THE Equalizer_Service SHALL validate that all required properties (Frequency, Gain) are present
4. IF JSON deserialization fails, THE Equalizer_Service SHALL log the exception and return empty band array
5. THE System SHALL support forward compatibility by ignoring unknown JSON properties


### Requirement 49: 搜索相关性排序算法

**User Story:** 作为用户，我希望搜索结果按相关性排序，这样最匹配的结果显示在前面。

#### Acceptance Criteria

1. WHEN search results contain exact matches, THE Search_Service SHALL rank them higher than partial matches
2. THE Search_Service SHALL use a weighted scoring system: exact title match (score 100), starts with term (score 50), contains term (score 25)
3. THE Search_Service SHALL boost scores for tracks with higher play counts (add log10(PlayCount) to score)
4. THE Search_Service SHALL boost scores for recently played tracks (add recency factor: days_ago < 7 adds 10 points)
5. THE Search_Service SHALL return results ordered by final score descending

### Requirement 50: 播放历史去重策略

**User Story:** 作为用户，我希望播放历史避免短时间内的重复记录，这样历史列表更简洁有意义。

#### Acceptance Criteria

1. WHEN recording play history, THE Play_History_Service SHALL check if the same TrackId was recorded within the last 60 seconds
2. IF a duplicate within 60 seconds is detected, THE Play_History_Service SHALL skip recording and log a debug message
3. THE Play_History_Service SHALL use an in-memory cache (ConcurrentDictionary) to track recently recorded track IDs and timestamps
4. THE Play_History_Service SHALL automatically expire cache entries older than 2 minutes to prevent memory growth
5. WHEN the application restarts, THE Play_History_Service SHALL rebuild the cache from the last 100 PlayHistory records

## 技术约束总结

1. **音频库**: 必须使用ManagedBass包装BASS音频库，所有DSP效果通过Bass.ChannelSetFX和Bass.FXSetParameters实现
2. **ORM**: 必须使用Dapper作为数据访问层，保持与现有Repository模式一致
3. **数据库**: SQLite 3.x，通过DbConnectionFactory创建连接，使用WAL模式和外键约束
4. **平台支持**: Windows桌面应用，使用Win32 API实现平台特定功能
5. **UI框架**: Blazor Server托管在Photino.NET中，多窗口通过共享DI容器同步状态
6. **依赖注入**: 所有服务通过ServiceRegistration.cs注册，音频和平台服务为Singleton，Repository为Scoped
7. **异步模式**: 所有I/O操作使用async/await，数据库查询使用Dapper异步方法
8. **日志**: 使用ILogger<T>接口，遵循Microsoft.Extensions.Logging约定


## 质量属性要求

### 可维护性

- 所有公共API必须具有完整的XML文档注释（`<summary>`, `<param>`, `<returns>`, `<exception>`）
- 代码复杂度：单个方法圈复杂度不超过15
- 命名约定：遵循C#标准命名规范（PascalCase for public members, camelCase for private fields）

### 可靠性

- 所有异常必须被捕获并记录日志，关键路径（播放、UI交互）不能因异常而崩溃
- BASS库缺失时应用必须能够启动并使用NAudio后备引擎
- 数据库操作失败不应影响用户界面响应性

### 性能

- 数据库查询响应时间: 单表查询 < 50ms, 关联查询 < 100ms (SSD硬盘)
- 搜索响应时间: 全局搜索 < 200ms (包含4个并发查询)
- 音频效果应用延迟: < 10ms (不影响播放连续性)
- UI线程阻塞时间: 所有I/O操作异步执行，UI线程阻塞 < 16ms

### 可测试性

- 所有Repository方法必须可单元测试（使用in-memory SQLite）
- 服务层必须通过接口依赖注入，便于mock测试
- BASS引擎操作必须在IsAvailable为false时可测试（不依赖原生DLL）

### 安全性

- 所有SQL查询必须使用参数化防止SQL注入
- 用户输入（搜索词、预设名称）必须进行长度和字符验证
- Win32 API调用必须验证窗口句柄有效性防止越权访问

### 兼容性

- 支持Windows 10 1809及以上版本
- 支持.NET 8.0运行时
- 数据库架构必须向前兼容（新增表和列不影响旧版本）

## 非功能性需求

### NFR-1: 启动性能

THE System SHALL complete initialization (BASS engine, database connection, service registration) within 2 seconds on typical hardware (SSD, 8GB RAM).

### NFR-2: 内存使用

WHILE the application is running, THE System SHALL maintain total memory usage below 200 MB with a library of 10,000 tracks.

### NFR-3: 并发处理

THE System SHALL support concurrent access from multiple Blazor components to singleton services without data corruption or deadlocks.

### NFR-4: 日志记录

THE System SHALL log all ERROR and WARNING level events with sufficient context (method name, parameters, stack trace) to enable remote troubleshooting.

### NFR-5: 优雅降级

WHEN optional components fail (BASS DLLs missing, tray icon creation fails), THE System SHALL continue operating with reduced functionality and log informative messages.
