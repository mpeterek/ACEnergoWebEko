(function () {
    'use strict';
    var root = document.querySelector('[data-submeter-tree]');
    if (!root) { return; }
    var tree = root.querySelector('[data-measurement-tree]');
    var search = root.querySelector('[data-tree-search]');
    var station = root.querySelector('[data-submeter-station]');
    var type = root.querySelector('[data-submeter-type]');
    var more = root.querySelector('[data-submeter-more]');
    var status = root.querySelector('[data-submeter-status]');
    var timer = null;
    var pending = null;
    var generation = 0;
    var next = Number(root.getAttribute('data-next-id')) || 0;
    var retry = null;
    function item(row) {
        var id = Number(row.id);
        if (id <= 0 || id >= 50000000 || id % 1 !== 0) { throw new Error('Invalid input ID'); }
        var li = document.createElement('li');
        li.setAttribute('data-tree-item', ''); li.setAttribute('data-group-id', String(id));
        li.setAttribute('data-search-text', id + ' ' + row.name + ' ' + row.type);
        var details = document.createElement('details');
        var summary = document.createElement('summary');
        var link = document.createElement('a');
        var url = root.getAttribute('data-detail-url').replace(/\/0$/, '/' + id);
        link.href = url; link.title = row.name;
        function four(value) { return ('0000' + value).slice(-4); }
        link.textContent = four(Math.floor(id / 10000)) + '/' + four(id % 10000) + ' ' + row.name;
        summary.appendChild(link); details.appendChild(summary);
        var children = document.createElement('ul'); children.className = 'tree-components';
        var routes = [{ url: url, label: 'Informace o vstupu' }, { url: url.replace('/ElectricitySubmeter/', '/ElectricitySubmeterEvaluation/'), label: 'Vyhodnocení spotřeby' }];
        var i;
        for (i = 0; i < routes.length; i += 1) {
            var child = document.createElement('li'); var a = document.createElement('a');
            a.href = routes[i].url; a.textContent = routes[i].label; child.appendChild(a); children.appendChild(child);
        }
        details.appendChild(children); li.appendChild(details); return li;
    }
    function load(append) {
        if (pending) { pending.abort(); }
        var current = ++generation;
        var xhr = new XMLHttpRequest(); pending = xhr;
        var url = new URL(root.getAttribute('data-tree-url'), window.location.href);
        url.searchParams.set('Station', station.value); url.searchParams.set('InputType', type.value);
        url.searchParams.set('Search', search.value); url.searchParams.set('AfterId', append ? next : 0);
        status.textContent = 'Načítám měření…'; more.disabled = true;
        function failed() {
            if (current !== generation) { return; }
            status.textContent = 'Seznam se nepodařilo načíst. Původní položky zůstaly zachované.';
            more.hidden = false; more.disabled = false; more.textContent = 'Zkusit načtení znovu'; retry = append;
        }
        xhr.open('GET', url.href, true); xhr.timeout = 45000;
        xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
        xhr.onload = function () {
            if (current !== generation) { return; }
            if (xhr.status === 401 || xhr.status === 403) { window.location.reload(); return; }
            if (xhr.status !== 200) { failed(); return; }
            try {
                var data = JSON.parse(xhr.responseText);
                if (!Array.isArray(data.rows) || typeof data.hasMore !== 'boolean') { throw new Error('Invalid list'); }
                var selectedLink = tree.querySelector('summary [aria-current="page"]');
                var selectedNode = selectedLink ? selectedLink.closest('[data-group-id]') : null;
                var fragment = document.createDocumentFragment(); var i;
                for (i = 0; i < data.rows.length; i += 1) { fragment.appendChild(selectedNode && Number(selectedNode.getAttribute('data-group-id')) === Number(data.rows[i].id) ? selectedNode.cloneNode(true) : item(data.rows[i])); }
                if (!append) { tree.textContent = ''; }
                // Detail může při souběžném načítání už doplnit právě vybraný vstup.
                var children = Array.prototype.slice.call(fragment.childNodes);
                for (i = 0; i < children.length; i += 1) {
                    if (!tree.querySelector('[data-group-id="' + children[i].getAttribute('data-group-id') + '"]')) { tree.appendChild(children[i]); }
                }
                next = data.rows.length ? Number(data.rows[data.rows.length - 1].id) : 0;
                more.hidden = !data.hasMore; more.disabled = false; more.textContent = 'Načíst další měření'; retry = null;
                status.textContent = tree.querySelectorAll('[data-tree-item]').length + ' načtených měření' + (data.hasMore ? ' · další jsou dostupná' : '');
                document.dispatchEvent(new Event('acenergo:tree-ready'));
            } catch (ignore) { failed(); }
        };
        xhr.onerror = failed; xhr.ontimeout = failed; xhr.send();
    }
    function filter() {
        window.clearTimeout(timer);
        if (pending) { pending.abort(); pending = null; generation += 1; }
        timer = window.setTimeout(function () { load(false); }, 250);
    }
    search.addEventListener('input', filter); station.addEventListener('change', filter); type.addEventListener('change', filter);
    root.querySelector('[data-submeter-filter]').addEventListener('submit', function (event) { event.preventDefault(); window.clearTimeout(timer); load(false); });
    more.addEventListener('click', function () { window.clearTimeout(timer); load(retry === null ? true : retry); });
    if (search.value) { load(false); }
}());
