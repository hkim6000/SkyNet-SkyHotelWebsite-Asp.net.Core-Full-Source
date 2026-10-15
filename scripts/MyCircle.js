var MyCircleJs = (function () {

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
        $ApiRequest('MyCircle/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('mc-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('mc-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('mc-open');
        }, 3200);
    }

    function openNav() {
        el('mc-nav').classList.add('mc-open');
        el('mc-scrim').classList.add('mc-open');
    }

    function closeNav() {
        el('mc-nav').classList.remove('mc-open');
        el('mc-scrim').classList.remove('mc-open');
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

    var ARGA = '';
    var ARGB = '';

    function arg(k) {
        return k ? (new URLSearchParams(location.search).get(k) || '') : '';
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function applyTheme(theme) {
        el('mc-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function toggleTheme() {
        var next = el('mc-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('sh-theme', next);
        } catch (e) {
        }
    }

    function demoBar() {
        el('mc-demo-bar').innerHTML = 'You are using your own demo copy of SkyHotel. It resets after an hour. <a href="Admin">Open the hotel admin</a>';
    }

    function hydrated(ok, accent, theme) {
        document.documentElement.classList.remove('mc-hydrating');
        if (!ok) {
            setCookie('sh_sbx', '', 0);
            if (window.MyCircleJsready) {
                window.MyCircleJsready();
            }
            return;
        }
        if (accent) {
            el('mc-site').style.setProperty('--mc-accent', accent);
        }
        var stored = '';
        try {
            stored = localStorage.getItem('sh-theme') || '';
        } catch (e) {
        }
        applyTheme(stored || theme);
        demoBar();
        times();
        if (window.MyCircleJsready) {
            window.MyCircleJsready();
        }
    }

    function sandboxed(id) {
        setCookie('sh_sbx', id, 3600);
        demoBar();
    }

    function signedIn(tok) {
        setCookie('sh_mem', tok, 8 * 3600);
    }

    function signedOut() {
        setCookie('sh_mem', '', 0);
    }

    function who(name, grade) {
        var m = el('mc-mem');
        if (!m) {
            return;
        }
        if (!name) {
            signedOut();
            return;
        }
        m.href = 'MyCircle';
        m.classList.add('mc-in');
        m.innerHTML = '<span class="mc-grade mc-g-' + grade + '">' + grade + '</span>' + name;
    }

    function mem() {
        return getCookie('sh_mem');
    }

    function sbx() {
        return getCookie('sh_sbx');
    }

    var askBox = null;
    var askDone = null;
    var askCheck = null;
    var askBack = null;

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('mc-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('mc-open');
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
        askBox.className = 'mc-ask';
        askBox.innerHTML = '<div class="mc-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="mc-err" role="alert"></em><div class="mc-askacts"><button type="button" class="mc-btn mc-btn-ghost mc-btn-sm"></button><button type="button" class="mc-btn mc-btn-sm"></button></div></div>';
        (el('mc-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('mc-open')) {
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
        x.b[1].className = 'mc-btn mc-btn-sm' + (o.danger ? ' mc-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('mc-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('mc-open');
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

    function load() {
        call('Load', [{ key: 'mtok', vlu: mem() }, { key: 'welcome', vlu: qs('welcome') }]);
    }

    function hist(pg) {
        call('Hist', [{ key: 'mtok', vlu: mem() }, { key: 'hkind', vlu: val('mc-hkind') }, { key: 'pg', vlu: String(pg || 1) }]);
    }

    function profile() {
        call('Profile', [{ key: 'mtok', vlu: mem() }, { key: 'pphone', vlu: val('mc-pphone') }, { key: 'pcountry', vlu: val('mc-pcountry') }, { key: 'ppass', vlu: el('mc-ppass').value }]);
        return false;
    }

    function signOut() {
        call('SignOut', [{ key: 'mtok', vlu: mem() }]);
    }

    function start() {
        load();
        window.MyCircleJsready = load;
    }

    function reveal() {
        var stored = '';
        try {
            stored = localStorage.getItem('sh-theme') || '';
        } catch (e) {
        }
        if (stored) {
            applyTheme(stored);
        }
        times();
        if (sbx()) {
            call('Hydrate', [{ key: 'sbx', vlu: sbx() }, { key: 'argA', vlu: arg(ARGA) }, { key: 'argB', vlu: arg(ARGB) }]);
        }
        if (mem()) {
            call('Who', [{ key: 'mtok', vlu: mem() }]);
        }
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('mc-', 'mc-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
        start();
    }

    if (document.cookie.indexOf('sh_sbx=') >= 0) {
        document.documentElement.classList.add('mc-hydrating');
        setTimeout(function () {
            document.documentElement.classList.remove('mc-hydrating');
        }, 2500);
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        toggleTheme: toggleTheme,
        hydrated: hydrated,
        sandboxed: sandboxed,
        signedIn: signedIn,
        signedOut: signedOut,
        who: who,
        hist: hist,
        profile: profile,
        signOut: signOut
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', MyCircleJs.reveal);
} else {
    MyCircleJs.reveal();
}
