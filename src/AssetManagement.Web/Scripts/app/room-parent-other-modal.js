/* eslint-env browser */
(function () {
    var MODAL_Z_INDEX = 1060;
    var BACKDROP_Z_INDEX = 1055;
    var BACKDROP_ATTR = "data-am-room-parent-backdrop";

    function purgeModalArtifacts() {
        document.querySelectorAll(".modal-backdrop, [" + BACKDROP_ATTR + "]").forEach(function (node) {
            node.remove();
        });
        document.body.classList.remove("modal-open", "am-room-parent-modal-open");
        document.body.style.removeProperty("overflow");
        document.body.style.removeProperty("padding-right");
    }

    function ensureModalInBody(modal) {
        if (!modal || modal.parentElement === document.body) {
            return;
        }

        document.body.appendChild(modal);
    }

    function disposeBootstrapModal(modal) {
        if (!modal || !window.bootstrap || !window.bootstrap.Modal) {
            return;
        }

        var instance = window.bootstrap.Modal.getInstance(modal);
        if (instance) {
            instance.dispose();
        }
    }

    function closeSidebarOverlay() {
        document.body.classList.remove("am-sidebar-open");
        var sidebar = document.getElementById("amSidebar");
        var overlay = document.getElementById("amSidebarOverlay");
        if (sidebar) {
            sidebar.setAttribute("aria-hidden", "true");
        }
        if (overlay) {
            overlay.setAttribute("aria-hidden", "true");
        }
    }

    function openControlledModal(modal, onBackdropDismiss) {
        if (!modal) {
            return;
        }

        disposeBootstrapModal(modal);
        purgeModalArtifacts();
        closeSidebarOverlay();
        ensureModalInBody(modal);

        document.body.classList.add("modal-open", "am-room-parent-modal-open");

        var backdrop = document.createElement("div");
        backdrop.className = "modal-backdrop fade show am-room-parent-backdrop";
        backdrop.setAttribute(BACKDROP_ATTR, "true");
        backdrop.style.zIndex = String(BACKDROP_Z_INDEX);
        backdrop.style.opacity = "0.5";
        backdrop.style.backgroundColor = "#000";
        backdrop.addEventListener("click", function () {
            if (typeof onBackdropDismiss === "function") {
                onBackdropDismiss();
            }
        });
        document.body.appendChild(backdrop);

        modal.classList.add("show");
        modal.style.display = "block";
        modal.style.zIndex = String(MODAL_Z_INDEX);
        modal.removeAttribute("aria-hidden");
        modal.setAttribute("aria-modal", "true");
        modal.setAttribute("role", "dialog");
    }

    function closeControlledModal(modal) {
        if (modal) {
            modal.classList.remove("show");
            modal.style.display = "none";
            modal.setAttribute("aria-hidden", "true");
            modal.removeAttribute("aria-modal");
            modal.removeAttribute("role");
        }

        purgeModalArtifacts();
    }

    function initRoomParentOtherModal(config) {
        var select = document.getElementById("room-parent-select");
        var hidden = document.getElementById("room-parent-id-hidden");
        var otherValue = config.otherValue;
        var modalEl = document.getElementById("roomParentOtherModal");
        var adminSelect = document.getElementById("room-other-admin");
        var subSelect = document.getElementById("room-other-sub");
        var applyBtn = document.getElementById("room-other-apply");

        if (!select || !hidden || !otherValue || !modalEl) {
            return;
        }

        ensureModalInBody(modalEl);
        disposeBootstrapModal(modalEl);

        var lastCommittedValue = select.value === otherValue ? hidden.value || "" : select.value;
        var appliedFromModal = false;

        function ensureCandidateOption(id, label) {
            var idText = String(id);
            var match = Array.prototype.find.call(select.options, function (opt) {
                return opt.value === idText;
            });
            if (!match) {
                var option = document.createElement("option");
                option.value = idText;
                option.textContent = label;
                var otherOption = select.querySelector('option[value="' + otherValue + '"]');
                select.insertBefore(option, otherOption);
            }
            select.value = idText;
        }

        function syncHiddenFromSelect() {
            if (select.value === otherValue) {
                return;
            }
            hidden.value = select.value || "";
            lastCommittedValue = select.value || "";
        }

        function restoreSelectFromCommitted() {
            if (lastCommittedValue) {
                var idText = String(lastCommittedValue);
                var existing = Array.prototype.find.call(select.options, function (opt) {
                    return opt.value === idText;
                });
                var label = existing ? existing.textContent : idText;
                ensureCandidateOption(lastCommittedValue, label);
                hidden.value = lastCommittedValue;
                return;
            }

            select.value = "";
            hidden.value = "";
        }

        function dismissModal() {
            appliedFromModal = false;
            closeControlledModal(modalEl);
            if (select.value === otherValue) {
                restoreSelectFromCommitted();
            }
        }

        select.addEventListener("change", function () {
            if (select.value === otherValue) {
                appliedFromModal = false;
                openControlledModal(modalEl, dismissModal);
                return;
            }
            syncHiddenFromSelect();
        });

        modalEl.querySelectorAll("[data-am-room-parent-dismiss]").forEach(function (button) {
            button.addEventListener("click", function (event) {
                event.preventDefault();
                dismissModal();
            });
        });

        document.addEventListener("keydown", function (event) {
            if (event.key === "Escape" && modalEl.classList.contains("show")) {
                event.preventDefault();
                dismissModal();
            }
        });

        if (adminSelect && subSelect) {
            adminSelect.addEventListener("change", function () {
                var adminId = adminSelect.value;
                Array.prototype.forEach.call(subSelect.options, function (opt) {
                    if (!opt.value) {
                        opt.hidden = false;
                        return;
                    }
                    opt.hidden = opt.getAttribute("data-admin-id") !== adminId;
                });
                subSelect.value = "";
            });
        }

        if (applyBtn) {
            applyBtn.addEventListener("click", function () {
                var adminId = adminSelect ? adminSelect.value : "";
                var subId = subSelect ? subSelect.value : "";
                var parentId = subId || adminId;
                if (!parentId) {
                    return;
                }
                var label = subId
                    ? subSelect.options[subSelect.selectedIndex].text
                    : adminSelect.options[adminSelect.selectedIndex].text;
                hidden.value = parentId;
                ensureCandidateOption(parentId, label);
                lastCommittedValue = parentId;
                appliedFromModal = true;
                closeControlledModal(modalEl);
            });
        }

        syncHiddenFromSelect();
        if (select.value && select.value !== otherValue) {
            lastCommittedValue = select.value;
        } else if (hidden.value) {
            lastCommittedValue = hidden.value;
        }

        window.addEventListener("pagehide", function () {
            closeControlledModal(modalEl);
        });
    }

    window.AmRoomParentOtherModal = {
        init: initRoomParentOtherModal,
        purgeModalArtifacts: purgeModalArtifacts
    };
})();
