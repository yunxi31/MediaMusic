# Backend Implementation Plan: MediaMusic Frontend Reserved Features

## Overview
This plan details the backend completion for all frontend-reserved features in **MediaMusic** (a C# / Blazor / Photino local music player). Several UI views currently feature stubs, mock data, or `TODO` comments. This project completes the backend services, P/Invoke bindings, database queries, and Blazor page state connections to make every feature fully operational and production-ready.

---

## Architectural Mapping & Dependencies

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Blazor Frontend Pages                           │
│ (MetadataEditorView, EqualizerPanel, DiscoverView, SettingsView, etc.) │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │
                                   ▼
┌────────────────────────────────────────────────────────────────────────┐
│                          Backend Services                              │
│   (LibraryService, EqualizerService, SearchService, SettingsService,   │
│         ThemeService, GlobalHotkeyService, FolderPickerService)        │
└──────────────────┬──────────────────────────────────┬──────────────────┘
                   │                                  │
                   ▼                                  ▼
┌────────────────────────────────────┐ ┌─────────────────────────────────┐
│     SQLite Repositories (Dapper)   │ │      Audio & Win32 Engines      │
│(TrackRepo, EqPresetRepo, SearchHst)│ │(BassEngine, TagLib, Win32Hooks) │
└────────────────────────────────────┘ └─────────────────────────────────┘
```

---

## Task List & Phases

### Phase 1: Metadata Editor Backend Completion
- [ ] Task 1: Complete Metadata Editor page logic, file tag writing, cover art selection, and database synchronization.

### Checkpoint: Metadata Editing
- [ ] Edit tags on local FLAC/MP3 files.
- [ ] Verify ID3/FLAC tag header writeback and SQLite database update.

### Phase 2: Equalizer & Audio Engine Connection
- [ ] Task 2: Connect `EqualizerPanel.razor` and `EqualizerView.razor` to `EqualizerService`, `EffectsService`, and `EqPresetRepository`.

### Checkpoint: Equalizer Controls
- [ ] 10-band slider gain updates apply to active BASS audio channel in real-time.
- [ ] Preset selection loads and applies frequency bands from database.

### Phase 3: Real Search & Discover Integration
- [ ] Task 3: Replace mock data in `DiscoverView.razor` with `SearchService` & `TrackRepository` database integration.

### Checkpoint: Search & History
- [ ] Search query queries database with debouncing and relevance ranking.
- [ ] Recent search tags load from SQLite and clear via `SearchService.ClearHistoryAsync()`.

### Phase 4: Settings & Platform Integration
- [ ] Task 4: Connect folder picker in Settings (`FolderPickerService`), live theme switching (`ThemeService`), and hotkey registration (`GlobalHotkeyService`).

### Checkpoint: Settings & Platform
- [ ] Folder picker opens native dialog and updates scan roots.
- [ ] Theme changes toggle light/dark modes dynamically.
- [ ] Global hotkeys register system key hooks.

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| TagLib write failures on open/locked audio files | Medium | Wrap file saving in try-catch with fallback database-only update and user feedback |
| WASAPI/ASIO device selection mismatch on non-Windows systems | Low | Guard Windows P/Invoke calls and audio output changes with `OperatingSystem.IsWindows()` checks |
| Concurrent SQLite writes during search history logging | Low | Utilize existing `ExecuteWithRetryAsync` logic and retry loops for `SQLITE_BUSY` errors |
