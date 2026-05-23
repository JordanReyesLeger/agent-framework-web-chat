/*
 * Live Avatar + Live Voice client.
 * - Uses Azure Speech SDK SpeechRecognizer for continuous STT (with barge-in).
 * - Uses AvatarSynthesizer + WebRTC peer connection for real-time avatar video/audio.
 * - Backend endpoints:
 *     GET  /api/LiveAvatar/speech-token
 *     GET  /api/LiveAvatar/ice-token
 *     GET  /api/LiveAvatar/config
 *     POST /api/LiveAvatar/message
 */
(function () {
    'use strict';

    const els = {
        stage: document.getElementById('vu-stage'),
        placeholder: document.getElementById('vu-placeholder'),
        video: document.getElementById('vu-video'),
        audio: document.getElementById('vu-audio'),
        status: document.getElementById('vu-status'),
        statusText: document.getElementById('vu-status-text'),
        caption: document.getElementById('vu-caption'),
        transcript: document.getElementById('vu-transcript'),
        btnConnect: document.getElementById('vu-btn-connect'),
        btnDisconnect: document.getElementById('vu-btn-disconnect'),
        btnMic: document.getElementById('vu-btn-mic'),
        btnStop: document.getElementById('vu-btn-stop'),
        character: document.getElementById('vu-avatar-character'),
        style: document.getElementById('vu-avatar-style'),
        styleField: document.getElementById('vu-avatar-style-field'),
        voiceName: document.getElementById('vu-voice'),
        systemPrompt: document.getElementById('vu-system-prompt'),
        headerPill: document.getElementById('vu-header-pill'),
        headerPillText: document.getElementById('vu-header-pill-text'),
        textInput: document.getElementById('vu-text-input'),
        btnSend: document.getElementById('vu-btn-send'),
    };

    const state = {
        config: null,
        token: null,
        region: null,
        avatarSynth: null,
        peerConnection: null,
        recognizer: null,
        sessionId: null,
        isConnected: false,
        isListening: false,
        isSpeaking: false,
        isProcessing: false,
        requestId: 0,
    };

    function setStatus(kind, text) {
        els.status.classList.remove('is-listening', 'is-speaking', 'is-thinking', 'is-error');
        if (kind) els.status.classList.add('is-' + kind);
        if (text) els.statusText.textContent = text;
        // Mirror state into the header pill + stage outline.
        if (els.headerPill) {
            els.headerPill.classList.remove('connected', 'error');
            if (kind === 'error') els.headerPill.classList.add('error');
            else if (state.isConnected) els.headerPill.classList.add('connected');
            if (els.headerPillText) els.headerPillText.textContent = text || (state.isConnected ? 'Conectado' : 'Desconectado');
        }
        if (els.stage) {
            els.stage.classList.toggle('is-connected', state.isConnected && kind !== 'error');
            els.stage.classList.toggle('is-speaking', kind === 'speaking');
        }
    }

    function appendTranscript(role, text) {
        const empty = els.transcript.querySelector('.vu-empty');
        if (empty) empty.remove();
        const div = document.createElement('div');
        div.className = 'vu-msg ' + role;
        const roleLabel = role === 'user' ? '🧑 Tú' : '🤖 Asistente';
        div.innerHTML = '<div class="vu-role"></div><div class="vu-text"></div>';
        div.querySelector('.vu-role').textContent = roleLabel;
        div.querySelector('.vu-text').textContent = text;
        els.transcript.appendChild(div);
        els.transcript.scrollTop = els.transcript.scrollHeight;
    }

    async function fetchJson(url, options) {
        const resp = await fetch(url, options);
        if (!resp.ok) {
            const txt = await resp.text();
            throw new Error(`HTTP ${resp.status}: ${txt}`);
        }
        return resp.json();
    }

    async function loadConfig() {
        // Called every time the user connects. Fetches token + caches config,
        // but does NOT touch the dropdowns (to avoid clobbering the user's pick).
        const [cfg, tok] = await Promise.all([
            fetchJson('/api/LiveAvatar/config'),
            fetchJson('/api/LiveAvatar/speech-token'),
        ]);
        state.config = cfg;
        state.token = tok.token;
        state.region = tok.region;
        if (!state.token) throw new Error('Speech token no disponible. Configura AzureSpeech:SubscriptionKey.');
    }

    async function loadInitialDefaults() {
        // Called once on page load to pre-select the configured character/style/voice.
        // After the user changes anything, those choices are preserved across (dis)connects.
        try {
            const cfg = await fetchJson('/api/LiveAvatar/config');
            state.config = cfg;
            if (cfg.avatarCharacter && els.character) els.character.value = cfg.avatarCharacter;
            if (cfg.avatarStyle && els.style) els.style.value = cfg.avatarStyle;
            if (cfg.synthesisVoiceName && els.voiceName) els.voiceName.value = cfg.synthesisVoiceName;
            // Pre-llena el textarea con el prompt compartido si aún está vacío.
            if (cfg.instructions && els.systemPrompt && !els.systemPrompt.value.trim()) {
                els.systemPrompt.value = cfg.instructions;
            }
            syncAvatarStyleVisibility();
        } catch (e) {
            console.warn('[INIT] Failed to load defaults', e);
        }
    }

    async function getIceServers() {
        const data = await fetchJson('/api/LiveAvatar/ice-token');
        // Response shape: { Urls: [...], Username: '...', Password: '...' }
        const urls = data.Urls || data.urls;
        const username = data.Username || data.username;
        const credential = data.Password || data.password;
        return [{ urls, username, credential }];
    }

    async function connect() {
        try {
            setStatus('thinking', 'Conectando...');
            els.btnConnect.disabled = true;

            await loadConfig();
            const iceServers = await getIceServers();

            const SDK = window.SpeechSDK;
            const speechConfig = SDK.SpeechConfig.fromAuthorizationToken(state.token, state.region);
            speechConfig.speechSynthesisVoiceName = els.voiceName.value || state.config.synthesisVoiceName;

            // Fallback defaults if the dropdown options haven't loaded yet.
            const character = (els.character && els.character.value) || state.config.avatarCharacter || 'lisa';
            const avatarStyle = (els.style && els.style.value) || state.config.avatarStyle || 'casual-sitting';

            // Detect Talking Heads (photo avatars, preview) via data-photo attribute on the selected option.
            const selectedOpt = els.character.options[els.character.selectedIndex];
            const isPhotoAvatar = selectedOpt && selectedOpt.dataset.photo === 'true';

            const videoFormat = new SDK.AvatarVideoFormat();
            // Photo avatars don't accept a style; full-body avatars do.
            const avatarConfig = isPhotoAvatar
                ? new SDK.AvatarConfig(character, undefined, videoFormat)
                : new SDK.AvatarConfig(character, avatarStyle, videoFormat);
            avatarConfig.customized = false;
            if (isPhotoAvatar) {
                // Photo avatar (Talking Heads) base model — required for preview characters.
                avatarConfig.photoAvatarBaseModel = 'vasa-1';
                console.log('[AVATAR] Talking Head (preview · vasa-1):', character);
            }
            if (state.config.avatarVideoCodec) {
                try { videoFormat.codec = state.config.avatarVideoCodec; } catch (_) {}
            }

            state.avatarSynth = new SDK.AvatarSynthesizer(speechConfig, avatarConfig);

            state.avatarSynth.avatarEventReceived = (s, e) => {
                console.log('[AVATAR]', e.description, e.offset);
            };

            const pc = new RTCPeerConnection({ iceServers });
            state.peerConnection = pc;

            pc.ontrack = (event) => {
                if (event.track.kind === 'video') {
                    els.video.srcObject = event.streams[0];
                    els.video.hidden = false;
                    els.placeholder.style.display = 'none';
                } else if (event.track.kind === 'audio') {
                    els.audio.srcObject = event.streams[0];
                }
            };
            pc.addTransceiver('video', { direction: 'sendrecv' });
            pc.addTransceiver('audio', { direction: 'sendrecv' });

            const result = await state.avatarSynth.startAvatarAsync(pc);
            if (result.reason !== SDK.ResultReason.SynthesizingAudioCompleted) {
                throw new Error('No se pudo iniciar el avatar: ' + (result.errorDetails || result.reason));
            }

            // Speech recognizer (continuous STT)
            const recoConfig = SDK.SpeechConfig.fromAuthorizationToken(state.token, state.region);
            recoConfig.speechRecognitionLanguage = state.config.recognitionLanguage || 'es-MX';
            const audioConfig = SDK.AudioConfig.fromDefaultMicrophoneInput();
            state.recognizer = new SDK.SpeechRecognizer(recoConfig, audioConfig);

            state.recognizer.recognizing = onRecognizing;
            state.recognizer.recognized = onRecognized;
            state.recognizer.canceled = (s, e) => console.warn('[STT] canceled', e.errorDetails);

            state.isConnected = true;
            els.btnDisconnect.disabled = false;
            els.btnMic.disabled = false;
            els.btnStop.disabled = false;
            if (els.textInput) els.textInput.disabled = false;
            if (els.btnSend) els.btnSend.disabled = false;
            setStatus(null, 'Conectado. Pulsa "Micrófono".');
        } catch (err) {
            console.error('[CONNECT]', err);
            setStatus('error', 'Error: ' + err.message);
            els.btnConnect.disabled = false;
        }
    }

    function onRecognizing(_s, e) {
        const text = e.result.text;
        if (!text) return;
        els.caption.textContent = text;
        // Barge-in: if avatar is speaking, interrupt as soon as user starts talking
        if (state.isSpeaking) {
            console.log('[BARGE-IN] Interrumpiendo avatar');
            stopSpeaking();
        }
    }

    async function onRecognized(_s, e) {
        const SDK = window.SpeechSDK;
        if (e.result.reason !== SDK.ResultReason.RecognizedSpeech) return;
        const text = (e.result.text || '').trim();
        els.caption.textContent = '';
        if (!text) return;
        appendTranscript('user', text);
        await sendMessage(text);
    }

    async function sendMessage(text) {
        state.isProcessing = true;
        state.requestId++;
        const myReq = state.requestId;
        setStatus('thinking', 'Pensando...');
        try {
            const resp = await fetchJson('/api/LiveAvatar/message', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    sessionId: state.sessionId,
                    text,
                    systemPrompt: els.systemPrompt.value || null,
                }),
            });
            if (myReq !== state.requestId) return; // interrupted
            state.sessionId = resp.sessionId;
            appendTranscript('assistant', resp.text);
            await speak(resp.text, myReq);
        } catch (err) {
            console.error('[MSG]', err);
            setStatus('error', 'Error: ' + err.message);
        } finally {
            state.isProcessing = false;
            if (state.isListening) setStatus('listening', 'Escuchando...');
        }
    }

    async function speak(text, myReq) {
        if (!state.avatarSynth) return;
        state.isSpeaking = true;
        setStatus('speaking', 'Hablando...');
        try {
            const voice = els.voiceName.value || state.config.synthesisVoiceName;
            const ssml = `<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="es-MX">`
                + `<voice name="${voice}">${escapeXml(text)}</voice></speak>`;
            await state.avatarSynth.speakSsmlAsync(ssml);
        } catch (err) {
            console.warn('[SPEAK]', err);
        } finally {
            if (myReq === state.requestId) {
                state.isSpeaking = false;
                if (state.isListening) setStatus('listening', 'Escuchando...');
                else setStatus(null, 'Listo');
            }
        }
    }

    function stopSpeaking() {
        state.requestId++;
        state.isSpeaking = false;
        if (state.avatarSynth) {
            try { state.avatarSynth.stopSpeakingAsync(); } catch (_) {}
        }
        if (state.isListening) setStatus('listening', 'Escuchando...');
    }

    function escapeXml(s) {
        return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;').replace(/'/g, '&apos;');
    }

    function startMic() {
        if (!state.recognizer || state.isListening) return;
        state.recognizer.startContinuousRecognitionAsync(
            () => {
                state.isListening = true;
                els.btnMic.innerHTML = '<i class="bi bi-mic-mute"></i> Detener';
                els.btnMic.classList.add('is-active');
                setStatus('listening', 'Escuchando...');
            },
            (err) => { console.error('[STT start]', err); setStatus('error', 'Error STT'); }
        );
    }

    function stopMic() {
        if (!state.recognizer || !state.isListening) return;
        state.recognizer.stopContinuousRecognitionAsync(
            () => {
                state.isListening = false;
                els.btnMic.innerHTML = '<i class="bi bi-mic"></i> Micrófono';
                els.btnMic.classList.remove('is-active');
                setStatus(null, 'Micrófono detenido');
            },
            (err) => console.warn('[STT stop]', err)
        );
    }

    async function disconnect() {
        try {
            stopMic();
            stopSpeaking();
            if (state.recognizer) { try { state.recognizer.close(); } catch (_) {} state.recognizer = null; }
            if (state.avatarSynth) { try { state.avatarSynth.close(); } catch (_) {} state.avatarSynth = null; }
            if (state.peerConnection) { try { state.peerConnection.close(); } catch (_) {} state.peerConnection = null; }
        } finally {
            state.isConnected = false;
            els.video.hidden = true;
            els.video.srcObject = null;
            els.audio.srcObject = null;
            els.placeholder.style.display = '';
            els.btnConnect.disabled = false;
            els.btnDisconnect.disabled = true;
            els.btnMic.disabled = true;
            els.btnStop.disabled = true;
            if (els.textInput) { els.textInput.disabled = true; els.textInput.value = ''; }
            if (els.btnSend) els.btnSend.disabled = true;
            setStatus(null, 'Desconectado');
        }
    }

    function sendTypedMessage() {
        if (!els.textInput) return;
        const text = (els.textInput.value || '').trim();
        if (!text || !state.isConnected) return;
        els.textInput.value = '';
        appendTranscript('user', text);
        sendMessage(text);
    }

    els.btnConnect.addEventListener('click', connect);
    els.btnDisconnect.addEventListener('click', disconnect);
    els.btnStop.addEventListener('click', stopSpeaking);
    els.btnMic.addEventListener('click', () => {
        if (state.isListening) stopMic(); else startMic();
    });
    if (els.btnSend) els.btnSend.addEventListener('click', sendTypedMessage);
    if (els.textInput) els.textInput.addEventListener('keydown', e => { if (e.key === 'Enter') sendTypedMessage(); });

    // Hide the pose/style dropdown when a Talking Head (photo) avatar is selected — photo avatars don't accept styles.
    function syncAvatarStyleVisibility() {
        if (!els.character || !els.styleField) return;
        const opt = els.character.options[els.character.selectedIndex];
        const isPhoto = opt && opt.dataset.photo === 'true';
        els.styleField.style.display = isPhoto ? 'none' : '';
    }
    if (els.character) els.character.addEventListener('change', syncAvatarStyleVisibility);
    syncAvatarStyleVisibility();

    // Click stage to interrupt
    els.stage.addEventListener('click', () => {
        if (state.isSpeaking) stopSpeaking();
    });

    // Load defaults from appsettings once on page open. The user's later selections
    // are preserved across reconnects (loadConfig no longer overwrites them).
    loadInitialDefaults();
})();
