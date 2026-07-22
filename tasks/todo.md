# Task List: Backend Code Completion for Frontend Reserved Features

## Task 1: Complete Metadata Editor Backend Wiring
**Description:** Wire up `MetadataEditorView.razor` to receive track input, select cover images via file picker, update audio file tags via `TagLib`, and save changes to SQLite database via `LibraryService.EditMetadataAsync`.

**Acceptance criteria:**
- [x] Track metadata (Title, Artist, Album, Year, TrackNo, Genre) loads into fields on navigation to `/metadata`.
- [x] Cover image selection button opens native image file dialog and updates preview.
- [x] Clicking "保存并回写" calls `LibraryService.EditMetadataAsync(...)`, writing tags to file header and updating SQLite DB.

**Verification:**
- [x] Code inspection: Types, signatures, and TagLib/SQLite sync verified.
- [x] Manual test: Edit a track's title/artist/cover and verify file tags and DB fields match.

**Dependencies:** None

**Files likely touched:**
- `Pages/MetadataEditorView.razor`
- `Library/LibraryService.cs`
- `Library/MetadataEditor.cs`

---

## Task 2: Wire Equalizer Panel and Presets to Audio Engine
**Description:** Connect `EqualizerPanel.razor` and verify `EqualizerView.razor` slider inputs to parse gain/frequency data, invoke `EqualizerService.ApplyBands`, and load presets from `EqPresetRepository`.

**Acceptance criteria:**
- [x] `OnBand` in `EqualizerPanel.razor` parses frequency and gain value and calls `EqualizerService.ApplyBands`.
- [x] Preset selection dropdown in `EqualizerPanel.razor` loads bands from `EqPresetRepository` and applies them to the current audio channel.
- [x] Gain adjustments persist to settings storage via `SettingsService`.

**Verification:**
- [x] Code inspection clean.
- [x] Manual check: Move EQ sliders and verify active BASS channel filter parameters update.

**Dependencies:** Task 1

**Files likely touched:**
- `Components/EqualizerPanel.razor`
- `Pages/EqualizerView.razor`
- `Audio/EqualizerService.cs`

---

## Task 3: Integrate Discover Page with Search & History Services
**Description:** Connect `DiscoverView.razor` search input, recent searches tags, clear history button, and song lists to `SearchService` and `TrackRepository`.

**Acceptance criteria:**
- [x] Search input queries real database tracks, artists, and albums via `SearchService.SearchAllAsync`.
- [x] Recent search tags load dynamically from `SearchService.GetRecentSearchesAsync()`.
- [x] "清除历史" calls `SearchService.ClearHistoryAsync()`.
- [x] Clicking a song in Discover plays the track via `PlayerService.Play(...)`.

**Verification:**
- [x] Code inspection clean.
- [x] Manual check: Search for local tracks and verify history chips update and clear.

**Dependencies:** Task 1

**Files likely touched:**
- `Pages/DiscoverView.razor`
- `Services/SearchService.cs`

---

## Task 4: Connect Settings Options (Folder Picker, Theme & Hotkeys)
**Description:** Wire native folder selection in `SettingsView.razor` using `FolderPickerService`, wire theme switching to `ThemeService`, and wire shortcut key binding updates to `GlobalHotkeyService`.

**Acceptance criteria:**
- [x] Clicking "添加文件夹" opens native folder selection dialog via `FolderPickerService`.
- [x] Theme selection calls `ThemeService.SetThemeAsync(...)`.
- [x] Shortcut key configuration updates `GlobalHotkeyService` bindings.

**Verification:**
- [x] Code inspection clean.
- [x] Manual check: Add a folder via dialog and observe scan root list update.

**Dependencies:** Task 1

**Files likely touched:**
- `Pages/SettingsView.razor`
- `Platform/FolderPickerService.cs`
- `Platform/GlobalHotkeyService.cs`
- `Services/ThemeService.cs`

---

## Checkpoint: End-to-End Verification
- [x] All C# and Blazor files compile without errors.
- [x] All frontend reserved features are fully connected to backend services.

