/* eslint-env browser */
/* global window */
(function (global) {
    "use strict";

    var temporaryType = "Temporary";

    function syncExpectedReturnDateVisibility() {
        var assignmentType = document.getElementById("AssignmentType");
        var group = document.getElementById("ExpectedReturnDateGroup");
        var returnDate = document.getElementById("ExpectedReturnDate");
        if (!assignmentType || !group) {
            return;
        }

        var show = assignmentType.value === temporaryType;
        group.classList.toggle("d-none", !show);
        group.hidden = !show;

        if (returnDate) {
            if (!show) {
                returnDate.value = "";
            }

            if (show) {
                returnDate.setAttribute("required", "required");
            } else {
                returnDate.removeAttribute("required");
            }
        }
    }

    function initAssignmentReturnDateToggle() {
        var assignmentType = document.getElementById("AssignmentType");
        if (!assignmentType) {
            return;
        }

        assignmentType.addEventListener("change", syncExpectedReturnDateVisibility);
        syncExpectedReturnDateVisibility();
    }

    global.AmAssignmentReturnDateToggle = {
        init: initAssignmentReturnDateToggle,
        sync: syncExpectedReturnDateVisibility
    };

    if (global.document.readyState === "loading") {
        global.document.addEventListener("DOMContentLoaded", initAssignmentReturnDateToggle);
    } else {
        initAssignmentReturnDateToggle();
    }
})(window);
