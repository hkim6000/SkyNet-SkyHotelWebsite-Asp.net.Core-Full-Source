var AdminContentJs = (function () {

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
        $ApiRequest('AdminContent/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ce-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ce-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ce-open');
        }, 3200);
    }

    function openNav() {
        el('ce-nav').classList.add('ce-open');
        el('ce-scrim').classList.add('ce-open');
    }

    function closeNav() {
        el('ce-nav').classList.remove('ce-open');
        el('ce-scrim').classList.remove('ce-open');
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
        el('ce-modal').classList.add('ce-open');
    }

    function closeModal() {
        var m = el('ce-modal');
        if (m) {
            m.classList.remove('ce-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('ce-per')) {
            el('ce-per').value = k;
        }
        if (k !== 'range') {
            if (el('ce-from')) {
                el('ce-from').value = '';
            }
            if (el('ce-to')) {
                el('ce-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('ce-per') }, { key: 'pfrom', vlu: val('ce-from') }, { key: 'pto', vlu: val('ce-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('ce-main'));
        if (window.AdminContentJsloaded) {
            window.AdminContentJsloaded();
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
        if (!askBox || !askBox.classList.contains('ce-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('ce-open');
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
        askBox.className = 'ce-ask';
        askBox.innerHTML = '<div class="ce-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="ce-err" role="alert"></em><div class="ce-askacts"><button type="button" class="ce-btn ce-btn-ghost ce-btn-sm"></button><button type="button" class="ce-btn ce-btn-sm"></button></div></div>';
        (el('ce-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('ce-open')) {
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
        x.b[1].className = 'ce-btn ce-btn-sm' + (o.danger ? ' ce-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('ce-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('ce-open');
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

    var cur = 'home';

    window.AdminContentJsloaded = function () {
        show(cur);
    };

    function loadArgs() {
        return [];
    }

    function list() {
        show(cur);
    }

    function show(k) {
        cur = k;
        var b = document.querySelectorAll('.ce-celist button');
        for (var i = 0; i < b.length; i++) {
            b[i].classList.toggle('ce-act', b[i].getAttribute('data-k') === k);
        }
        api('Block', [{ key: 'ckey', vlu: k }]);
    }

    function addItem() {
        var box = el('ce-items');
        var n = box.querySelectorAll('.ce-item').length;
        if (n >= 8) {
            return;
        }
        var d = document.createElement('div');
        d.className = 'ce-item';
        d.innerHTML = '<label class="ce-field"><span>Name</span><input type="text" maxlength="60" data-f="name"></label><label class="ce-field"><span>Detail</span><input type="text" maxlength="60" data-f="meta"></label><label class="ce-field"><span>Text</span><input type="text" maxlength="200" data-f="text"></label><button type="button" class="ce-btn ce-btn-sm ce-btn-danger" onclick="this.parentNode.remove()">Remove</button>';
        box.appendChild(d);
    }

    function save() {
        var items = Array.prototype.map.call(document.querySelectorAll('#ce-items .ce-item'), function (r) {
            return {
                name: r.querySelector('[data-f="name"]').value.trim(),
                meta: r.querySelector('[data-f="meta"]').value.trim(),
                text: r.querySelector('[data-f="text"]').value.trim()
            };
        });
        var img = document.querySelector('input[name="ce-cimg"]:checked');
        var vid = document.querySelector('input[name="ce-cvid"]:checked');
        api('Save', [{ key: 'ckey', vlu: cur }, { key: 'ctitle', vlu: val('ce-ctitle') }, { key: 'ctext', vlu: val('ce-ctext') }, { key: 'cimg', vlu: img ? img.value : '' }, { key: 'cvid', vlu: vid ? vid.value : '' }, { key: 'citems', vlu: JSON.stringify(items) }]);
    }

    function saveGallery() {
        var g = Array.prototype.map.call(document.querySelectorAll('input[name="ce-gal"]:checked'), function (x) { return x.value; });
        api('SaveGallery', [{ key: 'cgal', vlu: g.join('|') }]);
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
            var id = e.target && e.target.id ? e.target.id.replace('ce-', 'ce-e-') : '';
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
        show: show,
        addItem: addItem,
        save: save,
        saveGallery: saveGallery
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminContentJs.reveal);
} else {
    AdminContentJs.reveal();
}
