/**
 * Raphael Voice Assistant — High-Fidelity Tensura Anime Magic Circle Canvas Renderer
 * Ultra-Smooth 60-144 FPS Audio-Reactive Engine with Low-Pass Filtering & Offscreen Caching:
 * - Low-pass filtered audio amplitude pipeline (Zero visual jitter/stepping)
 * - Critically damped spring physics for scale & rotation transitions
 * - Offscreen canvas caching for GPU-accelerated rune rings (0ms shadowBlur stalls)
 * - Persistent single-loop animation with clamped delta-time
 */

class MagicCircleRenderer {
    constructor(canvasId) {
        this.canvas = document.getElementById(canvasId);
        this.ctx = this.canvas.getContext('2d');

        this.baseSize = 650;
        this.width = 0;
        this.height = 0;
        this.centerX = 0;
        this.centerY = 0;
        this.dpr = window.devicePixelRatio || 1;

        this.lastTime = performance.now();

        this.rawVoiceIntensity = 0;
        this.smoothedVoiceIntensity = 0;

        this.scale = 1.0;
        this.targetScale = 1.0;
        this.scaleVelocity = 0;

        this.rotationAngle = 0;
        this.rotationSpeed = 1.0;
        this.targetRotationSpeed = 1.0;

        this.currentColor = { r: 235, g: 195, b: 60 };
        this.targetColor  = { r: 235, g: 195, b: 60 };

        this.coreSize = 50;
        this.targetCoreSize = 50;

        this.kanjiText = "";
        this.kanjiSubText = "";
        this.kanjiAlpha = 0;
        this.kanjiTargetAlpha = 0;
        this.kanjiScale = 0.8;

        this.runesGroup1 = "告·個体名リムル·テンペスト進化完了·智慧之王獲得·告·解析鑑定完了·神智核シエル·究極能力".split("");
        this.runesGroup2 = "RAPHAEL·WISDOM·GREAT·SAGE·CIEL·EVOLUTION·SYSTEM·ACQUIRE·SUCCESS·WISDOM·LORD".split("");
        this.runesGroup3 = "ᚱᚐᛈᚺᚨᛖᛚ·ᚹᛁᛋᛞᛟᛗ·ᚷᚱᛖᚨᛏ·ᛋᚨᚷᛖ·ᚲᛁᛖᛚ·ᛖᚠᛟᛚᚢᛏᛁᛟᚾ".split("");

        this.particles = [];
        this.maxParticles = 120;
        this.auraPhase = 0;

        this.offscreenRings = {};

        this.resize();
        window.addEventListener('resize', () => this.resize());

        this.loopBound = this.animate.bind(this);
        requestAnimationFrame(this.loopBound);
    }

    resize() {
        const rect = this.canvas.parentNode.getBoundingClientRect();
        this.width = rect.width;
        this.height = rect.height;

        this.canvas.width  = this.width  * this.dpr;
        this.canvas.height = this.height * this.dpr;

        this.canvas.style.width  = `${this.width}px`;
        this.canvas.style.height = `${this.height}px`;

        this.ctx.scale(this.dpr, this.dpr);
        this.centerX = this.width  / 2;
        this.centerY = this.height / 2;

        this.buildOffscreenRings();
    }

    buildOffscreenRings() {
        this.offscreenRings = {
            ring1: this.createRuneRingCanvas(90, this.runesGroup1, 11, '"Noto Serif JP", serif'),
            ring2: this.createRuneRingCanvas(135, this.runesGroup3, 12, '"Cinzel Decorative", serif'),
            ring3: this.createRuneRingCanvas(190, this.runesGroup2, 10, '"Orbitron", monospace')
        };
    }

    createRuneRingCanvas(radius, runes, fontSize, fontStyle) {
        const padding = 30;
        const size = (radius + padding) * 2;
        const offCanvas = document.createElement('canvas');
        offCanvas.width = size * this.dpr;
        offCanvas.height = size * this.dpr;

        const offCtx = offCanvas.getContext('2d');
        offCtx.scale(this.dpr, this.dpr);
        offCtx.translate(size / 2, size / 2);

        offCtx.font = `700 ${fontSize}px ${fontStyle}`;
        offCtx.fillStyle = '#ffffff';
        offCtx.textAlign = 'center';
        offCtx.textBaseline = 'middle';

        const numRunes = runes.length;
        const angleStep = (Math.PI * 2) / numRunes;

        for (let i = 0; i < numRunes; i++) {
            offCtx.save();
            offCtx.rotate(i * angleStep);
            offCtx.translate(0, -radius);
            offCtx.fillText(runes[i], 0, 0);
            offCtx.restore();
        }

        return { canvas: offCanvas, width: size, height: size };
    }

    setTheme(theme) {
        if (theme === 'user' || theme === 'listening') {
            this.targetColor = { r: 0, g: 240, b: 255 };
            this.targetRotationSpeed = 1.4;
            this.triggerKanji("音声認識中", "VOICE INPUT ACTIVE");
        } else if (theme === 'processing') {
            this.targetColor = { r: 255, g: 70, b: 140 };
            this.targetRotationSpeed = 2.5;
            this.triggerKanji("解析鑑定中", "ANALYZING QUERY...");
        } else {
            this.targetColor = { r: 235, g: 195, b: 60 };
            this.targetRotationSpeed = 1.0;
        }
    }

    triggerKanji(mainText, subText = "") {
        this.kanjiText = mainText;
        this.kanjiSubText = subText;
        this.kanjiAlpha = 0;
        this.kanjiTargetAlpha = 1.0;
        this.kanjiScale = 0.75;

        setTimeout(() => {
            this.kanjiTargetAlpha = 0;
        }, 2600);
    }

    updateVoiceData(intensity) {
        this.rawVoiceIntensity = Math.max(0, Math.min(1.0, intensity));
    }

    update(dt) {
        const lowPassAlpha = 1 - Math.exp(-12 * dt);
        this.smoothedVoiceIntensity += (this.rawVoiceIntensity - this.smoothedVoiceIntensity) * lowPassAlpha;

        const idleBreathing = Math.sin(performance.now() * 0.0015) * 0.015;
        const voiceExpansion = this.smoothedVoiceIntensity * 0.08;
        this.targetScale = 1.0 + idleBreathing + voiceExpansion;

        this.targetRotationSpeed = (this.targetColor.r === 255 ? 2.5 : (this.targetColor.r === 0 ? 1.4 : 1.0)) + (this.smoothedVoiceIntensity * 0.8);
        this.targetCoreSize = 50 + (this.smoothedVoiceIntensity * 25);

        const springStiffness = 140;
        const springDamping = 18;
        const scaleDiff = this.targetScale - this.scale;
        const springForce = scaleDiff * springStiffness;
        const dampingForce = -this.scaleVelocity * springDamping;

        this.scaleVelocity += (springForce + dampingForce) * dt;
        this.scale += this.scaleVelocity * dt;

        const lerpRate = 1 - Math.exp(-8 * dt);
        this.rotationSpeed += (this.targetRotationSpeed - this.rotationSpeed) * lerpRate;
        this.coreSize += (this.targetCoreSize - this.coreSize) * lerpRate;

        this.currentColor.r += (this.targetColor.r - this.currentColor.r) * lerpRate;
        this.currentColor.g += (this.targetColor.g - this.currentColor.g) * lerpRate;
        this.currentColor.b += (this.targetColor.b - this.currentColor.b) * lerpRate;

        this.kanjiAlpha += (this.kanjiTargetAlpha - this.kanjiAlpha) * lerpRate;
        if (this.kanjiAlpha > 0.01) {
            this.kanjiScale += (1.0 - this.kanjiScale) * lerpRate;
        }

        this.rotationAngle += 0.25 * dt * this.rotationSpeed;
        this.auraPhase += 0.5 * dt;

        this.updateParticles(dt);
    }

    updateParticles(dt) {
        const spawnChance = (0.15 + (this.smoothedVoiceIntensity * 0.4)) * (dt * 60);
        if (this.particles.length < this.maxParticles && Math.random() < spawnChance) {
            const pAngle = Math.random() * Math.PI * 2;
            const speed  = 0.6 + Math.random() * 2.0 + (this.smoothedVoiceIntensity * 2);

            const useTeal = Math.random() > 0.6;
            const color = useTeal 
                ? { r: 0, g: 230, b: 255 } 
                : { r: 255, g: 215, b: 70 };

            this.particles.push({
                x: 0, y: 0,
                vx: Math.cos(pAngle) * speed,
                vy: Math.sin(pAngle) * speed,
                size: 1.5 + Math.random() * 3.5 + (this.smoothedVoiceIntensity * 2),
                alpha: 0.8 + Math.random() * 0.2,
                decay: (0.005 + Math.random() * 0.01) * (dt * 60),
                color: color
            });
        }

        for (let i = this.particles.length - 1; i >= 0; i--) {
            const p = this.particles[i];
            p.x += p.vx * (dt * 60);
            p.y += p.vy * (dt * 60);
            p.vx *= Math.pow(0.985, dt * 60);
            p.vy *= Math.pow(0.985, dt * 60);
            p.alpha -= p.decay;
            if (p.alpha <= 0) this.particles.splice(i, 1);
        }
    }

    draw() {
        this.ctx.clearRect(0, 0, this.width, this.height);
        this.ctx.save();

        const currentScale = (Math.min(this.width, this.height) / this.baseSize) * this.scale;
        this.ctx.translate(this.centerX, this.centerY);
        this.ctx.scale(currentScale, currentScale);

        const r = Math.round(this.currentColor.r);
        const g = Math.round(this.currentColor.g);
        const b = Math.round(this.currentColor.b);

        this.ctx.save();
        this.ctx.globalCompositeOperation = 'lighter';

        const auraGrad = this.ctx.createRadialGradient(0, 0, 60, 0, 0, 310);
        auraGrad.addColorStop(0,   `rgba(${r}, ${g}, ${b}, 0.28)`);
        auraGrad.addColorStop(0.4, `rgba(0, 220, 255, 0.12)`);
        auraGrad.addColorStop(0.8, `rgba(180, 100, 255, 0.04)`);
        auraGrad.addColorStop(1,   'transparent');

        this.ctx.fillStyle = auraGrad;
        this.ctx.beginPath();
        this.ctx.arc(0, 0, 310, 0, Math.PI * 2);
        this.ctx.fill();

        const numRibbons = 6;
        for (let i = 0; i < numRibbons; i++) {
            const rAngle = this.auraPhase + (i * Math.PI * 2 / numRibbons);
            const rRadius = 180 + Math.sin(this.auraPhase * 2 + i) * 25;

            this.ctx.save();
            this.ctx.rotate(rAngle);
            this.ctx.strokeStyle = i % 2 === 0 
                ? `rgba(0, 240, 255, ${0.15 + this.smoothedVoiceIntensity * 0.15})` 
                : `rgba(255, 215, 60, ${0.18 + this.smoothedVoiceIntensity * 0.15})`;
            this.ctx.lineWidth = 10 + Math.sin(i) * 5;
            this.ctx.beginPath();
            this.ctx.arc(0, 0, rRadius, 0, Math.PI * 0.6);
            this.ctx.stroke();
            this.ctx.restore();
        }
        this.ctx.restore();

        for (const p of this.particles) {
            this.ctx.fillStyle = `rgba(${p.color.r}, ${p.color.g}, ${p.color.b}, ${p.alpha})`;
            this.ctx.beginPath();
            this.ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
            this.ctx.fill();
        }

        const colorStr    = `rgba(${r}, ${g}, ${b}, 0.95)`;
        const dimColorStr = `rgba(${r}, ${g}, ${b}, 0.35)`;

        this.ctx.save();
        this.ctx.rotate(-this.rotationAngle * 0.4);
        const numRays = 32;
        const rayLength = 270 + (this.smoothedVoiceIntensity * 50);

        for (let i = 0; i < numRays; i++) {
            const rAngle = (i * Math.PI * 2) / numRays;
            this.ctx.save();
            this.ctx.rotate(rAngle);

            const isMajorRay = i % 4 === 0;
            const alphaVal = isMajorRay ? 0.55 : 0.22;

            const rayGrad = this.ctx.createLinearGradient(0, 0, 0, -rayLength);
            rayGrad.addColorStop(0,   '#ffffff');
            rayGrad.addColorStop(0.2, `rgba(${r}, ${g}, ${b}, ${alphaVal})`);
            rayGrad.addColorStop(0.7, `rgba(${r}, ${g}, ${b}, 0.04)`);
            rayGrad.addColorStop(1,   'transparent');

            this.ctx.strokeStyle = rayGrad;
            this.ctx.lineWidth = isMajorRay ? 2.5 : 1.2;
            this.ctx.beginPath();
            this.ctx.moveTo(0, 0);
            this.ctx.lineTo(0, -rayLength);
            this.ctx.stroke();

            if (isMajorRay) {
                this.ctx.fillStyle = '#ffffff';
                this.ctx.beginPath();
                this.ctx.arc(0, -rayLength * 0.9, 2.2, 0, Math.PI * 2);
                this.ctx.fill();
            }
            this.ctx.restore();
        }
        this.ctx.restore();

        this.ctx.strokeStyle = '#ffffff';
        this.ctx.lineWidth = 3.0;
        this.ctx.beginPath();
        this.ctx.arc(0, 0, 50, 0, Math.PI * 2);
        this.ctx.stroke();

        this.ctx.strokeStyle = colorStr;
        this.ctx.lineWidth = 1.5;
        this.ctx.setLineDash([4, 8]);
        this.ctx.save();
        this.ctx.rotate(this.rotationAngle * 2.5);
        this.ctx.beginPath();
        this.ctx.arc(0, 0, 62, 0, Math.PI * 2);
        this.ctx.stroke();
        this.ctx.restore();
        this.ctx.setLineDash([]);

        if (this.offscreenRings.ring1) {
            this.ctx.save();
            this.ctx.rotate(-this.rotationAngle * 1.2);
            const r1 = this.offscreenRings.ring1;
            this.ctx.drawImage(r1.canvas, -r1.width / 2, -r1.height / 2, r1.width, r1.height);
            this.ctx.restore();
        }

        this.ctx.strokeStyle = colorStr;
        this.ctx.lineWidth = 2.0;
        this.ctx.save();
        this.ctx.rotate(-this.rotationAngle * 0.8);
        [[0, 0.45], [0.5, 0.95], [1, 1.45], [1.5, 1.95]].forEach(([s, e]) => {
            this.ctx.beginPath();
            this.ctx.arc(0, 0, 106, Math.PI * s, Math.PI * e);
            this.ctx.stroke();
        });
        this.ctx.restore();

        if (this.offscreenRings.ring2) {
            this.ctx.save();
            this.ctx.rotate(this.rotationAngle * 0.9);
            const r2 = this.offscreenRings.ring2;
            this.ctx.drawImage(r2.canvas, -r2.width / 2, -r2.height / 2, r2.width, r2.height);
            this.ctx.restore();
        }

        this.ctx.save();
        this.ctx.rotate(this.rotationAngle * 0.35);
        this.ctx.strokeStyle = '#ffffff';
        this.ctx.lineWidth = 2.2;
        const sides = 8;
        const oRadius = 158;
        this.ctx.beginPath();
        for (let i = 0; i <= sides; i++) {
            const sAngle = (i * Math.PI * 2) / sides;
            const x = Math.cos(sAngle) * oRadius;
            const y = Math.sin(sAngle) * oRadius;
            if (i === 0) this.ctx.moveTo(x, y);
            else this.ctx.lineTo(x, y);
        }
        this.ctx.stroke();
        this.ctx.restore();

        if (this.offscreenRings.ring3) {
            this.ctx.save();
            this.ctx.rotate(-this.rotationAngle * 0.6);
            const r3 = this.offscreenRings.ring3;
            this.ctx.drawImage(r3.canvas, -r3.width / 2, -r3.height / 2, r3.width, r3.height);
            this.ctx.restore();
        }

        this.ctx.strokeStyle = colorStr;
        this.ctx.lineWidth = 3.5;
        this.ctx.save();
        this.ctx.rotate(-this.rotationAngle * 0.5);
        this.ctx.setLineDash([45, 12, 6, 12]);
        this.ctx.beginPath();
        this.ctx.arc(0, 0, 218, 0, Math.PI * 2);
        this.ctx.stroke();
        this.ctx.restore();
        this.ctx.setLineDash([]);

        this.ctx.strokeStyle = dimColorStr;
        this.ctx.lineWidth = 1.5;
        this.ctx.beginPath();
        this.ctx.arc(0, 0, 242, 0, Math.PI * 2);
        this.ctx.stroke();

        let coreGrad = this.ctx.createRadialGradient(0, 0, 2, 0, 0, this.coreSize * 1.4);
        coreGrad.addColorStop(0,   '#ffffff');
        coreGrad.addColorStop(0.25, '#ffffff');
        coreGrad.addColorStop(0.55, `rgba(${r}, ${g}, ${b}, 0.92)`);
        coreGrad.addColorStop(1,   'transparent');

        this.ctx.fillStyle = coreGrad;
        this.ctx.beginPath();
        this.ctx.arc(0, 0, this.coreSize * 1.4, 0, Math.PI * 2);
        this.ctx.fill();

        this.ctx.fillStyle = '#ffffff';
        this.ctx.beginPath();
        this.ctx.arc(0, 0, 7 + (this.smoothedVoiceIntensity * 5), 0, Math.PI * 2);
        this.ctx.fill();

        this.ctx.restore();

        if (this.kanjiAlpha > 0.01 && this.kanjiText) {
            this.ctx.save();
            this.ctx.translate(this.centerX, this.centerY);
            this.ctx.scale(this.kanjiScale, this.kanjiScale);
            this.ctx.globalAlpha = this.kanjiAlpha;

            this.ctx.font = '900 50px "Noto Serif JP", serif';
            this.ctx.textAlign = 'center';
            this.ctx.textBaseline = 'middle';

            this.ctx.strokeStyle = '#000000';
            this.ctx.lineWidth = 7;
            this.ctx.strokeText(this.kanjiText, 0, -10);

            this.ctx.fillStyle = '#ffffff';
            this.ctx.fillText(this.kanjiText, 0, -10);

            if (this.kanjiSubText) {
                this.ctx.font = '700 12px "Orbitron", sans-serif';
                this.ctx.fillStyle = `rgba(${r}, ${g}, ${b}, 0.95)`;
                this.ctx.fillText(this.kanjiSubText, 0, 30);
            }

            this.ctx.restore();
        }
    }

    animate(currentTime) {
        const rawDt = (currentTime - this.lastTime) / 1000;
        const dt = Math.max(0.001, Math.min(0.033, rawDt));
        this.lastTime = currentTime;

        this.update(dt);
        this.draw();

        requestAnimationFrame(this.loopBound);
    }
}

window.MagicCircleRenderer = MagicCircleRenderer;
