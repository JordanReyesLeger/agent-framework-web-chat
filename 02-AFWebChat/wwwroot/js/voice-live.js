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
        voiceGender: document.getElementById('vu-voice-gender'),
        voiceCount: document.getElementById('vu-voice-count'),
        voiceIcon: document.getElementById('vu-voice-icon'),
        voiceName: document.getElementById('vu-voice-name'),
        voiceMeta: document.getElementById('vu-voice-meta'),
        voicePickerOpen: document.getElementById('vu-voice-picker-open'),
        voiceDialog: document.getElementById('vu-voice-dialog'),
        voiceDialogClose: document.getElementById('vu-voice-dialog-close'),
        voiceSearch: document.getElementById('vu-voice-search'),
        voiceFilterBar: document.getElementById('vu-voice-filter-bar'),
        voiceGallery: document.getElementById('vu-voice-gallery'),
        voiceResultsCount: document.getElementById('vu-voice-results-count'),
        outputLocaleRow: document.getElementById('vu-output-locale-row'),
        outputLocale: document.getElementById('vu-output-locale'),
        outputLocaleHelp: document.getElementById('vu-output-locale-help'),
        inputLanguage: document.getElementById('vu-input-language'),
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
        outputLocales: [],
        omniCatalog: null,
        // Map<voiceId, {styles, gender, lang, label, type}>
        voicesById: new Map(),
        groups: [],
        gender: 'all',
        language: 'all',
        pickerFilter: 'all',
        pickerSearch: '',
    };

    let languageDisplayNames = null;
    try {
        languageDisplayNames = new Intl.DisplayNames(['es'], { type: 'language' });
    } catch {
        // Older browsers fall back to the BCP-47 code.
    }

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
        // Siempre arranca con el prompt por default del servidor. Los cambios en el
        // textarea viven solo en memoria durante la sesión (se envían al conectar) y
        // NO persisten: al recargar la página se vuelve al default.
        els.instructions.value = cfg.instructions || '';
        if (els.model) {
            els.model.replaceChildren();
            (cfg.models || []).forEach(model => {
                const option = document.createElement('option');
                option.value = model.id;
                option.textContent = `${model.label} · ${model.tier}`;
                option.title = model.description || '';
                els.model.appendChild(option);
            });
            els.model.value = cfg.model || 'gpt-realtime-mini';
        }
        if (els.inputLanguage) els.inputLanguage.value = cfg.inputLanguage ?? 'es-MX';
        await loadVoices(cfg.voice);
    }

    async function loadVoices(currentVoice) {
        try {
            const r = await fetch('/api/VoiceLive/voices');
            const data = await r.json();
            catalog.voicesById.clear();
            catalog.avatar = data.avatar || null;
            catalog.groups = data.groups || [];
            catalog.outputLocales = data.outputLocales || [];
            catalog.omniCatalog = data.omniCatalog || null;

            const preferred = currentVoice || data.defaultVoice;
            catalog.groups.forEach(group => {
                (group.voices || []).forEach(v => {
                    catalog.voicesById.set(v.id, {
                        ...v,
                        type: group.type || 'azure',
                        groupName: group.name,
                    });
                });
            });
            catalog.language = 'all';
            catalog.gender = 'all';
            renderVoiceOptions(preferred);

            populateStylesForSelectedVoice();
            updateVoiceSummary();
            renderVoiceGallery();
            populateAvatarChoices();
            if (catalog.omniCatalog?.isFallback) {
                logEvent('Catálogo Omni remoto no disponible; se cargó el respaldo es-ES/es-MX.', 'info');
            }
        } catch (e) {
            logEvent('No se pudo cargar el catálogo de voces: ' + e.message, 'error');
        }
    }

    function renderVoiceOptions(preferredVoice) {
        const select = els.voice;
        const previous = preferredVoice || select.value;
        const previousLanguage = catalog.voicesById.get(previous)?.lang;
        select.innerHTML = '';
        let visibleCount = 0;
        let matchedPrevious = false;

        const availableForLanguage = Array.from(catalog.voicesById.values()).filter(matchesVoiceLanguage);
        if (catalog.gender !== 'all' && !availableForLanguage.some(v => v.gender === catalog.gender)) {
            catalog.gender = 'all';
            syncGenderButtons();
        }

        catalog.groups.forEach(group => {
            const voices = (group.voices || []).filter(v => matchesVoiceLanguage(v) && (catalog.gender === 'all' || v.gender === catalog.gender));
            if (!voices.length) return;

            const og = document.createElement('optgroup');
            og.label = friendlyGroupName(group.name);
            voices.forEach(v => {
                const opt = document.createElement('option');
                opt.value = v.id;
                opt.dataset.voiceType = group.type || 'azure';
                opt.textContent = `${genderIcon(v.gender)} ${v.label}  (${v.lang})`;
                if (v.id === previous) {
                    opt.selected = true;
                    matchedPrevious = true;
                }
                og.appendChild(opt);
                visibleCount++;
            });
            select.appendChild(og);
        });

        if (!matchedPrevious && previousLanguage) {
            const sameLanguage = Array.from(select.options).find(option => catalog.voicesById.get(option.value)?.lang === previousLanguage);
            if (sameLanguage) sameLanguage.selected = true;
        }
        if (!select.value && select.options.length > 0) select.selectedIndex = 0;
        if (els.voiceCount) els.voiceCount.textContent = `${visibleCount} ${visibleCount === 1 ? 'voz' : 'voces'}`;
        syncGenderAvailability(availableForLanguage);
    }

    function voiceLanguageBucket(language) {
        if (language === 'es-MX') return 'es-MX';
        if (language?.startsWith('es-')) return 'es';
        if (language === 'multi') return 'auto';
        if (language === 'en-US') return 'en-US';
        return 'all';
    }

    function matchesVoiceLanguage(voice) {
        switch (catalog.language) {
            case 'auto': return voice.lang === 'multi';
            case 'es': return voice.lang?.startsWith('es-');
            case 'es-MX': return voice.lang === 'es-MX';
            case 'multi': return voice.lang === 'multi';
            case 'en-US': return voice.lang === 'en-US';
            default: return true;
        }
    }

    function friendlyGroupName(name) {
        if (name.includes('Speech-to-Speech')) return 'Tiempo real · voces nativas';
        if (name.includes('Dragon HD Omni')) return 'Omni HD · Multilingüe';
        if (name.includes('Azure HD')) return 'HD · English (US)';
        if (name === 'Multilingüe') return 'Voces multilingües';
        return name;
    }

    function syncGenderButtons() {
        els.voiceGender?.querySelectorAll('button[data-gender]').forEach(button => {
            const active = button.dataset.gender === catalog.gender;
            button.classList.toggle('is-active', active);
            button.setAttribute('aria-pressed', active ? 'true' : 'false');
        });
    }

    function syncGenderAvailability(voices) {
        els.voiceGender?.querySelectorAll('button[data-gender]').forEach(button => {
            const gender = button.dataset.gender;
            button.disabled = gender !== 'all' && !voices.some(voice => voice.gender === gender);
        });
    }

    function updateVoiceLanguageHelp() {
        if (!els.voiceLanguageHelp) return;
        const messages = {
            auto: 'Voces nativas S2S y multilingües: el modelo conserva el idioma de la conversación y del prompt.',
            es: 'Muestra todas las voces españolas disponibles, incluyendo México, España, Argentina y Colombia.',
            'es-MX': 'Restringe la salida a voces nativas de español de México.',
            multi: 'Muestra voces marcadas como multilingües para elegir manualmente el timbre.',
            'en-US': 'Muestra las voces Azure HD disponibles en inglés de Estados Unidos.',
            all: 'Muestra todo el catálogo, sin filtrar por idioma de salida.',
        };
        els.voiceLanguageHelp.textContent = messages[catalog.language] || messages.all;
    }

    function friendlyLanguage(code) {
        const names = {
            'de-DE': 'Deutsch (Deutschland)',
            'es-MX': 'Español (México)',
            'es-ES': 'Español (España)',
            'es-AR': 'Español (Argentina)',
            'es-CO': 'Español (Colombia)',
            'en-US': 'English (US)',
            'fr-FR': 'Français (France)',
            'ja-JP': '日本語 (日本)',
            'zh-CN': '中文 (中国)',
            multi: 'Multilingüe',
        };
        if (names[code]) return names[code];
        if (!code) return 'Idioma flexible';
        return languageDisplayNames?.of(code) || code;
    }

    function friendlyGender(gender) {
        return ({ female: 'Mujer', male: 'Hombre', neutral: 'Neutral' })[gender] || 'Sin especificar';
    }

    function updateVoiceSummary() {
        const voice = catalog.voicesById.get(els.voice.value);
        if (!voice) return;
        if (els.voiceIcon) els.voiceIcon.textContent = genderIcon(voice.gender);
        if (els.voiceName) els.voiceName.textContent = voice.label;
        if (els.voiceMeta) {
            const provider = voice.type === 'openai' ? 'Speech-to-Speech' : 'Azure Speech';
            const quality = friendlyQuality(voice);
            const age = friendlyAgeGroup(voice.ageGroup);
            els.voiceMeta.textContent = `${friendlyGender(voice.gender)}${age ? ` · ${age}` : ''} · ${friendlyLanguage(voice.lang)} · ${quality || provider}`;
        }
        populateOutputLocales(voice);
        els.voiceGallery?.querySelectorAll('.vu-voice-card').forEach(card => {
            const selected = card.dataset.voiceId === voice.id;
            card.classList.toggle('is-selected', selected);
            card.setAttribute('aria-selected', selected ? 'true' : 'false');
        });
    }

    function friendlyQuality(voice) {
        if (voice.quality === 'omni-hd') return 'Dragon HD Omni';
        if (voice.quality === 'hd') return 'Neural HD';
        if (voice.quality === 'multilingual') return 'Multilingüe';
        if (voice.quality === 'realtime') return 'Tiempo real S2S';
        return 'Neural estándar';
    }

    function friendlyAgeGroup(ageGroup) {
        return ({
            Child: 'Infantil',
            'Young Adult': 'Adulto joven',
            Adult: 'Adulto',
            Senior: 'Senior',
        })[ageGroup] || ageGroup || '';
    }

    function localeLabel(locale) {
        return ({
            'de-DE': 'Deutsch · Deutschland',
            'es-MX': 'Español · México',
            'es-ES': 'Español · España',
            'es-AR': 'Español · Argentina',
            'es-CO': 'Español · Colombia',
            'en-US': 'English · United States',
            'fr-FR': 'Français · France',
            'ja-JP': '日本語 · 日本',
            'zh-CN': '中文 · 中国',
        })[locale] || friendlyLanguage(locale);
    }

    function outputLocalesForVoice(voice) {
        return voice.quality === 'omni-hd'
            ? catalog.outputLocales
            : (voice.locales || []);
    }

    function populateOutputLocales(voice) {
        if (!els.outputLocale) return;
        const previous = els.outputLocale.value;
        els.outputLocale.replaceChildren();
        const automatic = document.createElement('option');
        automatic.value = '';
        automatic.textContent = voice.type === 'openai'
            ? 'Automático · según la conversación'
            : 'Automático · detecta el idioma del texto';
        els.outputLocale.appendChild(automatic);

        const locales = outputLocalesForVoice(voice);
        locales.forEach(locale => {
            const option = document.createElement('option');
            option.value = locale;
            option.textContent = localeLabel(locale);
            els.outputLocale.appendChild(option);
        });
        if (Array.from(els.outputLocale.options).some(option => option.value === previous)) {
            els.outputLocale.value = previous;
        }
        if (els.outputLocaleHelp) {
            els.outputLocaleHelp.textContent = voice.type === 'openai'
                ? 'Las voces S2S cambian de idioma automáticamente con la conversación.'
                : locales.length
                    ? 'Puedes dejar detección automática o forzar uno de los locales compatibles.'
                    : 'Esta voz usa automáticamente su locale principal.';
        }
    }

    function voiceMatchesPickerFilter(voice) {
        switch (catalog.pickerFilter) {
            // Geographic filters represent the persona's primary locale, not
            // every output language that a multilingual voice can synthesize.
            case 'es': return voice.lang?.startsWith('es-');
            case 'es-ES': return voice.lang === 'es-ES';
            case 'es-MX': return voice.lang === 'es-MX';
            case 'en-US': return voice.lang === 'en-US';
            case 'hd': return voice.quality === 'hd' || voice.quality === 'omni-hd';
            case 'omni': return voice.quality === 'omni-hd';
            case 'multi': return voice.multilingual || voice.quality === 'multilingual' || voice.lang === 'multi';
            case 'realtime': return voice.type === 'openai';
            default: return true;
        }
    }

    function voiceMatchesSearch(voice) {
        if (!catalog.pickerSearch) return true;
        const text = [
            voice.label, voice.id, voice.lang, voice.gender, voice.quality,
            voice.groupName, voice.description, voice.ageGroup, voice.status,
            friendlyLanguage(voice.lang), friendlyGender(voice.gender), friendlyAgeGroup(voice.ageGroup),
            ...(voice.locales || []).map(localeLabel),
        ].join(' ').toLocaleLowerCase('es-MX');
        return text.includes(catalog.pickerSearch);
    }

    function renderVoiceGallery() {
        if (!els.voiceGallery) return;
        const voices = Array.from(catalog.voicesById.values())
            .filter(voiceMatchesPickerFilter)
            .filter(voiceMatchesSearch);
        els.voiceGallery.replaceChildren();
        if (els.voiceResultsCount) els.voiceResultsCount.textContent = `${voices.length} ${voices.length === 1 ? 'voz' : 'voces'}`;

        if (!voices.length) {
            const empty = document.createElement('div');
            empty.className = 'vu-voice-empty';
            empty.innerHTML = '<i class="bi bi-search"></i><strong>Sin resultados</strong><span>Prueba otro nombre o filtro.</span>';
            els.voiceGallery.appendChild(empty);
            return;
        }

        const fragment = document.createDocumentFragment();
        voices.forEach(voice => {
            const card = document.createElement('button');
            card.type = 'button';
            card.className = 'vu-voice-card';
            card.dataset.voiceId = voice.id;
            card.setAttribute('role', 'option');
            card.setAttribute('aria-label', `Seleccionar voz ${voice.label}`);
            card.innerHTML = '<span class="vu-voice-card-icon"></span><span class="vu-voice-card-copy"><span class="vu-voice-card-title"><strong></strong><span class="vu-voice-quality"></span></span><small></small><code></code></span><span class="vu-avatar-check"><i class="bi bi-check-lg"></i></span>';
            card.querySelector('.vu-voice-card-icon').textContent = genderIcon(voice.gender);
            card.querySelector('strong').textContent = voice.label;
            card.querySelector('.vu-voice-quality').textContent = friendlyQuality(voice);
            const age = friendlyAgeGroup(voice.ageGroup);
            card.querySelector('small').textContent = `${friendlyLanguage(voice.lang)} · ${friendlyGender(voice.gender)}${age ? ` · ${age}` : ''}`;
            if (voice.description) card.title = voice.description;
            card.querySelector('code').textContent = voice.id;
            card.addEventListener('click', () => selectVoiceFromPicker(voice.id));
            fragment.appendChild(card);
        });
        els.voiceGallery.appendChild(fragment);
        updateVoiceSummary();
    }

    function selectVoiceFromPicker(voiceId) {
        els.voice.value = voiceId;
        els.voice.dispatchEvent(new Event('change', { bubbles: true }));
        els.voiceDialog?.close();
        els.voicePickerOpen?.focus();
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
            og.label = '🎭 Talking Heads (Photo Avatar · Preview)';
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
        window.VoiceUiAvatarPreview?.refresh();
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
        window.VoiceUiAvatarPreview?.syncPose();
        window.VoiceUiAvatarPreview?.sync();
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
        if (els.model?.value) params.set('model', els.model.value);
        if (els.voice.value) {
            params.set('voice', els.voice.value);
            const opt = els.voice.options[els.voice.selectedIndex];
            const vt = opt && opt.dataset && opt.dataset.voiceType;
            if (vt) params.set('voiceType', vt);
        }
        if (els.style && els.style.value) params.set('style', els.style.value);
        if (els.outputLocale && els.outputLocale.value) params.set('outputLocale', els.outputLocale.value);
        if (els.inputLanguage) params.set('inputLanguage', els.inputLanguage.value);
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
                    logEvent(`Listo · modelo=${msg.model} voz=${msg.voice} salida=${msg.outputLocale || 'automática'} entrada=${msg.inputLanguage || 'automática'}` + (msg.style ? ` estilo=${msg.style}` : ''), 'info');
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

    // Repopulate styles and metadata when the voice changes.
    els.voice.addEventListener('change', () => {
        populateStylesForSelectedVoice();
        updateVoiceSummary();
    });

    els.voicePickerOpen?.addEventListener('click', () => {
        catalog.pickerSearch = '';
        if (els.voiceSearch) els.voiceSearch.value = '';
        renderVoiceGallery();
        els.voiceDialog?.showModal();
        setTimeout(() => els.voiceSearch?.focus(), 0);
    });
    els.voiceDialogClose?.addEventListener('click', () => els.voiceDialog?.close());
    els.voiceDialog?.addEventListener('click', event => {
        if (event.target === els.voiceDialog) els.voiceDialog.close();
    });
    let voiceSearchTimer = null;
    els.voiceSearch?.addEventListener('input', () => {
        clearTimeout(voiceSearchTimer);
        voiceSearchTimer = setTimeout(() => {
            catalog.pickerSearch = els.voiceSearch.value.trim().toLocaleLowerCase('es-MX');
            renderVoiceGallery();
        }, 120);
    });
    els.voiceFilterBar?.addEventListener('click', event => {
        const button = event.target.closest('button[data-filter]');
        if (!button) return;
        catalog.pickerFilter = button.dataset.filter;
        els.voiceFilterBar.querySelectorAll('button[data-filter]').forEach(item => {
            const active = item === button;
            item.classList.toggle('is-active', active);
            item.setAttribute('aria-pressed', active ? 'true' : 'false');
        });
        renderVoiceGallery();
    });

    if (els.voiceGender) {
        els.voiceGender.addEventListener('click', e => {
            const button = e.target.closest('button[data-gender]');
            if (!button) return;
            catalog.gender = button.dataset.gender;
            els.voiceGender.querySelectorAll('button').forEach(item => {
                const active = item === button;
                item.classList.toggle('is-active', active);
                item.setAttribute('aria-pressed', active ? 'true' : 'false');
            });
            renderVoiceOptions();
            populateStylesForSelectedVoice();
            updateVoiceSummary();
        });
    }

    if (els.voiceLanguage) {
        els.voiceLanguage.addEventListener('change', () => {
            catalog.language = els.voiceLanguage.value;
            renderVoiceOptions();
            populateStylesForSelectedVoice();
            updateVoiceSummary();
            updateVoiceLanguageHelp();
        });
    }

    // Avatar toggle: reveal character/style fields.
    els.avatarEnabled.addEventListener('change', () => {
        els.avatarFields.style.display = els.avatarEnabled.checked ? '' : 'none';
        if (els.avatarEnabled.checked) window.VoiceUiAvatarPreview?.sync();
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
