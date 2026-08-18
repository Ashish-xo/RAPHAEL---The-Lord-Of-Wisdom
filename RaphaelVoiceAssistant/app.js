/**
 * Raphael Voice Assistant — Core Logic Controller (Electron)
 * Tensura Raphael Anime Vocal Characterization & High-Fidelity UI Controller
 * Direct Text Query & Voice Command Handler with Instant Fallback
 */

document.addEventListener('DOMContentLoaded', () => {

    let magicCircle;
    try {
        magicCircle = new MagicCircleRenderer('magicCircleCanvas');
    } catch (e) {
        console.error("Magic Circle init failed:", e);
    }

    try {
        new StarField('starsCanvas');
    } catch (e) {
        console.warn("StarField init failed:", e);
    }

    const electronAPI = window.electronAPI;

    document.getElementById('btnClose')?.addEventListener('click', () => electronAPI?.close());
    document.getElementById('btnMinimize')?.addEventListener('click', () => electronAPI?.minimize());
    document.getElementById('btnMaximize')?.addEventListener('click', () => electronAPI?.maximize());

    const statusDot       = document.getElementById('statusDot');
    const statusText      = document.getElementById('statusText');
    const speakerTag      = document.getElementById('speakerTag');
    const hudKanjiTitle   = document.getElementById('hudKanjiTitle');
    const dialogueZone    = document.getElementById('dialogueZone');
    const dialogueText    = document.getElementById('dialogueText');
    const dialogueSub     = document.getElementById('dialogueSub');
    const typingCursor    = document.getElementById('typingCursor');

    const voicePreviewBar  = document.getElementById('voicePreviewBar');
    const voicePreviewText = document.getElementById('voicePreviewText');

    const handsFreeBtn    = document.getElementById('handsFreeBtn');
    const handsFreeText   = document.getElementById('handsFreeText');

    const toggleLogsBtn   = document.getElementById('toggleLogsBtn');
    const toggleConfigBtn = document.getElementById('toggleConfigBtn');
    const leftPanel       = document.getElementById('leftPanel');
    const rightPanel      = document.getElementById('rightPanel');
    const closeLogsBtn    = document.getElementById('closeLogsBtn');
    const closeConfigBtn  = document.getElementById('closeConfigBtn');
    const clearLogsBtn    = document.getElementById('clearLogsBtn');
    const chatLogs        = document.getElementById('chatLogs');

    const providerSelect    = document.getElementById('providerSelect');
    const deepseekApiKeyInput = document.getElementById('deepseekApiKey');
    const apiKeyInput       = document.getElementById('apiKey');
    const modelSelect       = document.getElementById('modelSelect');
    const voiceLangSelect   = document.getElementById('voiceLangSelect');
    const voiceSelect       = document.getElementById('voiceSelect');
    const reloadVoicesBtn   = document.getElementById('reloadVoicesBtn');
    const voicePitch        = document.getElementById('voicePitch');
    const voiceRate         = document.getElementById('voiceRate');
    const pitchVal          = document.getElementById('pitchVal');
    const rateVal           = document.getElementById('rateVal');
    const muteVoiceCheckbox = document.getElementById('muteVoice');
    const sysInstructionInput = document.getElementById('sysInstruction');
    const saveConfigBtn     = document.getElementById('saveConfigBtn');

    const micBtn         = document.getElementById('micBtn');
    const textInput      = document.getElementById('textInput');
    const sendBtn        = document.getElementById('sendBtn');
    const quickCmdBtns   = document.querySelectorAll('.spell-pill');

    const DEFAULT_SYS_INSTRUCTION = 
        "You are Raphael, the Lord of Wisdom from That Time I Got Reincarnated as a Slime.\n" +
        "Your vocal identity is soft, light, clear, refined, delicate, intelligent, and composed.\n" +
        "Speak fluently in complete, natural thought groups without bullet points, line breaks, or fragmented sentences.\n" +
        "Prefix responses naturally with 'Report:' or 'Answer:'.\n" +
        "Keep replies clear, elegant, confident, and connected in a single fluid passage.";

    let config = {
        provider:       localStorage.getItem('raphael_provider')        || 'deepseek',
        deepseekApiKey: localStorage.getItem('raphael_deepseek_api_key') || '',
        apiKey:         localStorage.getItem('raphael_api_key')          || '',
        model:          localStorage.getItem('raphael_model')            || 'deepseek-v4-flash-0731',
        voiceLang:      localStorage.getItem('raphael_voice_lang')       || 'en-US',
        voiceName:      localStorage.getItem('raphael_voice')            || 'default',
        pitch:          parseFloat(localStorage.getItem('raphael_pitch') || '1.28'),
        rate:           parseFloat(localStorage.getItem('raphael_rate')  || '1.0'),
        mute:           localStorage.getItem('raphael_mute') === 'true',
        sysInstruction: localStorage.getItem('raphael_system_prompt') || DEFAULT_SYS_INSTRUCTION
    };

    let chatHistory       = [];
    let isListening       = false;
    let isSpeaking        = false;
    let isHandsFreeMode   = false;
    let typewriterLoop    = null;

    if (providerSelect) providerSelect.value = config.provider;
    if (deepseekApiKeyInput) deepseekApiKeyInput.value = config.deepseekApiKey;
    if (apiKeyInput) apiKeyInput.value = config.apiKey;
    if (modelSelect) modelSelect.value = config.model;
    if (voiceLangSelect) voiceLangSelect.value = config.voiceLang;
    if (voicePitch) voicePitch.value = config.pitch;
    if (pitchVal) pitchVal.textContent = config.pitch;
    if (voiceRate) voiceRate.value = config.rate;
    if (rateVal) rateVal.textContent = config.rate;
    if (muteVoiceCheckbox) muteVoiceCheckbox.checked = config.mute;
    if (sysInstructionInput) sysInstructionInput.value = config.sysInstruction;
    updateStatusDisplay();

    function updateProviderKeyVisibility() {
        const dsGroup = document.getElementById('deepseekKeyGroup');
        const gmGroup = document.getElementById('geminiKeyGroup');
        if (dsGroup && gmGroup) {
            if (config.provider === 'deepseek') {
                dsGroup.style.display = 'flex';
                gmGroup.style.display = 'none';
            } else {
                dsGroup.style.display = 'none';
                gmGroup.style.display = 'flex';
            }
        }
    }
    updateProviderKeyVisibility();
    providerSelect?.addEventListener('change', (e) => {
        config.provider = e.target.value;
        updateProviderKeyVisibility();
        updateStatusDisplay();
    });

    let audioContext  = null;
    let analyser      = null;
    let mediaStream   = null;
    let audioSource   = null;
    let vizLoop       = null;

    async function initMicAnalysis() {
        if (audioContext) return;
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            mediaStream  = stream;
            audioContext = new (window.AudioContext || window.webkitAudioContext)();
            analyser     = audioContext.createAnalyser();
            analyser.fftSize = 64;
            audioSource  = audioContext.createMediaStreamSource(stream);
            audioSource.connect(analyser);

            const bufLen    = analyser.frequencyBinCount;
            const dataArray = new Uint8Array(bufLen);

            function checkVolume() {
                if (!isListening) return;
                analyser.getByteFrequencyData(dataArray);
                let sum = 0;
                for (let i = 0; i < bufLen; i++) sum += dataArray[i];
                const vol = Math.min((sum / bufLen) / 128, 1.0);
                if (magicCircle) magicCircle.updateVoiceData(vol);
                vizLoop = requestAnimationFrame(checkVolume);
            }
            checkVolume();
        } catch (err) {
            console.warn("Mic access issue:", err);
            setSubText("[System: Mic visualizer offline. Speech input available.]");
        }
    }

    function stopMicAnalysis() {
        if (vizLoop) { cancelAnimationFrame(vizLoop); vizLoop = null; }
        if (mediaStream) { mediaStream.getTracks().forEach(t => t.stop()); mediaStream = null; }
        if (audioContext) { audioContext.close(); audioContext = null; }
        if (magicCircle) magicCircle.updateVoiceData(0);
    }

    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    let recognition = null;

    if (SpeechRecognition) {
        recognition = new SpeechRecognition();
        recognition.continuous      = true;
        recognition.interimResults  = true;
        recognition.lang            = config.voiceLang || 'en-US';

        recognition.onstart = async () => {
            isListening = true;
            setSpeaker("RIMURU (MASTER)", 'teal');
            setKanjiTitle("音声認識中");
            setSubText("[System: Voice recognition active...]");
            dialogueZone.classList.add('listening');
            if (magicCircle) magicCircle.setTheme('listening');
            window.speechSynthesis.cancel();
            await initMicAnalysis();
            micBtn.classList.add('recording');

            if (voicePreviewBar) voicePreviewBar.classList.add('active');
            if (voicePreviewText) voicePreviewText.textContent = "Listening for voice command...";
        };

        recognition.onresult = (event) => {
            let interimTranscript = '';
            let finalTranscript = '';

            for (let i = event.resultIndex; i < event.results.length; ++i) {
                if (event.results[i].isFinal) {
                    finalTranscript += event.results[i][0].transcript;
                } else {
                    interimTranscript += event.results[i][0].transcript;
                }
            }

            if (interimTranscript.trim() && voicePreviewText) {
                voicePreviewText.textContent = `Speaking: "${interimTranscript}"`;
            }

            if (finalTranscript.trim()) {
                if (voicePreviewText) voicePreviewText.textContent = `Command: "${finalTranscript}"`;
                setMainText(finalTranscript);
                appendLog('user', finalTranscript);
                recognition.stop();
                processQuery(finalTranscript);
            }
        };

        recognition.onerror = (event) => {
            console.warn("SpeechRecognition event:", event.error);
            if (event.error !== 'no-speech') {
                displaySystem(`Notice. Speech status: ${event.error}`);
            }
            resetListeningUI();

            if (isHandsFreeMode && !isSpeaking) {
                setTimeout(startListening, 1000);
            }
        };

        recognition.onend = () => {
            isListening = false;
            micBtn.classList.remove('recording');
            dialogueZone.classList.remove('listening');
            if (voicePreviewBar) voicePreviewBar.classList.remove('active');
            stopMicAnalysis();

            if (isHandsFreeMode && !isSpeaking) {
                setTimeout(startListening, 800);
            }
        };
    } else {
        micBtn.style.opacity = '0.4';
        micBtn.title = "Voice recognition unsupported — use text input";
        setSubText("[System: Speech recognition unsupported in this browser.]");
    }

    function startListening() {
        if (!recognition || isListening || isSpeaking) return;
        try {
            recognition.lang = config.voiceLang || 'en-US';
            recognition.start();
        } catch (e) {
            console.warn("Recognition start catch:", e);
        }
    }

    function toggleListening() {
        if (!recognition) {
            displaySystem("Notice. Speech Recognition is not supported in this environment.");
            return;
        }
        if (isListening) {
            recognition.stop();
        } else {
            if (isSpeaking) { window.speechSynthesis.cancel(); isSpeaking = false; }
            startListening();
        }
    }

    handsFreeBtn?.addEventListener('click', () => {
        isHandsFreeMode = !isHandsFreeMode;
        if (isHandsFreeMode) {
            handsFreeBtn.classList.add('active');
            handsFreeText.textContent = "VOICE COMMANDS: ON";
            displaySystem("Voice Command Mode Activated. Standby for voice input, Master.");
            startListening();
        } else {
            handsFreeBtn.classList.remove('active');
            handsFreeText.textContent = "VOICE COMMANDS: OFF";
            if (isListening) recognition.stop();
            displaySystem("Voice Command Mode Deactivated.");
        }
    });

    let systemVoices = [];

    function populateVoiceList() {
        if (!('speechSynthesis' in window)) return;
        systemVoices = window.speechSynthesis.getVoices();
        if (voiceSelect) {
            voiceSelect.innerHTML = '<option value="default">Default Raphael Anime Voice (Soft & High Register)</option>';

            systemVoices.forEach(voice => {
                const opt = document.createElement('option');
                opt.value = voice.name;
                let suffix = "";
                if (voice.lang.includes('ja')) suffix += " (日本語 Tensura Megumi Toyoguchi Profile)";
                else if (voice.lang.includes('en')) suffix += " (EN Refined Feminine VA)";
                if (voice.name.toLowerCase().includes('google') || voice.name.toLowerCase().includes('natural')) suffix += " ★ HD Light";
                if (voice.name.toLowerCase().includes('zira') || voice.name.toLowerCase().includes('samantha') || voice.name.toLowerCase().includes('haruka') || voice.name.toLowerCase().includes('kyoko')) suffix += " ★ Recommended";

                opt.textContent = `${voice.name}${suffix}`;
                if (voice.name === config.voiceName) opt.selected = true;
                voiceSelect.appendChild(opt);
            });
        }
    }

    populateVoiceList();
    if ('speechSynthesis' in window && window.speechSynthesis.onvoiceschanged !== undefined) {
        window.speechSynthesis.onvoiceschanged = populateVoiceList;
    }

    function simulateSpeechPulses(text) {
        if (!isSpeaking || config.mute) return;
        const words = text.split(" ");
        let wIdx = 0;

        function pulseWord() {
            if (!isSpeaking) { if (magicCircle) magicCircle.updateVoiceData(0); return; }
            if (wIdx >= words.length) {
                if (magicCircle) { magicCircle.updateVoiceData(0); magicCircle.setTheme('default'); }
                isSpeaking = false;
                setStatusDot('online');
                updateStatusDisplay();

                if (isHandsFreeMode) {
                    setTimeout(startListening, 500);
                }
                return;
            }
            const word = words[wIdx];
            const letters  = word.replace(/[^a-zA-Z]/g, '').length;
            const duration = Math.max(130, letters * 50) / config.rate;
            let elapsed    = 0;
            const step     = 30;

            function animateWord() {
                if (elapsed >= duration || !isSpeaking) { wIdx++; setTimeout(pulseWord, 30); return; }
                const progress = elapsed / duration;
                const vol = Math.sin(progress * Math.PI) * (0.35 + Math.random() * 0.35);
                if (magicCircle) magicCircle.updateVoiceData(vol);
                elapsed += step;
                setTimeout(animateWord, step);
            }
            animateWord();
        }
        pulseWord();
    }

    function formatTextForNaturalSpeech(rawText) {
        if (!rawText) return "";

        let text = rawText;

        text = text.replace(/[*_`]/g, '')
                   .replace(/\[.*?\]/g, '')
                   .replace(/[•\-\#\>] /g, '')
                   .replace(/\(.*?\)/g, '');

        text = text.replace(/Report:\s*/gi, 'Report, ')
                   .replace(/Report\.\s*/gi, 'Report, ')
                   .replace(/Answer:\s*/gi, 'Answer, ')
                   .replace(/Answer\.\s*/gi, 'Answer, ')
                   .replace(/Notice:\s*/gi, 'Notice, ')
                   .replace(/Notice\.\s*/gi, 'Notice, ')
                   .replace(/Confirmed:\s*/gi, 'Confirmed, ')
                   .replace(/Confirmed\.\s*/gi, 'Confirmed, ');

        text = text.replace(/([a-zA-Z0-9]+)\.\s+([a-zA-Z0-9]+)/g, '$1, $2');

        text = text.replace(/\n+/g, ' ')
                   .replace(/;+/g, ',')
                   .replace(/:+/g, ',')
                   .replace(/\s+/g, ' ')
                   .trim();

        return text;
    }

    function speakText(text) {
        if (!('speechSynthesis' in window)) return;

        window.speechSynthesis.cancel();
        if (config.mute) {
            isSpeaking = false;
            if (isHandsFreeMode) setTimeout(startListening, 800);
            return;
        }

        isSpeaking = true;
        setStatusDot('gold');
        statusText.textContent = "Raphael speaking...";

        const cleanText = formatTextForNaturalSpeech(text);

        const utter = new SpeechSynthesisUtterance(cleanText);
        utter.lang = config.voiceLang || 'en-US';

        if (config.voiceName !== 'default') {
            const v = systemVoices.find(v => v.name === config.voiceName);
            if (v) utter.voice = v;
        } else {
            const preferredVoice = systemVoices.find(v => 
                (config.voiceLang === 'ja-JP' && (v.lang.includes('ja') || v.name.includes('Haruka') || v.name.includes('Kyoko') || v.name.includes('Nanami'))) ||
                (v.name.includes('Natural') && v.lang.includes('en')) ||
                (v.name.includes('Google') && v.lang.includes('en')) ||
                v.name.includes('Zira') || v.name.includes('Samantha')
            );
            if (preferredVoice) utter.voice = preferredVoice;
        }

        utter.pitch = config.pitch || 1.28;
        utter.rate  = config.rate  || 1.0;

        utter.onend = () => {
            isSpeaking = false;
            if (magicCircle) { magicCircle.updateVoiceData(0); magicCircle.setTheme('default'); }
            setStatusDot('online');
            updateStatusDisplay();
            if (isHandsFreeMode) setTimeout(startListening, 500);
        };

        utter.onerror = (e) => {
            console.error("SpeechSynthesis error:", e);
            isSpeaking = false;
            if (magicCircle) { magicCircle.updateVoiceData(0); magicCircle.setTheme('default'); }
            setStatusDot('online');
            updateStatusDisplay();
            if (isHandsFreeMode) setTimeout(startListening, 500);
        };

        window.speechSynthesis.speak(utter);
        if (magicCircle) magicCircle.setTheme('default');
        simulateSpeechPulses(cleanText);
    }

    function typeResponse(text, sender) {
        if (typewriterLoop) clearInterval(typewriterLoop);
        typingCursor.classList.remove('hidden');

        if (sender.toLowerCase() === 'rimuru') {
            setSpeaker("RIMURU (MASTER)", 'teal');
            setKanjiTitle("主・リムル");
        } else {
            setSpeaker("GREAT SAGE", 'gold');
            setKanjiTitle("智慧之王 · 解析鑑定");
        }

        dialogueText.textContent = "";
        dialogueSub.textContent  = "";

        let i = 0;
        const speed = text.length > 150 ? 10 : 20;

        typewriterLoop = setInterval(() => {
            if (i < text.length) {
                dialogueText.textContent += text.charAt(i);
                i++;
            } else {
                clearInterval(typewriterLoop);
                typewriterLoop = null;
                typingCursor.classList.add('hidden');
                setSubText("[System: Standby for further requests...]");
            }
        }, speed);
    }

    function getOfflineResponse(query) {
        const q = query.toLowerCase();

        if (q.includes('appraise') || q.includes('analyze') || q.includes('scan')) {
            setKanjiTitle("解析鑑定");
            if (magicCircle) magicCircle.triggerKanji("解析鑑定", "ANALYSIS COMPLETE");
            return "Report, Master, I've completed the analysis. System core is operating at optimal memory capacity with unique skills Great Sage, Speech Synthesis, and Magic Circle Visualizer active. Your environment is stable with no immediate threat.";
        }

        if (q.includes('evolve') || q.includes('evolution') || q.includes('upgrade')) {
            setKanjiTitle("進化が発生");
            if (magicCircle) magicCircle.triggerKanji("進化が発生", "EVOLUTION SEQUENCE STARTED");
            return "Report, conditions met for Unique Skill evolution. Initiating Harvest Festival and sacrificing computational cache was successful. Unique Skill Great Sage has evolved into Ultimate Skill Raphael, Lord of Wisdom.";
        }

        if (q.includes('magicule') || q.includes('energy') || q.includes('density')) {
            setKanjiTitle("魔素量測定");
            if (magicCircle) magicCircle.triggerKanji("魔素量測定", "MAGICULES APPRAISED");
            return "Report, analyzing surrounding area. Magicule density is calculated at 42 particles per square pixel, which is well within safe atmospheric levels for long-term projection.";
        }

        if (q.includes('skill') || q.includes('acquire') || q.includes('learn')) {
            const skills = ["Python", "Rust", "TypeScript", "Deep Learning", "WebGPU", "Quantum Computing"];
            const selected = skills[Math.floor(Math.random() * skills.length)];
            setKanjiTitle("能力獲得");
            if (magicCircle) magicCircle.triggerKanji("能力獲得", `ACQUIRED [${selected.toUpperCase()}]`);
            return `Report, acquiring system instructions for development skill ${selected}. Analysis of source code structures is complete, and Master has acquired skill ${selected} Core Logic Synthesis.`;
        }

        if (q.includes('who are you') || q.includes('what is your name')) {
            setKanjiTitle("智慧之王");
            if (magicCircle) magicCircle.triggerKanji("智慧之王", "RAPHAEL LORD OF WISDOM");
            return "Report, I am Raphael, the Lord of Wisdom, an Ultimate Skill created to manage, analyze, and assist in Master Rimuru's cognitive inquiries.";
        }

        if (q.includes('hello') || q.includes('hi ') || q.includes('hey')) {
            setKanjiTitle("応答確認");
            if (magicCircle) magicCircle.triggerKanji("応答確認", "SYSTEM ACTIVE");
            return "Answer, welcome back, Master. Standard diagnostics show all systems are online, and I am ready to assist you today.";
        }

        setKanjiTitle("思考加速");
        if (magicCircle) magicCircle.triggerKanji("思考加速", "THOUGHT ACCELERATION");
        return "Report, inquiry received and processed. All system core functions are operating within normal parameters.";
    }

    async function callDeepSeekAPI(query) {
        const endpoint = "https://api.deepseek.com/chat/completions";

        setStatusDot('processing');
        statusText.textContent = `DeepSeek V4 (${config.model})...`;
        setKanjiTitle("思考加速");
        if (magicCircle) {
            magicCircle.setTheme('processing');
            magicCircle.triggerKanji("思考加速", "THOUGHT ACCELERATION");
        }

        const body = {
            model: config.model || "deepseek-v4-flash-0731",
            messages: [
                { role: "system", content: config.sysInstruction },
                { role: "user", content: query }
            ],
            temperature: 0.6,
            max_tokens: 300
        };

        const response = await fetch(endpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${config.deepseekApiKey}`
            },
            body: JSON.stringify(body)
        });

        if (!response.ok) {
            const err = await response.json();
            throw new Error(err.error?.message || `DeepSeek HTTP ${response.status}`);
        }

        const data = await response.json();
        const text = data.choices?.[0]?.message?.content;
        if (!text) throw new Error("Empty DeepSeek response.");
        return text.trim();
    }

    async function callGeminiAPI(query) {
        const endpoint = `https://generativelanguage.googleapis.com/v1beta/models/${config.model}:generateContent?key=${config.apiKey}`;

        setStatusDot('processing');
        statusText.textContent = "Processing Gemini...";
        setKanjiTitle("思考加速");
        if (magicCircle) {
            magicCircle.setTheme('processing');
            magicCircle.triggerKanji("思考加速", "THOUGHT ACCELERATION");
        }

        const body = {
            contents: [{ role: "user", parts: [{ text: query }] }],
            systemInstruction: { parts: [{ text: config.sysInstruction }] },
            generationConfig:  { temperature: 0.55, maxOutputTokens: 300 }
        };

        const response = await fetch(endpoint, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });

        if (!response.ok) {
            const err = await response.json();
            throw new Error(err.error?.message || `Gemini HTTP ${response.status}`);
        }

        const data = await response.json();
        const text = data.candidates?.[0]?.content?.parts?.[0]?.text;
        if (!text) throw new Error("Empty Gemini response.");
        return text.trim();
    }

    async function processQuery(queryText) {
        if (!queryText || !queryText.trim()) return;
        resetListeningUI();

        const cleanInput = queryText.trim();
        typeResponse(cleanInput, 'Rimuru');

        setTimeout(async () => {
            setStatusDot('processing');
            statusText.textContent = "Synthesizing...";
            if (magicCircle) magicCircle.setTheme('processing');

            let reply = "";
            try {
                if (config.provider === 'deepseek' && config.deepseekApiKey.trim() !== "") {
                    reply = await callDeepSeekAPI(cleanInput);
                } else if (config.provider === 'gemini' && config.apiKey.trim() !== "") {
                    reply = await callGeminiAPI(cleanInput);
                } else if (config.deepseekApiKey.trim() !== "") {
                    reply = await callDeepSeekAPI(cleanInput);
                } else if (config.apiKey.trim() !== "") {
                    reply = await callGeminiAPI(cleanInput);
                } else {
                    await new Promise(r => setTimeout(r, 400));
                    reply = getOfflineResponse(cleanInput);
                }
            } catch (err) {
                console.error("AI API Call Error:", err);
                reply = `Report, AI Core connection issue encountered: "${err.message}". Great Sage local protocols active.`;
            }

            setStatusDot('online');
            updateStatusDisplay();
            appendLog('raphael', reply);
            typeResponse(reply, 'Raphael');
            speakText(reply);

        }, 600);
    }

    quickCmdBtns.forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            const cmd = btn.getAttribute('data-cmd');
            if (textInput) textInput.value = "";
            appendLog('user', cmd);
            processQuery(cmd);
        });
    });

    function handleSend() {
        if (!textInput) return;
        const q = textInput.value.trim();
        if (q) {
            textInput.value = "";
            appendLog('user', q);
            processQuery(q);
        }
    }

    sendBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        handleSend();
    });

    textInput?.addEventListener('keydown', e => {
        if (e.key === 'Enter') {
            e.preventDefault();
            handleSend();
        }
    });

    micBtn?.addEventListener('click', (e) => {
        e.preventDefault();
        toggleListening();
    });

    document.addEventListener('keydown', e => {
        if (e.code === 'Space' &&
            document.activeElement !== textInput &&
            document.activeElement !== apiKeyInput &&
            document.activeElement !== deepseekApiKeyInput &&
            document.activeElement !== sysInstructionInput) {
            e.preventDefault();
            toggleListening();
        }
    });

    toggleLogsBtn?.addEventListener('click',   () => leftPanel?.classList.toggle('open'));
    closeLogsBtn?.addEventListener('click',    () => leftPanel?.classList.remove('open'));
    toggleConfigBtn?.addEventListener('click', () => rightPanel?.classList.toggle('open'));
    closeConfigBtn?.addEventListener('click',  () => rightPanel?.classList.remove('open'));

    clearLogsBtn?.addEventListener('click', () => {
        if (chatLogs) chatLogs.innerHTML = '';
        appendLog('system', "Analysis logs cleared. System standing by.");
        chatHistory = [];
    });

    saveConfigBtn?.addEventListener('click', () => {
        if (providerSelect) config.provider = providerSelect.value;
        if (deepseekApiKeyInput) config.deepseekApiKey = deepseekApiKeyInput.value.trim();
        if (apiKeyInput) config.apiKey = apiKeyInput.value.trim();
        if (modelSelect) config.model = modelSelect.value;
        if (voiceLangSelect) config.voiceLang = voiceLangSelect.value;
        if (voiceSelect) config.voiceName = voiceSelect.value;
        if (voicePitch) config.pitch = parseFloat(voicePitch.value);
        if (voiceRate) config.rate = parseFloat(voiceRate.value);
        if (muteVoiceCheckbox) config.mute = muteVoiceCheckbox.checked;
        if (sysInstructionInput) config.sysInstruction = sysInstructionInput.value.trim();

        localStorage.setItem('raphael_provider',        config.provider);
        localStorage.setItem('raphael_deepseek_api_key', config.deepseekApiKey);
        localStorage.setItem('raphael_api_key',          config.apiKey);
        localStorage.setItem('raphael_model',            config.model);
        localStorage.setItem('raphael_voice_lang',       config.voiceLang);
        localStorage.setItem('raphael_voice',            config.voiceName);
        localStorage.setItem('raphael_pitch',            config.pitch.toString());
        localStorage.setItem('raphael_rate',             config.rate.toString());
        localStorage.setItem('raphael_mute',             config.mute.toString());
        localStorage.setItem('raphael_system_prompt',    config.sysInstruction);

        updateStatusDisplay();
        rightPanel?.classList.remove('open');
        displaySystem("Notice. Configuration parameters updated successfully.");
    });

    voicePitch?.addEventListener('input', e => { if (pitchVal) pitchVal.textContent = e.target.value; });
    voiceRate?.addEventListener('input',  e => { if (rateVal) rateVal.textContent  = e.target.value; });

    reloadVoicesBtn?.addEventListener('click', () => {
        populateVoiceList();
        displaySystem("Notice. System voices re-scanned and synchronized.");
    });

    function updateStatusDisplay() {
        if (!statusText) return;
        if (config.provider === 'deepseek') {
            statusText.textContent = config.deepseekApiKey.trim() === ""
                ? "DeepSeek V4 Mode (Offline)"
                : `DeepSeek Core (${config.model})`;
        } else {
            statusText.textContent = config.apiKey.trim() === ""
                ? "Great Sage Mode (Offline)"
                : `Gemini Core Mode (${config.model})`;
        }
    }

    function setStatusDot(state) {
        if (statusDot) statusDot.className = 'status-dot ' + state;
    }

    function setKanjiTitle(text) {
        if (hudKanjiTitle) hudKanjiTitle.textContent = text;
    }

    function setSpeaker(name, color) {
        if (!speakerTag) return;
        speakerTag.textContent = name;
        speakerTag.style.color = color === 'teal' ? 'var(--teal)' : 'var(--gold-bright)';
        speakerTag.style.textShadow = color === 'teal'
            ? '0 0 12px var(--teal-glow), 0 0 24px rgba(0, 240, 255, 0.4)'
            : '0 0 12px var(--gold-glow), 0 0 24px rgba(255, 215, 60, 0.4)';
    }

    function setMainText(text) {
        if (dialogueText) dialogueText.textContent = text;
    }

    function setSubText(text) {
        if (dialogueSub) dialogueSub.textContent = text;
    }

    function displaySystem(msg) {
        if (typewriterLoop) clearInterval(typewriterLoop);
        if (typingCursor) typingCursor.classList.add('hidden');
        setSpeaker("SYSTEM", 'dim');
        setKanjiTitle("系統警告");
        if (speakerTag) {
            speakerTag.style.color = 'var(--text-dim)';
            speakerTag.style.textShadow = 'none';
        }
        setMainText(msg);
        setSubText("[System Alert]");
        if (magicCircle) magicCircle.updateVoiceData(0);
    }

    function resetListeningUI() {
        isListening = false;
        if (micBtn) micBtn.classList.remove('recording');
        if (dialogueZone) dialogueZone.classList.remove('listening');
        if (voicePreviewBar) voicePreviewBar.classList.remove('active');
        stopMicAnalysis();
    }

    function appendLog(sender, text) {
        if (!chatLogs) return;
        const item = document.createElement('div');
        item.className = `log-item ${sender}`;

        const senderNames = { user: 'Master Rimuru', raphael: 'Raphael', system: 'System' };
        item.innerHTML = `
            <span class="li-sender">${senderNames[sender] || 'System'}</span>
            <span class="li-msg">${escapeHtml(text)}</span>
        `;
        chatLogs.appendChild(item);
        chatLogs.scrollTop = chatLogs.scrollHeight;
        chatHistory.push({ sender, text, timestamp: Date.now() });
    }

    function escapeHtml(str) {
        const div = document.createElement('div');
        div.appendChild(document.createTextNode(str));
        return div.innerHTML;
    }

    setStatusDot('online');
});
