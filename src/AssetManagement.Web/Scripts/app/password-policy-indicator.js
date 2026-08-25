(function () {
    var evaluators = {
        minLength: function (password) {
            return password.length >= 8;
        },
        maxLength: function (password) {
            return password.length <= 128;
        },
        uppercase: function (password) {
            return /[A-Z]/.test(password);
        },
        lowercase: function (password) {
            return /[a-z]/.test(password);
        },
        digit: function (password) {
            return /\d/.test(password);
        },
        special: function (password) {
            return /[^a-zA-Z0-9]/.test(password);
        },
        noSequential: function (password) {
            return !containsSequentialChars(password);
        }
    };

    function containsSequentialChars(password) {
        if (!password) {
            return false;
        }

        var lowerPassword = password.toLowerCase();

        for (var i = 0; i < lowerPassword.length - 2; i++) {
            if (isDigit(lowerPassword.charAt(i))
                && isDigit(lowerPassword.charAt(i + 1))
                && isDigit(lowerPassword.charAt(i + 2))) {
                var first = lowerPassword.charCodeAt(i) - 48;
                var second = lowerPassword.charCodeAt(i + 1) - 48;
                var third = lowerPassword.charCodeAt(i + 2) - 48;
                if (second === first + 1 && third === second + 1) {
                    return true;
                }
                if (second === first - 1 && third === second - 1) {
                    return true;
                }
            }
        }

        for (var j = 0; j < lowerPassword.length - 2; j++) {
            if (isLetter(lowerPassword.charAt(j))
                && isLetter(lowerPassword.charAt(j + 1))
                && isLetter(lowerPassword.charAt(j + 2))) {
                var letterFirst = lowerPassword.charCodeAt(j) - 97;
                var letterSecond = lowerPassword.charCodeAt(j + 1) - 97;
                var letterThird = lowerPassword.charCodeAt(j + 2) - 97;
                if (letterSecond === letterFirst + 1 && letterThird === letterSecond + 1) {
                    return true;
                }
                if (letterSecond === letterFirst - 1 && letterThird === letterSecond - 1) {
                    return true;
                }
            }
        }

        return false;
    }

    function isDigit(value) {
        var code = value.charCodeAt(0);
        return code >= 48 && code <= 57;
    }

    function isLetter(value) {
        var code = value.charCodeAt(0);
        return (code >= 97 && code <= 122) || (code >= 65 && code <= 90);
    }

    function setRuleState(item, state) {
        item.classList.remove("am-password-policy__item--pending", "am-password-policy__item--met", "am-password-policy__item--unmet");
        item.classList.add("am-password-policy__item--" + state);
    }

    function updatePolicyIndicator(passwordInput) {
        var container = passwordInput.closest("[data-password-policy-root]");
        if (!container) {
            return;
        }

        var indicator = container.querySelector("[data-password-policy-indicator]");
        if (!indicator) {
            return;
        }

        var password = passwordInput.value || "";
        var hasValue = password.length > 0;

        indicator.querySelectorAll("[data-rule-id]").forEach(function (item) {
            var ruleId = item.getAttribute("data-rule-id");
            var evaluator = evaluators[ruleId];
            if (!evaluator) {
                return;
            }

            if (!hasValue) {
                setRuleState(item, "pending");
                return;
            }

            setRuleState(item, evaluator(password) ? "met" : "unmet");
        });

        var confirmInput = container.querySelector("[data-password-confirm-for]");
        var matchItem = container.querySelector("[data-password-match-indicator]");
        if (confirmInput && matchItem) {
            var confirmValue = confirmInput.value || "";
            if (!confirmValue && !hasValue) {
                setRuleState(matchItem, "pending");
            } else if (confirmValue && password && confirmValue === password) {
                setRuleState(matchItem, "met");
            } else {
                setRuleState(matchItem, "unmet");
            }
        }
    }

    function initContainer(container) {
        var passwordInput = container.querySelector("[data-password-policy-input]");
        if (!passwordInput) {
            return;
        }

        var refresh = function () {
            updatePolicyIndicator(passwordInput);
        };

        passwordInput.addEventListener("input", refresh);
        passwordInput.addEventListener("change", refresh);

        var confirmInput = container.querySelector("[data-password-confirm-for]");
        if (confirmInput) {
            confirmInput.addEventListener("input", refresh);
            confirmInput.addEventListener("change", refresh);
        }

        refresh();
    }

    document.querySelectorAll("[data-password-policy-root]").forEach(initContainer);
})();
