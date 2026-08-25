(function () {
    var wizard = document.querySelector("[data-am-wizard]");
    if (!wizard) {
        return;
    }

    var panels = wizard.querySelectorAll("[data-am-wizard-panel]");
    var indicators = wizard.querySelectorAll("[data-am-wizard-step-indicator]");
    var prevBtn = wizard.querySelector("[data-am-wizard-prev]");
    var nextBtn = wizard.querySelector("[data-am-wizard-next]");
    var finishBtn = wizard.querySelector("[data-am-wizard-finish]");
    var step = 0;

    function render() {
        for (var i = 0; i < panels.length; i++) {
            if (i === step) {
                panels[i].classList.remove("d-none");
            } else {
                panels[i].classList.add("d-none");
            }
        }

        for (var j = 0; j < indicators.length; j++) {
            if (parseInt(indicators[j].getAttribute("data-am-wizard-step-indicator"), 10) === step) {
                indicators[j].classList.add("active");
            } else {
                indicators[j].classList.remove("active");
            }
        }

        if (prevBtn) {
            prevBtn.disabled = step === 0;
        }

        var isLast = step >= panels.length - 1;
        if (nextBtn) {
            nextBtn.classList.toggle("d-none", isLast);
        }

        if (finishBtn) {
            finishBtn.classList.toggle("d-none", !isLast);
        }
    }

    if (prevBtn) {
        prevBtn.addEventListener("click", function () {
            if (step > 0) {
                step--;
                render();
            }
        });
    }

    if (nextBtn) {
        nextBtn.addEventListener("click", function () {
            if (step < panels.length - 1) {
                step++;
                render();
            }
        });
    }

    render();
})();
