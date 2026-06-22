// MediaMusic drag-and-drop interop (PRD §2.2 lightweight mode).
// Listens for HTML5 file/folder drops on the playlist region and forwards the
// resolved local paths to a [JSInvokable] C# method (DragDropHandler.HandleDrop).
//
// NOTE: browsers expose dropped files via DataTransfer but do not reveal the
// absolute filesystem path of folder drops. The real implementation will use
// Photino's native drag-drop events (set via WebMessageReceivedHandler) rather
// than the HTML5 API; this stub keeps a minimal, safe listener in place.

window.mediamusic = window.mediamusic || {};

window.mediamusic.dragdrop = {
    // Wire up a drop zone to forward file paths to the given .NET ref.
    // dotnetRef: DotNetObjectReference to the component exposing [JSInvokable] OnFilesDropped.
    init(zoneSelector, dotnetRef) {
        const zone = document.querySelector(zoneSelector);
        if (!zone) return;

        zone.addEventListener('dragover', (e) => {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'copy';
        });

        zone.addEventListener('drop', async (e) => {
            e.preventDefault();
            if (!e.dataTransfer?.files?.length) return;

            // TODO: replace with Photino native drop to get folder paths.
            const paths = [];
            for (const file of e.dataTransfer.files) {
                // browsers expose no real path; use name as a placeholder.
                paths.push(file.name);
            }
            try {
                await dotnetRef.invokeMethodAsync('OnFilesDropped', paths);
            } catch (err) {
                console.error('dragdrop forward failed', err);
            }
        });
    }
};
