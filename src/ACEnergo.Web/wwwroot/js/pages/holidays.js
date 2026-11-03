// Hromadný výběr je progresivní vylepšení. Jednotlivé checkboxy fungují bez JS.
const holidayForm = document.querySelector("[data-holiday-form]");
if (holidayForm instanceof HTMLFormElement) {
    const choices = Array.from(holidayForm.querySelectorAll("[data-holiday-select]"));
    const all = holidayForm.querySelector("[data-holiday-all]");
    const remove = holidayForm.querySelector("[data-holiday-remove]");
    const count = holidayForm.querySelector("[data-holiday-count]");
    const refresh = () => {
        const selected = choices.filter((choice) => choice.checked).length;
        if (all instanceof HTMLInputElement) {
            all.checked = choices.length > 0 && selected === choices.length;
            all.indeterminate = selected > 0 && selected < choices.length;
        }
        if (remove instanceof HTMLButtonElement) remove.disabled = selected === 0;
        if (count instanceof HTMLElement) count.textContent = `Vybraná data: ${selected}`;
    };
    choices.forEach((choice) => choice.addEventListener("change", refresh));
    if (all instanceof HTMLInputElement) {
        all.hidden = false;
        all.addEventListener("change", () => {
            choices.forEach((choice) => { choice.checked = all.checked; });
            refresh();
        });
    }
    refresh();
}
