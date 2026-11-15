for (const toggle of document.querySelectorAll("[data-password-toggle]")) {
    const input = document.getElementById(toggle.getAttribute("aria-controls"));
    if (!(input instanceof HTMLInputElement) || input.type !== "password") continue;

    toggle.hidden = false;
    toggle.addEventListener("click", () => {
        const reveal = input.type === "password";
        input.type = reveal ? "text" : "password";
        toggle.textContent = reveal ? "Gizle" : "Göster";
        toggle.setAttribute("aria-label", reveal ? "Şifreyi gizle" : "Şifreyi göster");
    });
}
