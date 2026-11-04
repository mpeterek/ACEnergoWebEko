const passwordToggle = document.querySelector("[data-password-toggle]");
if (passwordToggle instanceof HTMLButtonElement) {
    const password = document.getElementById(passwordToggle.getAttribute("aria-controls"));
    if (password instanceof HTMLInputElement) {
        passwordToggle.hidden = false;
        passwordToggle.addEventListener("click", () => {
            const visible = password.type === "password";
            password.type = visible ? "text" : "password";
            const label = visible ? "Skrýt heslo" : "Zobrazit heslo";
            passwordToggle.setAttribute("aria-label", label);
            passwordToggle.title = label;
            const icon = passwordToggle.querySelector("[data-password-icon]");
            if (icon instanceof HTMLImageElement) icon.src = visible ? passwordToggle.dataset.visibleIcon : passwordToggle.dataset.hiddenIcon;
            passwordToggle.setAttribute("aria-pressed", String(visible));
        });
    }
}
