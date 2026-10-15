var ReservationJs = (function () {

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
        $ApiRequest('Reservation/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('rv-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('rv-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('rv-open');
        }, 3200);
    }

    function openNav() {
        el('rv-nav').classList.add('rv-open');
        el('rv-scrim').classList.add('rv-open');
    }

    function closeNav() {
        el('rv-nav').classList.remove('rv-open');
        el('rv-scrim').classList.remove('rv-open');
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

    var ARGA = 'ci';
    var ARGB = 'co';

    function arg(k) {
        return k ? (new URLSearchParams(location.search).get(k) || '') : '';
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function applyTheme(theme) {
        el('rv-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function toggleTheme() {
        var next = el('rv-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('sh-theme', next);
        } catch (e) {
        }
    }

    function demoBar() {
        el('rv-demo-bar').innerHTML = 'You are using your own demo copy of SkyHotel. It resets after an hour. <a href="Admin">Open the hotel admin</a>';
    }

    function hydrated(ok, accent, theme) {
        document.documentElement.classList.remove('rv-hydrating');
        if (!ok) {
            setCookie('sh_sbx', '', 0);
            if (window.ReservationJsready) {
                window.ReservationJsready();
            }
            return;
        }
        if (accent) {
            el('rv-site').style.setProperty('--rv-accent', accent);
        }
        var stored = '';
        try {
            stored = localStorage.getItem('sh-theme') || '';
        } catch (e) {
        }
        applyTheme(stored || theme);
        demoBar();
        times();
        if (window.ReservationJsready) {
            window.ReservationJsready();
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
        var m = el('rv-mem');
        if (!m) {
            return;
        }
        if (!name) {
            signedOut();
            return;
        }
        m.href = 'MyCircle';
        m.classList.add('rv-in');
        m.innerHTML = '<span class="rv-grade rv-g-' + grade + '">' + grade + '</span>' + name;
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
        if (!askBox || !askBox.classList.contains('rv-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('rv-open');
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
        askBox.className = 'rv-ask';
        askBox.innerHTML = '<div class="rv-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="rv-err" role="alert"></em><div class="rv-askacts"><button type="button" class="rv-btn rv-btn-ghost rv-btn-sm"></button><button type="button" class="rv-btn rv-btn-sm"></button></div></div>';
        (el('rv-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('rv-open')) {
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
        x.b[1].className = 'rv-btn rv-btn-sm' + (o.danger ? ' rv-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('rv-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('rv-open');
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

    var cur = 1;

    function ask_() {
        return [
            { key: 'din', vlu: val('rv-din') },
            { key: 'dout', vlu: val('rv-dout') },
            { key: 'nad', vlu: val('rv-nad') },
            { key: 'nch', vlu: val('rv-nch') },
            { key: 'pcode', vlu: val('rv-pcode') },
            { key: 'sbx', vlu: sbx() },
            { key: 'mtok', vlu: mem() }
        ];
    }

    function guest_() {
        return [
            { key: 'gfirst', vlu: val('rv-gfirst') },
            { key: 'glast', vlu: val('rv-glast') },
            { key: 'gmail', vlu: val('rv-gmail') },
            { key: 'gphone', vlu: val('rv-gphone') },
            { key: 'gcountry', vlu: val('rv-gcountry') },
            { key: 'garr', vlu: val('rv-garr') },
            { key: 'greq', vlu: val('rv-greq') }
        ];
    }

    function room_() {
        return [{ key: 'rtype', vlu: val('rv-rtype') }, { key: 'rplan', vlu: val('rv-rplan') }, { key: 'pts', vlu: val('rv-pts') }];
    }

    function step(n) {
        if (n === 4 || cur !== 4) {
            cur = n;
        }
        for (var i = 1; i <= 4; i++) {
            var s = el('rv-s' + i);
            if (s) {
                s.hidden = i !== cur;
            }
        }
        var lis = document.querySelectorAll('#rv-steps li');
        for (var k = 0; k < lis.length; k++) {
            var sn = parseInt(lis[k].getAttribute('data-s'), 10);
            lis[k].classList.toggle('rv-act', sn === cur);
            lis[k].classList.toggle('rv-done', sn < cur);
        }
        var box = el('rv-s' + cur);
        if (box && n > 1) {
            var top = box.getBoundingClientRect().top + window.scrollY - 90;
            if (window.scrollY > top || n === 4) {
                window.scrollTo(0, Math.max(0, top));
            }
        }
    }

    function find() {
        if (cur === 4) {
            return false;
        }
        call('Find', ask_().concat([{ key: 'want', vlu: qs('t') }]));
        return false;
    }

    function found() {
        step(1);
    }

    function pick(type, plan) {
        el('rv-rtype').value = type;
        el('rv-rplan').value = plan;
        el('rv-pts').value = '0';
        call('Pick', ask_().concat(room_()));
    }

    function details() {
        el('rv-pts').value = '0';
        call('Details', ask_().concat(room_(), guest_()));
        return false;
    }

    function points() {
        el('rv-pts').value = val('rv-ptsel') || '0';
        call('Points', ask_().concat(room_()));
    }

    function payLabel(t) {
        var b = el('rv-paybtn');
        if (b) {
            b.textContent = 'Pay ' + t;
        }
    }

    function fmtCard(inp) {
        var d = inp.value.replace(/\D/g, '').substring(0, 19);
        inp.value = d.replace(/(.{4})/g, '$1 ').trim();
    }

    function pay() {
        var b = el('rv-paybtn');
        if (!b || b.disabled) {
            return false;
        }
        b.disabled = true;
        b.setAttribute('data-t', b.textContent);
        b.textContent = 'Processing payment...';
        el('rv-s3').classList.add('rv-paying');
        setTimeout(function () {
            call('Pay', ask_().concat(room_(), guest_(), [
                { key: 'cname', vlu: val('rv-cname') },
                { key: 'cnum', vlu: val('rv-cnum') },
                { key: 'cexp', vlu: val('rv-cexp') },
                { key: 'ccvc', vlu: val('rv-ccvc') },
                { key: 'cagree', vlu: el('rv-cagree') && el('rv-cagree').checked ? '1' : '0' }
            ]));
        }, 900);
        return false;
    }

    function payDone() {
        var b = el('rv-paybtn');
        if (b) {
            b.disabled = false;
            b.textContent = b.getAttribute('data-t') || b.textContent;
        }
        var s = el('rv-s3');
        if (s) {
            s.classList.remove('rv-paying');
        }
    }

    function booked() {
        payDone();
        step(4);
        var f = el('rv-s1');
        if (f) {
            f.hidden = true;
        }
    }

    function showSignIn() {
        var b = el('rv-msign');
        b.hidden = false;
        window.scrollTo(0, Math.max(0, b.getBoundingClientRect().top + window.scrollY - 90));
        el('rv-memail').focus();
    }

    function memberIn() {
        call('MemberIn', [
            { key: 'memail', vlu: val('rv-memail') },
            { key: 'mpass', vlu: el('rv-mpass').value },
            { key: 'sbx', vlu: sbx() }
        ]);
        return false;
    }

    function memberReady() {
        el('rv-msign').hidden = true;
        call('Who', [{ key: 'mtok', vlu: mem() }]);
        if (cur >= 2 && val('rv-rtype')) {
            pick(val('rv-rtype'), val('rv-rplan'));
        } else {
            find();
        }
        toast('Signed in. Member prices applied.');
    }

    function start() {
        var a = qs('ad');
        var c = qs('ch');
        if (a && el('rv-nad')) {
            el('rv-nad').value = a;
        }
        if (c && el('rv-nch')) {
            el('rv-nch').value = c;
        }
        if (qs('promo')) {
            el('rv-pcode').value = qs('promo').toUpperCase().substring(0, 20);
        }
        var ci = el('rv-din');
        ci.addEventListener('change', function () {
            var co = el('rv-dout');
            if (!ci.value) {
                return;
            }
            var d = new Date(ci.value + 'T00:00:00Z');
            d.setUTCDate(d.getUTCDate() + 1);
            var min = d.toISOString().substring(0, 10);
            co.min = min;
            if (!co.value || co.value < min) {
                d.setUTCDate(d.getUTCDate() + 1);
                co.value = d.toISOString().substring(0, 10);
            }
        });
        if (!sbx()) {
            find();
        }
        window.ReservationJsready = function () {
            var a2 = qs('ad');
            var c2 = qs('ch');
            if (a2) {
                el('rv-nad').value = a2;
            }
            if (c2) {
                el('rv-nch').value = c2;
            }
            if (qs('promo')) {
                el('rv-pcode').value = qs('promo').toUpperCase().substring(0, 20);
            }
            find();
        };
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
            var id = e.target && e.target.id ? e.target.id.replace('rv-', 'rv-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
        start();
    }

    if (document.cookie.indexOf('sh_sbx=') >= 0) {
        document.documentElement.classList.add('rv-hydrating');
        setTimeout(function () {
            document.documentElement.classList.remove('rv-hydrating');
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
        find: find,
        found: found,
        pick: pick,
        details: details,
        points: points,
        payLabel: payLabel,
        fmtCard: fmtCard,
        pay: pay,
        payDone: payDone,
        booked: booked,
        step: step,
        showSignIn: showSignIn,
        memberIn: memberIn,
        memberReady: memberReady
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ReservationJs.reveal);
} else {
    ReservationJs.reveal();
}
