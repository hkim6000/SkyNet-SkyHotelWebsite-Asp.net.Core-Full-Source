var AdminCalendarJs = (function () {

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
        $ApiRequest('AdminCalendar/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('cl-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('cl-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('cl-open');
        }, 3200);
    }

    function openNav() {
        el('cl-nav').classList.add('cl-open');
        el('cl-scrim').classList.add('cl-open');
    }

    function closeNav() {
        el('cl-nav').classList.remove('cl-open');
        el('cl-scrim').classList.remove('cl-open');
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
        el('cl-modal').classList.add('cl-open');
    }

    function closeModal() {
        var m = el('cl-modal');
        if (m) {
            m.classList.remove('cl-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('cl-per')) {
            el('cl-per').value = k;
        }
        if (k !== 'range') {
            if (el('cl-from')) {
                el('cl-from').value = '';
            }
            if (el('cl-to')) {
                el('cl-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('cl-per') }, { key: 'pfrom', vlu: val('cl-from') }, { key: 'pto', vlu: val('cl-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('cl-main'));
        if (window.AdminCalendarJsloaded) {
            window.AdminCalendarJsloaded();
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
        if (!askBox || !askBox.classList.contains('cl-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('cl-open');
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
        askBox.className = 'cl-ask';
        askBox.innerHTML = '<div class="cl-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="cl-err" role="alert"></em><div class="cl-askacts"><button type="button" class="cl-btn cl-btn-ghost cl-btn-sm"></button><button type="button" class="cl-btn cl-btn-sm"></button></div></div>';
        (el('cl-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('cl-open')) {
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
        x.b[1].className = 'cl-btn cl-btn-sm' + (o.danger ? ' cl-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('cl-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('cl-open');
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

    var view = 'day';

    window.AdminCalendarJsloaded = function () {
        list(1);
    };

    function loadArgs() {
        return [];
    }

    function list(pg) {
        api('List', [{ key: 'cview', vlu: view }, { key: 'cstart', vlu: val('cl-cstart') }, { key: 'ctype', vlu: val('cl-ctype') }, { key: 'term', vlu: val('cl-q') }, { key: 'pg', vlu: String(pg || 1) }]);
    }

    function setView(v) {
        view = v;
        var b = document.querySelectorAll('.cl-views .cl-tab');
        for (var i = 0; i < b.length; i++) {
            b[i].classList.toggle('cl-act', b[i].getAttribute('data-v') === v);
        }
        var d = document.querySelectorAll('.cl-donly');
        for (var j = 0; j < d.length; j++) {
            d[j].hidden = v !== 'day';
        }
        el('cl-list').className = v === 'day' ? 'cl-cal' : 'cl-mview';
        el('cl-calwrap').classList.toggle('cl-plain', v !== 'day');
        list(1);
    }

    function shift(d) {
        var v = val('cl-cstart');
        var t = v ? new Date(v + 'T00:00:00Z') : new Date();
        if (d === 0) {
            t = new Date();
        } else if (view === 'month') {
            t.setUTCDate(1);
            t.setUTCMonth(t.getUTCMonth() + d);
        } else {
            t.setUTCDate(t.getUTCDate() + d * 7);
        }
        el('cl-cstart').value = t.toISOString().substring(0, 10);
        list(1);
    }

    function dayList(day, kind) {
        api('DayList', [{ key: 'cday', vlu: day }, { key: 'ckind', vlu: kind }, { key: 'ctype', vlu: val('cl-ctype') }, { key: 'term', vlu: val('cl-q') }]);
    }

    function open(id, day, kind) {
        api('Open', [{ key: 'bid', vlu: id }, { key: 'bday', vlu: day || '' }, { key: 'bkind', vlu: kind || '' }]);
    }

    function assign(id) {
        var r = document.querySelector('input[name="cl-room"]:checked');
        api('Assign', [{ key: 'bid', vlu: id }, { key: 'room', vlu: r ? r.value : '' }]);
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
            var id = e.target && e.target.id ? e.target.id.replace('cl-', 'cl-e-') : '';
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
        setView: setView,
        shift: shift,
        dayList: dayList,
        open: open,
        assign: assign
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminCalendarJs.reveal);
} else {
    AdminCalendarJs.reveal();
}
