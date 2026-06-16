(function () {
    var sidebar = document.getElementById("amSidebar");
    if (!sidebar) {
        return;
    }

    var modules = sidebar.querySelectorAll(".am-nav-module");
    if (!modules.length) {
        return;
    }

    function setAriaExpanded(module, expanded) {
        var chevronBtn = module.querySelector(".am-nav-module-chevron-btn");
        if (chevronBtn) {
            chevronBtn.setAttribute("aria-expanded", expanded ? "true" : "false");
        }
    }

    for (var i = 0; i < modules.length; i++) {
        (function (module) {
            if (module.querySelector(".nav-link.active")) {
                module.classList.add("has-active");
                module.classList.add("is-expanded");
                setAriaExpanded(module, true);
            }

            var chevronBtn = module.querySelector(".am-nav-module-chevron-btn");
            if (!chevronBtn) {
                return;
            }

            module.addEventListener("mouseenter", function () {
                if (!module.classList.contains("is-click-collapsed")) {
                    setAriaExpanded(module, true);
                }
            });

            module.addEventListener("mouseleave", function () {
                module.classList.remove("is-click-collapsed");
                if (!module.classList.contains("is-expanded")) {
                    setAriaExpanded(module, false);
                }
            });

            chevronBtn.addEventListener("click", function (event) {
                event.preventDefault();
                event.stopPropagation();

                if (module.classList.contains("is-click-collapsed")) {
                    module.classList.remove("is-click-collapsed");
                    module.classList.add("is-expanded");
                    setAriaExpanded(module, true);
                    return;
                }

                if (module.classList.contains("is-expanded")) {
                    module.classList.remove("is-expanded");
                    module.classList.add("is-click-collapsed");
                    setAriaExpanded(module, false);
                    return;
                }

                module.classList.add("is-click-collapsed");
                setAriaExpanded(module, false);
            });
        })(modules[i]);
    }
})();
