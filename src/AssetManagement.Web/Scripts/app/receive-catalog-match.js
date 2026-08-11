/* eslint-env browser */
/* global window */
(function (global) {
    "use strict";

    function byId(id) {
        return global.document.getElementById(id);
    }

    function showModal(modal) {
        if (!modal) {
            return;
        }

        if (global.bootstrap && global.bootstrap.Modal) {
            global.bootstrap.Modal.getOrCreateInstance(modal).show();
            return;
        }

        if (global.jQuery) {
            global.jQuery(modal).modal("show");
        }
    }

    function initReceiveCatalogMatch() {
        var modal = byId("catalogMatchConfirmModal");
        if (!modal) {
            return;
        }

        var confirmButton = byId("catalog-match-confirm");
        if (confirmButton) {
            confirmButton.addEventListener("click", function () {
                var url = modal.getAttribute("data-am-confirm-url");
                if (url) {
                    global.location.href = url;
                }
            });
        }

        if (modal.getAttribute("data-am-auto-show") === "true") {
            showModal(modal);
        }
    }

    if (global.document.readyState === "loading") {
        global.document.addEventListener("DOMContentLoaded", initReceiveCatalogMatch);
    } else {
        initReceiveCatalogMatch();
    }
})(window);
