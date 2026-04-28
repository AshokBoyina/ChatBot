/**
 * ChatBot – JS helpers called from Blazor via IJSRuntime
 */
window.chatWidget = {
    /** Scrolls the messages container to the very bottom */
    scrollToBottom: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) el.scrollTop = el.scrollHeight;
    },

    /** Auto-resizes a textarea element to fit its content */
    autoResize: function (el) {
        if (!el) return;
        el.style.height = 'auto';
        el.style.height = Math.min(el.scrollHeight, 120) + 'px';
    },

    /** Focuses a given element */
    focus: function (el) {
        if (el) el.focus();
    }
};
