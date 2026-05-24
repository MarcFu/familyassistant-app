/**
 * JS-managed file picker — all preview/file handling happens client-side.
 * Blazor only gets notified of file count changes via DotNetObjectReference.
 * Upload goes directly via HTTP POST (no SignalR).
 */
const pickers = {};

/**
 * Initialize a file picker.
 * @param {string} pickerId - unique ID for this picker instance
 * @param {string} previewContainerId - ID of the div where previews are rendered
 * @param {object} dotnetRef - DotNetObjectReference for callbacks
 */
window.initFilePicker = function (pickerId, previewContainerId, dotnetRef) {
    pickers[pickerId] = {
        files: [],
        previewContainerId: previewContainerId,
        dotnetRef: dotnetRef
    };
};

/**
 * Called when user selects files (from camera or gallery).
 * Reads files from the input, adds to stored array, renders previews.
 * @param {string} pickerId
 * @param {string} inputContainerId - ID of the label/container with the input
 * @param {boolean} append - true to add to existing files, false to replace
 */
window.onFilesSelected = function (pickerId, inputContainerId, append) {
    const picker = pickers[pickerId];
    if (!picker) return;

    const container = document.getElementById(inputContainerId);
    if (!container) return;
    const input = container.querySelector('input[type="file"]');
    if (!input || !input.files || input.files.length === 0) return;

    if (!append) {
        picker.files = [];
    }

    for (let i = 0; i < input.files.length; i++) {
        picker.files.push(input.files[i]);
    }

    renderPreviews(pickerId);
    notifyDotnet(picker);
};

/**
 * Remove a file by index.
 */
window.removePickerFile = function (pickerId, index) {
    const picker = pickers[pickerId];
    if (!picker) return;

    picker.files.splice(index, 1);
    renderPreviews(pickerId);
    notifyDotnet(picker);
};

/**
 * Upload all stored files via HTTP POST.
 * @returns {Promise<Array>} - array of uploaded attachment info
 */
window.uploadPickerFiles = async function (pickerId, taskId, commentId) {
    const picker = pickers[pickerId];
    if (!picker || picker.files.length === 0) return [];

    const formData = new FormData();
    formData.append('taskId', taskId.toString());
    formData.append('commentId', commentId.toString());

    for (const file of picker.files) {
        formData.append('files', file);
    }

    const response = await fetch('/api/upload-attachment', {
        method: 'POST',
        body: formData
    });

    if (!response.ok) {
        throw new Error('Upload fehlgeschlagen: ' + response.statusText);
    }

    return await response.json();
};

/**
 * Clear all files and previews.
 */
window.clearPickerFiles = function (pickerId) {
    const picker = pickers[pickerId];
    if (!picker) return;

    picker.files = [];
    renderPreviews(pickerId);
    notifyDotnet(picker);
};

/**
 * Dispose/cleanup a picker instance.
 */
window.disposeFilePicker = function (pickerId) {
    const picker = pickers[pickerId];
    if (picker) {
        const previewDiv = document.getElementById(picker.previewContainerId);
        if (previewDiv) previewDiv.innerHTML = '';
    }
    delete pickers[pickerId];
};

// ─── Internal helpers ───

function renderPreviews(pickerId) {
    const picker = pickers[pickerId];
    if (!picker) return;

    const previewDiv = document.getElementById(picker.previewContainerId);
    if (!previewDiv) return;

    previewDiv.innerHTML = '';

    if (picker.files.length === 0) return;

    const wrapper = document.createElement('div');
    wrapper.style.cssText = 'display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 8px;';

    picker.files.forEach((file, index) => {
        const thumb = document.createElement('div');
        thumb.style.cssText = 'position: relative; width: 64px; height: 64px; border: 1px solid rgba(255,255,255,0.12); border-radius: 8px; overflow: hidden;';

        const img = document.createElement('img');
        img.style.cssText = 'width: 100%; height: 100%; object-fit: cover;';
        img.alt = file.name;

        // Read file for thumbnail
        const reader = new FileReader();
        reader.onload = function () {
            img.src = reader.result;
        };
        reader.readAsDataURL(file);

        // Delete button
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.style.cssText = 'position: absolute; top: 2px; right: 2px; width: 20px; height: 20px; border-radius: 50%; background: rgba(0,0,0,0.7); color: white; border: none; cursor: pointer; font-size: 12px; line-height: 20px; text-align: center; padding: 0;';
        btn.textContent = '✕';
        btn.onclick = function () {
            window.removePickerFile(pickerId, index);
        };

        thumb.appendChild(img);
        thumb.appendChild(btn);
        wrapper.appendChild(thumb);
    });

    previewDiv.appendChild(wrapper);
}

function notifyDotnet(picker) {
    if (picker.dotnetRef) {
        picker.dotnetRef.invokeMethodAsync('OnFileCountChanged', picker.files.length);
    }
}
