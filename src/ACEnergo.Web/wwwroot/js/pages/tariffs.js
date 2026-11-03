// Změny a barvy tarifů jsou progresivní vylepšení; formulář funguje i bez JS.
const editor = document.querySelector("[data-tariff-editor]");
if (editor instanceof HTMLFormElement) {
    let dirty = false;
    const notice = editor.querySelector("[data-unsaved-notice]");
    const markDirty = () => {
        dirty = true;
        if (notice instanceof HTMLElement) notice.hidden = false;
    };
    editor.querySelectorAll("[data-tariff-cell]").forEach((cell) => {
        cell.addEventListener("change", () => {
            cell.classList.remove("tariff-value-0", "tariff-value-1", "tariff-value-2");
            if (["0", "1", "2"].includes(cell.value)) cell.classList.add(`tariff-value-${cell.value}`);
            markDirty();
        });
    });
    editor.addEventListener("submit", () => { dirty = false; });
    window.addEventListener("beforeunload", (event) => {
        if (dirty) {
            event.preventDefault();
            event.returnValue = "";
        }
    });
}
