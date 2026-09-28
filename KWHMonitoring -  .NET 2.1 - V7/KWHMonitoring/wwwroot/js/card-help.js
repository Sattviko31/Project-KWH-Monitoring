/* ============================================================
   Card Help — menampilkan keterangan kartu saat kursor hover.
   Membaca atribut: data-card-help="Judul|Fungsi|Rumus|Detail"

   Zona aktif keterangan:
   1. Kartu TANPA .card-header (mis. kartu statistik) -> aktif di
      seluruh area kartu.
   2. Kartu DENGAN .card-header (grafik, tabel, chart) -> aktif HANYA
      saat kursor berada di header; begitu kursor masuk ke isi/konten
      kartu, keterangan langsung hilang.
   3. data-card-help-zone="card" -> paksa seluruh area kartu walau
      kartu tersebut punya header.
   4. data-card-help-pos="below" -> overlay tampil DI BAWAH kartu
      (kartu kontrol/toggle, agar label tidak tertutup).

   Overlay: position:fixed + pointer-events:none -> tidak mengubah
   layout dan tidak menangkap klik/interaksi apa pun. Untuk kartu
   berheader overlay dimulai dari bawah header, sehingga judul dan
   kontrol di header (dropdown, tombol) tetap terlihat.

   Info lokasi (khusus kartu panel monitoring / _PanelCard):
   data-card-help-lokasi-kode   -> Kode Lokasi  (ERP: TitikLokasi.KodeLokasi)
   data-card-help-lokasi-alamat -> Alamat       (ERP: TitikLokasi.Address)
   data-card-help-lokasi-kota   -> Kota         (ERP: TitikLokasi.City + Province)
   Ketiga atribut diisi server dari database ERP lewat pencocokan
   DeviceKey = TitikLokasiID. Kartu yang tidak memiliki atribut ini
   (semua kartu non-panel) tampil persis seperti sebelumnya.
   ============================================================ */
(function () {
    'use strict';

    var tip = null;
    var activeCard = null;

    function escapeHtml(s) {
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function withBreaks(s) {
        return escapeHtml(s).replace(/\r?\n/g, '<br>');
    }

    function ensureTip() {
        if (tip) return tip;
        tip = document.createElement('div');
        tip.className = 'ch-help';
        tip.setAttribute('aria-hidden', 'true');
        document.body.appendChild(tip);
        return tip;
    }

    // Info lokasi dari database ERP (WWMERP2019.dbo.TitikLokasi).
    // Hanya kartu panel monitoring (_PanelCard) yang memiliki atribut ini,
    // sehingga kartu lain tidak terpengaruh sedikit pun.
    function renderLokasi(card) {
        var kode = card.getAttribute('data-card-help-lokasi-kode');
        var alamat = card.getAttribute('data-card-help-lokasi-alamat');
        var kota = card.getAttribute('data-card-help-lokasi-kota');

        if (!kode && !alamat && !kota) return '';

        var html = '<div class="ch-lokasi">';
        if (kode) {
            html += '<div class="ch-lokasi-kode"><i class="fas fa-map-marker-alt"></i>' +
                    escapeHtml(kode) + '</div>';
        }
        if (alamat) {
            html += '<div class="ch-lokasi-row"><span class="ch-lokasi-label">Alamat</span>' +
                    '<span class="ch-lokasi-value">' + escapeHtml(alamat) + '</span></div>';
        }
        if (kota) {
            html += '<div class="ch-lokasi-row"><span class="ch-lokasi-label">Kota</span>' +
                    '<span class="ch-lokasi-value">' + escapeHtml(kota) + '</span></div>';
        }

        return html + '</div>';
    }

    function renderHtml(raw, card) {
        var parts = String(raw).split('|');
        var html = '';
        if (parts[0]) html += '<div class="ch-title">' + withBreaks(parts[0]) + '</div>';
        html += renderLokasi(card);
        if (parts[1]) html += '<div class="ch-desc">' + withBreaks(parts[1]) + '</div>';
        if (parts[2]) html += '<div class="ch-formula">' + withBreaks(parts[2]) + '</div>';
        if (parts[3]) html += '<div class="ch-extra">' + withBreaks(parts[3]) + '</div>';
        return html;
    }

    // Header milik kartu ini sendiri — header kartu bersarang diabaikan
    function ownHeader(card) {
        var header = card.querySelector('.card-header');
        if (!header) return null;
        var owner = header.parentNode;
        while (owner && owner !== card) {
            if (owner.classList && owner.classList.contains('card')) return null;
            owner = owner.parentNode;
        }
        return owner === card ? header : null;
    }

    // Kartu berheader (grafik/tabel/chart) => keterangan hanya aktif di header.
    // Pakai data-card-help-zone="card" bila ingin aktif di seluruh area kartu.
    function helpHeader(card) {
        if (card.getAttribute('data-card-help-zone') === 'card') return null;
        return ownHeader(card);
    }

    function inHeader(header, target) {
        return header === target || header.contains(target);
    }

    function show(card) {
        if (activeCard === card) return;
        var raw = card.getAttribute('data-card-help');
        if (!raw) return;
        var el = ensureTip();
        el.innerHTML = renderHtml(raw, card);
        var r = card.getBoundingClientRect();
        el.style.left = Math.round(r.left) + 'px';
        el.style.width = Math.round(r.width) + 'px';

        // Kartu kontrol (mis. toggle Settings) memakai data-card-help-pos="below"
        // agar label/kontrol tetap terlihat saat keterangan tampil.
        var below = (card.getAttribute('data-card-help-pos') === 'below');
        var header = below ? null : helpHeader(card);
        if (below) {
            el.style.minHeight = '';
            var h = el.offsetHeight || 90;
            var topPos = r.bottom + 6;
            if (topPos + h > window.innerHeight - 8) {
                topPos = Math.max(8, r.top - h - 6);
            }
            el.style.top = Math.round(topPos) + 'px';
        } else if (header) {
            // Kartu berheader: judul tetap terlihat, overlay menutupi
            // hanya area isi kartu (mulai dari bawah header).
            var hr = header.getBoundingClientRect();
            var bodyTop = hr.bottom;
            var bodyHeight = r.bottom - bodyTop;
            el.style.top = Math.round(bodyTop) + 'px';
            el.style.minHeight = Math.round(bodyHeight > 0 ? bodyHeight : 0) + 'px';
        } else {
            el.style.top = Math.round(r.top) + 'px';
            el.style.minHeight = Math.round(r.height) + 'px';
        }
        el.classList.add('ch-show');
        activeCard = card;
    }

    function hide() {
        if (!tip) { activeCard = null; return; }
        tip.classList.remove('ch-show');
        activeCard = null;
    }

    // Delegasi event global: satu listener untuk semua kartu (termasuk yang dirender dinamis)
    document.addEventListener('mouseover', function (e) {
        var target = e.target;
        if (!target || !target.closest) return;
        var card = target.closest('[data-card-help]');
        if (!card) { hide(); return; }

        // Kartu berheader (grafik/tabel/chart): kursor harus berada di header.
        // Begitu kursor masuk ke isi/konten kartu, keterangan langsung hilang.
        var header = helpHeader(card);
        if (header && !inHeader(header, target)) { hide(); return; }

        show(card);
    });

    document.addEventListener('mouseout', function (e) {
        if (!e.target || !e.target.closest) return;
        var card = e.target.closest('[data-card-help]');
        if (!card) return;
        var to = e.relatedTarget;
        if (!to || !card.contains(to)) hide();
    });

    // Sembunyikan saat posisi berubah agar overlay tidak "melayang" jauh dari kartu
    window.addEventListener('scroll', hide, true);
    window.addEventListener('resize', hide);
})();
