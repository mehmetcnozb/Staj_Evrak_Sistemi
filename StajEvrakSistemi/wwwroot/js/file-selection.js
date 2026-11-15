const fileSizes = new Intl.NumberFormat("tr-TR", { maximumFractionDigits: 1 });

function fileSize(bytes) {
    if (bytes < 1024) return `${fileSizes.format(bytes)} B`;
    if (bytes < 1024 * 1024) return `${fileSizes.format(bytes / 1024)} KB`;
    return `${fileSizes.format(bytes / (1024 * 1024))} MB`;
}

for (const picker of document.querySelectorAll("[data-file-picker]")) {
    const input = picker.querySelector('input[type="file"]');
    const selection = picker.querySelector("[data-file-selection]");
    const clear = picker.querySelector("[data-file-clear]");
    if (!input || !selection) continue;

    function showSelection() {
        const file = input.files[0];
        selection.textContent = file ? `Seçilen dosya: ${file.name} · ${fileSize(file.size)}` : "";
        if (clear) clear.hidden = !file;
    }

    input.addEventListener("change", showSelection);
    clear?.addEventListener("click", () => {
        input.value = "";
        showSelection();
        selection.textContent = "Dosya seçimi kaldırıldı.";
        input.focus();
    });
    showSelection();
}
