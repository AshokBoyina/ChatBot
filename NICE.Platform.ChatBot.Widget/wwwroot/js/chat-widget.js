/**
 * NICE.Platform.ChatBot.Widget – JS helpers called from Blazor via IJSRuntime
 */
window.chatWidget = {

    /** Scrolls the messages container to the very bottom. */
    scrollToBottom: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) el.scrollTop = el.scrollHeight;
    }

};
