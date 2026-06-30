// VoiceLive client: bridges browser mic <-> server WebSocket <-> Azure VoiceLive
// Audio: PCM16 mono 24kHz both directions. Uses AudioContext at 24kHz to skip resampling.
(function () {
    'use strict';

    const SAMPLE_RATE = 24000;
    const SEND_CHUNK_MS = 80; // ~1920 samples per send

    const els = {
        // Stage / status (shared with the unified UI)
        stage: document.getElementById('vu-stage'),
        placeholder: document.getElementById('vu-placeholder'),
        status: document.getElementById('vu-status'),
        statusText: document.getElementById('vu-status-text'),
        caption: document.getElementById('vu-caption'),
        meter: document.getElementById('vu-meter-fill'),
        headerPill: document.getElementById('vu-header-pill'),
        headerPillText: document.getElementById('vu-header-pill-text'),

        // Controls
        connect: document.getElementById('vu-btn-connect'),
        disconnect: document.getElementById('vu-btn-disconnect'),
        mic: document.getElementById('vu-btn-mic'),
        stop: document.getElementById('vu-btn-stop'),

        // Config
        instructions: document.getElementById('vu-system-prompt'),
        voice: document.getElementById('vu-voice'),
        model: document.getElementById('vu-model'),
        styleRow: document.getElementById('vu-style-row'),
        style: document.getElementById('vu-style'),
        ragEnabled: document.getElementById('vu-rag-enable'),
        avatarEnabled: document.getElementById('vu-avatar-enable'),
        avatarFields: document.getElementById('vu-avatar-fields'),
        avatarCharacter: document.getElementById('vu-avatar-character'),
        avatarStyle: document.getElementById('vu-avatar-style'),

        // Conversation
        transcript: document.getElementById('vu-transcript'),
        events: document.getElementById('vu-events'),
        textInput: document.getElementById('vu-text-input'),
        sendText: document.getElementById('vu-btn-send'),

        // Avatar media (reuses the main stage video/audio)
        avatarVideo: document.getElementById('vu-video'),
        avatarAudio: document.getElementById('vu-audio'),
    };

    // Holds the full /voices response so we can repopulate styles/avatar on demand.
    const catalog = {
        avatar: null,
        // Map<voiceId, {styles, gender, lang, label, type}>
        voicesById: new Map(),
    };

    function genderIcon(g) {
        switch ((g || '').toLowerCase()) {
            case 'male': return '👨';
            case 'female': return '👩';
            case 'neutral': return '🤖';
            default: return '🎙️';
        }
    }

    const state = {
        ws: null,
        audioCtx: null,
        playbackCtx: null,
        micStream: null,
        micNode: null,
        workletNode: null,
        connected: false,
        micOn: false,
        nextPlayTime: 0,
        currentAssistantLine: null,
        currentUserLine: null,
        avatarPc: null,
        avatarStream: null,
        avatarIceServers: null,
        avatarEnabledForSession: false,
    };

    function setStatus(text, kind) {
        // Mirror state on stage status pill + header pill.
        if (els.status) {
            els.status.classList.remove('is-listening', 'is-speaking', 'is-thinking', 'is-error');
            if (kind === 'connected') els.status.classList.add('is-listening');
            else if (kind === 'error') els.status.classList.add('is-error');
            if (els.statusText) els.statusText.textContent = text || '';
        }
        if (els.headerPill) {
            els.headerPill.classList.remove('connected', 'error');
            if (kind === 'connected') els.headerPill.classList.add('connected');
            else if (kind === 'error') els.headerPill.classList.add('error');
            if (els.headerPillText) els.headerPillText.textContent = text || 'Desconectado';
        }
        if (els.stage) {
            els.stage.classList.toggle('is-connected', kind === 'connected');
        }
    }
    function logEvent(text, kind) {
        if (!els.events) return;
        const div = document.createElement('div');
        div.className = 'ev-' + (kind || 'info');
        const ts = new Date().toLocaleTimeString();
        div.textContent = `[${ts}] ${text}`;
        els.events.appendChild(div);
        els.events.scrollTop = els.events.scrollHeight;
    }
    function appendTranscript(role, textChunk, isFinal) {
        // Remove empty-state placeholder if present.
        const empty = els.transcript.querySelector('.vu-empty');
        if (empty) empty.remove();
        // Reuse the same bubble for streaming deltas.
        const key = role === 'assistant' ? 'currentAssistantLine' : 'currentUserLine';
        if (!state[key]) {
            const bubble = document.createElement('div');
            bubble.className = 'vu-msg ' + role;
            const roleLabel = role === 'assistant' ? '🤖 Asistente' : '🧑 Tú';
            bubble.innerHTML = '<div class="vu-role"></div><div class="vu-text"></div>';
            bubble.querySelector('.vu-role').textContent = roleLabel;
            els.transcript.appendChild(bubble);
            state[key] = bubble.querySelector('.vu-text');
        }
        // Si la burbuja venía con un placeholder (ej. "transcribiendo…"), límpialo
        // antes de empezar a concatenar el transcript real.
        if (state[key].querySelector('.vu-typing')) {
            state[key].innerHTML = '';
        }
        state[key].textContent += textChunk;
        els.transcript.scrollTop = els.transcript.scrollHeight;
        if (isFinal) state[key] = null;
    }

    // Reemplaza el contenido de la burbuja actual con el transcript final (útil cuando
    // el servidor sólo manda "completed"/"done" sin haber mandado deltas previas).
    function setFinalTranscript(role, fullText) {
        const empty = els.transcript.querySelector('.vu-empty');
        if (empty) empty.remove();
        const key = role === 'assistant' ? 'currentAssistantLine' : 'currentUserLine';
        if (state[key]) {
            state[key].textContent = fullText;
            state[key] = null;
        } else {
            const bubble = document.createElement('div');
            bubble.className = 'vu-msg ' + role;
            const roleLabel = role === 'assistant' ? '🤖 Asistente' : '🧑 Tú';
            bubble.innerHTML = '<div class="vu-role"></div><div class="vu-text"></div>';
            bubble.querySelector('.vu-role').textContent = roleLabel;
            bubble.querySelector('.vu-text').textContent = fullText;
            els.transcript.appendChild(bubble);
        }
        els.transcript.scrollTop = els.transcript.scrollHeight;
    }

    // Reserva la burbuja del usuario en el orden cronológico correcto, ANTES de que
    // el asistente empiece a responder. Se llena cuando llegue la transcripción de Whisper.
    function ensureUserBubble() {
        if (state.currentUserLine) return; // ya existe
        const empty = els.transcript.querySelector('.vu-empty');
        if (empty) empty.remove();
        const bubble = document.createElement('div');
        bubble.className = 'vu-msg user';
        bubble.innerHTML = '<div class="vu-role">🧑 Tú</div><div class="vu-text"><span class="vu-typing">transcribiendo…</span></div>';
        els.transcript.appendChild(bubble);
        state.currentUserLine = bubble.querySelector('.vu-text');
        els.transcript.scrollTop = els.transcript.scrollHeight;
    }

    async function loadConfig() {
        const r = await fetch('/api/VoiceLive/config');
        const cfg = await r.json();
        els.instructions.value = cfg.instructions || '';
        els.model.value = cfg.model || '';
        await loadVoices(cfg.voice);
    }

    async function loadVoices(currentVoice) {
        try {
            const r = await fetch('/api/VoiceLive/voices');
            const data = await r.json();
            const select = els.voice;
            select.innerHTML = '';
            catalog.voicesById.clear();
            catalog.avatar = data.avatar || null;

            const preferred = currentVoice || data.defaultVoice;
            (data.groups || []).forEach(group => {
                const og = document.createElement('optgroup');
                og.label = group.name + (group.description ? ' — ' + group.description : '');
                (group.voices || []).forEach(v => {
                    const opt = document.createElement('option');
                    opt.value = v.id;
                    opt.dataset.voiceType = group.type || 'azure';
                    const icon = genderIcon(v.gender);
                    opt.textContent = `${icon} ${v.label}  (${v.lang})`;
                    if (v.id === preferred) opt.selected = true;
                    og.appendChild(opt);
                    catalog.voicesById.set(v.id, {
                        ...v,
                        type: group.type || 'azure',
                    });
                });
                select.appendChild(og);
            });
            if (!select.value && select.options.length > 0) select.selectedIndex = 0;

            populateStylesForSelectedVoice();
            populateAvatarChoices();
        } catch (e) {
            logEvent('No se pudo cargar el catálogo de voces: ' + e.message, 'error');
        }
    }

    function populateStylesForSelectedVoice() {
        const voiceId = els.voice.value;
        const v = catalog.voicesById.get(voiceId);
        const styles = (v && Array.isArray(v.styles)) ? v.styles : [];
        els.style.innerHTML = '';
        if (!styles.length) {
            els.styleRow.style.display = 'none';
            return;
        }
        const blank = document.createElement('option');
        blank.value = '';
        blank.textContent = '— (sin estilo)';
        els.style.appendChild(blank);
        styles.forEach(s => {
            const opt = document.createElement('option');
            opt.value = s;
            opt.textContent = s;
            els.style.appendChild(opt);
        });
        els.styleRow.style.display = '';
    }

    function populateAvatarChoices() {
        if (!catalog.avatar) return;
        const charSel = els.avatarCharacter;
        charSel.innerHTML = '';

        const characters = catalog.avatar.characters || [];
        const fullbody = characters.filter(c => c.type !== 'photo');
        const photos = characters.filter(c => c.type === 'photo');

        if (fullbody.length) {
            const og = document.createElement('optgroup');
            og.label = '👥 Full body (3D)';
            fullbody.forEach(c => {
                const opt = document.createElement('option');
                opt.value = c.id;
                opt.textContent = `${genderIcon(c.gender)} ${c.label}`;
                opt.dataset.styles = JSON.stringify(c.styles || []);
                opt.dataset.photo = 'false';
                og.appendChild(opt);
            });
            charSel.appendChild(og);
        }
        if (photos.length) {
            const og = document.createElement('optgroup');
            og.label = '🎭 Talking Heads (Preview · solo Live Avatar)';
            photos.forEach(c => {
                const opt = document.createElement('option');
                opt.value = c.id;
                opt.textContent = `${genderIcon(c.gender)} ${c.label}`;
                opt.dataset.styles = JSON.stringify(c.styles || []);
                opt.dataset.photo = 'true';
                og.appendChild(opt);
            });
            charSel.appendChild(og);
        }

        if (catalog.avatar.defaultCharacter) {
            const match = Array.from(charSel.options).find(o => o.value === catalog.avatar.defaultCharacter);
            if (match) match.selected = true;
        }
        populateAvatarStyles();
        syncPhotoAvatarWarning();
    }

    function isSelectedAvatarPhoto() {
        const sel = els.avatarCharacter;
        if (!sel) return false;
        const opt = sel.options[sel.selectedIndex];
        return !!(opt && opt.dataset.photo === 'true');
    }

    function syncPhotoAvatarWarning() {
        const styleField = document.getElementById('vu-avatar-style-field');
        const isPhoto = isSelectedAvatarPhoto();
        if (styleField) styleField.style.display = isPhoto ? 'none' : '';
        // Surface a one-time hint in the event log when the user picks a Talking Head here.
        if (isPhoto && els.avatarEnabled && els.avatarEnabled.checked) {
            logEvent('Talking Heads (foto avatar) aún no soportado por Voice Live SDK 1.0 — usa la página "Live Avatar" para previsualizarlos.', 'warn');
        }
    }

    function populateAvatarStyles() {
        const charSel = els.avatarCharacter;
        const opt = charSel.options[charSel.selectedIndex];
        const styles = opt && opt.dataset.styles ? JSON.parse(opt.dataset.styles) : [];
        els.avatarStyle.innerHTML = '';
        styles.forEach(s => {
            const o = document.createElement('option');
            o.value = s;
            o.textContent = s;
            els.avatarStyle.appendChild(o);
        });
        if (catalog.avatar && catalog.avatar.defaultStyle) {
            const m = Array.from(els.avatarStyle.options).find(o => o.value === catalog.avatar.defaultStyle);
            if (m) m.selected = true;
        }
    }

    async function createPromptSession() {
        const instructions = (els.instructions.value || '').trim();
        if (!instructions) return null;

        const response = await fetch('/api/VoiceLive/prompt-session', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ instructions })
        });

        if (!response.ok) {
            throw new Error('No se pudo registrar el prompt del sistema (' + response.status + ')');
        }

        const payload = await response.json();
        return payload.promptId || null;
    }

    async function buildWsUrl() {
        const scheme = location.protocol === 'https:' ? 'wss' : 'ws';
        const params = new URLSearchParams();
        if (els.voice.value) {
            params.set('voice', els.voice.value);
            const opt = els.voice.options[els.voice.selectedIndex];
            const vt = opt && opt.dataset && opt.dataset.voiceType;
            if (vt) params.set('voiceType', vt);
        }
        if (els.style && els.style.value) params.set('style', els.style.value);
        // Keep the WebSocket URL small: register the editable prompt with the
        // backend first, then pass only a short promptId in the WS query string.
        const promptId = await createPromptSession();
        if (promptId) params.set('promptId', promptId);
        if (els.ragEnabled && els.ragEnabled.checked) params.set('rag', '1');
        if (els.avatarEnabled && els.avatarEnabled.checked) {
            params.set('avatar', '1');
            if (els.avatarCharacter.value) params.set('avatarCharacter', els.avatarCharacter.value);
            if (els.avatarStyle.value) params.set('avatarStyle', els.avatarStyle.value);
            if (isSelectedAvatarPhoto()) params.set('avatarPhoto', '1');
        }
        const qs = params.toString();
        return `${scheme}://${location.host}/api/VoiceLive/ws${qs ? '?' + qs : ''}`;
    }

    async function connect() {
        // Note: Talking Heads (photo avatars) aren't officially supported by
        // Azure.AI.VoiceLive 1.0, but we let the user try anyway — the server
        // will surface whatever response Voice Live gives us.
        if (els.avatarEnabled && els.avatarEnabled.checked && isSelectedAvatarPhoto()) {
            logEvent('Probando Talking Head en Voice Live (preview, no oficial). Si falla, el servicio devolverá un error.', 'warn');
        }
        try {
            state.ws = new WebSocket(await buildWsUrl());
            state.ws.binaryType = 'arraybuffer';

            state.ws.onopen = () => {
                state.connected = true;
                setStatus('Conectando a VoiceLive…');
                logEvent('WebSocket abierto', 'info');
                els.disconnect.disabled = false;
                els.connect.disabled = true;
            };
            state.ws.onmessage = onWsMessage;
            state.ws.onerror = (e) => { logEvent('WS error', 'error'); console.error(e); };
            state.ws.onclose = () => {
                state.connected = false;
                setStatus('Desconectado');
                logEvent('WebSocket cerrado', 'info');
                resetUi();
            };
        } catch (e) {
            logEvent('Error al conectar: ' + e.message, 'error');
        }
    }

    function disconnect() {
        stopMic();
        teardownAvatar();
        if (state.ws && state.ws.readyState <= 1) state.ws.close();
        if (state.playbackCtx) { try { state.playbackCtx.close(); } catch {} state.playbackCtx = null; }
        state.nextPlayTime = 0;
    }

    function resetUi() {
        els.connect.disabled = false;
        els.disconnect.disabled = true;
        els.mic.disabled = true;
        els.stop.disabled = true;
        els.textInput.disabled = true;
        els.sendText.disabled = true;
        els.mic.innerHTML = '<i class="bi bi-mic"></i> Activar micrófono';
    }

    async function onWsMessage(ev) {
        if (typeof ev.data === 'string') {
            let msg;
            try { msg = JSON.parse(ev.data); } catch { return; }
            switch (msg.type) {
                case 'ready':
                    setStatus('Conectado', 'connected');
                    logEvent(`Listo · modelo=${msg.model} voz=${msg.voice}` + (msg.style ? ` estilo=${msg.style}` : ''), 'info');
                    if (msg.avatar && msg.avatar.enabled) {
                        state.avatarEnabledForSession = true;
                        if (els.stage) els.stage.classList.add('is-connected');
                        if (els.caption) els.caption.textContent = `Avatar ${msg.avatar.character || ''} · ${msg.avatar.style || ''}`;
                        logEvent(`🎭 Avatar solicitado: ${msg.avatar.character} (${msg.avatar.style || 'sin estilo'})`, 'info');
                        setAvatarStatus('esperando ICE servers de Azure…');
                    } else {
                        state.avatarEnabledForSession = false;
                        teardownAvatar();
                    }
                    els.mic.disabled = false;
                    els.textInput.disabled = false;
                    els.sendText.disabled = false;
                    await ensurePlaybackContext();
                    break;
                case 'session_created':
                    logEvent(`Sesión creada: ${msg.id || ''}`, 'info');
                    break;
                case 'session_updated':
                    logEvent('Sesión configurada', 'info');
                    break;
                case 'speech_started':
                    logEvent('🎤 Usuario hablando', 'speech');
                    // Crea ya la burbuja del usuario (vacía) para asegurar orden cronológico:
                    // Whisper devuelve la transcripción ~1-2s después, pero el asistente
                    // ya empezó a responder y crearía su burbuja primero.
                    ensureUserBubble();
                    // Barge-in on the client: stop pending playback
                    flushPlayback();
                    els.stop.disabled = false;
                    break;
                case 'speech_stopped':
                    logEvent('🎤 Usuario terminó', 'speech');
                    break;
                case 'response_created':
                    logEvent('🤖 Respuesta iniciada', 'response');
                    els.stop.disabled = false;
                    break;
                case 'response_done':
                    logEvent('✅ Respuesta completa', 'response');
                    els.stop.disabled = true;
                    state.currentAssistantLine = null;
                    break;
                case 'audio_done':
                    logEvent('🔊 Audio terminado', 'response');
                    break;
                case 'transcript_delta':
                    appendTranscript('assistant', msg.text || '', false);
                    break;
                case 'transcript_done':
                    if (msg.text) setFinalTranscript('assistant', msg.text);
                    else if (state.currentAssistantLine) state.currentAssistantLine = null;
                    break;
                case 'user_transcript_delta':
                    appendTranscript('user', msg.text || '', false);
                    break;
                case 'user_transcript':
                    // Whisper suele entregar el transcript completo en "completed" sin deltas previas.
                    setFinalTranscript('user', msg.text || '');
                    break;
                case 'error':
                    logEvent('❌ ' + msg.message, 'error');
                    setStatus('Error', 'error');
                    break;
                case 'avatar_answer':
                    await applyAvatarAnswer(msg);
                    break;
                case 'avatar_ice':
                    state.avatarIceServers = Array.isArray(msg.iceServers) ? msg.iceServers : [];
                    logEvent(`🧊 ICE servers de Azure recibidos: ${state.avatarIceServers.length}`, 'info');
                    if (state.avatarEnabledForSession) await initiateAvatarWebRtc();
                    break;
                case 'viseme':
                case 'animation':
                case 'blendshape':
                    handleAvatarFrame(msg);
                    break;
                default:
                    if (msg.type && /viseme|blendshape|animation/i.test(msg.type)) {
                        handleAvatarFrame(msg);
                    } else {
                        logEvent('· ' + msg.type, 'info');
                    }
            }
        } else {
            // Binary = PCM16 24kHz mono assistant audio
            playPcm16(ev.data);
        }
    }

    function setAvatarStatus(text) {
        // Surface WebRTC state in the caption bar (less noisy than a dedicated badge).
        if (els.caption) els.caption.textContent = 'WebRTC: ' + text;
    }

    async function initiateAvatarWebRtc() {
        try {
            setAvatarStatus('preparando offer…');

            // Tear down any previous peer connection.
            if (state.avatarPc) {
                try { state.avatarPc.close(); } catch {}
                state.avatarPc = null;
            }
            if (state.avatarStream) {
                state.avatarStream.getTracks().forEach(t => { try { t.stop(); } catch {} });
                state.avatarStream = null;
            }

            const iceServers = (state.avatarIceServers && state.avatarIceServers.length)
                ? state.avatarIceServers
                : [{ urls: 'stun:stun.l.google.com:19302' }];
            const pc = new RTCPeerConnection({ iceServers });
            state.avatarPc = pc;

            pc.addTransceiver('video', { direction: 'recvonly' });
            pc.addTransceiver('audio', { direction: 'recvonly' });

            pc.ontrack = (event) => {
                logEvent(`📺 Avatar track recibido: ${event.track.kind}`, 'info');
                if (event.track.kind === 'video' && els.avatarVideo) {
                    els.avatarVideo.srcObject = event.streams[0];
                    els.avatarVideo.hidden = false;
                    els.avatarVideo.muted = false;
                    els.avatarVideo.play().catch(err => logEvent('⚠️ video.play() rechazado: ' + err.message, 'error'));
                    if (els.placeholder) els.placeholder.style.display = 'none';
                } else if (event.track.kind === 'audio' && els.avatarAudio) {
                    els.avatarAudio.srcObject = event.streams[0];
                    els.avatarAudio.play().catch(err => logEvent('⚠️ audio.play() rechazado: ' + err.message, 'error'));
                }
            };

            pc.oniceconnectionstatechange = () => {
                setAvatarStatus(pc.iceConnectionState);
                logEvent('ICE: ' + pc.iceConnectionState, 'info');
                if (pc.iceConnectionState === 'connected' || pc.iceConnectionState === 'completed') {
                    if (els.caption) els.caption.textContent = 'Avatar conectado · lipsync en vivo';
                }
            };
            pc.onconnectionstatechange = () => logEvent('PC: ' + pc.connectionState, 'info');
            pc.createDataChannel('eventChannel');

            const offer = await pc.createOffer();
            await pc.setLocalDescription(offer);
            await new Promise(resolve => {
                if (pc.iceGatheringState === 'complete') return resolve();
                const check = () => {
                    if (pc.iceGatheringState === 'complete') {
                        pc.removeEventListener('icegatheringstatechange', check);
                        resolve();
                    }
                };
                pc.addEventListener('icegatheringstatechange', check);
                setTimeout(resolve, 3000);
            });

            const localDesc = pc.localDescription;
            const clientSdpBase64 = btoa(JSON.stringify({ type: localDesc.type, sdp: localDesc.sdp }));
            logEvent(`🎭 Offer SDP listo (${clientSdpBase64.length} chars base64) — enviando…`, 'info');

            if (state.ws && state.ws.readyState === 1) {
                state.ws.send(JSON.stringify({ type: 'avatar_offer', sdp: clientSdpBase64 }));
            }
        } catch (e) {
            logEvent('Avatar WebRTC error: ' + e.message, 'error');
            setAvatarStatus('error');
            console.error(e);
        }
    }

    async function applyAvatarAnswer(msg) {
        if (!state.avatarPc) {
            logEvent('⚠️ avatar_answer recibido pero no hay PeerConnection', 'error');
            return;
        }
        if (!msg.sdp) {
            logEvent('⚠️ avatar_answer sin SDP', 'error');
            return;
        }
        try {
            // Server SDP is base64-encoded JSON of an RTCSessionDescription (per official sample).
            let answer;
            try {
                const json = atob(msg.sdp);
                answer = JSON.parse(json);
            } catch {
                // Backwards-compat: if it isn't base64, treat as raw SDP.
                answer = { type: 'answer', sdp: msg.sdp };
            }
            await state.avatarPc.setRemoteDescription(answer);
            logEvent(`📥 SDP answer aplicado (type=${answer.type}, ${(answer.sdp || '').length} chars) — esperando media…`, 'info');
        } catch (e) {
            logEvent('Error al aplicar SDP answer: ' + e.message, 'error');
            console.error(e);
        }
    }

    function handleAvatarFrame(_msg) {
        // Visemes/blendshapes arrive only when there is no avatar video stream
        // (otherwise the video already shows the lipsync). Kept as a fallback
        // activity indicator on the audio meter bar.
        if (!els.meter) return;
        els.meter.style.width = '70%';
        clearTimeout(handleAvatarFrame._t);
        handleAvatarFrame._t = setTimeout(() => { els.meter.style.width = '0%'; }, 220);
    }

    function teardownAvatar() {
        if (state.avatarPc) { try { state.avatarPc.close(); } catch {} state.avatarPc = null; }
        if (state.avatarStream) { state.avatarStream.getTracks().forEach(t => { try { t.stop(); } catch {} }); state.avatarStream = null; }
        if (els.avatarVideo) { els.avatarVideo.srcObject = null; els.avatarVideo.hidden = true; }
        if (els.avatarAudio) { els.avatarAudio.srcObject = null; }
        if (els.placeholder) els.placeholder.style.display = '';
        setAvatarStatus('desconectado');
    }

    // ---------- Playback ----------
    async function ensurePlaybackContext() {
        if (!state.playbackCtx) {
            state.playbackCtx = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: SAMPLE_RATE });
        }
        if (state.playbackCtx.state === 'suspended') await state.playbackCtx.resume();
        state.nextPlayTime = state.playbackCtx.currentTime;
    }

    function playPcm16(arrayBuffer) {
        if (!state.playbackCtx) return;
        const int16 = new Int16Array(arrayBuffer);
        if (int16.length === 0) return;
        const float32 = new Float32Array(int16.length);
        for (let i = 0; i < int16.length; i++) float32[i] = int16[i] / 32768;
        const buf = state.playbackCtx.createBuffer(1, float32.length, SAMPLE_RATE);
        buf.copyToChannel(float32, 0);
        const src = state.playbackCtx.createBufferSource();
        src.buffer = buf;
        src.connect(state.playbackCtx.destination);
        const now = state.playbackCtx.currentTime;
        if (state.nextPlayTime < now) state.nextPlayTime = now;
        src.start(state.nextPlayTime);
        state.nextPlayTime += buf.duration;
    }

    function flushPlayback() {
        // Stop & recreate playback context to drop queued audio quickly
        if (state.playbackCtx) {
            try { state.playbackCtx.close(); } catch {}
            state.playbackCtx = null;
        }
        ensurePlaybackContext();
    }

    // ---------- Capture ----------
    async function startMic() {
        try {
            await ensurePlaybackContext();
            state.micStream = await navigator.mediaDevices.getUserMedia({
                audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true, autoGainControl: true }
            });
            state.audioCtx = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: SAMPLE_RATE });
            await state.audioCtx.audioWorklet.addModule(workletUrl());
            state.micNode = state.audioCtx.createMediaStreamSource(state.micStream);
            state.workletNode = new AudioWorkletNode(state.audioCtx, 'vl-pcm16-encoder', {
                processorOptions: { chunkMs: SEND_CHUNK_MS, sampleRate: SAMPLE_RATE }
            });
            state.workletNode.port.onmessage = (e) => {
                const { pcm, level } = e.data;
                if (state.ws && state.ws.readyState === 1) state.ws.send(pcm);
                els.meter.style.width = Math.min(100, Math.round(level * 140)) + '%';
            };
            state.micNode.connect(state.workletNode);
            // Silent terminator so the worklet runs (no audio leaked to speakers)
            const silent = state.audioCtx.createGain();
            silent.gain.value = 0;
            state.workletNode.connect(silent).connect(state.audioCtx.destination);

            state.micOn = true;
            els.mic.innerHTML = '<i class="bi bi-mic-mute"></i> Desactivar micrófono';
            logEvent('🎙️ Micrófono activo', 'info');
        } catch (e) {
            logEvent('Error mic: ' + e.message, 'error');
            console.error(e);
        }
    }

    function stopMic() {
        if (state.workletNode) { try { state.workletNode.disconnect(); } catch {} state.workletNode = null; }
        if (state.micNode) { try { state.micNode.disconnect(); } catch {} state.micNode = null; }
        if (state.audioCtx) { try { state.audioCtx.close(); } catch {} state.audioCtx = null; }
        if (state.micStream) { state.micStream.getTracks().forEach(t => t.stop()); state.micStream = null; }
        state.micOn = false;
        els.mic.innerHTML = '<i class="bi bi-mic"></i> Activar micrófono';
        els.meter.style.width = '0%';
    }

    // Inline worklet (Blob URL) — converts mic floats to Int16 chunks every SEND_CHUNK_MS
    function workletUrl() {
        const code = `
class VlPcm16Encoder extends AudioWorkletProcessor {
    constructor(options) {
        super();
        const opts = options.processorOptions || {};
        this.sampleRate = opts.sampleRate || 24000;
        const ms = opts.chunkMs || 80;
        this.chunkSize = Math.floor(this.sampleRate * ms / 1000);
        this.buffer = new Float32Array(this.chunkSize);
        this.offset = 0;
    }
    process(inputs) {
        const input = inputs[0];
        if (!input || input.length === 0) return true;
        const ch = input[0];
        if (!ch) return true;
        let peak = 0;
        for (let i = 0; i < ch.length; i++) {
            const s = ch[i];
            if (Math.abs(s) > peak) peak = Math.abs(s);
            this.buffer[this.offset++] = s;
            if (this.offset >= this.chunkSize) {
                const pcm = new Int16Array(this.chunkSize);
                for (let j = 0; j < this.chunkSize; j++) {
                    let v = Math.max(-1, Math.min(1, this.buffer[j]));
                    pcm[j] = v < 0 ? v * 0x8000 : v * 0x7FFF;
                }
                this.port.postMessage({ pcm: pcm.buffer, level: peak }, [pcm.buffer]);
                this.offset = 0;
                peak = 0;
            }
        }
        return true;
    }
}
registerProcessor('vl-pcm16-encoder', VlPcm16Encoder);
        `;
        return URL.createObjectURL(new Blob([code], { type: 'application/javascript' }));
    }

    // ---------- Wire up ----------
    els.connect.addEventListener('click', connect);
    els.disconnect.addEventListener('click', disconnect);
    els.mic.addEventListener('click', () => state.micOn ? stopMic() : startMic());
    els.stop.addEventListener('click', () => {
        if (state.ws && state.ws.readyState === 1) state.ws.send(JSON.stringify({ type: 'stop' }));
        flushPlayback();
    });
    els.sendText.addEventListener('click', sendText);
    els.textInput.addEventListener('keydown', (e) => { if (e.key === 'Enter') sendText(); });

    // Repopulate styles when the voice changes.
    els.voice.addEventListener('change', populateStylesForSelectedVoice);

    // Avatar toggle: reveal character/style fields.
    els.avatarEnabled.addEventListener('change', () => {
        els.avatarFields.style.display = els.avatarEnabled.checked ? 'grid' : 'none';
    });

    // Update avatar style choices when character changes.
    els.avatarCharacter.addEventListener('change', populateAvatarStyles);
    els.avatarCharacter.addEventListener('change', syncPhotoAvatarWarning);

    function sendText() {
        const t = els.textInput.value.trim();
        if (!t || !state.ws || state.ws.readyState !== 1) return;
        appendTranscript('user', t, true);
        state.ws.send(JSON.stringify({ type: 'text', text: t }));
        els.textInput.value = '';
    }

    loadConfig();
})();
