// Lightweight neon electric-grid background for authentication pages
(function() {
    var canvas = document.getElementById('authBackgroundCanvas');
    if (!canvas) return;

    var ctx = canvas.getContext('2d');
    var width, height;
    var nodes = [];
    var pulses = [];
    var sparks = [];
    var nextPulse = 0;
    var isVisible = true;
    var frameCount = 0;
    var lastTime = 0;
    var performanceMode = 0; // 0=full, 1=reduced, 2=minimal
    var lastClickSpark = 0;

    var phaseColors = [
        { name: 'L1', color: '#f59e0b', glow: 'rgba(245, 158, 11, 0.5)' },
        { name: 'L2', color: '#22c55e', glow: 'rgba(34, 197, 94, 0.5)' },
        { name: 'L3', color: '#ef4444', glow: 'rgba(239, 68, 68, 0.5)' }
    ];

    function random(min, max) {
        return Math.random() * (max - min) + min;
    }

    function resize() {
        width = window.innerWidth;
        height = window.innerHeight;
        canvas.width = width;
        canvas.height = height;
        createNodes();
    }

    function createNodes() {
        nodes = [];
        var count = Math.floor((width * height) / 45000); // density ~1 node per 45k px
        count = Math.max(12, Math.min(count, 24));
        for (var i = 0; i < count; i++) {
            var phase = phaseColors[i % phaseColors.length];
            nodes.push({
                x: random(60, width - 60),
                y: random(60, height - 60),
                vx: random(-0.3, 0.3),
                vy: random(-0.3, 0.3),
                radius: random(3, 5),
                phase: phase,
                energy: random(0, Math.PI * 2)
            });
        }
    }

    function drawBackground() {
        var gradient = ctx.createLinearGradient(0, 0, 0, height);
        gradient.addColorStop(0, '#020617');
        gradient.addColorStop(0.5, '#0a0f2b');
        gradient.addColorStop(1, '#0f172a');
        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, width, height);
    }

    function updateNodes() {
        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            node.x += node.vx;
            node.y += node.vy;
            node.energy += 0.02;

            if (node.x < 50 || node.x > width - 50) node.vx *= -1;
            if (node.y < 50 || node.y > height - 50) node.vy *= -1;
        }
    }

    function drawConnections() {
        var maxDistance = performanceMode === 0 ? 220 : 160;
        for (var i = 0; i < nodes.length; i++) {
            var a = nodes[i];
            for (var j = i + 1; j < nodes.length; j++) {
                var b = nodes[j];
                var dx = a.x - b.x;
                var dy = a.y - b.y;
                var dist = Math.sqrt(dx * dx + dy * dy);
                if (dist < maxDistance) {
                    var alpha = (1 - dist / maxDistance) * 0.35;
                    ctx.beginPath();
                    ctx.moveTo(a.x, a.y);
                    ctx.lineTo(b.x, b.y);
                    ctx.strokeStyle = 'rgba(100, 150, 255, ' + alpha + ')';
                    ctx.lineWidth = 1;
                    ctx.stroke();
                }
            }
        }
    }

    function drawNodes() {
        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            var pulse = Math.sin(node.energy) * 0.5 + 0.5;

            // Glow
            ctx.beginPath();
            ctx.arc(node.x, node.y, node.radius + 4 + pulse * 2, 0, Math.PI * 2);
            ctx.fillStyle = node.phase.glow;
            ctx.fill();

            // Core
            ctx.beginPath();
            ctx.arc(node.x, node.y, node.radius, 0, Math.PI * 2);
            ctx.fillStyle = node.phase.color;
            ctx.fill();
        }
    }

    function createSpark(x, y, color) {
        if (sparks.length > 30) return;
        var count = performanceMode === 0 ? 6 : 3;
        for (var i = 0; i < count; i++) {
            var angle = random(0, Math.PI * 2);
            var speed = random(1, 3);
            sparks.push({
                x: x,
                y: y,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed,
                life: 1,
                color: color
            });
        }
    }

    function drawSparks() {
        for (var i = sparks.length - 1; i >= 0; i--) {
            var s = sparks[i];
            ctx.save();
            ctx.globalCompositeOperation = 'screen';
            ctx.fillStyle = 'rgba(255, 255, 255, ' + s.life + ')';
            ctx.shadowBlur = 4;
            ctx.shadowColor = s.color;
            ctx.beginPath();
            ctx.arc(s.x, s.y, 1.5, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();

            s.x += s.vx;
            s.y += s.vy;
            s.life -= 0.025;
            if (s.life <= 0) sparks.splice(i, 1);
        }
    }

    function spawnPulse() {
        if (nodes.length < 2) return;
        var start = nodes[Math.floor(random(0, nodes.length))];
        var candidates = nodes.filter(function(n) {
            return n !== start;
        });
        var end = candidates[Math.floor(random(0, candidates.length))];
        var color = start.phase.color;
        pulses.push({
            start: start,
            end: end,
            progress: 0,
            speed: random(0.008, 0.015),
            color: color,
            trail: []
        });
    }

    function drawPulses() {
        for (var i = pulses.length - 1; i >= 0; i--) {
            var p = pulses[i];
            p.progress += p.speed;
            if (p.progress >= 1) {
                createSpark(p.end.x, p.end.y, p.color);
                pulses.splice(i, 1);
                continue;
            }

            var x = p.start.x + (p.end.x - p.start.x) * p.progress;
            var y = p.start.y + (p.end.y - p.start.y) * p.progress;

            p.trail.push({ x: x, y: y });
            if (p.trail.length > 8) p.trail.shift();

            // Trail
            ctx.beginPath();
            for (var t = 0; t < p.trail.length; t++) {
                var pt = p.trail[t];
                if (t === 0) ctx.moveTo(pt.x, pt.y);
                else ctx.lineTo(pt.x, pt.y);
            }
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.4)';
            ctx.lineWidth = 2;
            ctx.stroke();

            // Head
            ctx.beginPath();
            ctx.arc(x, y, 3, 0, Math.PI * 2);
            ctx.fillStyle = p.color;
            ctx.shadowBlur = 8;
            ctx.shadowColor = p.color;
            ctx.fill();
            ctx.shadowBlur = 0;
        }
    }

    function updatePerformance(timestamp) {
        frameCount++;
        if (timestamp - lastTime >= 1000) {
            var fps = Math.round((frameCount * 1000) / (timestamp - lastTime));
            frameCount = 0;
            lastTime = timestamp;
            if (fps < 25) {
                performanceMode = 2;
            } else if (fps < 45) {
                performanceMode = 1;
            } else {
                performanceMode = 0;
            }
        }
    }

    function animate(timestamp) {
        if (!isVisible) {
            requestAnimationFrame(animate);
            return;
        }

        updatePerformance(timestamp);
        drawBackground();
        updateNodes();
        drawConnections();
        drawNodes();
        drawPulses();
        drawSparks();

        // Spawn new pulse occasionally
        if (performanceMode < 2 && timestamp > nextPulse) {
            spawnPulse();
            nextPulse = timestamp + (performanceMode === 0 ? 400 : 800) + random(0, 600);
        }

        requestAnimationFrame(animate);
    }

    var resizeTimeout;
    function throttleResize() {
        clearTimeout(resizeTimeout);
        resizeTimeout = setTimeout(resize, 150);
    }

    function init() {
        if (window.console) console.log('[AuthBg] init electric grid');
        resize();
        animate(0);
        window.addEventListener('resize', throttleResize);

        document.addEventListener('click', function(e) {
            if (e.target.closest('.auth-card') || e.target.closest('.erp-topbar')) return;
            var now = performance.now();
            if (now - lastClickSpark < 200) return;
            lastClickSpark = now;

            // Find nearest node and trigger burst
            var nearest = null;
            var minDist = Infinity;
            for (var i = 0; i < nodes.length; i++) {
                var dx = nodes[i].x - e.clientX;
                var dy = nodes[i].y - e.clientY;
                var d = dx * dx + dy * dy;
                if (d < minDist) {
                    minDist = d;
                    nearest = nodes[i];
                }
            }
            if (nearest) {
                createSpark(nearest.x, nearest.y, nearest.phase.color);
                pulses.push({
                    start: nearest,
                    end: nodes[Math.floor(random(0, nodes.length))],
                    progress: 0,
                    speed: 0.02,
                    color: nearest.phase.color,
                    trail: []
                });
            }
        });

        document.addEventListener('visibilitychange', function() {
            isVisible = !document.hidden;
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
