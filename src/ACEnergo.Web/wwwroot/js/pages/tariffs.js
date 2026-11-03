// Změny a barvy tarifů jsou progresivní vylepšení; formulář funguje i bez JS.
const editor = document.querySelector("[data-tariff-editor]");
if (editor instanceof HTMLFormElement) {
    let dirty = false;
    const notice = editor.querySelector("[data-unsaved-notice]");
    const markDirty = () => {
        dirty = true;
        if (notice instanceof HTMLElement) notice.hidden = false;
    };
    editor.querySelectorAll("select[data-tariff-cell]").forEach((cell) => {
        const names = Array.from(cell.options, (option) => option.textContent);
        const button = document.createElement("button");
        button.type = "button";
        button.className = "tariff-cycle";
        button.setAttribute("data-tariff-cell", "");
        const label = editor.querySelector(`label[for="${cell.id}"]`);
        const context = label?.textContent.trim() || "Tarif";
        const refresh = () => {
            const value = Number(cell.value);
            button.textContent = names[value];
            button.classList.remove("tariff-value-0", "tariff-value-1", "tariff-value-2");
            button.classList.add(`tariff-value-${value}`);
            button.setAttribute("aria-label", `${context}: ${names[value]}. Kliknutím přepnout tarif.`);
            button.title = `${context}: ${names[value]}`;
        };
        button.addEventListener("click", () => {
            cell.value = String((Number(cell.value) + 1) % cell.options.length);
            refresh();
            markDirty();
        });
        cell.addEventListener("change", () => { refresh(); markDirty(); });
        cell.hidden = true;
        cell.removeAttribute("data-tariff-cell");
        cell.closest("td")?.classList.add("tariff-interactive");
        cell.after(button);
        refresh();
    });
    editor.addEventListener("submit", () => { dirty = false; });
    window.addEventListener("beforeunload", (event) => {
        if (dirty) {
            event.preventDefault();
            event.returnValue = "";
        }
    });
}
