for (const [mode, target] of [["bNedocerpMWhProc", "numProcNedocerp"], ["bPrecerpMWhProc", "numProcPrecerp"]]) {
    const select = document.querySelector(`[data-unit-mode="${mode}"]`);
    const unit = document.querySelector(`[data-unit-for="${target}"]`);
    if (select && unit) select.addEventListener("change", () => {
        unit.textContent = select.value === "1" ? "MWh" : select.value === "0" ? "%" : "Neuvedeno";
    });
}
