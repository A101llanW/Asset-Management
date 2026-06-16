(function () {
    function showAssetTab(tabId) {
        if (!tabId) {
            return false;
        }

        var selector = '.asset-tabs a[href="#' + tabId + '"]';
        var trigger = document.querySelector(selector);
        if (!trigger) {
            return false;
        }

        if (window.bootstrap && window.bootstrap.Tab) {
            window.bootstrap.Tab.getOrCreateInstance(trigger).show();
        } else {
            trigger.click();
        }

        var module = document.querySelector(".am-tab-module");
        if (module && module.scrollIntoView) {
            module.scrollIntoView({ behavior: "smooth", block: "start" });
        }

        if (window.history && window.history.replaceState) {
            window.history.replaceState(null, "", "#" + tabId);
        } else {
            window.location.hash = tabId;
        }

        return true;
    }

    document.querySelectorAll("[data-am-asset-tab]").forEach(function (control) {
        control.addEventListener("click", function (event) {
            event.preventDefault();
            showAssetTab(control.getAttribute("data-am-asset-tab"));
        });
    });

    if (window.location.hash) {
        var tabId = window.location.hash.replace("#", "");
        showAssetTab(tabId);
    }
})();
