/**
 * Returns the clientWidth of an element (0 if null/undefined).
 * Used by AchievementBoard for pixel-perfect column calculation.
 */
window.measureWidth = function (el) { return el ? el.clientWidth : 0; };

/**
 * Theme interop: detects OS dark mode preference, watches for changes,
 * and manages theme CSS classes on <html>.
 */
window.themeInterop = {
    /**
     * Returns true if the OS/browser prefers dark mode.
     */
    getSystemDarkMode: function () {
        return window.matchMedia('(prefers-color-scheme: dark)').matches;
    },

    /**
     * Watches for OS dark mode changes and invokes a .NET callback.
     * @param {object} dotNetRef - DotNetObjectReference
     * @param {string} methodName - method to invoke on change
     */
    watchSystemDarkMode: function (dotNetRef, methodName) {
        const mq = window.matchMedia('(prefers-color-scheme: dark)');
        const handler = function (e) {
            dotNetRef.invokeMethodAsync(methodName, e.matches);
        };
        mq.addEventListener('change', handler);
        window.themeInterop._handler = handler;
        window.themeInterop._mq = mq;
    },

    /**
     * Stop watching for OS dark mode changes.
     */
    stopWatchingSystemDarkMode: function () {
        if (window.themeInterop._mq && window.themeInterop._handler) {
            window.themeInterop._mq.removeEventListener('change', window.themeInterop._handler);
            window.themeInterop._handler = null;
        }
    },

    /**
     * Sets the theme class on <html> to control CSS custom properties.
     * @param {boolean} isDark - true=dark mode, false=light mode
     * @param {boolean} lightBadges - true=use light badge variant (only applies in light mode)
     */
    setThemeClass: function (isDark, lightBadges) {
        var html = document.documentElement;
        if (isDark) {
            html.classList.remove('theme-light', 'theme-light-badges');
        } else {
            html.classList.add('theme-light');
            if (lightBadges) {
                html.classList.add('theme-light-badges');
            } else {
                html.classList.remove('theme-light-badges');
            }
        }
    }
};
