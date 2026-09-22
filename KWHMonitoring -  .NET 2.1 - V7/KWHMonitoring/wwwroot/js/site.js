// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// ============================================
// DYNAMIC VISUAL ENHANCEMENTS
// Untuk membuat tampilan lebih jelas dan dinamis
// ============================================

// ============================================
// DYNAMIC CATEGORY CSS INJECTION
// Generates CSS classes for category colors
// so new categories get proper styling without
// needing to edit site.css
// ============================================
var _categoryStyleEl = null;

function injectCategoryStyles(categories) {
    if (!_categoryStyleEl) {
        _categoryStyleEl = document.createElement('style');
        _categoryStyleEl.id = 'dynamic-category-styles';
        document.head.appendChild(_categoryStyleEl);
    }

    var css = '';
    (categories || []).forEach(function(cat) {
        var name = cat.name;
        var color = cat.color || '#607d8b';
        var r = parseInt(color.substr(1, 2), 16);
        var g = parseInt(color.substr(3, 2), 16);
        var b = parseInt(color.substr(5, 2), 16);
        var rgba25 = 'rgba(' + r + ',' + g + ',' + b + ',0.25)';
        var rgba50 = 'rgba(' + r + ',' + g + ',' + b + ',0.5)';

        // Category selector dropdown
        css += '.category-selector.cat-' + name + ' { background-color: ' + rgba25 + '; border-color: ' + rgba50 + '; }\n';
        // Category icon
        css += '.category-icon.cat-' + name + ' { color: ' + color + '; }\n';
        // Badge
        css += '.badge.cat-' + name + ' { background-color: ' + color + ' !important; color: #fff; }\n';
        // Category dropdown dot
        css += '.category-dropdown-item .cat-dot.dot-' + name + ' { background: ' + color + '; }\n';
    });

    _categoryStyleEl.textContent = css;
}

// ============================================
// DYNAMIC TEXT UPDATE WITH VISUAL FEEDBACK
// ============================================

/**
 * Memperbarui nilai dengan efek visual
 * @param {string} elementId - ID elemen yang akan diperbarui
 * @param {string|number} newValue - Nilai baru
 * @param {string} suffix - Suffix opsional (misal: 'W', 'kWh')
 * @param {boolean} animate - Apakah menampilkan animasi
 */
function updateValueWithVisualFeedback(elementId, newValue, suffix = '', animate = true) {
    const element = document.getElementById(elementId);
    if (!element) return;

    const oldValue = element.textContent.trim();
    const newText = typeof newValue === 'number' ? 
        newValue.toLocaleString('id-ID') + (suffix ? ' ' + suffix : '') :
        newValue + (suffix ? ' ' + suffix : '');

    if (oldValue !== newText) {
        if (animate) {
            // Tambah kelas animasi
            element.classList.add('updated');
            
            // Update nilai
            element.textContent = newText;
            
            // Hapus kelas animasi setelah selesai
            setTimeout(() => {
                element.classList.remove('updated');
            }, 600);
        } else {
            element.textContent = newText;
        }
    }
}

/**
 * Memperbarui stat card dengan efek visual
 * @param {string} cardSelector - Selector untuk stat card
 * @param {object} data - Data baru
 */
function updateStatCard(cardSelector, data) {
    const card = document.querySelector(cardSelector);
    if (!card) return;

    // Tambah efek update
    card.classList.add('updated');
    
    // Jika ada elemen stat-value di dalam card
    const statValue = card.querySelector('.stat-value');
    if (statValue && data.value) {
        updateValueWithVisualFeedback(statValue.id || null, data.value, data.suffix);
    }
    
    // Hapus efek setelah selesai
    setTimeout(() => {
        card.classList.remove('updated');
    }, 800);
}

/**
 * Highlight elemen untuk menarik perhatian
 * @param {string} elementId - ID elemen yang akan di-highlight
 * @param {number} duration - Durasi highlight dalam ms
 */
function highlightElement(elementId, duration = 2000) {
    const element = document.getElementById(elementId);
    if (!element) return;

    element.classList.add('highlight');
    
    setTimeout(() => {
        element.classList.remove('highlight');
    }, duration);
}

/**
 * Memperbarui progress bar dengan efek smooth
 * @param {string} progressBarId - ID progress bar
 * @param {number} newPercent - Persentase baru (0-100)
 */
function updateProgressBar(progressBarId, newPercent) {
    const progressBar = document.getElementById(progressBarId);
    if (!progressBar) return;

    const currentPercent = parseInt(progressBar.style.width) || 0;
    
    // Animasi smooth
    let current = currentPercent;
    const target = Math.min(newPercent, 100);
    const step = (target - current) / 20;
    
    const animate = () => {
        current += step;
        if ((step > 0 && current >= target) || (step < 0 && current <= target)) {
            current = target;
        }
        
        progressBar.style.width = current + '%';
        progressBar.textContent = Math.round(current) + '%';
        
        if (current !== target) {
            requestAnimationFrame(animate);
        }
    };
    
    animate();
}

/**
 * Memperbaiki kontras teks berdasarkan background
 * @param {string} elementId - ID elemen yang akan diperbaiki kontrasnya
 */
function improveTextContrast(elementId) {
    const element = document.getElementById(elementId);
    if (!element) return;

    // Dapatkan warna background
    const bgColor = window.getComputedStyle(element).backgroundColor;
    
    // Parsing warna RGB
    const rgb = bgColor.match(/\d+/g);
    if (!rgb || rgb.length < 3) return;
    
    const r = parseInt(rgb[0]), g = parseInt(rgb[1]), b = parseInt(rgb[2]);
    
    // Hitung luminance
    const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
    
    // Atur warna teks berdasarkan luminance background
    if (luminance > 0.5) {
        element.style.color = '#333'; // Gelap untuk background terang
    } else {
        element.style.color = '#fff'; // Putih untuk background gelap
    }
}

// Auto-improve contrast untuk semua stat cards saat halaman dimuat
document.addEventListener('DOMContentLoaded', function() {
    // Tunggu sebentar untuk memastikan semua elemen sudah dirender
    setTimeout(() => {
        document.querySelectorAll('.stat-card .stat-value, .stat-card .stat-label').forEach(el => {
            if (!el.id) el.id = 'temp_' + Math.random().toString(36).substr(2, 9);
            improveTextContrast(el.id);
        });
    }, 500);
});

// ============================================
// DYNAMIC FONT SIZING — ResizeObserver
// Makes stat-card font sizes proportional to card dimensions
// ============================================
(function() {
    var _statCardRO = null;

    function observeStatCards() {
        if (_statCardRO) _statCardRO.disconnect();
        _statCardRO = new ResizeObserver(function(entries) {
            entries.forEach(function(entry) {
                var card = entry.target;
                var w = entry.contentRect.width;
                var h = entry.contentRect.height;
                // Reference: card 220px wide × 85px tall
                var refW = 220, refH = 85;
                // Width drives the scale (grid layout is width-based)
                var scaleW = Math.max(w / refW, 0.5);
                // Height acts as a safety cap so text doesn't overflow vertically
                var scaleH = Math.max(h / refH, 0.5);
                // Prefer width scale, but clamp if height is too small
                var scale = scaleW;
                if (scaleH < scaleW) {
                    scale = scaleW * 0.55 + scaleH * 0.45;
                }
                // Clamp between 0.55 and 1.4
                scale = Math.max(0.55, Math.min(1.4, scale));
                card.style.setProperty('--sc-label-size', (0.75 * scale).toFixed(3) + 'rem');
                card.style.setProperty('--sc-value-size', (1.4 * scale).toFixed(3) + 'rem');
                card.style.setProperty('--sc-small-size', (0.7 * scale).toFixed(3) + 'rem');
                card.style.setProperty('--sc-icon-size', (2 * scale).toFixed(3) + 'rem');
            });
        });
        document.querySelectorAll('.stats-row .stat-card').forEach(function(card) {
            _statCardRO.observe(card);
        });
    }

    document.addEventListener('DOMContentLoaded', function() {
        observeStatCards();
    });
})();

// ============================================
// AUTHENTICATION PAGE HELPERS
// ============================================

// Toggle password visibility on auth pages
(function() {
    function initPasswordToggles() {
        document.querySelectorAll('[data-toggle-password]').forEach(function(btn) {
            btn.addEventListener('click', function() {
                var target = document.querySelector(btn.getAttribute('data-toggle-password'));
                if (!target) return;
                var icon = btn.querySelector('i');
                if (target.type === 'password') {
                    target.type = 'text';
                    if (icon) {
                        icon.classList.remove('fa-eye');
                        icon.classList.add('fa-eye-slash');
                    }
                } else {
                    target.type = 'password';
                    if (icon) {
                        icon.classList.remove('fa-eye-slash');
                        icon.classList.add('fa-eye');
                    }
                }
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initPasswordToggles);
    } else {
        initPasswordToggles();
    }
})();

// ============================================
// INTERACTIVE AUTHENTICATION BACKGROUND
// Simple 3-phase electrical cable animation
// ============================================
(function() {
    var canvas = document.getElementById('authBackgroundCanvas');
    if (!canvas) return;

    var ctx = canvas.getContext('2d');
    var width, height;
    var cables = [];
    var sparks = [];
    var pulses = [];
    var nextPulse = 0;
    var nextSpark = 0;

    // Standard 3-phase cable colors (L1, L2, L3)
    var phases = [
        { name: 'L1', color: '#f59e0b', glow: 'rgba(245, 158, 11, 0.5)', phaseOffset: 0 },         // Yellow
        { name: 'L2', color: '#22c55e', glow: 'rgba(34, 197, 94, 0.5)', phaseOffset: 2.09 },        // Green
        { name: 'L3', color: '#ef4444', glow: 'rgba(239, 68, 68, 0.5)', phaseOffset: 4.18 }         // Red
    ];

    function resize() {
        width = window.innerWidth;
        height = window.innerHeight;
        canvas.width = width;
        canvas.height = height;
        cables = [];
        var centerY = height / 2;
        var spacing = Math.min(height * 0.15, 100);
        for (var i = 0; i < phases.length; i++) {
            cables.push({
                phase: phases[i],
                y: centerY + (i - 1) * spacing,
                amp: 12 + i * 4
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

        // Technical grid
        ctx.strokeStyle = 'rgba(100, 150, 255, 0.05)';
        ctx.lineWidth = 1;
        var gs = 60;
        for (var x = 0; x <= width; x += gs) {
            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, height);
            ctx.stroke();
        }
        for (var y = 0; y <= height; y += gs) {
            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(width, y);
            ctx.stroke();
        }

        // Vignette
        var radial = ctx.createRadialGradient(width / 2, height / 2, height * 0.15, width / 2, height / 2, height * 0.85);
        radial.addColorStop(0, 'rgba(0, 0, 0, 0)');
        radial.addColorStop(1, 'rgba(0, 0, 0, 0.55)');
        ctx.fillStyle = radial;
        ctx.fillRect(0, 0, width, height);
    }

    function cableY(cable, time) {
        return cable.y + Math.sin(time * 0.0015 + cable.phase.phaseOffset) * cable.amp;
    }

    function drawCables(time) {
        for (var i = 0; i < cables.length; i++) {
            var cable = cables[i];
            var phase = cable.phase;
            var y = cableY(cable, time);

            ctx.save();
            ctx.lineCap = 'round';
            ctx.lineJoin = 'round';

            // Outer glow
            ctx.beginPath();
            ctx.strokeStyle = phase.glow;
            ctx.lineWidth = 16;
            ctx.shadowBlur = 28;
            ctx.shadowColor = phase.color;
            ctx.moveTo(0, y);
            ctx.lineTo(width, y);
            ctx.stroke();

            // Cable sheath
            ctx.beginPath();
            ctx.strokeStyle = 'rgba(25, 25, 35, 0.9)';
            ctx.lineWidth = 10;
            ctx.shadowBlur = 0;
            ctx.moveTo(0, y);
            ctx.lineTo(width, y);
            ctx.stroke();

            // Inner conductor
            ctx.beginPath();
            ctx.strokeStyle = phase.color;
            ctx.lineWidth = 4;
            ctx.shadowBlur = 20;
            ctx.shadowColor = phase.color;
            ctx.moveTo(0, y);
            ctx.lineTo(width, y);
            ctx.stroke();

            ctx.restore();
        }
    }

    function createSpark(x, y, color) {
        for (var i = 0; i < 10; i++) {
            var angle = Math.random() * Math.PI * 2;
            var speed = Math.random() * 4 + 1;
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
            ctx.shadowBlur = 10;
            ctx.shadowColor = s.color;
            ctx.beginPath();
            ctx.arc(s.x, s.y, 2.5, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();

            s.x += s.vx;
            s.y += s.vy;
            s.life -= 0.025;
            if (s.life <= 0) sparks.splice(i, 1);
        }
    }

    function drawPulses(time) {
        for (var i = pulses.length - 1; i >= 0; i--) {
            var p = pulses[i];
            var cable = cables[p.cableIndex];
            var y = cableY(cable, time);

            ctx.save();
            ctx.globalCompositeOperation = 'screen';
            ctx.fillStyle = cable.phase.color;
            ctx.shadowBlur = 22;
            ctx.shadowColor = cable.phase.color;
            ctx.beginPath();
            ctx.arc(p.x, y, 7, 0, Math.PI * 2);
            ctx.fill();

            ctx.fillStyle = 'rgba(255, 255, 255, 0.9)';
            ctx.beginPath();
            ctx.arc(p.x, y, 3, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();

            p.x += p.speed;
            if (p.x > width + 20) pulses.splice(i, 1);
        }
    }

    function animate(timestamp) {
        try {
            drawBackground();
            drawCables(timestamp);
            drawSparks();
            drawPulses(timestamp);

            // Spawn traveling pulses
            if (timestamp > nextPulse) {
                var idx = Math.floor(Math.random() * cables.length);
                pulses.push({
                    cableIndex: idx,
                    x: -20,
                    speed: 4 + Math.random() * 3
                });
                nextPulse = timestamp + 300 + Math.random() * 700;
            }

            // Spawn random sparks
            if (timestamp > nextSpark) {
                var idx = Math.floor(Math.random() * cables.length);
                var cable = cables[idx];
                var y = cableY(cable, timestamp);
                createSpark(Math.random() * width, y, cable.phase.color);
                nextSpark = timestamp + 150 + Math.random() * 400;
            }
        } catch (err) {
            if (window.console) console.error('[AuthBg] error:', err);
        }

        requestAnimationFrame(animate);
    }

    function init() {
        if (window.console) console.log('[AuthBg] init');
        resize();
        animate(0);
        window.addEventListener('resize', resize);

        // Click outside auth card creates a spark burst
        document.addEventListener('click', function(e) {
            if (e.target.closest('.auth-card') || e.target.closest('.erp-topbar')) return;
            var idx = Math.floor(Math.random() * cables.length);
            createSpark(e.clientX, e.clientY, cables[idx].phase.color);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

// ============================================
// MOBILE RESPONSIVE HELPERS
// ============================================
(function() {
    function scrollActiveTabIntoView() {
        var tabs = document.querySelectorAll('.erp-tabs');
        tabs.forEach(function(tabContainer) {
            var active = tabContainer.querySelector('.erp-tab.active');
            if (active) {
                // Scroll the active tab into view on mobile
                var containerRect = tabContainer.getBoundingClientRect();
                var activeRect = active.getBoundingClientRect();
                if (activeRect.left < containerRect.left || activeRect.right > containerRect.right) {
                    active.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' });
                }
            }
        });
    }

    // Fix 100vh on mobile browsers (address bar issues)
    function setMobileVh() {
        var vh = window.innerHeight * 0.01;
        document.documentElement.style.setProperty('--vh', vh + 'px');
    }

    // Prevent double-tap zoom on interactive elements
    function preventDoubleTapZoom() {
        var elements = document.querySelectorAll('.btn, .erp-tab, .topbar-icon-btn, .panel-card-clickable, .nav-link');
        var lastTouchEnd = 0;
        elements.forEach(function(el) {
            el.addEventListener('touchend', function(e) {
                var now = Date.now();
                if (now - lastTouchEnd <= 300) {
                    e.preventDefault();
                }
                lastTouchEnd = now;
            }, { passive: false });
        });
    }

    function init() {
        scrollActiveTabIntoView();
        setMobileVh();
        preventDoubleTapZoom();
        window.addEventListener('resize', setMobileVh);
        window.addEventListener('orientationchange', function() {
            setTimeout(setMobileVh, 100);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

// ============================================
// THEME TOGGLE — Dark/Light Mode
// ============================================
(function() {
    var STORAGE_KEY = 'kwh_theme';

    function applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        try { localStorage.setItem(STORAGE_KEY, theme); } catch(e) {}
        if (window.__dashboardBg) {
            window.__dashboardBg.setMode(theme === 'dark' ? 'dark' : 'light');
        }
        // Notify charts to refresh their theme colors
        window.dispatchEvent(new CustomEvent('kwh-theme-changed', { detail: { theme: theme } }));
    }

    function getStoredTheme() {
        try { return localStorage.getItem(STORAGE_KEY); } catch(e) { return null; }
    }

    function init() {
        var stored = getStoredTheme();
        if (stored) {
            applyTheme(stored);
        }

        // Remove no-transition class and mark theme ready after page load
        var cleanup = function() {
            document.documentElement.classList.remove('no-transition');
            document.documentElement.setAttribute('data-theme-ready', '');
            // Clean up inline background color
            document.documentElement.style.backgroundColor = '';
        };
        window.addEventListener('load', cleanup);
        setTimeout(cleanup, 1500);

        var btn = document.getElementById('themeToggle');
        if (!btn) return;

        btn.addEventListener('click', function() {
            var current = document.documentElement.getAttribute('data-theme');
            var next = (current === 'dark') ? 'light' : 'dark';
            applyTheme(next);
        });
    }

    // Apply theme BEFORE DOMContentLoaded to avoid flash
    var stored = getStoredTheme();
    if (stored) {
        document.documentElement.setAttribute('data-theme', stored);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();

// ============================================
// CHART THEME HELPERS — Dynamic grid/tick colors
// Returns colors based on current data-theme
// ============================================
window.getChartGridColor = function() {
    return document.documentElement.getAttribute('data-theme') === 'dark'
        ? 'rgba(255, 255, 255, 0.08)'   // dark mode: subtle white grid
        : 'rgba(0, 0, 0, 0.08)';        // light mode: subtle black grid
};

window.getChartTickColor = function() {
    return document.documentElement.getAttribute('data-theme') === 'dark'
        ? '#8a9ab8'   // dark mode: muted blue-gray ticks
        : '#6c757d';  // light mode: bootstrap secondary
};

window.getChartGridBorderColor = function() {
    return document.documentElement.getAttribute('data-theme') === 'dark'
        ? 'rgba(255, 255, 255, 0.12)'
        : 'rgba(0, 0, 0, 0.12)';
};

window.isDarkMode = function() {
    return document.documentElement.getAttribute('data-theme') === 'dark';
};

// Patch Chart.js defaults on theme change
(function() {
    function patchChartDefaults() {
        if (typeof Chart === 'undefined') return;
        var dark = window.isDarkMode();
        var tick = dark ? '#8a9ab8' : '#666';
        var grid = dark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.08)';
        var border = dark ? 'rgba(255,255,255,0.12)' : 'rgba(0,0,0,0.12)';

        // Tick labels
        try { Chart.defaults.color = tick; } catch(e) {}

        // Grid lines (Chart.js v3)
        try {
            if (Chart.defaults.scale) Chart.defaults.scale.grid = Chart.defaults.scale.grid || {};
            if (Chart.defaults.scale.grid) Chart.defaults.scale.grid.color = grid;
        } catch(e) {}

        // Border / grid default
        try { Chart.defaults.borderColor = border; } catch(e) {}

        // Tooltip
        try {
            if (Chart.defaults.plugins && Chart.defaults.plugins.tooltip) {
                Chart.defaults.plugins.tooltip.backgroundColor = dark ? 'rgba(0,0,0,0.8)' : 'rgba(255,255,255,0.95)';
                Chart.defaults.plugins.tooltip.titleColor = dark ? '#fff' : '#1a1d23';
                Chart.defaults.plugins.tooltip.bodyColor = dark ? '#d0d8e8' : '#333';
                Chart.defaults.plugins.tooltip.borderColor = dark ? '#444' : '#ccc';
            }
        } catch(e) {}

        // Refresh all existing charts
        try {
            if (Chart.helpers && Chart.helpers.each && Chart.instances) {
                Chart.helpers.each(Chart.instances, function(chart) {
                    if (chart && chart.update) chart.update('none');
                });
            }
        } catch(e) {}
    }

    // Listen for theme changes
    window.addEventListener('kwh-theme-changed', function() {
        // Small delay to ensure DOM has updated
        setTimeout(patchChartDefaults, 100);
    });

    // Patch defaults on load (if Chart.js is already loaded)
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', patchChartDefaults);
    } else {
        patchChartDefaults();
    }
})();

// ============================================
// DASHBOARD BACKGROUND ANIMATION
// Subtle particle grid for dark mode,
// soft gradient waves for light mode
// ============================================
(function() {
    var canvas = document.getElementById('dashboardBgCanvas');
    if (!canvas) return;

    var ctx = canvas.getContext('2d');
    var width, height;
    var mode = (document.documentElement.getAttribute('data-theme') === 'dark') ? 'dark' : 'light';
    var particles = [];
    var waves = [];

    var PARTICLE_COUNT = 40;
    var WAVE_COUNT = 4;

    function resize() {
        width = window.innerWidth;
        height = window.innerHeight;
        canvas.width = width;
        canvas.height = height;
    }

    function initParticles() {
        particles = [];
        for (var i = 0; i < PARTICLE_COUNT; i++) {
            particles.push({
                x: Math.random() * width,
                y: Math.random() * height,
                vx: (Math.random() - 0.5) * 0.3,
                vy: (Math.random() - 0.5) * 0.3,
                r: Math.random() * 1.5 + 0.5,
                opacity: Math.random() * 0.3 + 0.1
            });
        }
    }

    function initWaves() {
        waves = [];
        for (var i = 0; i < WAVE_COUNT; i++) {
            waves.push({
                y: height * (0.25 + i * 0.2),
                amplitude: 20 + i * 8,
                frequency: 0.003 + i * 0.001,
                speed: 0.008 + i * 0.003,
                phase: Math.random() * Math.PI * 2,
                opacity: 0.03 - i * 0.005
            });
        }
    }

    function drawDark(time) {
        var gradient = ctx.createLinearGradient(0, 0, 0, height);
        gradient.addColorStop(0, '#0b0f1a');
        gradient.addColorStop(0.5, '#0f1424');
        gradient.addColorStop(1, '#0b0f1a');
        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, width, height);

        ctx.strokeStyle = 'rgba(92, 160, 255, 0.025)';
        ctx.lineWidth = 1;
        var gs = 60;
        for (var x = 0; x <= width; x += gs) {
            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, height);
            ctx.stroke();
        }
        for (var y = 0; y <= height; y += gs) {
            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(width, y);
            ctx.stroke();
        }

        for (var i = 0; i < particles.length; i++) {
            var p = particles[i];
            p.x += p.vx;
            p.y += p.vy;
            if (p.x < 0) p.x = width;
            if (p.x > width) p.x = 0;
            if (p.y < 0) p.y = height;
            if (p.y > height) p.y = 0;

            ctx.beginPath();
            ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
            ctx.fillStyle = 'rgba(92, 160, 255,' + p.opacity + ')';
            ctx.fill();
        }

        ctx.lineWidth = 0.5;
        for (var i = 0; i < particles.length; i++) {
            for (var j = i + 1; j < particles.length; j++) {
                var dx = particles[i].x - particles[j].x;
                var dy = particles[i].y - particles[j].y;
                var dist = Math.sqrt(dx * dx + dy * dy);
                if (dist < 150) {
                    var opacity = 0.04 * (1 - dist / 150);
                    ctx.strokeStyle = 'rgba(92, 160, 255,' + opacity + ')';
                    ctx.beginPath();
                    ctx.moveTo(particles[i].x, particles[i].y);
                    ctx.lineTo(particles[j].x, particles[j].y);
                    ctx.stroke();
                }
            }
        }

        var radial = ctx.createRadialGradient(width / 2, height / 2, height * 0.3, width / 2, height / 2, height * 0.9);
        radial.addColorStop(0, 'rgba(0, 0, 0, 0)');
        radial.addColorStop(1, 'rgba(0, 0, 0, 0.3)');
        ctx.fillStyle = radial;
        ctx.fillRect(0, 0, width, height);
    }

    function drawLight(time) {
        var gradient = ctx.createLinearGradient(0, 0, 0, height);
        gradient.addColorStop(0, '#eef2f8');
        gradient.addColorStop(0.5, '#f0f2f5');
        gradient.addColorStop(1, '#e8ecf2');
        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, width, height);

        for (var w = 0; w < waves.length; w++) {
            var wave = waves[w];
            ctx.beginPath();
            ctx.moveTo(0, wave.y);
            for (var x = 0; x <= width; x += 4) {
                var y = wave.y + Math.sin(x * wave.frequency + wave.phase + time * wave.speed) * wave.amplitude;
                ctx.lineTo(x, y);
            }
            ctx.lineTo(width, height);
            ctx.lineTo(0, height);
            ctx.closePath();
            ctx.fillStyle = 'rgba(13, 110, 253,' + wave.opacity + ')';
            ctx.fill();
        }

        ctx.fillStyle = 'rgba(13, 110, 253, 0.03)';
        var gs = 40;
        for (var gx = gs / 2; gx < width; gx += gs) {
            for (var gy = gs / 2; gy < height; gy += gs) {
                ctx.beginPath();
                ctx.arc(gx, gy, 1, 0, Math.PI * 2);
                ctx.fill();
            }
        }
    }

    function animate(timestamp) {
        try {
            if (mode === 'dark') {
                drawDark(timestamp);
            } else {
                drawLight(timestamp);
            }
        } catch (err) {
            if (window.console) console.error('[DashBg] error:', err);
        }
        requestAnimationFrame(animate);
    }

    window.__dashboardBg = {
        setMode: function(newMode) {
            mode = newMode;
            if (mode === 'dark') {
                initParticles();
            } else {
                initWaves();
            }
        }
    };

    function init() {
        resize();
        if (mode === 'dark') {
            initParticles();
        } else {
            initWaves();
        }
        animate(0);
        window.addEventListener('resize', function() {
            resize();
            if (mode === 'dark') initParticles();
            else initWaves();
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})(); 
