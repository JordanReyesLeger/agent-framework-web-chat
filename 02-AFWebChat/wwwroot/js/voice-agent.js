/**
 * Voice Agent — Real-time voice conversation with animated orb
 * Uses Azure Speech SDK for STT/TTS, Canvas for orb visualization
 */
(function () {
    'use strict';

    // ── State ──
    const state = {
        sessionId: null,
        isListening: false,
        isSpeaking: false,
        isProcessing: false,
        recognizer: null,
        synthesizer: null,
        player: null,
        speechConfig: null,
        audioConfig: null,
        token: null,
        region: null,
        recognitionLanguage: 'es-MX',
        synthesisVoiceName: 'es-MX-DaliaNeural',
        systemPrompt: '',
        autoContinue: true,
        speechRate: 1.0,
        audioLevel: 0,
        orbAnimId: null,
        requestId: 0,
        activeAudio: null,
    };

    // ── DOM Elements ──
    const $ = id => document.getElementById(id);
    const els = {};

    function cacheDom() {
        [
            'orbCanvas', 'orbGlow', 'liveCaption', 'responseText', 'responseArea',
            'btnStartVoice', 'btnStopVoice', 'btnTranscript', 'btnSettings',
            'btnNewSession', 'btnCloseTranscript', 'btnCloseSettings', 'btnExportTranscript',
            'statusIndicator', 'statusText', 'statusDot',
            'transcriptSidebar', 'transcriptBody', 'settingsOverlay',
            'systemPrompt', 'speechRate', 'speechRateValue', 'autoContinue',
            'sessionInfo',
        ].forEach(id => { els[id] = $(id); });
        els.statusDot = els.statusIndicator?.querySelector('.status-dot');
    }

    // ── Initialization ──
    async function init() {
        cacheDom();
        bindEvents();
        state.systemPrompt = els.systemPrompt?.value || '';
        state.autoContinue = els.autoContinue?.checked ?? true;
        initOrb();

        try {
            await fetchSpeechConfig();
            setStatus('idle', 'Listo — presiona el micrófono para hablar');
        } catch (err) {
            setStatus('error', 'Error al obtener configuración de voz');
            console.error('Init error:', err);
        }
    }

    function bindEvents() {
        els.btnStartVoice?.addEventListener('click', toggleVoice);
        els.btnStopVoice?.addEventListener('click', stopAll);
        els.btnTranscript?.addEventListener('click', toggleTranscript);
        els.btnCloseTranscript?.addEventListener('click', toggleTranscript);
        els.btnSettings?.addEventListener('click', () => els.settingsOverlay?.classList.remove('hidden'));
        els.btnCloseSettings?.addEventListener('click', () => els.settingsOverlay?.classList.add('hidden'));
        els.btnNewSession?.addEventListener('click', newSession);
        els.btnExportTranscript?.addEventListener('click', exportTranscript);
        els.systemPrompt?.addEventListener('change', e => { state.systemPrompt = e.target.value; });
        els.autoContinue?.addEventListener('change', e => { state.autoContinue = e.target.checked; });
        els.speechRate?.addEventListener('input', e => {
            state.speechRate = parseFloat(e.target.value);
            if (els.speechRateValue) els.speechRateValue.textContent = state.speechRate.toFixed(1) + 'x';
        });
        // Close settings on overlay click
        els.settingsOverlay?.addEventListener('click', e => {
            if (e.target === els.settingsOverlay) els.settingsOverlay.classList.add('hidden');
        });

        // Tap/click on the orb while speaking = manual barge-in
        els.orbCanvas?.addEventListener('click', () => {
            if (state.isSpeaking) {
                console.log('[BARGE-IN] Orb clicked — stopping speech');
                state.requestId++;
                stopSpeaking();
                state.isProcessing = false;
            }
        });
    }

    // ── Speech Config ──
    async function fetchSpeechConfig() {
        const [tokenRes, configRes] = await Promise.all([
            fetch('/api/VoiceConversation/speech-token'),
            fetch('/api/VoiceConversation/speech-config'),
        ]);
        if (!tokenRes.ok || !configRes.ok) throw new Error('Failed to fetch speech config');

        const tokenData = await tokenRes.json();
        const configData = await configRes.json();

        state.token = tokenData.token;
        state.region = tokenData.region;
        state.recognitionLanguage = configData.recognitionLanguage || 'es-MX';
        state.synthesisVoiceName = configData.synthesisVoiceName || 'es-MX-DaliaNeural';
    }

    async function refreshToken() {
        const res = await fetch('/api/VoiceConversation/speech-token');
        if (!res.ok) throw new Error('Token refresh failed');
        const data = await res.json();
        state.token = data.token;
        state.region = data.region;
    }

    // ── Voice Toggle ──
    async function toggleVoice() {
        if (state.isListening) {
            stopListening();
        } else {
            await startListening();
        }
    }

    async function startListening() {
        if (state.isListening) return;
        try {
            await refreshToken();

            const sdk = SpeechSDK;
            state.speechConfig = sdk.SpeechConfig.fromAuthorizationToken(state.token, state.region);
            state.speechConfig.speechRecognitionLanguage = state.recognitionLanguage;
            state.speechConfig.speechSynthesisVoiceName = state.synthesisVoiceName;

            // Set SSML speech rate via property
            if (state.speechRate !== 1.0) {
                state.speechConfig.setProperty(
                    sdk.PropertyId.SpeechServiceConnection_SynthesisRate,
                    state.speechRate.toString()
                );
            }

            state.audioConfig = sdk.AudioConfig.fromDefaultMicrophoneInput();
            state.recognizer = new sdk.SpeechRecognizer(state.speechConfig, state.audioConfig);

            // Events
            state.recognizer.recognizing = onRecognizing;
            state.recognizer.recognized = onRecognized;
            state.recognizer.canceled = onCanceled;
            state.recognizer.sessionStopped = onSessionStopped;

            state.recognizer.startContinuousRecognitionAsync(
                () => {
                    state.isListening = true;
                    els.btnStartVoice?.classList.add('hidden');
                    els.btnStopVoice?.classList.remove('hidden');
                    setStatus('listening', 'Escuchando...');

                    if (!state.sessionId) {
                        state.sessionId = crypto.randomUUID();
                        updateSessionInfo();
                    }
                },
                err => {
                    console.error('Start recognition error:', err);
                    setStatus('error', 'Error al iniciar micrófono');
                }
            );
        } catch (err) {
            console.error('startListening error:', err);
            setStatus('error', 'No se pudo acceder al micrófono');
        }
    }

    function stopListening() {
        if (state.recognizer) {
            state.recognizer.stopContinuousRecognitionAsync(
                () => {
                    state.isListening = false;
                    if (!state.isSpeaking) {
                        els.btnStartVoice?.classList.remove('hidden');
                        els.btnStopVoice?.classList.add('hidden');
                        setStatus('idle', 'Micrófono detenido');
                    }
                },
                err => console.error('Stop recognition error:', err)
            );
        }
    }

    function stopAll() {
        state.requestId++;
        stopListening();
        stopSpeaking();
        state.isProcessing = false;
        els.btnStartVoice?.classList.remove('hidden');
        els.btnStopVoice?.classList.add('hidden');
        setStatus('idle', 'Conversación pausada');
    }

    // ── Recognition Handlers ──
    function onRecognizing(sender, event) {
        const text = event.result.text;
        if (text) {
            setLiveCaption(text, true);
            state.audioLevel = 0.6;

            // Barge-in: if agent is speaking or processing, cancel and stop
            if (state.isSpeaking || state.isProcessing) {
                console.log('[BARGE-IN] User interrupted — stopping speech');
                state.requestId++;
                stopSpeaking();
                state.isProcessing = false;
            }
        }
    }

    async function onRecognized(sender, event) {
        const sdk = SpeechSDK;
        if (event.result.reason === sdk.ResultReason.RecognizedSpeech) {
            const text = event.result.text;
            if (text && text.trim().length > 0) {
                setLiveCaption(text, false);
                addTranscriptEntry('user', text);
                state.audioLevel = 0;
                await processAndSpeak(text);
            }
        } else if (event.result.reason === sdk.ResultReason.NoMatch) {
            state.audioLevel = 0;
        }
    }

    function onCanceled(sender, event) {
        const sdk = SpeechSDK;
        if (event.reason === sdk.CancellationReason.Error) {
            console.error('Recognition canceled:', event.errorDetails);
            setStatus('error', 'Error de reconocimiento');
        }
        state.audioLevel = 0;
    }

    function onSessionStopped() {
        state.isListening = false;
        els.btnStartVoice?.classList.remove('hidden');
        els.btnStopVoice?.classList.add('hidden');
        state.audioLevel = 0;
    }

    // ── Process & Speak ──
    async function processAndSpeak(userText) {
        stopSpeaking();
        const myRequestId = ++state.requestId;

        setStatus('processing', 'Procesando...');
        state.isProcessing = true;

        try {
            const res = await fetch('/api/VoiceConversation/message', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    sessionId: state.sessionId,
                    text: userText,
                    systemPrompt: state.systemPrompt || undefined,
                }),
            });

            if (myRequestId !== state.requestId) return;

            if (!res.ok) {
                const errData = await res.json().catch(() => ({}));
                throw new Error(errData.error || 'API error');
            }

            const data = await res.json();
            if (myRequestId !== state.requestId) return;

            state.sessionId = data.sessionId;
            updateSessionInfo();

            const responseText = data.text;
            setResponseText(responseText);
            addTranscriptEntry('assistant', responseText);

            // Speak the response
            await speak(responseText, myRequestId);
        } catch (err) {
            if (myRequestId !== state.requestId) return;
            console.error('Process error:', err);
            setStatus('error', 'Error al procesar mensaje');
            setResponseText('Error: ' + err.message);
        } finally {
            if (myRequestId === state.requestId) {
                state.isProcessing = false;
            }
        }
    }

    function speak(text, requestId) {
        return new Promise((resolve) => {
            if (requestId !== state.requestId) { resolve(); return; }
            try {
                const sdk = SpeechSDK;
                const synthConfig = sdk.SpeechConfig.fromAuthorizationToken(state.token, state.region);
                synthConfig.speechSynthesisVoiceName = state.synthesisVoiceName;

                // Create local references so callbacks never close a newer instance
                const localPlayer = new sdk.SpeakerAudioDestination();
                const audioOut = sdk.AudioConfig.fromSpeakerOutput(localPlayer);
                const localSynth = new sdk.SpeechSynthesizer(synthConfig, audioOut);

                // Store in state for external cancellation (stopSpeaking)
                state.player = localPlayer;
                state.synthesizer = localSynth;

                // Build SSML for rate control
                const ratePercent = Math.round((state.speechRate - 1.0) * 100);
                const rateStr = ratePercent >= 0 ? `+${ratePercent}%` : `${ratePercent}%`;
                const ssml = `<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="${state.recognitionLanguage}">
                    <voice name="${state.synthesisVoiceName}">
                        <prosody rate="${rateStr}">${escapeXml(text)}</prosody>
                    </voice>
                </speak>`;

                state.isSpeaking = true;
                setStatus('speaking', 'Hablando...');
                state.audioLevel = 0.5;

                // Viseme event for orb animation sync
                localSynth.visemeReceived = (s, e) => {
                    if (requestId !== state.requestId) return;
                    state.audioLevel = e.visemeId > 0 ? 0.4 + Math.random() * 0.4 : 0.1;
                    // Capture audio element reference as soon as available
                    if (!state.activeAudio && localPlayer) {
                        try {
                            state.activeAudio = localPlayer.internalAudio || localPlayer.privAudio || null;
                        } catch (_) { /* not available yet */ }
                    }
                };

                localSynth.speakSsmlAsync(
                    ssml,
                    result => {
                        const isCurrent = requestId === state.requestId;
                        if (isCurrent) {
                            state.isSpeaking = false;
                            state.audioLevel = 0;
                            state.player = null;
                            state.synthesizer = null;
                            state.activeAudio = null;
                        }
                        // Close LOCAL refs only if not already closed by stopSpeaking
                        try { if (!localPlayer.isClosed) localPlayer.close(); } catch (e) { /* ignore */ }
                        try { localSynth.close(); } catch (e) { /* ignore */ }

                        if (isCurrent && result.reason === sdk.ResultReason.SynthesizingAudioCompleted) {
                            if (state.autoContinue && state.isListening) {
                                setStatus('listening', 'Escuchando...');
                            } else if (state.autoContinue && !state.isListening) {
                                startListening();
                            } else {
                                setStatus('idle', 'Listo');
                            }
                        } else if (isCurrent) {
                            console.error('Synthesis error:', result.errorDetails);
                            setStatus('error', 'Error de síntesis de voz');
                        }
                        resolve();
                    },
                    err => {
                        const isCurrent = requestId === state.requestId;
                        if (isCurrent) {
                            state.isSpeaking = false;
                            state.audioLevel = 0;
                            state.player = null;
                            state.synthesizer = null;
                            state.activeAudio = null;
                            console.error('Speak error:', err);
                            setStatus('error', 'Error al reproducir voz');
                        }
                        // Close LOCAL refs only if not already closed by stopSpeaking
                        try { if (!localPlayer.isClosed) localPlayer.close(); } catch (e) { /* ignore */ }
                        try { localSynth.close(); } catch (e) { /* ignore */ }
                        resolve();
                    }
                );
            } catch (err) {
                if (requestId === state.requestId) {
                    state.isSpeaking = false;
                    state.audioLevel = 0;
                    console.error('Speak setup error:', err);
                }
                resolve();
            }
        });
    }

    function stopSpeaking() {
        // ── STEP 1: IMMEDIATELY silence audio (before any async callbacks can fire) ──
        // Access HTMLAudioElement via activeAudio ref, SDK getter, or private field
        let audio = state.activeAudio;
        if (!audio && state.player) {
            try { audio = state.player.internalAudio; } catch (_) {}
            if (!audio) try { audio = state.player.privAudio; } catch (_) {}
        }
        if (audio) {
            try { audio.pause(); } catch (_) {}
            try { audio.volume = 0; } catch (_) {}
        }

        // Also silence via SDK API (belt-and-suspenders)
        if (state.player) {
            try { state.player.volume = 0; } catch (_) {}
            try { state.player.mute(); } catch (_) {}
            try { state.player.pause(); } catch (_) {}
        }

        // ── STEP 2: Close synthesizer (may trigger callbacks, but audio is already paused) ──
        if (state.synthesizer) {
            try { state.synthesizer.close(); } catch (_) {}
            state.synthesizer = null;
        }

        // ── STEP 3: Close player ──
        if (state.player) {
            try { state.player.close(); } catch (_) {}
            state.player = null;
        }

        state.activeAudio = null;
        state.isSpeaking = false;
        state.audioLevel = 0;
        if (state.isListening) {
            setStatus('listening', 'Escuchando...');
        }
    }

    // closeSynthesizer is no longer needed — speak() callbacks now close
    // their own local references, preventing the race condition where an
    // old callback would accidentally kill a newer player/synthesizer.

    // ── Session Management ──
    function newSession() {
        stopAll();
        if (state.sessionId) {
            fetch(`/api/VoiceConversation/session/${state.sessionId}`, { method: 'DELETE' }).catch(() => {});
        }
        state.sessionId = null;
        els.transcriptBody.innerHTML = `
            <div class="transcript-empty">
                <i class="bi bi-chat-left-text" style="font-size: 2rem; opacity: 0.3;"></i>
                <p>La transcripción aparecerá aquí cuando inicies una conversación.</p>
            </div>`;
        setLiveCaption('');
        setResponseText('');
        updateSessionInfo();
        setStatus('idle', 'Nueva sesión — presiona el micrófono');
    }

    function updateSessionInfo() {
        if (els.sessionInfo) {
            els.sessionInfo.textContent = state.sessionId
                ? `Sesión: ${state.sessionId.substring(0, 8)}...`
                : 'Sesión no iniciada';
        }
    }

    // ── Transcript ──
    function addTranscriptEntry(role, text) {
        // Remove empty state
        const empty = els.transcriptBody?.querySelector('.transcript-empty');
        if (empty) empty.remove();

        const entry = document.createElement('div');
        entry.className = `transcript-entry ${role}`;
        const icon = role === 'user' ? 'bi-person-fill' : 'bi-robot';
        const label = role === 'user' ? 'Tú' : 'Agente';
        const time = new Date().toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' });

        entry.innerHTML = `
            <div class="entry-header">
                <i class="bi ${icon} entry-icon"></i>
                <span>${label}</span>
                <span>·</span>
                <span>${time}</span>
            </div>
            <div class="entry-text">${escapeHtml(text)}</div>
        `;
        els.transcriptBody?.appendChild(entry);
        els.transcriptBody.scrollTop = els.transcriptBody.scrollHeight;
    }

    function toggleTranscript() {
        els.transcriptSidebar?.classList.toggle('open');
    }

    function exportTranscript() {
        if (!state.sessionId) return;
        fetch(`/api/VoiceConversation/transcript/${state.sessionId}`)
            .then(r => r.json())
            .then(data => {
                const lines = (data.entries || []).map(e =>
                    `[${new Date(e.timestamp).toLocaleString('es-MX')}] ${e.role.toUpperCase()}: ${e.text}`
                );
                const blob = new Blob([lines.join('\n')], { type: 'text/plain' });
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = `transcript-${state.sessionId.substring(0, 8)}.txt`;
                a.click();
                URL.revokeObjectURL(url);
            })
            .catch(err => console.error('Export error:', err));
    }

    // ── UI Helpers ──
    function setStatus(type, text) {
        if (els.statusDot) {
            els.statusDot.className = 'status-dot';
            if (type !== 'idle') els.statusDot.classList.add(type);
        }
        if (els.statusText) els.statusText.textContent = text;
    }

    function setLiveCaption(text, interim) {
        if (els.liveCaption) {
            els.liveCaption.textContent = text;
            els.liveCaption.className = 'live-caption' + (interim ? ' interim' : '');
        }
    }

    function setResponseText(text) {
        if (els.responseText) els.responseText.textContent = text;
    }

    function escapeHtml(str) {
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    function escapeXml(str) {
        return str
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&apos;');
    }

    // ══════════════════════════════════════════════
    // ── Orb Animation (Canvas 2D) ──
    // ══════════════════════════════════════════════

    const orbState = {
        ctx: null,
        w: 400,
        h: 400,
        cx: 200,
        cy: 200,
        baseRadius: 70,
        points: [],
        numPoints: 80,
        time: 0,
    };

    function initOrb() {
        const canvas = els.orbCanvas;
        if (!canvas) return;

        // Adapt canvas size
        const container = $('orbContainer');
        if (container) {
            const size = Math.min(container.clientWidth, container.clientHeight);
            canvas.width = size;
            canvas.height = size;
            orbState.w = size;
            orbState.h = size;
            orbState.cx = size / 2;
            orbState.cy = size / 2;
            orbState.baseRadius = size * 0.22;
        }

        orbState.ctx = canvas.getContext('2d');

        // Generate points on a circle
        for (let i = 0; i < orbState.numPoints; i++) {
            const angle = (i / orbState.numPoints) * Math.PI * 2;
            orbState.points.push({
                angle,
                offset: Math.random() * 10,
                speed: 0.5 + Math.random() * 1.5,
                amplitude: 2 + Math.random() * 6,
            });
        }

        animateOrb();
    }

    function animateOrb() {
        const { ctx, w, h, cx, cy, baseRadius, points, numPoints } = orbState;
        if (!ctx) return;

        ctx.clearRect(0, 0, w, h);
        orbState.time += 0.016;

        const audioMult = state.audioLevel;
        const isActive = state.isListening || state.isSpeaking || state.isProcessing;

        // Get primary color from CSS
        const wrapper = document.querySelector('.voice-page-wrapper');
        const primaryColor = wrapper?.dataset.primaryColor || '#0078d4';
        const accentColor = wrapper?.dataset.accentColor || '#C9A227';

        // Dynamic parameters
        const breathe = Math.sin(orbState.time * 0.8) * 3;
        const dynamicRadius = baseRadius + breathe + audioMult * 25;

        // Draw glow layers
        for (let layer = 3; layer >= 0; layer--) {
            const layerRadius = dynamicRadius + layer * (8 + audioMult * 12);
            const alpha = (0.03 + audioMult * 0.04) * (1 - layer * 0.2);

            ctx.beginPath();
            for (let i = 0; i <= numPoints; i++) {
                const p = points[i % numPoints];
                const angle = p.angle;
                const noise = Math.sin(orbState.time * p.speed + p.offset) * p.amplitude;
                const audioNoise = audioMult * Math.sin(orbState.time * 3 + i * 0.5) * 8;
                const r = layerRadius + noise + audioNoise;

                const x = cx + Math.cos(angle) * r;
                const y = cy + Math.sin(angle) * r;

                if (i === 0) ctx.moveTo(x, y);
                else ctx.lineTo(x, y);
            }
            ctx.closePath();

            const color = state.isSpeaking ? accentColor : primaryColor;
            ctx.fillStyle = hexToRgba(color, alpha);
            ctx.fill();
        }

        // Main orb body
        ctx.beginPath();
        for (let i = 0; i <= numPoints; i++) {
            const p = points[i % numPoints];
            const angle = p.angle;
            const noise = Math.sin(orbState.time * p.speed + p.offset) * p.amplitude;
            const audioNoise = audioMult * Math.sin(orbState.time * 4 + i * 0.7) * 12;
            const r = dynamicRadius + noise + audioNoise;

            const x = cx + Math.cos(angle) * r;
            const y = cy + Math.sin(angle) * r;

            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        }
        ctx.closePath();

        // Gradient fill
        const grad = ctx.createRadialGradient(cx, cy, 0, cx, cy, dynamicRadius + 20);
        if (state.isSpeaking) {
            grad.addColorStop(0, hexToRgba(accentColor, 0.6));
            grad.addColorStop(0.5, hexToRgba(accentColor, 0.3));
            grad.addColorStop(1, hexToRgba(accentColor, 0.05));
        } else if (state.isListening) {
            grad.addColorStop(0, hexToRgba(primaryColor, 0.7));
            grad.addColorStop(0.5, hexToRgba(primaryColor, 0.35));
            grad.addColorStop(1, hexToRgba(primaryColor, 0.05));
        } else if (state.isProcessing) {
            const pulse = 0.3 + Math.sin(orbState.time * 5) * 0.2;
            grad.addColorStop(0, hexToRgba(primaryColor, pulse + 0.3));
            grad.addColorStop(0.5, hexToRgba(primaryColor, pulse));
            grad.addColorStop(1, hexToRgba(primaryColor, 0.05));
        } else {
            grad.addColorStop(0, hexToRgba(primaryColor, 0.25));
            grad.addColorStop(0.5, hexToRgba(primaryColor, 0.12));
            grad.addColorStop(1, hexToRgba(primaryColor, 0.02));
        }
        ctx.fillStyle = grad;
        ctx.fill();

        // Inner bright core
        const coreRadius = dynamicRadius * 0.3;
        const coreGrad = ctx.createRadialGradient(cx, cy, 0, cx, cy, coreRadius);
        const coreColor = state.isSpeaking ? accentColor : primaryColor;
        coreGrad.addColorStop(0, hexToRgba(coreColor, 0.5 + audioMult * 0.3));
        coreGrad.addColorStop(1, 'transparent');
        ctx.beginPath();
        ctx.arc(cx, cy, coreRadius, 0, Math.PI * 2);
        ctx.fillStyle = coreGrad;
        ctx.fill();

        // Glow element sync
        const glow = els.orbGlow;
        if (glow) {
            glow.classList.toggle('active', isActive);
            if (state.isSpeaking) {
                glow.style.background = `radial-gradient(circle, ${hexToRgba(accentColor, 0.3)} 0%, transparent 70%)`;
            } else {
                glow.style.background = '';
            }
        }

        state.orbAnimId = requestAnimationFrame(animateOrb);
    }

    function hexToRgba(hex, alpha) {
        hex = hex.replace('#', '');
        if (hex.length === 3) {
            hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
        }
        const r = parseInt(hex.substring(0, 2), 16);
        const g = parseInt(hex.substring(2, 4), 16);
        const b = parseInt(hex.substring(4, 6), 16);
        return `rgba(${r},${g},${b},${alpha})`;
    }

    // ── Start ──
    document.addEventListener('DOMContentLoaded', init);
})();
