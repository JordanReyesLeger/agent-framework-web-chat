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
        stage: document.getElementById('avatarStage'),
        placeholder: document.getElementById('stagePlaceholder'),
        video: document.getElementById('avatarVideo'),
        audio: document.getElementById('avatarAudio'),
        status: document.getElementById('stageStatus'),
        statusText: document.getElementById('stageStatusText'),
        caption: document.getElementById('captionBar'),
        transcript: document.getElementById('transcript'),
        btnConnect: document.getElementById('btnConnect'),
        btnDisconnect: document.getElementById('btnDisconnect'),
        btnMic: document.getElementById('btnMic'),
        btnStop: document.getElementById('btnStop'),
        character: document.getElementById('avatarCharacter'),
        style: document.getElementById('avatarStyle'),
        voiceName: document.getElementById('voiceName'),
        systemPrompt: document.getElementById('systemPrompt'),
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
    }

    function appendTranscript(role, text) {
        const empty = els.transcript.querySelector('.text-muted');
        if (empty) empty.remove();
        const div = document.createElement('div');
        div.className = 'msg ' + role;
        div.innerHTML = `<div class="role">${role}</div><div class="text"></div>`;
        div.querySelector('.text').textContent = text;
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
        const [cfg, tok] = await Promise.all([
            fetchJson('/api/LiveAvatar/config'),
            fetchJson('/api/LiveAvatar/speech-token'),
        ]);
        state.config = cfg;
        state.token = tok.token;
        state.region = tok.region;
        if (cfg.avatarCharacter) els.character.value = cfg.avatarCharacter;
        if (cfg.avatarStyle) els.style.value = cfg.avatarStyle;
        if (cfg.synthesisVoiceName) els.voiceName.value = cfg.synthesisVoiceName;
        if (!state.token) throw new Error('Speech token no disponible. Configura AzureSpeech:SubscriptionKey.');
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

            const videoFormat = new SDK.AvatarVideoFormat();
            const avatarConfig = new SDK.AvatarConfig(
                els.character.value,
                els.style.value,
                videoFormat
            );
            avatarConfig.customized = false;
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
            setStatus(null, 'Conectado. Pulsa "Iniciar micrófono".');
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
                els.btnMic.innerHTML = '<i class="bi bi-mic-mute"></i> Detener micrófono';
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
                els.btnMic.innerHTML = '<i class="bi bi-mic"></i> Iniciar micrófono';
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
            setStatus(null, 'Desconectado');
        }
    }

    els.btnConnect.addEventListener('click', connect);
    els.btnDisconnect.addEventListener('click', disconnect);
    els.btnStop.addEventListener('click', stopSpeaking);
    els.btnMic.addEventListener('click', () => {
        if (state.isListening) stopMic(); else startMic();
    });
    // Click stage to interrupt
    els.stage.addEventListener('click', () => {
        if (state.isSpeaking) stopSpeaking();
    });
})();
