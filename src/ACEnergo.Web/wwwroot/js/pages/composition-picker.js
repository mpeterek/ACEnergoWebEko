(function ($) {
    'use strict';
    function initialize() {
    var modal = document.getElementById('composition-picker');
    if (!modal || !$ || modal.getAttribute('data-picker-ready')) { return; }
    modal.setAttribute('data-picker-ready', 'true');
    var category = modal.getAttribute('data-picker-category');
    var term = modal.querySelector('#picker-term');
    var type = modal.querySelector('#picker-type');
    var station = modal.querySelector('#picker-station');
    function pad(value) {
        var text = String(value);
        while (text.length < 4) { text = '0' + text; }
        return text;
    }
    var body = modal.querySelector('[data-picker-rows]');
    var status = modal.querySelector('[data-picker-status]');
    var results = modal.querySelector('#picker-results');
    var more = modal.querySelector('[data-picker-more]');
    var table = null;
    var request = null;
    var afterId = 0;
    var total = 0;
    function cell(row, value) {
        var td = document.createElement('td');
        td.textContent = value;
        row.appendChild(td);
        return td;
    }
    function load(append) {
        var restoreFocus = append && document.activeElement === more;
        var firstNewButton = null;
        if (request) { request.abort(); }
        if (!append) {
            afterId = 0; total = 0;
            if (table) { table.clear().draw(); } else { body.textContent = ''; }
        }
        status.textContent = 'Načítám položky…';
        results.setAttribute('aria-busy', 'true');
        more.hidden = true;
        request = $.ajax({
            url: modal.getAttribute('data-picker-url'), dataType: 'json',
            data: { category: category, term: term.value, inputType: type.value, station: station.value, afterId: afterId }
        }).done(function (data) {
            var selected = type.value;
            var selectedStation = station.value;
            var i;
            type.textContent = '';
            type.appendChild(new Option('Všechny elektrické typy', ''));
            for (i = 0; i < data.types.length; i += 1) { type.appendChild(new Option(data.types[i], data.types[i])); }
            type.value = selected;
            station.textContent = '';
            station.appendChild(new Option('Všechny stanice', ''));
            var stations = data.stations || [];
            for (i = 0; i < stations.length; i += 1) { station.appendChild(new Option(pad(stations[i]), String(stations[i]))); }
            station.value = selectedStation;
            for (i = 0; i < data.rows.length; i += 1) {
                var item = data.rows[i];
                var row = document.createElement('tr');
                cell(row, String(item.id)); cell(row, item.station === null ? "—" : pad(item.station)); cell(row, pad(item.input)); cell(row, item.name); cell(row, item.type);
                cell(row, item.powerUnit); cell(row, item.energyUnit);
                var action = cell(row, '');
                if (item.assigned) {
                    var badge = document.createElement('span');
                    badge.className = 'badge'; badge.textContent = 'Již přiřazeno'; action.appendChild(badge);
                } else {
                    var button = document.createElement('button');
                    button.type = 'submit'; button.className = 'btn btn-primary btn-sm';
                    button.setAttribute('form', 'picker-add-form'); button.name = 'ChildId'; button.value = item.id;
                    button.textContent = 'Přidat'; button.setAttribute('aria-label', 'Přidat ' + item.name);
                    action.appendChild(button);
                    if (!firstNewButton) { firstNewButton = button; }
                }
                if (table) { table.row.add(row); } else { body.appendChild(row); }
                afterId = item.id; total += 1;
            }
            if (table) { table.draw(false); }
            status.textContent = total ? 'Zobrazeno položek: ' + total + (data.hasMore ? '. Další položky načtete tlačítkem níže.' : '.') : 'Žádné položky neodpovídají výběru.';
            more.hidden = !data.hasMore;
            if (restoreFocus) {
                status.tabIndex = -1;
                (firstNewButton || status).focus();
            }
        }).fail(function (xhr, reason) {
            if (reason === 'abort') { return; }
            if (restoreFocus) { status.tabIndex = -1; status.focus(); }
            status.textContent = xhr.status === 401 || xhr.status === 403 ? 'Seznam nelze načíst. Ověřte přihlášení a oprávnění.' : 'Seznam se nepodařilo načíst. Zkuste hledání znovu.';
        }).always(function () { results.setAttribute('aria-busy', 'false'); });
    }
    function activate(tab) {
        var tabs = modal.querySelectorAll('[data-picker-tab]');
        var i;
        for (i = 0; i < tabs.length; i += 1) {
            var active = tabs[i] === tab;
            tabs[i].classList.toggle('active', active);
            tabs[i].setAttribute('aria-selected', active ? 'true' : 'false');
            tabs[i].tabIndex = active ? 0 : -1;
        }
        category = tab.getAttribute('data-picker-tab');
        station.value = '';
        results.setAttribute('aria-labelledby', tab.id);
        load(false);
    }
    modal.addEventListener('shown.bs.modal', function () {
        if (!table && window.DataTable) {
            table = new window.DataTable(modal.querySelector('#picker-table'), {
                paging: false, searching: false, ordering: true, order: [], info: false, autoWidth: false,
                columnDefs: [{ targets: 7, orderable: false }],
                layout: { topStart: null, topEnd: null, bottomStart: null, bottomEnd: null },
                language: { emptyTable: 'Seznam je prázdný.' }
            });
        }
        load(false);
        // AdminLTE rovněž nastavuje fokus při otevření; hledání zaměříme po této obsluze.
        window.setTimeout(function () { if (modal.classList.contains('show')) { term.focus(); } }, 0);
    });
    modal.addEventListener('hidden.bs.modal', function () { if (request) { request.abort(); } });
    $(modal).on('click', '[data-picker-tab]', function () { activate(this); });
    $(modal).on('keydown', '[data-picker-tab]', function (event) {
        var tabs = modal.querySelectorAll('[data-picker-tab]');
        var index;
        if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') { index = this === tabs[0] ? 1 : 0; }
        else if (event.key === 'Home') { index = 0; }
        else if (event.key === 'End') { index = tabs.length - 1; }
        else { return; }
        event.preventDefault(); event.stopPropagation(); tabs[index].focus(); activate(tabs[index]);
    });
    $(modal).find('[data-picker-search]').on('submit', function (event) { event.preventDefault(); load(false); });
    station.addEventListener('change', function () { load(false); });
    type.addEventListener('change', function () { load(false); });
    more.addEventListener('click', function () { load(true); });
    modal.addEventListener('acenergo:dispose', function () {
        if (request) { request.abort(); }
        if (table) { table.destroy(); }
        $(modal).off();
    });
    }
    document.addEventListener('acenergo:workspace-ready', initialize);
    initialize();
}(window.jQuery));
