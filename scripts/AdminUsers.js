var AdminUsersJs = (function () {

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
        $ApiRequest('AdminUsers/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('us-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('us-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('us-open');
        }, 3200);
    }

    function openNav() {
        el('us-nav').classList.add('us-open');
        el('us-scrim').classList.add('us-open');
    }

    function closeNav() {
        el('us-nav').classList.remove('us-open');
        el('us-scrim').classList.remove('us-open');
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
        el('us-modal').classList.add('us-open');
    }

    function closeModal() {
        var m = el('us-modal');
        if (m) {
            m.classList.remove('us-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('us-per')) {
            el('us-per').value = k;
        }
        if (k !== 'range') {
            if (el('us-from')) {
                el('us-from').value = '';
            }
            if (el('us-to')) {
                el('us-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('us-per') }, { key: 'pfrom', vlu: val('us-from') }, { key: 'pto', vlu: val('us-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('us-main'));
        if (window.AdminUsersJsloaded) {
            window.AdminUsersJsloaded();
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
        if (!askBox || !askBox.classList.contains('us-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('us-open');
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
        askBox.className = 'us-ask';
        askBox.innerHTML = '<div class="us-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="us-err" role="alert"></em><div class="us-askacts"><button type="button" class="us-btn us-btn-ghost us-btn-sm"></button><button type="button" class="us-btn us-btn-sm"></button></div></div>';
        (el('us-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('us-open')) {
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
        x.b[1].className = 'us-btn us-btn-sm' + (o.danger ? ' us-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('us-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('us-open');
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

    window.AdminUsersJsloaded = function () {
        list(1);
    };

    function loadArgs() {
        return [];
    }

    function list(pg) {
        api('List', [{ key: 'term', vlu: val('us-q') }, { key: 'urole', vlu: val('us-urole') }, { key: 'pg', vlu: String(pg || 1) }]);
    }

    function edit(id) {
        api('Form', [{ key: 'uid', vlu: id || '' }]);
    }

    function adminToggle() {
        el('us-matrix').classList.toggle('us-all', el('us-uadmin').checked);
    }

    function save(id) {
        var perms = [];
        var rows = document.querySelectorAll('#us-matrix [data-area]');
        for (var i = 0; i < rows.length; i++) {
            var a = rows[i].getAttribute('data-area');
            var lv = rows[i].querySelector('input[type="radio"]:checked');
            var x = rows[i].querySelector('input[type="checkbox"]');
            perms.push(a + ':' + (lv ? lv.value : '') + (x && x.checked ? 'X' : ''));
        }
        api('Save', [
            { key: 'uid', vlu: id || '' },
            { key: 'uname', vlu: val('us-uname') },
            { key: 'ulogin', vlu: val('us-ulogin') },
            { key: 'utitle', vlu: val('us-utitle') },
            { key: 'upass', vlu: el('us-upass').value },
            { key: 'uadmin', vlu: el('us-uadmin').checked ? '1' : '0' },
            { key: 'uactive', vlu: el('us-uactive').checked ? '1' : '0' },
            { key: 'uperms', vlu: perms.join('|') }
        ]);
    }

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
            var id = e.target && e.target.id ? e.target.id.replace('us-', 'us-e-') : '';
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
        edit: edit,
        adminToggle: adminToggle,
        save: save
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminUsersJs.reveal);
} else {
    AdminUsersJs.reveal();
}
