const passwordToggle = document.querySelector("[data-password-toggle]");
if (passwordToggle instanceof HTMLButtonElement) {
    const password = document.getElementById(passwordToggle.getAttribute("aria-controls"));
    if (password instanceof HTMLInputElement) {
        passwordToggle.hidden = false;
        passwordToggle.addEventListener("click", () => {
            const visible = password.type === "password";
            password.type = visible ? "text" : "password";
            passwordToggle.textContent = visible ? "Skrýt heslo" : "Zobrazit heslo";
            passwordToggle.setAttribute("aria-pressed", String(visible));
        });
    }
}
