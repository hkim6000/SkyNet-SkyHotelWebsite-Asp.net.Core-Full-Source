var AdminFrontDeskJs = (function () {

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
        $ApiRequest('AdminFrontDesk/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('fd-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('fd-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('fd-open');
        }, 3200);
    }

    function openNav() {
        el('fd-nav').classList.add('fd-open');
        el('fd-scrim').classList.add('fd-open');
    }

    function closeNav() {
        el('fd-nav').classList.remove('fd-open');
        el('fd-scrim').classList.remove('fd-open');
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
        el('fd-modal').classList.add('fd-open');
    }

    function closeModal() {
        var m = el('fd-modal');
        if (m) {
            m.classList.remove('fd-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('fd-per')) {
            el('fd-per').value = k;
        }
        if (k !== 'range') {
            if (el('fd-from')) {
                el('fd-from').value = '';
            }
            if (el('fd-to')) {
                el('fd-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('fd-per') }, { key: 'pfrom', vlu: val('fd-from') }, { key: 'pto', vlu: val('fd-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('fd-main'));
        if (window.AdminFrontDeskJsloaded) {
            window.AdminFrontDeskJsloaded();
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
        if (!askBox || !askBox.classList.contains('fd-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('fd-open');
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
        askBox.className = 'fd-ask';
        askBox.innerHTML = '<div class="fd-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="fd-err" role="alert"></em><div class="fd-askacts"><button type="button" class="fd-btn fd-btn-ghost fd-btn-sm"></button><button type="button" class="fd-btn fd-btn-sm"></button></div></div>';
        (el('fd-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('fd-open')) {
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
        x.b[1].className = 'fd-btn fd-btn-sm' + (o.danger ? ' fd-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('fd-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('fd-open');
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

    var tab = 'arr';

    window.AdminFrontDeskJsloaded = function () {
        list(1);
    };

    function loadArgs() {
        tab = qs('tab') === 'in' ? 'in' : qs('tab') === 'out' ? 'out' : 'arr';
        return [{ key: 'fdtab', vlu: tab }];
    }

    function setTab(t) {
        tab = t;
        history.replaceState(null, '', t === 'arr' ? 'AdminFrontDesk' : 'AdminFrontDesk?tab=' + t);
        var b = document.querySelectorAll('.fd-tab');
        for (var i = 0; i < b.length; i++) {
            b[i].classList.toggle('fd-act', b[i].getAttribute('data-t') === t);
        }
        list(1);
    }

    function list(pg) {
        api('List', [{ key: 'fdtab', vlu: tab }, { key: 'fday', vlu: val('fd-fday') }, { key: 'term', vlu: val('fd-q') }, { key: 'ftype', vlu: val('fd-ftype') }, { key: 'pg', vlu: String(pg || 1) }]);
    }

    function today() {
        el('fd-fday').value = '';
        list(1);
    }

    function checkIn(id) {
        api('InForm', [{ key: 'bid', vlu: id }]);
    }

    function details() {
        return [{ key: 'dphone', vlu: val('fd-dphone') }, { key: 'daddr', vlu: val('fd-daddr') }, { key: 'dcountry', vlu: val('fd-dcountry') }, { key: 'dpass', vlu: val('fd-dpass') }, { key: 'dplate', vlu: val('fd-dplate') }, { key: 'dvalet', vlu: val('fd-dvalet') }, { key: 'dout', vlu: val('fd-dout') }];
    }

    function inBase(id) {
        var r = document.querySelector('input[name="fd-room"]:checked');
        var pr = document.querySelector('input[name="fd-promo"]:checked');
        return [{ key: 'bid', vlu: id }, { key: 'room', vlu: r ? r.value : '' }, { key: 'pkeep', vlu: pr ? pr.value : 'keep' }].concat(details());
    }

    function doIn(id) {
        api('CheckIn', inBase(id).concat([{ key: 'step', vlu: 'check' }]));
    }

    function showPay() {
        el('fd-instep').hidden = true;
        el('fd-paystep').hidden = false;
        el('fd-modal').scrollTop = 0;
    }

    function backIn() {
        el('fd-paystep').hidden = true;
        el('fd-instep').hidden = false;
    }

    function confirmIn(id) {
        var p = document.querySelector('input[name="fd-inpay"]:checked');
        var d = document.querySelector('input[name="fd-indep"]:checked');
        el('fd-inbtn').disabled = true;
        api('CheckIn', inBase(id).concat([{ key: 'step', vlu: 'go' }, { key: 'inpay', vlu: p ? p.value : '' }, { key: 'indep', vlu: d ? d.value : '' }]));
    }

    function inFail() {
        if (el('fd-inbtn')) {
            el('fd-inbtn').disabled = false;
        }
    }

    function saveDetails(id) {
        api('Details', [{ key: 'bid', vlu: id }].concat(details()));
    }

    function checkOut(id) {
        api('OutForm', [{ key: 'bid', vlu: id }]);
    }

    function charge(id) {
        api('Charge', [{ key: 'bid', vlu: id }, { key: 'cwhat', vlu: val('fd-cwhat') }, { key: 'camt', vlu: val('fd-camt') }]);
    }

    function doOut(id, room) {
        var p = document.querySelector('input[name="fd-pay"]:checked');
        ask({ title: 'Check out room ' + room + '?', text: 'The guest leaves and the room is marked Dirty for housekeeping.', ok: 'Check out', no: 'Not yet' }, function () {
            api('CheckOut', [{ key: 'bid', vlu: id }, { key: 'pay', vlu: p ? p.value : '' }]);
        });
    }

    function done() {
        list(1);
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
            var id = e.target && e.target.id ? e.target.id.replace('fd-', 'fd-e-') : '';
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
        today: today,
        checkIn: checkIn,
        doIn: doIn,
        showPay: showPay,
        backIn: backIn,
        confirmIn: confirmIn,
        inFail: inFail,
        saveDetails: saveDetails,
        checkOut: checkOut,
        charge: charge,
        doOut: doOut,
        done: done
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminFrontDeskJs.reveal);
} else {
    AdminFrontDeskJs.reveal();
}
