/* eslint-env browser */

(function () {
    var modalElement = document.getElementById("roleTemplateSaveModal");
    if (!modalElement) {
        return;
    }

    var promptSections = modalElement.querySelectorAll(".js-role-template-prompt");
    var formSection = modalElement.querySelector(".js-role-template-form");
    var yesButton = modalElement.querySelector(".js-role-template-yes");
    var nameInput = modalElement.querySelector("#TemplateName");
    var showFormOnLoad = modalElement.getAttribute("data-show-form") === "true";

    function showPrompt() {
        promptSections.forEach(function (section) {
            section.classList.remove("d-none");
        });
        if (formSection) {
            formSection.classList.add("d-none");
        }
    }

    function showForm() {
        promptSections.forEach(function (section) {
            section.classList.add("d-none");
        });
        if (formSection) {
            formSection.classList.remove("d-none");
        }
        if (nameInput) {
            nameInput.focus();
        }
    }

    if (yesButton) {
        yesButton.addEventListener("click", showForm);
    }

    if (window.bootstrap && window.bootstrap.Modal) {
        var modal = window.bootstrap.Modal.getOrCreateInstance(modalElement);
        modal.show();
        if (showFormOnLoad) {
            showForm();
        }
    }
})();
