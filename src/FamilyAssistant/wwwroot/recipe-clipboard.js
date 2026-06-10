window.FamilyAssistantRecipeClipboard = {
    register: function (element, dotNetRef) {
        if (!element) {
            return;
        }

        this.unregister(element);

        const handler = async function (event) {
            const items = event.clipboardData?.items;
            if (!items) {
                return;
            }

            for (const item of items) {
                if (!item.type?.startsWith("image/")) {
                    continue;
                }

                const file = item.getAsFile();
                if (!file) {
                    continue;
                }

                event.preventDefault();
                const dataUrl = await new Promise((resolve, reject) => {
                    const reader = new FileReader();
                    reader.onload = () => resolve(reader.result);
                    reader.onerror = reject;
                    reader.readAsDataURL(file);
                });

                await dotNetRef.invokeMethodAsync("OnRecipeImagePasted", file.name || "clipboard-image.png", file.type || "image/png", dataUrl);
                return;
            }
        };

        element.__recipePasteHandler = handler;
        element.addEventListener("paste", handler);
    },

    unregister: function (element) {
        if (!element || !element.__recipePasteHandler) {
            return;
        }

        element.removeEventListener("paste", element.__recipePasteHandler);
        delete element.__recipePasteHandler;
    }
};

window.FamilyAssistantRecipeMarkup = {
    insertAtCursor: function (element, text) {
        if (!element) {
            return "";
        }

        const value = element.value || "";
        const start = typeof element.selectionStart === "number" ? element.selectionStart : value.length;
        const end = typeof element.selectionEnd === "number" ? element.selectionEnd : value.length;
        const prefix = value.slice(0, start);
        const suffix = value.slice(end);
        const separatorBefore = prefix.length > 0 && !/\s$/.test(prefix) ? " " : "";
        const separatorAfter = suffix.length > 0 && !/^\s|^[.,;:!?]/.test(suffix) ? " " : "";
        const nextValue = `${prefix}${separatorBefore}${text}${separatorAfter}${suffix}`;
        element.value = nextValue;
        const cursor = prefix.length + separatorBefore.length + text.length + separatorAfter.length;
        element.selectionStart = cursor;
        element.selectionEnd = cursor;
        element.focus();
        element.dispatchEvent(new Event("input", { bubbles: true }));
        return nextValue;
    }
};
