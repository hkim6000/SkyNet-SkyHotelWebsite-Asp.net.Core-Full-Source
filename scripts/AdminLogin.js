var AdminLoginJs = (function () {

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
        $ApiRequest('AdminLogin/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('lg-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('lg-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('lg-open');
        }, 3200);
    }

    function openNav() {
        el('lg-nav').classList.add('lg-open');
        el('lg-scrim').classList.add('lg-open');
    }

    function closeNav() {
        el('lg-nav').classList.remove('lg-open');
        el('lg-scrim').classList.remove('lg-open');
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

    function signIn() {
        call('SignIn', [
            { key: 'user', vlu: val('lg-user') },
            { key: 'pass', vlu: el('lg-pass').value },
            { key: 'sbx', vlu: getCookie('sh_sbx') }
        ]);
        return false;
    }

    function as(login) {
        el('lg-user').value = login;
        el('lg-pass').value = 'demo';
        signIn();
    }

    function signedIn(tok, sbx, secs) {
        setCookie('sh_tok', tok, secs);
        if (sbx) {
            setCookie('sh_sbx', sbx, secs);
        }
        location.href = 'Admin';
    }

    function reveal() {
        document.addEventListener('input', function () {
            el('lg-e-pass').textContent = '';
        });
    }

    return {
        reveal: reveal,
        signIn: signIn,
        as: as,
        signedIn: signedIn
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminLoginJs.reveal);
} else {
    AdminLoginJs.reveal();
}
