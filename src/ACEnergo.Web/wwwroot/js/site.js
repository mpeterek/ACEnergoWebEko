// Sdílené chování bez inline obsluh událostí v HTML.
// Skript se načítá s defer; formuláře fungují i bez JavaScriptu.
const autofocusTarget = document.querySelector("[data-autofocus]");
if (autofocusTarget instanceof HTMLElement &&
    (document.activeElement === document.body || document.activeElement === null)) {
    autofocusTarget.focus();
}
