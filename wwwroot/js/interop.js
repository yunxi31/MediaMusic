// MediaMusic JS interop layer.
// Exposes namespaced helpers under `window.mediamusic.*` that Blazor components
// invoke via IJSRuntime. Window-control calls go through Photino.NET's injected
// `window.Photino.window` API defensively (no-op if unavailable).

window.mediamusic = window.mediamusic || {};

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



