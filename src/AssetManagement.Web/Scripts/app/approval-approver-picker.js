/* eslint-env browser */

(function () {

    function parseOptions(raw) {

        if (!raw) {

            return [];

        }



        try {

            var parsed = JSON.parse(raw);

            return Array.isArray(parsed) ? parsed : [];

        } catch (error) {

            return [];

        }

    }



    function findRoleGroup(options, roleId) {

        for (var i = 0; i < options.length; i++) {

            if (String(options[i].roleId) === String(roleId)) {

                return options[i];

            }

        }



        return null;

    }



    function findUser(group, userId) {

        if (!group || !group.users) {

            return null;

        }



        for (var i = 0; i < group.users.length; i++) {

            if (String(group.users[i].id) === String(userId)) {

                return group.users[i];

            }

        }



        return null;

    }



    function buildRoleStep(menu, options) {

        var roleStep = menu.querySelector(".am-approver-picker-step-roles");

        if (!roleStep) {

            return;

        }



        var header = roleStep.querySelector(".dropdown-header");

        roleStep.innerHTML = "";

        if (header) {

            roleStep.appendChild(header);

        } else {

            var roleHeader = document.createElement("div");

            roleHeader.className = "dropdown-header";

            roleHeader.textContent = "Select title";

            roleStep.appendChild(roleHeader);

        }



        options.forEach(function (group) {

            var button = document.createElement("button");

            button.type = "button";

            button.className = "dropdown-item am-approver-picker-role";

            button.setAttribute("data-role-id", group.roleId);

            button.setAttribute("data-role-name", group.roleName);

            button.textContent = group.roleName;

            roleStep.appendChild(button);

        });

    }



    function buildUserStep(menu, group) {

        var userStep = menu.querySelector(".am-approver-picker-step-users");

        if (!userStep) {

            return;

        }



        var backButton = userStep.querySelector(".am-approver-picker-back");

        var roleTitle = userStep.querySelector(".am-approver-picker-role-title");

        userStep.innerHTML = "";

        if (backButton) {

            userStep.appendChild(backButton);

        } else {

            backButton = document.createElement("button");

            backButton.type = "button";

            backButton.className = "dropdown-item am-approver-picker-back";

            backButton.textContent = "Back to titles";

            userStep.appendChild(backButton);

        }



        if (!roleTitle) {

            roleTitle = document.createElement("div");

            roleTitle.className = "dropdown-header am-approver-picker-role-title";

            userStep.appendChild(roleTitle);

        } else {

            userStep.appendChild(roleTitle);

        }



        roleTitle.textContent = group.roleName;



        (group.users || []).forEach(function (user) {

            var button = document.createElement("button");

            button.type = "button";

            button.className = "dropdown-item am-approver-picker-user";

            button.setAttribute("data-role-id", group.roleId);

            button.setAttribute("data-role-name", group.roleName);

            button.setAttribute("data-user-id", user.id);

            button.setAttribute("data-user-name", user.name);

            button.textContent = user.name;

            userStep.appendChild(button);

        });

    }



    function showRoleStep(picker) {

        var roleStep = picker.querySelector(".am-approver-picker-step-roles");

        var userStep = picker.querySelector(".am-approver-picker-step-users");

        if (roleStep) {

            roleStep.classList.remove("d-none");

        }

        if (userStep) {

            userStep.classList.add("d-none");

        }

    }



    function showUserStep(picker, group) {

        var menu = picker.querySelector(".am-approver-picker-menu");

        buildUserStep(menu, group);

        var roleStep = picker.querySelector(".am-approver-picker-step-roles");

        var userStep = picker.querySelector(".am-approver-picker-step-users");

        if (roleStep) {

            roleStep.classList.add("d-none");

        }

        if (userStep) {

            userStep.classList.remove("d-none");

        }

    }



    function setSelection(picker, roleId, roleName, userId, userName) {

        var roleInput = picker.querySelector(".am-approver-role-input");

        var userInput = picker.querySelector(".am-approver-user-input");

        var label = picker.querySelector(".am-approver-picker-label");



        if (roleInput) {

            roleInput.value = roleId || "";

        }

        if (userInput) {

            userInput.value = userId || "";

        }

        if (label) {

            label.textContent = roleId && userId

                ? roleName + " — " + userName

                : "-- Select approver --";

        }



        picker.setAttribute("data-role-id", roleId || "");

        picker.setAttribute("data-user-id", userId || "");

    }



    function syncInitialSelection(picker, options) {

        var roleId = picker.getAttribute("data-role-id");

        var userId = picker.getAttribute("data-user-id");

        if (!roleId || !userId) {

            return;

        }



        var group = findRoleGroup(options, roleId);

        var user = findUser(group, userId);

        if (!group || !user) {

            return;

        }



        setSelection(picker, group.roleId, group.roleName, user.id, user.name);

    }



    function wirePicker(picker, options) {

        var menu = picker.querySelector(".am-approver-picker-menu");

        if (!menu) {

            return;

        }



        buildRoleStep(menu, options);

        syncInitialSelection(picker, options);



        picker.addEventListener("click", function (event) {

            var target = event.target;

            if (!target || !target.classList) {

                return;

            }



            if (target.classList.contains("am-approver-picker-role")) {

                event.preventDefault();

                var group = findRoleGroup(options, target.getAttribute("data-role-id"));

                if (group) {

                    showUserStep(picker, group);

                }

                return;

            }



            if (target.classList.contains("am-approver-picker-back")) {

                event.preventDefault();

                showRoleStep(picker);

                return;

            }



            if (target.classList.contains("am-approver-picker-user")) {

                event.preventDefault();

                setSelection(

                    picker,

                    target.getAttribute("data-role-id"),

                    target.getAttribute("data-role-name"),

                    target.getAttribute("data-user-id"),

                    target.getAttribute("data-user-name")

                );

                showRoleStep(picker);



                var toggle = picker.querySelector(".am-approver-picker-toggle");

                if (toggle && window.bootstrap && window.bootstrap.Dropdown) {

                    var instance = window.bootstrap.Dropdown.getInstance(toggle);

                    if (instance) {

                        instance.hide();

                    }

                }

            }

        });



        picker.addEventListener("show.bs.dropdown", function () {

            showRoleStep(picker);

        });

    }



    function createPickerElement(options) {

        var picker = document.createElement("div");

        picker.className = "am-approver-picker dropdown w-100";

        picker.innerHTML =

            '<button type="button" class="form-select text-start am-approver-picker-toggle dropdown-toggle" data-bs-toggle="dropdown" data-bs-auto-close="outside" aria-expanded="false">' +

                '<span class="am-approver-picker-label">-- Select approver --</span>' +

            "</button>" +

            '<input type="hidden" class="am-approver-role-input" value="" />' +

            '<input type="hidden" class="am-approver-user-input" value="" />' +

            '<div class="dropdown-menu am-approver-picker-menu w-100 p-0 shadow">' +

                '<div class="am-approver-picker-step-roles"><div class="dropdown-header">Select title</div></div>' +

                '<div class="am-approver-picker-step-users d-none">' +

                    '<button type="button" class="dropdown-item am-approver-picker-back">Back to titles</button>' +

                    '<div class="dropdown-header am-approver-picker-role-title"></div>' +

                "</div>" +

            "</div>";



        wirePicker(picker, options);

        return picker;

    }



    window.ApprovalApproverPicker = {

        parseOptions: parseOptions,

        wirePicker: wirePicker,

        createPickerElement: createPickerElement,

        setSelection: setSelection

    };



    document.querySelectorAll(".am-approver-picker").forEach(function (picker) {

        var matrix = picker.closest(".am-approval-matrix");

        var options = parseOptions(matrix ? matrix.getAttribute("data-approver-options") : "[]");

        wirePicker(picker, options);

    });

})();

