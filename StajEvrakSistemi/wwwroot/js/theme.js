(() => {
    const root = document.documentElement;
    const toggle = document.querySelector(".theme-toggle");
    const color = document.querySelector('meta[name="theme-color"]');
    const moon = toggle.querySelector('[data-theme-icon="moon"]');
    const sun = toggle.querySelector('[data-theme-icon="sun"]');
    const system = window.matchMedia?.("(prefers-color-scheme: dark)");
    const storageKey = "staj-theme";

    function readPreference() {
        try {
            const theme = localStorage.getItem(storageKey);
            return theme === "dark" || theme === "light" ? theme : null;
        } catch {
            return null;
        }
    }

    let preference = readPreference();

    function applyTheme(theme) {
        const dark = theme === "dark";
        root.dataset.theme = theme;
        color.content = color.dataset[theme];
        toggle.setAttribute("aria-pressed", String(dark));
        moon.toggleAttribute("hidden", dark);
        sun.toggleAttribute("hidden", !dark);
    }

    function followPreference() {
        applyTheme(preference ?? (system?.matches ? "dark" : "light"));
    }

    toggle.addEventListener("click", () => {
        preference = root.dataset.theme === "dark" ? "light" : "dark";
        applyTheme(preference);
        try { localStorage.setItem(storageKey, preference); } catch { }
    });
    if (typeof system?.addEventListener === "function") {
        system.addEventListener("change", followPreference);
    } else {
        system?.addListener?.(followPreference);
    }
    window.addEventListener("storage", event => {
        if (event.key !== storageKey && event.key !== null) return;
        preference = readPreference();
        followPreference();
    });

    followPreference();
    toggle.hidden = false;
})();
