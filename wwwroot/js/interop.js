// MediaMusic JS interop layer.
// Exposes namespaced helpers under `window.mediamusic.*` that Blazor components
// invoke via IJSRuntime. Window-control calls go through Photino.NET's injected
// `window.Photino.window` API defensively (no-op if unavailable).

window.mediamusic = window.mediamusic || {};

// Flag set by Blazor when user is actively recording a shortcut key binding.
// While true, the global keydown handler is suppressed so Blazor can capture.
window.mediamusic._isRecordingShortcut = false;
window.mediamusic.setRecording = function(active) {
    window.mediamusic._isRecordingShortcut = !!active;
};

// --- Theme switching (PRD §3.1) ---
window.mediamusic.theme = {
    set(theme) {
        const root = document.documentElement;
        if (theme === 'dark') root.classList.add('dark');
        else root.classList.remove('dark');
    },
    toggle() {
        const root = document.documentElement;
        root.classList.toggle('dark');
        return root.classList.contains('dark') ? 'dark' : 'light';
    }
};

// --- Chromeless window controls (PRD §3.1) ---
window.mediamusic.window = {
    minimize() {
        if (window.DotNet) {
            window.DotNet.invokeMethodAsync('MediaMusic', 'MinimizeWindow');
        }
    },
    maximize() {
        if (window.DotNet) {
            window.DotNet.invokeMethodAsync('MediaMusic', 'MaximizeWindow');
        }
    },
    close() {
        if (window.DotNet) {
            window.DotNet.invokeMethodAsync('MediaMusic', 'CloseWindow');
        }
    }
};

// --- Title bar drag (PRD §3.1, chromeless) ---
window.mediamusic.drag = {
    init() {
        document.addEventListener('mousedown', (e) => {
            const dragRegion = e.target.closest('#titlebar-drag-region');
            if (e.button !== 0) return;

            if (dragRegion || isChromelessTopDragSurface(e)) {
                if (window.DotNet) {
                    window.DotNet.invokeMethodAsync('MediaMusic', 'StartDragWindow');
                }
            }
        });

        document.addEventListener('dblclick', (e) => {
            const dragRegion = e.target.closest('#titlebar-drag-region');
            if (dragRegion || isChromelessTopDragSurface(e)) {
                if (window.DotNet) {
                    window.DotNet.invokeMethodAsync('MediaMusic', 'MaximizeWindow');
                }
            }
        });
    }
};

function isChromelessTopDragSurface(e) {
    if (e.clientY > 48) return false;

    const interactiveSelector = [
        'a',
        'button',
        'input',
        'select',
        'textarea',
        '[role="button"]',
        '[contenteditable="true"]',
        '[data-no-window-drag]'
    ].join(',');

    return !e.target.closest(interactiveSelector);
}

// Initialize drag listener immediately.
window.mediamusic.drag.init();

// Disable default right-click context menu
document.addEventListener('contextmenu', e => e.preventDefault());

// --- Native platform bridges (folder picker, etc.) ---
window.mediamusic.platform = {
    // Shows a native Win32 folder-picker dialog (IFileOpenDialog + FOS_PICKFOLDERS).
    // Returns the chosen path string, or null if the user cancelled.
    // Delegates to the C# [JSInvokable("PickFolder")] static method on FolderPicker.
    pickFolder() {
        if (window.DotNet) {
            return window.DotNet.invokeMethodAsync('MediaMusic', 'PickFolder');
        }
        return Promise.resolve(null);
    }
};

// Measure element-relative click position for the progress bar seek.
window.mediamusic.utils = {
    getClickFraction(element, clientX) {
        const rect = element.getBoundingClientRect();
        if (rect.width <= 0) return 0;
        return (clientX - rect.left) / rect.width;
    }
};

// --- Synced Lyrics Auto-scroll ---
window.mediamusic.lyrics = {
    scrollToActive() {
        const container = document.querySelector('.lyrics-container');
        const activeItem = document.querySelector('.lyric-line-active');
        if (container && activeItem) {
            const containerHeight = container.clientHeight;
            const itemTop = activeItem.offsetTop;
            const itemHeight = activeItem.clientHeight;
            container.scrollTo({
                top: itemTop - containerHeight / 2 + itemHeight / 2,
                behavior: 'smooth'
            });
        }
    }
};

// Handle all webview shortcut key combinations (Space, Ctrl+Right, Ctrl+Left, Alt+Up, Alt+Down, etc.)
document.addEventListener('keydown', async (e) => {
    // Ignore when typing text in inputs
    const tag = e.target ? e.target.tagName.toLowerCase() : '';
    if (tag === 'input' || tag === 'textarea' || (e.target && e.target.isContentEditable)) {
        return;
    }
    // Ignore when user is actively recording a key binding (flag set by Blazor)
    if (window.mediamusic._isRecordingShortcut) {
        return;
    }
    // Ignore standalone modifier keypresses
    if (['Control', 'Shift', 'Alt', 'Meta'].includes(e.key)) {
        return;
    }

    const parts = [];
    if (e.ctrlKey) parts.push('Ctrl');
    if (e.altKey) parts.push('Alt');
    if (e.shiftKey) parts.push('Shift');

    let mainKey = e.key;
    if (e.code === 'Space' || mainKey === ' ' || mainKey === 'Spacebar') mainKey = 'Space';
    else if (mainKey === 'ArrowUp') mainKey = 'Up';
    else if (mainKey === 'ArrowDown') mainKey = 'Down';
    else if (mainKey === 'ArrowLeft') mainKey = 'Left';
    else if (mainKey === 'ArrowRight') mainKey = 'Right';
    else if (mainKey.length === 1) mainKey = mainKey.toUpperCase();

    parts.push(mainKey);
    const combination = parts.join(' + ');

    if (window.DotNet) {
        try {
            const handled = await window.DotNet.invokeMethodAsync('MediaMusic', 'HandleGlobalShortcut', combination);
            if (handled) {
                e.preventDefault();
            }
        } catch { }
    }
});




