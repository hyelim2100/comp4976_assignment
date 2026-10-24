// Candidate comparison: remembers ticked "Compare" checkboxes (data-compare-id) for this browser tab
// and shows the floating compare bar from the _CompareBar partial.
(function () {
    var bar = document.getElementById("compare-bar");
    if (!bar) {
        return;
    }

    var key = "compareIds";
    var max = parseInt(bar.getAttribute("data-compare-max"), 10) || 4;
    var baseUrl = bar.getAttribute("data-compare-url");

    function load() {
        try {
            var ids = JSON.parse(sessionStorage.getItem(key) || "[]");
            return Array.isArray(ids) ? ids.slice(0, max) : [];
        } catch (e) {
            return [];
        }
    }

    function save(ids) {
        try { sessionStorage.setItem(key, JSON.stringify(ids)); } catch (e) { }
    }

    var ids = load();

    function render() {
        document.querySelectorAll("[data-compare-id]").forEach(function (box) {
            box.checked = ids.indexOf(box.getAttribute("data-compare-id")) >= 0;
            box.disabled = !box.checked && ids.length >= max;
        });
        bar.classList.toggle("d-none", ids.length === 0);
        bar.querySelector("[data-compare-count]").textContent = ids.length;
        var link = bar.querySelector("[data-compare-link]");
        link.href = baseUrl + "?" + ids.map(function (id) { return "ids=" + encodeURIComponent(id); }).join("&");
        link.classList.toggle("disabled", ids.length < 2);
        link.setAttribute("aria-disabled", ids.length < 2 ? "true" : "false");
    }

    document.addEventListener("change", function (event) {
        var box = event.target.closest ? event.target.closest("[data-compare-id]") : null;
        if (!box) {
            return;
        }
        var id = box.getAttribute("data-compare-id");
        ids = ids.filter(function (x) { return x !== id; });
        if (box.checked && ids.length < max) {
            ids.push(id);
        }
        save(ids);
        render();
    });

    bar.querySelector("[data-compare-clear]").addEventListener("click", function () {
        ids = [];
        save(ids);
        render();
    });

    render();
})();
