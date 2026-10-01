/* eslint-env browser */
(function () {
    function cleanupStaleModalState() {
        var openModals = document.querySelectorAll(".modal.show");
        if (openModals.length > 0) {
            return;
        }

        document.querySelectorAll(".modal-backdrop").forEach(function (backdrop) {
            backdrop.remove();
        });
        document.body.classList.remove("modal-open");
        document.body.style.removeProperty("overflow");
        document.body.style.removeProperty("padding-right");
    }

    function ensureModalInBody(modal) {
        if (!modal || modal.parentElement === document.body) {
            return;
        }

        document.body.appendChild(modal);
    }

    function showModal(modal) {
        if (!modal) {
            return;
        }

        ensureModalInBody(modal);
        cleanupStaleModalState();

        if (window.bootstrap && window.bootstrap.Modal) {
            window.bootstrap.Modal.getOrCreateInstance(modal).show();
        }
    }

    function hideModal(modal) {
        if (!modal || !window.bootstrap || !window.bootstrap.Modal) {
            return;
        }

        window.bootstrap.Modal.getOrCreateInstance(modal).hide();
    }

    function initRoomParentOtherModal(config) {
        var select = document.getElementById("room-parent-select");
        var hidden = document.getElementById("room-parent-id-hidden");
        var otherValue = config.otherValue;
        var modalEl = document.getElementById("roomParentOtherModal");
        var adminSelect = document.getElementById("room-other-admin");
        var subSelect = document.getElementById("room-other-sub");
        var applyBtn = document.getElementById("room-other-apply");

        if (!select || !hidden || !otherValue) {
            return;
        }

        ensureModalInBody(modalEl);

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

        select.addEventListener("change", function () {
            if (select.value === otherValue) {
                appliedFromModal = false;
                showModal(modalEl);
                return;
            }
            syncHiddenFromSelect();
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
                hideModal(modalEl);
            });
        }

        if (modalEl) {
            modalEl.addEventListener("hidden.bs.modal", function () {
                cleanupStaleModalState();
                if (!appliedFromModal && select.value === otherValue) {
                    restoreSelectFromCommitted();
                }
            });
        }

        syncHiddenFromSelect();
        if (select.value && select.value !== otherValue) {
            lastCommittedValue = select.value;
        } else if (hidden.value) {
            lastCommittedValue = hidden.value;
        }
    }

    window.AmRoomParentOtherModal = {
        init: initRoomParentOtherModal
    };
})();
