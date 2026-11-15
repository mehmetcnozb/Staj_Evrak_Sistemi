const studentList = document.getElementById("student-application-list");
const filters = Array.from(document.querySelectorAll("[data-status-filter]"));
const table = studentList?.querySelector("[data-filter-table]");
const count = studentList?.querySelector("[data-filter-result]");
const empty = studentList?.querySelector("[data-filter-empty]");
const reset = studentList?.querySelector("[data-reset-filter]");
const all = filters.find(button => button.dataset.statusFilter === "all");

if (table && count && empty && reset && all) {
    const rows = Array.from(table.querySelectorAll("[data-application-status]"));

    function applyFilter(filter) {
        const selected = filter.dataset.statusFilter;
        let visibleCount = 0;

        for (const row of rows) {
            row.hidden = selected !== "all" && row.dataset.applicationStatus !== selected;
            if (!row.hidden) visibleCount++;
        }
        for (const button of filters) {
            button.setAttribute("aria-pressed", String(button === filter));
        }
        table.hidden = visibleCount === 0;
        empty.hidden = visibleCount !== 0;
        count.textContent = selected === "all"
            ? `${visibleCount} başvuru`
            : `${filter.dataset.filterLabel} · ${visibleCount} başvuru`;
    }

    for (const filter of filters) {
        filter.hidden = false;
        filter.addEventListener("click", () => applyFilter(filter));
    }
    reset.addEventListener("click", () => {
        applyFilter(all);
        all.focus();
    });
}
