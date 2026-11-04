(function () {
    'use strict';
    var workspace = document.querySelector('.measurement-workspace');
    if (!workspace) { return; }
    var input = workspace.querySelector('[data-tree-search]');
    var tree = workspace.querySelector('[data-measurement-tree]');
    var content = workspace.querySelector('.workspace-content');
    var kind = workspace.getAttribute('data-workspace-kind');
    var key = 'ACEnergo:tree:' + kind;
    var loaded = new URL(window.location.href);
    var pending = null;
    var generation = 0;
    function fitTree() {
        var labels = tree.querySelectorAll('summary a');
        var width = 304;
        var i;
        for (i = 0; i < labels.length; i += 1) { width = Math.max(width, labels[i].scrollWidth + 64); }
        workspace.style.setProperty('--measurement-tree-width', Math.min(width, 480) + 'px');
    }
    function filter() {
        var items = tree.querySelectorAll('[data-tree-item]');
        var query = input.value.toLocaleLowerCase();
        var count = 0;
        var i;
        for (i = 0; i < items.length; i += 1) {
            var visible = items[i].getAttribute('data-search-text').toLocaleLowerCase().indexOf(query) >= 0;
            items[i].hidden = !visible;
            if (visible) { count += 1; }
        }
        workspace.querySelector('[data-tree-empty]').hidden = count !== 0;
        fitTree();
    }
    function selectedId() {
        var link = tree.querySelector('summary [aria-current="page"]');
        return link ? link.closest('[data-group-id]').getAttribute('data-group-id') : null;
    }
    function state() {
        var open = [];
        var items = tree.querySelectorAll('[data-group-id]');
        var anchor = null;
        var offset = 0;
        var bounds = tree.getBoundingClientRect();
        var i;
        for (i = 0; i < items.length; i += 1) {
            var summary = items[i].querySelector('summary').getBoundingClientRect();
            if (!anchor && !items[i].hidden && summary.bottom > bounds.top && summary.top < bounds.bottom) { anchor = items[i].getAttribute('data-group-id'); offset = summary.top - bounds.top; }
            if (items[i].querySelector('details').open) { open.push(items[i].getAttribute('data-group-id')); }
        }
        return { top: tree.scrollTop, filter: input.value, open: open, selected: selectedId(), anchor: anchor, offset: offset };
    }
    function save() { try { window.sessionStorage.setItem(key, JSON.stringify(state())); } catch (ignore) { /* Storage may be blocked. */ } }
    function restore() {
        try {
            var saved = JSON.parse(window.sessionStorage.getItem(key));
            if (!saved || !Array.isArray(saved.open) || typeof saved.filter !== 'string') { return; }
            var current = selectedId();
            var items = tree.querySelectorAll('[data-group-id]');
            var i;
            input.value = saved.filter;
            for (i = 0; i < items.length; i += 1) {
                var id = items[i].getAttribute('data-group-id');
                items[i].querySelector('details').open = saved.open.indexOf(id) >= 0 || (current !== saved.selected && id === current);
            }
            filter();
            tree.scrollTop = Number(saved.top) || 0;
            for (i = 0; i < items.length; i += 1) {
                if (items[i].getAttribute('data-group-id') === saved.anchor && !items[i].hidden && typeof saved.offset === 'number') {
                    tree.scrollTop += items[i].querySelector('summary').getBoundingClientRect().top - tree.getBoundingClientRect().top - saved.offset;
                    break;
                }
            }
        } catch (ignore) { /* Default server-rendered selection remains usable. */ }
    }
    function eligible(url) {
        var match = url.pathname.match(/\/Electricity(?:Group|GroupComposition|Groups)\/([^/]+)(?:\/[0-9]+)?\/?$/);
        return url.origin === window.location.origin && match && match[1] === kind && !url.searchParams.has('handler');
    }
    function initTables() {
        if (!window.DataTable) { return; }
        var tables = content.querySelectorAll('[data-price-table]');
        var i;
        for (i = 0; i < tables.length; i += 1) {
            if (!window.DataTable.isDataTable(tables[i])) {
                new window.DataTable(tables[i], {
                    paging: false, info: false, ordering: false, autoWidth: false,
                    layout: { topStart: null, topEnd: 'search', bottomStart: null, bottomEnd: null },
                    language: { search: 'Hledat položku:', zeroRecords: 'Žádná odpovídající položka', emptyTable: 'Nejsou dostupné položky' }
                });
            }
        }
    }
    function clean() {
        var modals = content.querySelectorAll('.modal');
        var i;
        for (i = 0; i < modals.length; i += 1) {
            modals[i].dispatchEvent(new Event('acenergo:dispose'));
            var instance = window.bootstrap && window.bootstrap.Modal.getInstance(modals[i]);
            if (instance) { instance.dispose(); }
        }
        if (window.DataTable) {
            var tables = content.querySelectorAll('table');
            for (i = 0; i < tables.length; i += 1) {
                if (window.DataTable.isDataTable(tables[i])) { new window.DataTable(tables[i]).destroy(); }
            }
        }
    }
    function updateBranch(parsed) {
        var selected = parsed.querySelector('.measurement-tree summary [aria-current="page"]');
        var old = tree.querySelectorAll('[aria-current="page"], .tree-selected');
        var i;
        for (i = 0; i < old.length; i += 1) { old[i].removeAttribute('aria-current'); old[i].classList.remove('tree-selected'); }
        if (!selected) { return; }
        var fresh = selected.closest('[data-group-id]');
        var items = tree.querySelectorAll('[data-group-id]');
        for (i = 0; i < items.length; i += 1) {
            if (items[i].getAttribute('data-group-id') === fresh.getAttribute('data-group-id')) {
                items[i].setAttribute('data-search-text', fresh.getAttribute('data-search-text'));
                items[i].querySelector('details').innerHTML = fresh.querySelector('details').innerHTML;
                items[i].querySelector('details').open = true;
                return;
            }
        }
        tree.appendChild(document.importNode(fresh, true));
    }
    function scrollHash(url) {
        if (!url.hash) { return; }
        try {
            var target = document.getElementById(decodeURIComponent(url.hash.substring(1)));
            if (target && content.contains(target)) { target.scrollIntoView({ block: 'nearest' }); }
        } catch (ignore) { /* Malformed fragment must not break selection. */ }
    }
    function error(url) {
        content.setAttribute('aria-busy', 'false');
        var message = content.querySelector('[data-workspace-status]');
        message.hidden = false;
        message.setAttribute('role', 'alert');
        message.classList.add('message-error');
        message.textContent = 'Detail se nepodařilo načíst. Vyberte měření znovu, nebo ';
        var link = document.createElement('a');
        link.href = url.href; link.textContent = 'otevřete celou stránku'; link.setAttribute('data-native-navigation', 'true');
        message.appendChild(link);
    }
    function navigate(url, push, focusHref) {
        save();
        if (pending) { pending.abort(); pending = null; }
        var current = ++generation;
        if (url.pathname === loaded.pathname && url.search === loaded.search && url.hash) {
            if (push && url.href !== window.location.href) { window.history.pushState(null, '', url.href); }
            loaded = url; content.setAttribute('aria-busy', 'false'); content.querySelector('[data-workspace-status]').hidden = true; scrollHash(url); return;
        }
        var xhr = new XMLHttpRequest();
        pending = xhr;
        var message = content.querySelector('[data-workspace-status]');
        message.hidden = false; message.setAttribute('role', 'status'); message.classList.remove('message-error'); message.textContent = 'Načítám detail měření…';
        content.setAttribute('aria-busy', 'true');
        xhr.open('GET', url.href, true);
        xhr.timeout = 45000;
        xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
        xhr.setRequestHeader('X-Workspace-Request', '1');
        xhr.onload = function () {
            if (current !== generation) { return; }
            if (xhr.status === 401 || xhr.status === 403) { window.location.assign(url.href); return; }
            if (xhr.responseURL && !eligible(new URL(xhr.responseURL))) { window.location.assign(xhr.responseURL); return; }
            var parsed = new DOMParser().parseFromString(xhr.responseText, 'text/html');
            var nextWorkspace = parsed.querySelector('.measurement-workspace');
            var next = parsed.querySelector('.workspace-content');
            if (!next || !nextWorkspace || nextWorkspace.getAttribute('data-workspace-kind') !== kind) { error(url); return; }
            function apply() {
                if (current !== generation) { return; }
                var top = tree.scrollTop;
                var windowX = window.scrollX;
                var windowY = window.scrollY;
                clean();
                updateBranch(parsed);
                content.innerHTML = next.innerHTML;
                content.setAttribute('aria-busy', 'false');
                document.title = parsed.title;
                loaded = url;
                if (push && url.href !== window.location.href) { window.history.pushState(null, '', url.href); }
                filter(); initTables();
                document.dispatchEvent(new Event('acenergo:workspace-ready'));
                if (focusHref) {
                    var links = tree.querySelectorAll('a[href]');
                    var i;
                    for (i = 0; i < links.length; i += 1) {
                        if (links[i].href === focusHref) { links[i].focus({ preventScroll: true }); break; }
                    }
                }
                tree.scrollTop = top;
                window.scrollTo(windowX, windowY);
                scrollHash(url); save();
            }
            var visibleModal = content.querySelector('.modal');
            var instance = visibleModal && window.bootstrap && window.bootstrap.Modal.getInstance(visibleModal);
            // Bootstrap uklízí backdrop až po skrytí samotného dialogu.
            if (instance && (visibleModal.classList.contains('show') || visibleModal.getAttribute('aria-modal') === 'true' || document.body.classList.contains('modal-open'))) {
                visibleModal.addEventListener('hidden.bs.modal', apply, { once: true });
                instance.hide();
            } else { apply(); }
        };
        xhr.onerror = function () { if (current === generation) { error(url); } };
        xhr.ontimeout = xhr.onerror;
        xhr.send();
    }
    workspace.addEventListener('click', function (event) {
        var link = event.target.closest('a[href]');
        if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || link.target || link.hasAttribute('download') || link.hasAttribute('data-native-navigation')) { return; }
        var url = new URL(link.href, window.location.href);
        if (!eligible(url)) { save(); return; }
        event.preventDefault();
        navigate(url, true, tree.contains(link) ? link.href : null);
    });
    window.addEventListener('popstate', function () {
        var url = new URL(window.location.href);
        if (eligible(url)) { navigate(url, false, null); } else { window.location.reload(); }
    });
    window.addEventListener('beforeunload', save);
    window.addEventListener('resize', fitTree);
    input.addEventListener('input', function () { filter(); save(); });
    tree.addEventListener('scroll', save);
    tree.addEventListener('toggle', save, true);
    filter(); restore(); initTables();
}());
