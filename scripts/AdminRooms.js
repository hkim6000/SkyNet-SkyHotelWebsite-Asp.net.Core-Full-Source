var AdminRoomsJs = (function () {

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
        $ApiRequest('AdminRooms/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('rr-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('rr-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('rr-open');
        }, 3200);
    }

    function openNav() {
        el('rr-nav').classList.add('rr-open');
        el('rr-scrim').classList.add('rr-open');
    }

    function closeNav() {
        el('rr-nav').classList.remove('rr-open');
        el('rr-scrim').classList.remove('rr-open');
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
        el('rr-modal').classList.add('rr-open');
    }

    function closeModal() {
        var m = el('rr-modal');
        if (m) {
            m.classList.remove('rr-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('rr-per')) {
            el('rr-per').value = k;
        }
        if (k !== 'range') {
            if (el('rr-from')) {
                el('rr-from').value = '';
            }
            if (el('rr-to')) {
                el('rr-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('rr-per') }, { key: 'pfrom', vlu: val('rr-from') }, { key: 'pto', vlu: val('rr-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('rr-main'));
        if (window.AdminRoomsJsloaded) {
            window.AdminRoomsJsloaded();
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
        if (!askBox || !askBox.classList.contains('rr-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('rr-open');
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
        askBox.className = 'rr-ask';
        askBox.innerHTML = '<div class="rr-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="rr-err" role="alert"></em><div class="rr-askacts"><button type="button" class="rr-btn rr-btn-ghost rr-btn-sm"></button><button type="button" class="rr-btn rr-btn-sm"></button></div></div>';
        (el('rr-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('rr-open')) {
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
        x.b[1].className = 'rr-btn rr-btn-sm' + (o.danger ? ' rr-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('rr-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('rr-open');
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

    var tab = 'types';

    window.AdminRoomsJsloaded = function () {
        list(1);
    };

    function loadArgs() {
        return [];
    }

    function setTab(t) {
        tab = t;
        var b = document.querySelectorAll('.rr-tab');
        for (var i = 0; i < b.length; i++) {
            b[i].classList.toggle('rr-act', b[i].getAttribute('data-t') === t);
        }
        if (el('rr-q')) {
            el('rr-q').value = '';
        }
        list(1);
    }

    function list(pg) {
        api('List', [{ key: 'rtab', vlu: tab }, { key: 'term', vlu: val('rr-q') }, { key: 'rtype', vlu: val('rr-rt') }, { key: 'pg', vlu: String(pg || 1) }]);
    }

    function form(kind, id) {
        api('Form', [{ key: 'fkind', vlu: kind }, { key: 'fid', vlu: id || '' }]);
    }

    function fields(prefix) {
        var out = [];
        var list2 = document.querySelectorAll('#rr-mbody [data-k]');
        for (var i = 0; i < list2.length; i++) {
            var e = list2[i];
            out.push({ key: e.getAttribute('data-k'), vlu: e.type === 'checkbox' ? (e.checked ? '1' : '0') : (e.value || '').trim() });
        }
        var imgs = document.querySelectorAll('#rr-mbody input[name="rr-img"]:checked');
        out.push({ key: 'fimgs', vlu: Array.prototype.map.call(imgs, function (x) { return x.value; }).join('|') });
        var vid = document.querySelector('#rr-mbody input[name="rr-vid"]:checked');
        out.push({ key: 'fvid', vlu: vid ? vid.value : '' });
        return out;
    }

    function save(kind, id) {
        api('Save', [{ key: 'fkind', vlu: kind }, { key: 'fid', vlu: id || '' }].concat(fields()));
    }

    function del(kind, id, name) {
        ask({ title: 'Delete ' + name + '?', text: 'This cannot be undone.', ok: 'Delete', danger: true }, function () {
            api('Remove', [{ key: 'fkind', vlu: kind }, { key: 'fid', vlu: id }]);
        });
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
            var id = e.target && e.target.id ? e.target.id.replace('rr-', 'rr-e-') : '';
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
        setTab: setTab,
        list: list,
        form: form,
        save: save,
        del: del
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminRoomsJs.reveal);
} else {
    AdminRoomsJs.reveal();
}
