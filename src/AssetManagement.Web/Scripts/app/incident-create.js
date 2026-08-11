(function () {
    var typeSelect = document.getElementById("IncidentType");
    var photoSection = document.getElementById("incidentDamagePhotoSection");
    if (!typeSelect || !photoSection) {
        return;
    }

    var photoRequiredTypes = {
        Damaged: true,
        FireDamage: true,
        WaterDamage: true,
        Accident: true,
        Negligence: true,
        Misuse: true
    };

    function syncPhotoSection() {
        var selectedType = typeSelect.value;
        var showPhoto = !!photoRequiredTypes[selectedType];
        photoSection.hidden = !showPhoto;
    }

    typeSelect.addEventListener("change", syncPhotoSection);
    syncPhotoSection();
})();
