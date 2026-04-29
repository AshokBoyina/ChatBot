/**
 * ChatBot – JS helpers called from Blazor via IJSRuntime
 */
window.chatWidget = {

    // ── DOM helpers ────────────────────────────────────────────────────────────

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
    },

    // ── API-driven chat ────────────────────────────────────────────────────────

    _abortController: null,

    /**
     * POST /api/chat, receive the full reply, then animate it word-by-word
     * back into Blazor via [JSInvokable] callbacks on dotnetRef.
     *
     * @param {object}  requestBody   { message, appId, history: [{role,content}] }
     * @param {object}  dotnetRef     DotNetObjectReference<ChatWidget>
     * @param {string|null} accessKey Value for the X-API-Access-Key header (null = omit)
     */
    sendMessage: async function (requestBody, dotnetRef, accessKey) {
        this._abortController = new AbortController();
        const signal = this._abortController.signal;

        const headers = { 'Content-Type': 'application/json' };
        if (accessKey) headers['X-API-Access-Key'] = accessKey;

        try {
            const response = await fetch('/api/chat', {
                method:  'POST',
                headers: headers,
                body:    JSON.stringify(requestBody),
                signal
            });

            if (signal.aborted) return;

            if (!response.ok) {
                const errText = await response.text().catch(() => `HTTP ${response.status}`);
                await dotnetRef.invokeMethodAsync('ReceiveError',
                    `API error ${response.status}: ${errText}`);
                return;
            }

            const data = await response.json();

            if (signal.aborted) return;

            if (data.error) {
                await dotnetRef.invokeMethodAsync('ReceiveError', data.error);
                return;
            }

            // Animate the reply word-by-word (same feel as before, but all in the browser)
            const text  = data.reply || '';
            const words = text.replace(/\n/g, ' \n ').split(' ');

            for (const word of words) {
                if (signal.aborted) return;
                if (!word) continue;
                const chunk = word === '\n' ? '\n' : word + ' ';
                await dotnetRef.invokeMethodAsync('ReceiveChunk', chunk);
                await new Promise(resolve => setTimeout(resolve, 18));
            }

            if (!signal.aborted) {
                await dotnetRef.invokeMethodAsync(
                    'ReceiveDone', text, data.citations ?? null);
            }

        } catch (err) {
            if (err.name === 'AbortError' || signal.aborted) return; // user cancelled — silent
            try {
                await dotnetRef.invokeMethodAsync(
                    'ReceiveError', `Network error: ${err.message}`);
            } catch { /* dotnet ref already disposed */ }
        } finally {
            this._abortController = null;
        }
    },

    /** Cancels any in-flight fetch or word animation immediately. */
    abort: function () {
        if (this._abortController) {
            this._abortController.abort();
            this._abortController = null;
        }
    }
};
