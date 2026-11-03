// Lokální DataTables; bez stránkování a scrollX, aby zůstalo všech 12 měsíců viditelných.
if (typeof DataTable !== "undefined") {
    document.querySelectorAll("[data-price-table]").forEach((table) => {
        new DataTable(table, {
            paging: false, info: false, ordering: false, autoWidth: false,
            layout: { topStart: null, topEnd: "search", bottomStart: null, bottomEnd: null },
            language: { search: "Hledat položku:", zeroRecords: "Žádná odpovídající položka", emptyTable: "Nejsou dostupné ceny" }
        });
    });
}
