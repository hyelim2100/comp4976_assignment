// Limits a position drop-down to the positions of the selected municipality.
// Markup: <select data-filters-positions="#PositionId"> on the municipality drop-down, and
// <option data-municipality="1"> on each position option (options without it are always shown).
document.querySelectorAll("[data-filters-positions]").forEach(function (municipalitySelect) {
    var positionSelect = document.querySelector(municipalitySelect.getAttribute("data-filters-positions"));
    if (!positionSelect) {
        return;
    }

    var allOptions = Array.prototype.map.call(positionSelect.options, function (option) {
        return option.cloneNode(true);
    });

    function apply() {
        var municipalityId = municipalitySelect.value;
        var selected = positionSelect.value;
        positionSelect.innerHTML = "";
        allOptions.forEach(function (option) {
            var owner = option.getAttribute("data-municipality");
            if (!owner || owner === municipalityId) {
                positionSelect.appendChild(option.cloneNode(true));
            }
        });
        var stillAvailable = Array.prototype.some.call(positionSelect.options, function (option) {
            return option.value === selected;
        });
        positionSelect.value = stillAvailable ? selected : "";
    }

    municipalitySelect.addEventListener("change", apply);
    apply();
});
