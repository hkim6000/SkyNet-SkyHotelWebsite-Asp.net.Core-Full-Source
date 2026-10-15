var AdminMediaJs = (function () {

    var ttimer = null;
    var BASE = location.pathname.replace(/[^\/]*$/, '');

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('AdminMedia/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ml-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ml-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ml-open');
        }, 3200);
    }

    function openNav() {
        el('ml-nav').classList.add('ml-open');
        el('ml-scrim').classList.add('ml-open');
    }

    function closeNav() {
        el('ml-nav').classList.remove('ml-open');
        el('ml-scrim').classList.remove('ml-open');
    }

    function ago(d) {
        var s = (Date.now() - d.getTime()) / 1000;
        if (s < 60) {
            return 'Just now';
        }
        if (s < 3600) {
            var m = Math.floor(s / 60);
            return m + (m === 1 ? ' min ago' : ' mins ago');
        }
        if (s < 86400) {
            var h = Math.floor(s / 3600);
            return h + (h === 1 ? ' hour ago' : ' hours ago');
        }
        return d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
    }

    function times(root) {
        var list = (root || document).querySelectorAll('time[data-t]');
        for (var i = 0; i < list.length; i++) {
            var t = list[i];
            var d = new Date(t.getAttribute('datetime'));
            if (isNaN(d.getTime())) {
                continue;
            }
            var mode = t.getAttribute('data-t');
            if (mode === 'ago') {
                t.textContent = ago(d);
                t.title = d.toLocaleString();
            } else if (mode === 'hm') {
                t.textContent = d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
            } else if (mode === 'full') {
                t.textContent = d.toLocaleString(undefined, { weekday: 'short', day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' });
            } else if (mode === 'day') {
                t.textContent = d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
            }
        }
    }

    var stimer = null;

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('sh_tok') });
        call(method, list);
    }

    function expired() {
        setCookie('sh_tok', '', 0);
        location.replace('AdminLogin');
    }

    function signOut() {
        api('SignOut', []);
    }

    function out() {
        setCookie('sh_tok', '', 0);
        location.href = 'AdminLogin';
    }

    function openModal() {
        el('ml-modal').classList.add('ml-open');
    }

    function closeModal() {
        var m = el('ml-modal');
        if (m) {
            m.classList.remove('ml-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('ml-per')) {
            el('ml-per').value = k;
        }
        if (k !== 'range') {
            if (el('ml-from')) {
                el('ml-from').value = '';
            }
            if (el('ml-to')) {
                el('ml-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('ml-per') }, { key: 'pfrom', vlu: val('ml-from') }, { key: 'pto', vlu: val('ml-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('ml-main'));
        if (window.AdminMediaJsloaded) {
            window.AdminMediaJsloaded();
        }
    }

    var askBox = null;
    var askDone = null;
    var askCheck = null;
    var askBack = null;

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('ml-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('ml-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        if (askCheck) {
            var m = askCheck(x.i.value.trim());
            if (m) {
                x.e.textContent = m;
                x.i.focus();
                return;
            }
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'ml-ask';
        askBox.innerHTML = '<div class="ml-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="ml-err" role="alert"></em><div class="ml-askacts"><button type="button" class="ml-btn ml-btn-ghost ml-btn-sm"></button><button type="button" class="ml-btn ml-btn-sm"></button></div></div>';
        (el('ml-site') || document.body).appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () {
            askClose(false);
        });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () {
            x.e.textContent = '';
        });
        askBox.addEventListener('mousedown', function (e) {
            if (e.target === askBox) {
                askClose(false);
            }
        });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('ml-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) {
                    return n.offsetParent !== null;
                });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts();
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'ml-btn ml-btn-sm' + (o.danger ? ' ml-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('ml-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('ml-open');
        if (input) {
            x.i.focus();
            x.i.setSelectionRange(x.i.value.length, x.i.value.length);
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function leave(go) {
        ask({ title: 'Leave without saving?', text: 'Your changes since the last save will be lost.', ok: 'Leave', no: 'Keep editing', danger: true }, go);
    }

    var kind = '';
    var page = 1;
    var full = '';
    var thumb = '';
    var vfile = null;
    var vposter = '';
    var vsecs = 0;
    var vuid = '';
    var vtotal = 0;
    var CHUNK = 2 * 1024 * 1024;

    window.AdminMediaJsloaded = function () {
        list(1);
    };

    function loadArgs() {
        return [];
    }

    function list(pg) {
        page = pg || 1;
        api('List', [{ key: 'mkind', vlu: kind }, { key: 'term', vlu: val('ml-q') }, { key: 'mby', vlu: val('ml-mby') }, { key: 'pg', vlu: String(page) }].concat(period()));
    }

    function setKind(k) {
        kind = k;
        list(1);
    }

    function shrink(img, max, type, q) {
        var w = img.naturalWidth;
        var h = img.naturalHeight;
        var r = Math.min(1, max / Math.max(w, h));
        var c = document.createElement('canvas');
        c.width = Math.round(w * r);
        c.height = Math.round(h * r);
        c.getContext('2d').drawImage(img, 0, 0, c.width, c.height);
        var d = c.toDataURL(type, q);
        return d.indexOf('data:' + type) === 0 ? d : c.toDataURL('image/jpeg', q);
    }

    function esc(t) {
        return String(t).replace(/[&<>"]/g, function (ch) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[ch];
        });
    }

    function mb(n) {
        return (n / 1048576).toFixed(n < 10485760 ? 1 : 0) + ' MB';
    }

    function pad2(n) {
        return (n < 10 ? '0' : '') + n;
    }

    function clock(sec) {
        return Math.floor(sec / 60) + ':' + pad2(sec % 60);
    }

    function setBox(boxId, inputId, html) {
        var inp = el(inputId);
        var box = el(boxId);
        box.innerHTML = html;
        box.appendChild(inp);
    }

    function loadImg(src, ok, bad) {
        var img = new Image();
        img.onload = function () {
            ok(img);
        };
        img.onerror = bad;
        img.src = src;
    }

    function file(input) {
        var f = input.files && input.files[0];
        el('ml-e-file').textContent = '';
        full = '';
        thumb = '';
        if (!f) {
            return;
        }
        if (!/^image\/(jpeg|png|webp)$/.test(f.type)) {
            el('ml-e-file').textContent = 'Use a JPG, PNG or WebP image.';
            return;
        }
        if (f.size > 30 * 1024 * 1024) {
            el('ml-e-file').textContent = 'The image must be under 30 MB.';
            return;
        }
        var label = esc(f.name) + ' &middot; ' + mb(f.size);
        setBox('ml-prev', 'ml-file', '<span>Reading&hellip;</span><span class="ml-finfo">' + label + '</span>');
        if (!val('ml-mtitle')) {
            el('ml-mtitle').value = f.name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ').substring(0, 80);
        }
        var done = function (img) {
            full = shrink(img, 1600, 'image/webp', 0.86);
            thumb = shrink(img, 640, 'image/webp', 0.8);
            if (full.substring(0, 15) !== thumb.substring(0, 15)) {
                thumb = shrink(img, 640, 'image/jpeg', 0.8);
                full = shrink(img, 1600, 'image/jpeg', 0.86);
            }
            setBox('ml-prev', 'ml-file', '<img src="' + thumb + '" alt=""><span class="ml-finfo">' + label + ' &middot; ' + img.naturalWidth + '&times;' + img.naturalHeight + '</span>');
        };
        var fail = function () {
            setBox('ml-prev', 'ml-file', '<span>Choose a photo<br>JPG, PNG or WebP</span><span class="ml-finfo">' + label + '</span>');
            el('ml-e-file').textContent = 'That image could not be read.';
        };
        var viaReader = function () {
            var r = new FileReader();
            r.onload = function () {
                loadImg(String(r.result), done, fail);
            };
            r.onerror = fail;
            r.readAsDataURL(f);
        };
        var url = '';
        try {
            url = URL.createObjectURL(f);
        } catch (e) {
            url = '';
        }
        if (!url) {
            viaReader();
            return;
        }
        loadImg(url, function (img) {
            done(img);
            URL.revokeObjectURL(url);
        }, function () {
            URL.revokeObjectURL(url);
            viaReader();
        });
    }

    function upload() {
        if (!full) {
            el('ml-e-file').textContent = 'Choose a photo first.';
            return;
        }
        if (val('ml-mtitle').length < 2) {
            el('ml-e-mtitle').textContent = 'Give the photo a title.';
            return;
        }
        el('ml-pbtn').disabled = true;
        api('Upload', [{ key: 'full', vlu: full }, { key: 'thumb', vlu: thumb }, { key: 'mtitle', vlu: val('ml-mtitle') }]);
    }

    function upFail(msg) {
        el('ml-e-file').textContent = msg;
        el('ml-pbtn').disabled = false;
    }

    function resetPhoto() {
        full = '';
        thumb = '';
        el('ml-file').value = '';
        setBox('ml-prev', 'ml-file', '<span>Choose a photo<br>JPG, PNG or WebP</span>');
        el('ml-mtitle').value = '';
        el('ml-e-file').textContent = '';
        el('ml-e-mtitle').textContent = '';
        el('ml-pbtn').disabled = false;
    }

    function resetVideo() {
        vfile = null;
        vposter = '';
        vsecs = 0;
        el('ml-vfile').value = '';
        setBox('ml-vprev', 'ml-vfile', '<span>Choose a video<br>MP4 or WebM</span>');
        el('ml-vtitle').value = '';
        el('ml-e-vtitle').textContent = '';
        chunkFail('');
    }

    function uploaded() {
        resetPhoto();
        kind = '';
        list(1);
    }

    function capture(f, src, label, retry) {
        var v = document.createElement('video');
        var finished = false;
        var timer = null;
        var stop = function (ok) {
            if (finished) {
                return;
            }
            finished = true;
            clearTimeout(timer);
            if (src.indexOf('blob:') === 0) {
                URL.revokeObjectURL(src);
            }
            if (!ok && retry && f.size <= 25 * 1024 * 1024) {
                var r = new FileReader();
                r.onload = function () {
                    capture(f, String(r.result), label, false);
                };
                r.readAsDataURL(f);
                return;
            }
            if (!ok) {
                setBox('ml-vprev', 'ml-vfile', '<span>' + esc(f.name) + '<br>No preview in this browser</span><span class="ml-finfo">' + label + (vsecs ? ' &middot; ' + clock(vsecs) : '') + '</span>');
            }
        };
        v.muted = true;
        v.preload = 'auto';
        v.playsInline = true;
        v.addEventListener('loadedmetadata', function () {
            vsecs = isFinite(v.duration) ? Math.round(v.duration) : 0;
            v.currentTime = Math.min(1, (v.duration || 2) / 2);
        });
        v.addEventListener('seeked', function () {
            try {
                var r = Math.min(1, 1280 / v.videoWidth);
                var c = document.createElement('canvas');
                c.width = Math.round(v.videoWidth * r);
                c.height = Math.round(v.videoHeight * r);
                c.getContext('2d').drawImage(v, 0, 0, c.width, c.height);
                vposter = c.toDataURL('image/jpeg', 0.82);
                setBox('ml-vprev', 'ml-vfile', '<img src="' + vposter + '" alt=""><span class="ml-play"></span><span class="ml-finfo">' + label + (vsecs ? ' &middot; ' + clock(vsecs) : '') + '</span>');
                stop(true);
            } catch (e) {
                vposter = '';
                stop(false);
            }
        });
        v.addEventListener('error', function () {
            stop(false);
        });
        timer = setTimeout(function () {
            stop(false);
        }, 8000);
        v.src = src;
    }

    function vpick(input) {
        var f = input.files && input.files[0];
        var max = parseInt(input.getAttribute('data-max'), 10);
        el('ml-e-vfile').textContent = '';
        vfile = null;
        vposter = '';
        vsecs = 0;
        if (!f) {
            return;
        }
        if (!/^video\/(mp4|webm)$/.test(f.type) && !/\.(mp4|webm)$/i.test(f.name)) {
            el('ml-e-vfile').textContent = 'Use an MP4 or WebM video.';
            return;
        }
        if (f.size > max) {
            el('ml-e-vfile').textContent = 'The video must be under ' + Math.round(max / 1048576) + ' MB.';
            return;
        }
        vfile = f;
        if (!val('ml-vtitle')) {
            el('ml-vtitle').value = f.name.replace(/\.[^.]+$/, '').replace(/[-_]+/g, ' ').substring(0, 80);
        }
        var label = esc(f.name) + ' &middot; ' + mb(f.size);
        setBox('ml-vprev', 'ml-vfile', '<span>Reading&hellip;</span><span class="ml-finfo">' + label + '</span>');
        var url = '';
        try {
            url = URL.createObjectURL(f);
        } catch (e) {
            url = '';
        }
        if (url) {
            capture(f, url, label, true);
        } else if (f.size <= 25 * 1024 * 1024) {
            var r = new FileReader();
            r.onload = function () {
                capture(f, String(r.result), label, false);
            };
            r.readAsDataURL(f);
        }
    }

    function hex() {
        var a = new Uint8Array(16);
        (window.crypto || window.msCrypto).getRandomValues(a);
        var s = '';
        for (var i = 0; i < a.length; i++) {
            s += (a[i] < 16 ? '0' : '') + a[i].toString(16);
        }
        return s;
    }

    function vupload() {
        if (!vfile) {
            el('ml-e-vfile').textContent = 'Choose a video first.';
            return;
        }
        if (val('ml-vtitle').length < 2) {
            el('ml-e-vtitle').textContent = 'Give the video a title.';
            return;
        }
        vuid = hex();
        vtotal = Math.ceil(vfile.size / CHUNK);
        el('ml-vbtn').disabled = true;
        el('ml-vprog').classList.add('ml-on');
        send(0);
    }

    function send(i) {
        var r = new FileReader();
        r.onload = function () {
            var d = String(r.result);
            api('Chunk', [{ key: 'uid', vlu: vuid }, { key: 'idx', vlu: String(i) }, { key: 'total', vlu: String(vtotal) }, { key: 'part', vlu: d.substring(d.indexOf(',') + 1) }]);
        };
        r.onerror = function () {
            chunkFail('That file could not be read.');
        };
        r.readAsDataURL(vfile.slice(i * CHUNK, (i + 1) * CHUNK));
    }

    function chunkOk(i) {
        el('ml-vbar').style.width = Math.round((i + 1) / vtotal * 100) + '%';
        if (i + 1 < vtotal) {
            send(i + 1);
            return;
        }
        api('VideoDone', [{ key: 'uid', vlu: vuid }, { key: 'total', vlu: String(vtotal) }, { key: 'vtitle', vlu: val('ml-vtitle') }, { key: 'poster', vlu: vposter }, { key: 'secs', vlu: String(vsecs) }]);
    }

    function chunkFail(msg) {
        el('ml-e-vfile').textContent = msg;
        el('ml-vbtn').disabled = false;
        el('ml-vprog').classList.remove('ml-on');
        el('ml-vbar').style.width = '0';
    }

    function vdone() {
        resetVideo();
        kind = '';
        list(1);
    }

    function view(id) {
        api('Open', [{ key: 'mid', vlu: id }]);
    }

    function stopPreview() {
        var v = document.querySelector('#ml-mbody video');
        if (v) {
            v.pause();
        }
    }

    function rename(id, title) {
        ask({ title: 'Rename', text: 'The title helps staff find it in the pickers.', value: title, ok: 'Save', check: function (v) {
            return v.length < 2 ? 'Give it a title.' : '';
        } }, function (v) {
            api('Rename', [{ key: 'mid', vlu: id }, { key: 'mtitle', vlu: v }, { key: 'pg', vlu: String(page) }].concat(filters()));
        });
    }

    function filters() {
        return [{ key: 'mkind', vlu: kind }, { key: 'term', vlu: val('ml-q') }, { key: 'mby', vlu: val('ml-mby') }].concat(period());
    }

    function del(id, title) {
        ask({ title: 'Delete from the library?', text: '“' + title + '” will be removed. This cannot be undone.', ok: 'Delete', danger: true }, function () {
            stopPreview();
            closeModal();
            api('Remove', [{ key: 'mid', vlu: id }, { key: 'pg', vlu: String(page) }].concat(filters()));
        });
    }

    document.addEventListener('click', function (e) {
        if (e.target && (e.target.id === 'ml-modal' || (e.target.closest && e.target.closest('.ml-mclose')))) {
            stopPreview();
        }
    });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            stopPreview();
        }
    });

    function reveal() {
        if (!getCookie('sh_tok')) {
            location.replace('AdminLogin');
            return;
        }
        api('Load', loadArgs());
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
                closeModal();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('ml-', 'ml-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        signOut: signOut,
        out: out,
        expired: expired,
        openModal: openModal,
        closeModal: closeModal,
        per: per,
        search: search,
        loaded: loaded,
        list: list,
        setKind: setKind,
        file: file,
        upload: upload,
        upFail: upFail,
        uploaded: uploaded,
        vpick: vpick,
        vupload: vupload,
        chunkOk: chunkOk,
        chunkFail: chunkFail,
        vdone: vdone,
        view: view,
        stopPreview: stopPreview,
        rename: rename,
        del: del
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminMediaJs.reveal);
} else {
    AdminMediaJs.reveal();
}
