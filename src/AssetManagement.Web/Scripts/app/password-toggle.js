(function () {
    function togglePassword(button) {
        var group = button.closest(".am-password-input");
        if (!group) {
            return;
        }

        var input = group.querySelector("input.am-password-field");
        if (!input) {
            return;
        }

        var show = input.type === "password";
        input.type = show ? "text" : "password";
        button.textContent = show ? "Hide" : "Show";
        button.setAttribute("aria-pressed", show ? "true" : "false");
        button.setAttribute("aria-label", show ? "Hide password" : "Show password");
    }

    document.querySelectorAll(".am-password-toggle").forEach(function (button) {
        button.addEventListener("click", function () {
            togglePassword(button);
        });
    });
})();
