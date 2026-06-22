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

