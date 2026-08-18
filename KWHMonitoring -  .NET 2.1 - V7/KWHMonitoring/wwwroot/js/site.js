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