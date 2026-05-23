// VoiceLive client: bridges browser mic <-> server WebSocket <-> Azure VoiceLive
// Audio: PCM16 mono 24kHz both directions. Uses AudioContext at 24kHz to skip resampling.
(function () {
    'use strict';

    const SAMPLE_RATE = 24000;
    const SEND_CHUNK_MS = 80; // ~1920 samples per send

    const els = {
        status: document.getElementById('vlStatus'),
        connect: document.getElementById('vlConnect'),
        disconnect: document.getElementById('vlDisconnect'),
        mic: document.getElementById('vlMic'),
        stop: document.getElementById('vlStop'),
        meter: document.getElementById('vlMeter'),
        instructions: document.getElementById('vlInstructions'),
        voice: document.getElementById('vlVoice'),
        model: document.getElementById('vlModel'),
        transcript: document.getElementById('vlTranscript'),
        events: document.getElementById('vlEvents'),
        textInput: document.getElementById('vlTextInput'),
        sendText: document.getElementById('vlSendText'),
        styleRow: document.getElementById('vlStyleRow'),
        style: document.getElementById('vlStyle'),
        avatarEnabled: document.getElementById('vlAvatarEnabled'),
        avatarFields: document.getElementById('vlAvatarFields'),
        avatarCharacter: document.getElementById('vlAvatarCharacter'),
        avatarStyle: document.getElementById('vlAvatarStyle'),
        avatarStage: document.getElementById('vlAvatarStage'),
        avatarVideo: document.getElementById('vlAvatarVideo'),
        avatarName: document.getElementById('vlAvatarName'),
        avatarDetails: document.getElementById('vlAvatarDetails'),
        avatarStatus: document.getElementById('vlAvatarStatus'),
        visemeBar: document.getElementById('vlVisemeBar'),
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
    };

    function setStatus(text, cls) {
        els.status.textContent = text;
        els.status.className = 'vl-status' + (cls ? ' ' + cls : '');
    }
    function logEvent(text, kind) {
        const div = document.createElement('div');
        div.className = 'ev-' + (kind || 'info');
        const ts = new Date().toLocaleTimeString();
        div.textContent = `[${ts}] ${text}`;
        els.events.appendChild(div);
        els.events.scrollTop = els.events.scrollHeight;
    }
    function appendTranscript(role, textChunk, isFinal) {
        // Reuse the same line for streaming deltas
        const key = role === 'assistant' ? 'currentAssistantLine' : 'currentUserLine';
        if (!state[key]) {
            const line = document.createElement('div');
            line.style.padding = '4px 0';
            line.style.borderTop = '1px solid #21262d';
            line.style.color = role === 'assistant' ? '#58a6ff' : '#e6edf3';
            line.innerHTML = `<strong>${role === 'assistant' ? '🤖' : '🧑'}</strong> <span class="vl-content"></span>`;
            els.transcript.appendChild(line);
            state[key] = line.querySelector('.vl-content');
        }
        state[key].textContent += textChunk;
        els.transcript.scrollTop = els.transcript.scrollHeight;
        if (isFinal) state[key] = null;
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
        (catalog.avatar.characters || []).forEach(c => {
            const opt = document.createElement('option');
            opt.value = c.id;
            opt.textContent = `${genderIcon(c.gender)} ${c.label}`;
            opt.dataset.styles = JSON.stringify(c.styles || []);
            charSel.appendChild(opt);
        });
        if (catalog.avatar.defaultCharacter) {
            const match = Array.from(charSel.options).find(o => o.value === catalog.avatar.defaultCharacter);
            if (match) match.selected = true;
        }
        populateAvatarStyles();
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

    function buildWsUrl() {
        const scheme = location.protocol === 'https:' ? 'wss' : 'ws';
        const params = new URLSearchParams();
        if (els.voice.value) {
            params.set('voice', els.voice.value);
            const opt = els.voice.options[els.voice.selectedIndex];
            const vt = opt && opt.dataset && opt.dataset.voiceType;
            if (vt) params.set('voiceType', vt);
        }
        if (els.style && els.style.value) params.set('style', els.style.value);
        const instr = (els.instructions.value || '').trim();
        if (instr) params.set('instructions', instr);
        if (els.avatarEnabled && els.avatarEnabled.checked) {
            params.set('avatar', '1');
            if (els.avatarCharacter.value) params.set('avatarCharacter', els.avatarCharacter.value);
            if (els.avatarStyle.value) params.set('avatarStyle', els.avatarStyle.value);
        }
        const qs = params.toString();
        return `${scheme}://${location.host}/api/VoiceLive/ws${qs ? '?' + qs : ''}`;
    }

    async function connect() {
        try {
            state.ws = new WebSocket(buildWsUrl());
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
                    setStatus('Conectado a VoiceLive', 'connected');
                    logEvent(`Listo · modelo=${msg.model} voz=${msg.voice}` + (msg.style ? ` estilo=${msg.style}` : ''), 'info');
                    if (msg.avatar && msg.avatar.enabled) {
                        els.avatarStage.classList.add('active');
                        els.avatarName.textContent = `Avatar: ${msg.avatar.character || '?'}`;
                        els.avatarDetails.textContent = `Estilo: ${msg.avatar.style || '-'} · iniciando WebRTC…`;
                        setAvatarStatus('iniciando oferta SDP');
                        logEvent(`🎭 Avatar activo: ${msg.avatar.character} (${msg.avatar.style})`, 'info');
                        // Client initiates the WebRTC offer — Voice Live spec requires session.avatar.connect from client first.
                        await initiateAvatarWebRtc();
                    } else {
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
                case 'user_transcript':
                    appendTranscript('user', msg.text || '', false);
                    break;
                case 'error':
                    logEvent('❌ ' + msg.message, 'error');
                    setStatus('Error', 'error');
                    break;
                case 'avatar_answer':
                    await applyAvatarAnswer(msg);
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
        if (els.avatarStatus) els.avatarStatus.textContent = 'WebRTC: ' + text;
    }

    async function initiateAvatarWebRtc() {
        try {
            setAvatarStatus('preparando offer…');
            els.avatarDetails.textContent = 'Preparando WebRTC…';

            // Tear down any previous peer connection.
            if (state.avatarPc) {
                try { state.avatarPc.close(); } catch {}
                state.avatarPc = null;
            }
            if (state.avatarStream) {
                state.avatarStream.getTracks().forEach(t => { try { t.stop(); } catch {} });
                state.avatarStream = null;
            }

            const pc = new RTCPeerConnection({
                iceServers: [
                    { urls: 'stun:stun.l.google.com:19302' },
                    { urls: 'stun:stun1.l.google.com:19302' }
                ]
            });
            state.avatarPc = pc;

            // Voice Live avatar uses sendrecv transceivers (per the official sample).
            pc.addTransceiver('video', { direction: 'sendrecv' });
            pc.addTransceiver('audio', { direction: 'sendrecv' });

            // Each track gets its own element (video + audio separately).
            // Voice Live sends them as separate streams; appending to a shared
            // MediaStream sometimes prevents autoplay from working.
            const stage = els.avatarStage;
            // Remove any prior media elements we created (but keep the info block).
            stage.querySelectorAll('.vl-avatar-media').forEach(el => el.remove());

            pc.ontrack = (event) => {
                logEvent(`📺 Avatar track recibido: ${event.track.kind}`, 'info');
                const media = document.createElement(event.track.kind === 'video' ? 'video' : 'audio');
                media.className = 'vl-avatar-media';
                media.srcObject = event.streams[0];
                media.autoplay = false;
                media.playsInline = true;
                media.addEventListener('loadeddata', () => {
                    media.play().catch(err => logEvent('⚠️ play() rechazado: ' + err.message, 'error'));
                });
                if (event.track.kind === 'video') {
                    media.style.width = '360px';
                    media.style.height = '360px';
                    media.style.borderRadius = '12px';
                    media.style.background = '#000';
                    media.style.objectFit = 'cover';
                    media.style.flexShrink = '0';
                    media.style.boxShadow = '0 4px 16px rgba(0,0,0,0.5)';
                    // Hide the placeholder <video id="vlAvatarVideo"> if present.
                    if (els.avatarVideo) els.avatarVideo.style.display = 'none';
                    stage.insertBefore(media, stage.firstChild);
                } else {
                    media.style.display = 'none';
                    stage.appendChild(media);
                }
            };

            pc.oniceconnectionstatechange = () => {
                setAvatarStatus(pc.iceConnectionState);
                logEvent('ICE: ' + pc.iceConnectionState, 'info');
                if (pc.iceConnectionState === 'connected' || pc.iceConnectionState === 'completed') {
                    els.avatarDetails.textContent = 'Video conectado · lipsync en vivo';
                }
                if (pc.iceConnectionState === 'failed') {
                    els.avatarDetails.textContent = 'WebRTC falló — revisa firewall/red';
                }
            };
            pc.onconnectionstatechange = () => logEvent('PC: ' + pc.connectionState, 'info');

            // Data channel for WebRTC-side avatar events (mirrors the official sample).
            pc.createDataChannel('eventChannel');

            // Build our OFFER and wait for ICE gathering.
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

            // Voice Live REQUIRES base64-encoded JSON of the full RTCSessionDescription.
            const localDesc = pc.localDescription;
            const clientSdpBase64 = btoa(JSON.stringify({ type: localDesc.type, sdp: localDesc.sdp }));
            logEvent(`🎭 Offer SDP listo (audio=${/m=audio/.test(localDesc.sdp)}, video=${/m=video/.test(localDesc.sdp)}, ${localDesc.sdp.length} chars, base64=${clientSdpBase64.length} chars) — enviando…`, 'info');

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
        // activity indicator on the bar.
        els.visemeBar.style.width = '70%';
        clearTimeout(handleAvatarFrame._t);
        handleAvatarFrame._t = setTimeout(() => { els.visemeBar.style.width = '0%'; }, 220);
    }

    function teardownAvatar() {
        if (state.avatarPc) { try { state.avatarPc.close(); } catch {} state.avatarPc = null; }
        if (state.avatarStream) { state.avatarStream.getTracks().forEach(t => { try { t.stop(); } catch {} }); state.avatarStream = null; }
        if (els.avatarVideo) els.avatarVideo.srcObject = null;
        els.avatarStage.classList.remove('active');
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

    function sendText() {
        const t = els.textInput.value.trim();
        if (!t || !state.ws || state.ws.readyState !== 1) return;
        appendTranscript('user', t, true);
        state.ws.send(JSON.stringify({ type: 'text', text: t }));
        els.textInput.value = '';
    }

    loadConfig();
})();
