/*
 * realtime-refresh.js
 * -------------------
 * Refresh halaman berbasis PERUBAHAN DATA (bukan interval tetap 5/10 detik).
 *
 * Cara kerja:
 *   1. Polling endpoint ringan /api/Api/data-version setiap 1 detik. Endpoint
 *      hanya mengembalikan "token perubahan" (waktu data KWH, relay, agregasi
 *      energi, dan log anomali) - BUKAN payload data.
 *   2. Halaman mendaftarkan watcher: RealtimeRefresh.watch(['kwh'], handler).
 *      handler hanya dijalankan ketika token yang di-watch berubah, sehingga
 *      panel/chart/tabel langsung menampilkan data terbaru begitu ada data baru.
 *   3. Jaring pengaman (safety): tiap 30 detik semua watcher dijalankan sekali
 *      lagi secara paksa, agar perubahan yang tidak tercakup token (edit manual
 *      di database, dst) tetap muncul - perilaku tidak pernah lebih buruk dari
 *      skema interval lama.
 *   4. Bila script ini gagal dimuat, halaman memakai fallback interval lama
 *      (lihat masing-masing startAutoRefresh() di tiap halaman).
 *
 * API:
 *   RealtimeRefresh.watch(keys, handler, options) -> id watcher (string)
 *   RealtimeRefresh.unwatch(id)
 *   RealtimeRefresh.setEnabled(id, enabled)
 *   RealtimeRefresh.checkNow()
 *   RealtimeRefresh.configure({ pollIntervalMs, safetyIntervalMs, throttleMs })
 *   RealtimeRefresh.getVersions()
 *
 * options.throttleMs : jeda minimum antar-execute handler yang sama (default
 *                      1000) - mencegah refresh bertubi-tubi saat data masuk
 *                      sangat cepat.
 */
(function (window, $) {
    'use strict';

    // Tanpa jQuery, script batal aktif dan halaman memakai interval lama.
    if (!window.jQuery) return;

    var config = {
        endpoint: '/api/Api/data-version',
        pollIntervalMs: 1000,     // seberapa sering cek perubahan data
        safetyIntervalMs: 30000,  // jaring pengaman: refresh paksa berkala
        throttleMs: 1000          // jeda minimum antar refresh per watcher
    };

    var watchers = {};       // id -> { keys, handler, throttleMs, lastRunAt, enabled, ... }
    var nextWatcherId = 1;
    var versions = null;     // token terakhir yang diterima dari server
    var started = false;
    var pollTimer = null;
    var safetyTimer = null;
    var visibilityBound = false;
    var requestInFlight = false;

    function nowMs() {
        return new Date().getTime();
    }

    function toArray(value) {
        if (Object.prototype.toString.call(value) === '[object Array]') return value;
        return [value];
    }

    function logError(e) {
        if (window.console && console.error) console.error('[RealtimeRefresh]', e);
    }

    function matches(watch, changedKeys) {
        if (!changedKeys || changedKeys.length === 0) return false;
        for (var i = 0; i < changedKeys.length; i++) {
            var changed = changedKeys[i];
            if (changed === '*') return true;
            for (var j = 0; j < watch.keys.length; j++) {
                if (watch.keys[j] === '*' || watch.keys[j] === changed) return true;
            }
        }
        return false;
    }

    function runWatch(watch, changedKeys) {
        if (!watch.enabled) return;
        try {
            watch.handler(changedKeys);
        } catch (e) {
            logError(e);
        }
    }

    function clearPending(watch) {
        if (watch.pendingTimer) {
            clearTimeout(watch.pendingTimer);
            watch.pendingTimer = null;
        }
        watch.pendingKeys = null;
    }

    function schedulePending(watch, changedKeys, delay) {
        // Sudah ada jadwal tunda, biarkan agar tidak menumpuk.
        if (watch.pendingTimer) return;
        watch.pendingKeys = changedKeys;
        watch.pendingTimer = setTimeout(function () {
            watch.pendingTimer = null;
            watch.pendingKeys = null;
            if (!watch.enabled) return;
            watch.lastRunAt = nowMs();
            runWatch(watch, changedKeys);
        }, delay);
    }

    function dispatch(changedKeys, force) {
        var stamp = nowMs();
        for (var id in watchers) {
            if (!watchers.hasOwnProperty(id)) continue;
            var watch = watchers[id];
            if (!watch.enabled) continue;
            if (!matches(watch, changedKeys)) continue;

            if (force) {
                clearPending(watch);
                watch.lastRunAt = stamp;
                runWatch(watch, changedKeys);
                continue;
            }

            var elapsed = stamp - watch.lastRunAt;
            if (elapsed >= watch.throttleMs) {
                clearPending(watch);
                watch.lastRunAt = stamp;
                runWatch(watch, changedKeys);
            } else {
                schedulePending(watch, changedKeys, watch.throttleMs - elapsed);
            }
        }
    }

    function applyVersions(next) {
        var previous = versions;
        versions = next;
        // Baseline pertama: data awal halaman sudah dirender server, jangan
        // langsung refresh agar tidak memicu double-load saat baru dibuka.
        if (!previous) return;

        var changed = [];
        for (var key in next) {
            if (!next.hasOwnProperty(key)) continue;
            if (String(previous[key]) !== String(next[key])) changed.push(key);
        }
        if (changed.length > 0) dispatch(changed, false);
    }

    function checkForChanges() {
        if (requestInFlight) return;
        // Tab tersembunyi: tidak perlu membebani server/browser.
        if (window.document && document.hidden) return;

        requestInFlight = true;
        $.ajax({
                url: config.endpoint,
                method: 'GET',
                dataType: 'json',
                cache: false,
                timeout: 5000
            })
            .done(function (response) {
                requestInFlight = false;
                if (!response || !response.success || !response.versions) return;
                applyVersions(response.versions);
            })
            .fail(function () {
                // Diamkan: safety timer tetap menjaga data tetap tampil.
                requestInFlight = false;
            });
    }

    function safetyTick() {
        if (window.document && document.hidden) return;
        dispatch(['*'], true);
    }

    function stopIfIdle() {
        if (Object.keys(watchers).length > 0) return;
        if (pollTimer) { clearInterval(pollTimer); pollTimer = null; }
        if (safetyTimer) { clearInterval(safetyTimer); safetyTimer = null; }
        started = false;
    }

    function ensureStarted() {
        if (started) return;
        started = true;
        pollTimer = setInterval(checkForChanges, config.pollIntervalMs);
        safetyTimer = setInterval(safetyTick, config.safetyIntervalMs);

        if (!visibilityBound && window.document && document.addEventListener) {
            visibilityBound = true;
            document.addEventListener('visibilitychange', function () {
                if (!document.hidden) checkForChanges();
            });
        }

        checkForChanges();
    }

    window.RealtimeRefresh = {
        configure: function (options) {
            if (!options) return;
            for (var key in options) {
                if (options.hasOwnProperty(key) && options[key] > 0) config[key] = options[key];
            }
        },

        watch: function (keys, handler, options) {
            if (typeof handler !== 'function') return null;
            var normalized = toArray(keys || []);
            if (normalized.length === 0) return null;

            var opts = options || {};
            var id = 'rw-' + (nextWatcherId++);
            watchers[id] = {
                keys: normalized,
                handler: handler,
                throttleMs: (typeof opts.throttleMs === 'number' && opts.throttleMs >= 0)
                    ? opts.throttleMs
                    : config.throttleMs,
                lastRunAt: 0,
                enabled: true,
                pendingTimer: null,
                pendingKeys: null
            };
            ensureStarted();
            return id;
        },

        unwatch: function (id) {
            var watch = watchers[id];
            if (!watch) return false;
            clearPending(watch);
            delete watchers[id];
            stopIfIdle();
            return true;
        },

        // Menonaktifkan sementara watcher tanpa membuangnya (mis. toggle auto-refresh).
        setEnabled: function (id, enabled) {
            var watch = watchers[id];
            if (!watch) return false;
            watch.enabled = !!enabled;
            if (!watch.enabled) clearPending(watch);
            return true;
        },

        checkNow: function () {
            checkForChanges();
        },

        getVersions: function () {
            return versions;
        }
    };
})(window, window.jQuery);
