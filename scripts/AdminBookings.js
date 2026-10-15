var AdminBookingsJs = (function () {

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
        $ApiRequest('AdminBookings/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('bk-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('bk-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('bk-open');
        }, 3200);
    }

    function openNav() {
        el('bk-nav').classList.add('bk-open');
        el('bk-scrim').classList.add('bk-open');
    }

    function closeNav() {
        el('bk-nav').classList.remove('bk-open');
        el('bk-scrim').classList.remove('bk-open');
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
        el('bk-modal').classList.add('bk-open');
    }

    function closeModal() {
        var m = el('bk-modal');
        if (m) {
            m.classList.remove('bk-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('bk-per')) {
            el('bk-per').value = k;
        }
        if (k !== 'range') {
            if (el('bk-from')) {
                el('bk-from').value = '';
            }
            if (el('bk-to')) {
                el('bk-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('bk-per') }, { key: 'pfrom', vlu: val('bk-from') }, { key: 'pto', vlu: val('bk-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('bk-main'));
        if (window.AdminBookingsJsloaded) {
            window.AdminBookingsJsloaded();
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
        if (!askBox || !askBox.classList.contains('bk-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('bk-open');
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
        askBox.className = 'bk-ask';
        askBox.innerHTML = '<div class="bk-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="bk-err" role="alert"></em><div class="bk-askacts"><button type="button" class="bk-btn bk-btn-ghost bk-btn-sm"></button><button type="button" class="bk-btn bk-btn-sm"></button></div></div>';
        (el('bk-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('bk-open')) {
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
        x.b[1].className = 'bk-btn bk-btn-sm' + (o.danger ? ' bk-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('bk-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('bk-open');
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

    window.AdminBookingsJsloaded = function () {
        if (qs('code')) {
            el('bk-q').value = qs('code');
        }
        list(1);
    };

    function loadArgs() {
        return [];
    }

    function list(pg) {
        api('List', [{ key: 'bst', vlu: val('bk-bst') }, { key: 'btype', vlu: val('bk-btype') }, { key: 'bsrc', vlu: val('bk-bsrc') }, { key: 'term', vlu: val('bk-q') }, { key: 'bsort', vlu: val('bk-bsort') }, { key: 'pg', vlu: String(pg || 1) }].concat(period()));
    }

    function open(id) {
        api('Open', [{ key: 'bid', vlu: id }]);
    }

    function note(id) {
        api('Note', [{ key: 'bid', vlu: id }, { key: 'bnote', vlu: val('bk-bnote') }]);
    }

    function editForm(id) {
        api('EditForm', [{ key: 'bid', vlu: id }]);
    }

    function reprice(id, save) {
        api('Change', [{ key: 'bid', vlu: id }, { key: 'ein', vlu: val('bk-ein') }, { key: 'eout', vlu: val('bk-eout') }, { key: 'ead', vlu: val('bk-ead') }, { key: 'ech', vlu: val('bk-ech') }, { key: 'etype', vlu: val('bk-etype') }, { key: 'save', vlu: save ? '1' : '0' }]);
    }

    function cancel(id, refund) {
        var waive = el('bk-waive') && el('bk-waive').checked;
        ask({ title: 'Cancel this booking?', text: waive ? 'The guest gets a full refund.' : 'The guest is refunded ' + refund + '.', ok: 'Cancel booking', no: 'Keep it', danger: true }, function () {
            api('Cancel', [{ key: 'bid', vlu: id }, { key: 'waive', vlu: waive ? '1' : '0' }]);
        });
    }

    var ntimer = null;

    function newForm(src) {
        api('NewForm', [{ key: 'nsrc', vlu: src }]);
    }

    function srcPicked() {
        var a = document.querySelector('input[name="bk-nsrcr"]:checked');
        el('bk-nagbox').hidden = !a || a.value !== 'agency';
    }

    function findGuest() {
        clearTimeout(ntimer);
        ntimer = setTimeout(function () {
            if (val('bk-nfind').length < 2) {
                el('bk-nfound').innerHTML = '';
                return;
            }
            api('FindGuest', [{ key: 'nfind', vlu: val('bk-nfind') }]);
        }, 300);
    }

    function pickGuest(btn) {
        var d = btn.dataset;
        el('bk-ngid').value = d.id;
        el('bk-nfirst').value = d.first;
        el('bk-nlast').value = d.last;
        el('bk-nmail').value = d.mail;
        el('bk-nphone').value = d.phone;
        el('bk-npicked').innerHTML = btn.getAttribute('data-label');
        el('bk-nfound').innerHTML = '';
        el('bk-nfind').value = '';
    }

    function unpick() {
        el('bk-ngid').value = '';
        el('bk-npicked').innerHTML = '';
    }

    function nfields() {
        var a = document.querySelector('input[name="bk-nsrcr"]:checked');
        return [{ key: 'nsrc', vlu: a ? a.value : '' }, { key: 'nagency', vlu: val('bk-nagency') }, { key: 'nagref', vlu: val('bk-nagref') }, { key: 'ngid', vlu: val('bk-ngid') }, { key: 'nfirst', vlu: val('bk-nfirst') }, { key: 'nlast', vlu: val('bk-nlast') }, { key: 'nmail', vlu: val('bk-nmail') }, { key: 'nphone', vlu: val('bk-nphone') }, { key: 'nin', vlu: val('bk-nin') }, { key: 'nout', vlu: val('bk-nout') }, { key: 'ntype', vlu: val('bk-ntype') }, { key: 'nplan', vlu: val('bk-nplan') }, { key: 'nad', vlu: val('bk-nad') }, { key: 'nch', vlu: val('bk-nch') }, { key: 'npromo', vlu: val('bk-npromo') }, { key: 'narr', vlu: val('bk-narr') }, { key: 'nreq', vlu: val('bk-nreq') }];
    }

    function newPrice() {
        api('NewPrice', nfields());
    }

    var npOpen = false;

    function promoOpen() {
        if (npOpen) {
            return;
        }
        npOpen = true;
        api('PromoList', [{ key: 'ntype', vlu: val('bk-ntype') }, { key: 'nin', vlu: val('bk-nin') }, { key: 'nout', vlu: val('bk-nout') }, { key: 'ngid', vlu: val('bk-ngid') }]);
    }

    function promoShown() {
        if (document.activeElement !== el('bk-npromo')) {
            return;
        }
        el('bk-nplist').hidden = false;
        promoFilter();
        el('bk-nplist').scrollIntoView({ block: 'nearest' });
    }

    function promoFilter() {
        var t = val('bk-npromo').toUpperCase();
        var items = document.querySelectorAll('#bk-nplist .bk-npi');
        for (var i = 0; i < items.length; i++) {
            items[i].hidden = t !== '' && items[i].textContent.toUpperCase().indexOf(t) < 0;
        }
    }

    function promoClose() {
        setTimeout(function () {
            if (el('bk-nplist')) {
                el('bk-nplist').hidden = true;
            }
            npOpen = false;
        }, 150);
    }

    function pickPromo(code) {
        el('bk-npromo').value = code;
        el('bk-nplist').hidden = true;
        npOpen = false;
        newPrice();
    }

    function typeChanged() {
        npOpen = false;
        if (el('bk-nplist')) {
            el('bk-nplist').hidden = true;
            el('bk-nplist').innerHTML = '';
        }
        if (val('bk-npromo')) {
            el('bk-npromo').value = '';
            el('bk-nprev').innerHTML = 'Promo code cleared. Room type changed. Press Check price.';
        } else {
            el('bk-nprev').innerHTML = 'Room type changed. Press Check price.';
        }
    }

    function create() {
        el('bk-nbtn').disabled = true;
        api('Create', nfields());
    }

    function createFail() {
        if (el('bk-nbtn')) {
            el('bk-nbtn').disabled = false;
        }
    }

    function noShow(id) {
        ask({ title: 'Mark as no-show?', text: 'The room is released and nothing is refunded.', ok: 'Mark no-show', danger: true }, function () {
            api('NoShow', [{ key: 'bid', vlu: id }]);
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
            var id = e.target && e.target.id ? e.target.id.replace('bk-', 'bk-e-') : '';
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
        open: open,
        note: note,
        editForm: editForm,
        reprice: reprice,
        cancel: cancel,
        noShow: noShow,
        newForm: newForm,
        srcPicked: srcPicked,
        findGuest: findGuest,
        pickGuest: pickGuest,
        unpick: unpick,
        newPrice: newPrice,
        promoOpen: promoOpen,
        promoShown: promoShown,
        promoFilter: promoFilter,
        promoClose: promoClose,
        pickPromo: pickPromo,
        typeChanged: typeChanged,
        create: create,
        createFail: createFail
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminBookingsJs.reveal);
} else {
    AdminBookingsJs.reveal();
}
