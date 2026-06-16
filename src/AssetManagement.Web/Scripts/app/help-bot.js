(function () {
    var root = document.getElementById("amHelpBot");
    if (!root) {
        return;
    }

    var toggle = document.getElementById("amHelpBotToggle");
    var closeButton = document.getElementById("amHelpBotClose");
    var panel = document.getElementById("amHelpBotPanel");
    var form = document.getElementById("amHelpBotForm");
    var input = document.getElementById("amHelpBotInput");
    var messages = document.getElementById("amHelpBotMessages");
    var chips = root.querySelectorAll("[data-helpbot-prompt]");

    function setOpenState(isOpen) {
        root.classList.toggle("am-helpbot-open", isOpen);
        panel.setAttribute("aria-hidden", isOpen ? "false" : "true");
        toggle.setAttribute("aria-expanded", isOpen ? "true" : "false");

        if (isOpen) {
            window.setTimeout(function () {
                input.focus();
            }, 120);
        }
    }

    function scrollToBottom() {
        messages.scrollTop = messages.scrollHeight;
    }

    function createMessage(text, sender, actions) {
        var article = document.createElement("article");
        article.className = "am-helpbot-message " + (sender === "user" ? "am-helpbot-message-user" : "am-helpbot-message-bot");

        var paragraphs = text.split("\n");
        for (var i = 0; i < paragraphs.length; i++) {
            if (!paragraphs[i]) {
                continue;
            }

            var paragraph = document.createElement("p");
            paragraph.textContent = paragraphs[i];
            article.appendChild(paragraph);
        }

        if (actions && actions.length) {
            var actionsWrapper = document.createElement("div");
            actionsWrapper.className = "am-helpbot-actions";

            for (var j = 0; j < actions.length; j++) {
                var action = document.createElement("a");
                action.className = "am-helpbot-action";
                action.href = actions[j].url;
                action.textContent = actions[j].label;
                actionsWrapper.appendChild(action);
            }

            article.appendChild(actionsWrapper);
        }

        messages.appendChild(article);
        scrollToBottom();
    }

    function rootUrl(key) {
        return root.getAttribute("data-" + key) || "#";
    }

    function normalize(text) {
        return (text || "")
            .toLowerCase()
            .replace(/[^\w\s]/g, " ")
            .replace(/\s+/g, " ")
            .trim();
    }

    function hasAny(text, terms) {
        for (var i = 0; i < terms.length; i++) {
            if (text.indexOf(terms[i]) >= 0) {
                return true;
            }
        }

        return false;
    }

    function buildResponse(text, actions) {
        return {
            text: text,
            actions: actions || []
        };
    }

    function getText(selector) {
        var element = document.querySelector(selector);
        return element ? element.textContent.replace(/\s+/g, " ").trim() : "";
    }

    function collectActionLinks() {
        var selectors = [
            ".am-page-header-actions a",
            ".am-page-header-back",
            ".am-empty-state a",
            ".card .btn-outline-primary",
            ".card .btn-primary"
        ];
        var links = [];
        var seen = {};

        for (var i = 0; i < selectors.length; i++) {
            var elements = document.querySelectorAll(selectors[i]);
            for (var j = 0; j < elements.length; j++) {
                var label = elements[j].textContent.replace(/\s+/g, " ").trim();
                var href = elements[j].getAttribute("href");

                if (!label || !href || seen[label]) {
                    continue;
                }

                links.push({ label: label, url: href });
                seen[label] = true;

                if (links.length >= 4) {
                    return links;
                }
            }
        }

        return links;
    }

    function getDomPageResponse() {
        var title = getText(".am-page-title");
        var subtitle = getText(".am-page-subtitle");
        var searchInput = document.querySelector(".am-list-toolbar input[type='text'], .am-list-toolbar input[name='search']");
        var searchHint = searchInput ? (searchInput.getAttribute("placeholder") || "").trim() : "";
        var headers = [];
        var headerNodes = document.querySelectorAll("table thead th");

        for (var i = 0; i < headerNodes.length && headers.length < 5; i++) {
            var headerText = headerNodes[i].textContent.replace(/\s+/g, " ").trim();
            if (headerText) {
                headers.push(headerText);
            }
        }

        var lines = [];
        if (title) {
            lines.push("You are on the " + title + " page.");
        }

        if (subtitle) {
            lines.push(subtitle);
        }

        if (searchHint) {
            lines.push("You can search from this page. " + searchHint + ".");
        }

        if (headers.length) {
            lines.push("The main list on this page lets you review information such as " + headers.join(", ") + ".");
        }

        var actions = collectActionLinks();
        if (actions.length) {
            lines.push("The main actions available here are shown below.");
        }

        return buildResponse(lines.join("\n"), actions);
    }

    function getCurrentPageResponse() {
        var controller = normalize(root.getAttribute("data-current-controller"));
        var action = normalize(root.getAttribute("data-current-action"));

        if (controller === "assets" && action === "index") {
            return buildResponse(
                "You are on the Assets list.\nUse this page to search, filter, and open an asset record.\nTo register a new asset, choose Create Asset.\nTo assign, transfer, return, log maintenance, report an incident, or raise a claim, open the asset details first.",
                [
                    { label: "Create asset", url: rootUrl("assets-create-url") },
                    { label: "Assets list", url: rootUrl("assets-url") }
                ]);
        }

        if (controller === "departments") {
            return buildResponse(
                "You are on the Departments page.\nUse this area to create, review, and edit departments that own or receive assets.\nDepartments should be set up before assigning users or routing assets through assignments and transfers.",
                collectActionLinks());
        }

        if (controller === "suppliers") {
            return buildResponse(
                "You are on the Suppliers page.\nUse this area to maintain approved vendors before recording purchases or linking suppliers to assets.\nKeeping suppliers current helps procurement and sourcing stay consistent.",
                collectActionLinks());
        }

        if (controller === "assetcategories") {
            return buildResponse(
                "You are in Asset Categories.\nCreate or edit categories here, then add asset types under each category so assets can be classified correctly.\nCategories are the parent grouping for asset types.",
                [
                    { label: "Asset categories", url: rootUrl("categories-url") },
                    { label: "Asset types", url: rootUrl("types-url") }
                ]);
        }

        if (controller === "assettypes") {
            return buildResponse(
                "You are in Asset Types.\nEach asset type belongs to one category.\nCreate or edit types here, then users will be able to select them on the asset create and edit screens after choosing the matching category.",
                [
                    { label: "Asset types", url: rootUrl("types-url") },
                    { label: "Asset categories", url: rootUrl("categories-url") }
                ]);
        }

        if (controller === "purchases") {
            return buildResponse(
                "You are on Purchases.\nUse this area to record supplier, purchase date, invoice, purchase order number, currency, and total cost information.\nPurchase records support procurement traceability before or alongside asset registration.",
                [
                    { label: "Create purchase", url: rootUrl("purchases-create-url") },
                    { label: "Suppliers", url: rootUrl("suppliers-url") }
                ]);
        }

        if (controller === "roles" || controller === "permissions") {
            return buildResponse(
                "You are in Roles and Permissions.\nCreate roles here, choose the permissions each role should have, then assign those roles to users.\nThis is how access to screens and actions is controlled across the module.",
                [
                    { label: "Roles & permissions", url: rootUrl("roles-url") },
                    { label: "Users", url: rootUrl("users-url") }
                ]);
        }

        if (controller === "users") {
            return buildResponse(
                "You are in Users.\nCreate staff profiles here, set the right department and role, and review the user's assigned asset count from the details page.\nUsers should usually exist before assets are assigned.",
                [
                    { label: "Create user", url: rootUrl("users-create-url") },
                    { label: "Users", url: rootUrl("users-url") }
                ]);
        }

        if (controller === "settings") {
            return buildResponse(
                "You are on Settings.\nUse this page to manage attachment storage, notification thresholds, finance defaults, and approval rules.\nThese settings affect how the module behaves across workflows.",
                collectActionLinks());
        }

        if (controller === "notifications") {
            return buildResponse(
                "You are on Notifications.\nUse this page to review recent system notifications and generate fresh operational notifications when needed.",
                collectActionLinks());
        }

        if (controller === "dashboard") {
            return buildResponse(
                "You are on the Dashboard.\nThis page gives a quick operational overview and helps users jump into the main module areas.",
                collectActionLinks());
        }

        if (controller === "reports") {
            return buildResponse(
                "You are on Reports.\nThis page shows the reporting dashboard summary for the module.\nUse it for oversight, then move into assets, purchases, or notifications when you need to follow up on something specific.",
                [
                    { label: "Reports", url: rootUrl("reports-url") },
                    { label: "Notifications", url: rootUrl("notifications-url") }
                ]);
        }

        var domResponse = getDomPageResponse();
        if (domResponse.text) {
            return domResponse;
        }

        return buildResponse(
            "This page is part of the asset management workflow.\nIf you tell me the task you want to complete, I can point you to the right screen and the next steps.",
            [
                { label: "Assets", url: rootUrl("assets-url") },
                { label: "Dashboard", url: rootUrl("dashboard-url") }
            ]);
    }

    function getProcessResponse(rawText) {
        var text = normalize(rawText);

        if (!text) {
            return buildResponse(
                "Ask me about a task in the module and I will point you to the right process.\nFor example: create an asset, assign an asset, return an asset, manage categories, create a role, or record a purchase.",
                [
                    { label: "Assets", url: rootUrl("assets-url") },
                    { label: "Dashboard", url: rootUrl("dashboard-url") }
                ]);
        }

        if (hasAny(text, ["this page", "where am i", "what can i do here", "what can i do on this page"])) {
            return getCurrentPageResponse();
        }

        if (hasAny(text, ["create asset", "register asset", "new asset", "add asset"])) {
            return buildResponse(
                "To create an asset:\n1. Open Assets and choose Create Asset.\n2. Enter the asset name, asset tag, and serial details.\n3. Choose the category first, then the asset type for that category.\n4. Select the department and supplier, then complete purchase and depreciation information.\n5. Save the record, then review the details page for the next workflow actions.",
                [
                    { label: "Create asset", url: rootUrl("assets-create-url") },
                    { label: "Assets list", url: rootUrl("assets-url") },
                    { label: "Asset categories", url: rootUrl("categories-url") }
                ]);
        }

        if (hasAny(text, ["assign asset", "assignment", "assign"])) {
            return buildResponse(
                "To assign an asset:\n1. Open the asset from the Assets list.\n2. On the asset details page, start the assignment action.\n3. Choose the receiving user, department, assignment type, and handed-over-by user.\n4. Save the assignment.\nThe system then returns you to the asset details page and the assignment history can be reviewed from the assignment screen.",
                [
                    { label: "Assets", url: rootUrl("assets-url") },
                    { label: "Assignment history", url: rootUrl("assignments-url") },
                    { label: "Users", url: rootUrl("users-url") }
                ]);
        }

        if (hasAny(text, ["transfer asset", "transfer"])) {
            return buildResponse(
                "To transfer an asset:\n1. Open the asset details page.\n2. Start the transfer action from that asset.\n3. Confirm the current custodian and department, then choose the new user and department.\n4. Save the transfer.\nTransfers are intended for assets that are already assigned, and the flow returns you to the asset details page after it is recorded.",
                [
                    { label: "Assets", url: rootUrl("assets-url") },
                    { label: "Users", url: rootUrl("users-url") },
                    { label: "Departments", url: rootUrl("departments-url") }
                ]);
        }

        if (hasAny(text, ["return asset", "asset return", "return"])) {
            return buildResponse(
                "To return an asset:\n1. Open the asset details page.\n2. Start the return action from the asset.\n3. Confirm the returning user, return date, condition, and notes.\n4. Save the return.\nThe system records the return and sends you back to the asset details page.",
                [
                    { label: "Assets", url: rootUrl("assets-url") },
                    { label: "Users", url: rootUrl("users-url") }
                ]);
        }

        if (hasAny(text, ["maintenance", "service ticket", "repair"])) {
            return buildResponse(
                "To log maintenance:\n1. Open the asset details page.\n2. Start the maintenance action from the asset.\n3. Choose the maintenance type, then record the vendor, cost, dates, and notes.\n4. Save the maintenance ticket.\nThis flow is used to track preventive or corrective work against the asset.",
                [
                    { label: "Assets", url: rootUrl("assets-url") }
                ]);
        }

        if (hasAny(text, ["incident", "damage", "lost", "stolen"])) {
            return buildResponse(
                "To report an incident:\n1. Open the asset details page.\n2. Start the incident action.\n3. Select the incident type, date, and description.\n4. Save the incident.\nIncidents can later be linked to insurance claims when needed.",
                [
                    { label: "Assets", url: rootUrl("assets-url") }
                ]);
        }

        if (hasAny(text, ["claim", "insurance"])) {
            return buildResponse(
                "To create an insurance claim:\n1. Open the asset details page.\n2. Start the claim action from the asset.\n3. Choose the claim type and, if available, link it to an existing incident for that asset.\n4. Save the claim.\nThis keeps claims tied to the asset and its incident history.",
                [
                    { label: "Assets", url: rootUrl("assets-url") }
                ]);
        }

        if (hasAny(text, ["category", "asset setup"]) && !hasAny(text, ["type"])) {
            return buildResponse(
                "Asset categories are managed under Asset Categories.\nUse categories for the top-level grouping such as IT equipment or furniture.\nAfter creating a category, add one or more asset types underneath it so users can classify assets properly.",
                [
                    { label: "Asset categories", url: rootUrl("categories-url") },
                    { label: "Asset types", url: rootUrl("types-url") }
                ]);
        }

        if (hasAny(text, ["asset type", "type"])) {
            return buildResponse(
                "Asset types depend on categories.\nCreate the category first, then create the asset type and attach it to that category.\nWhen users create or edit an asset, they choose the category first and the matching asset types become available.",
                [
                    { label: "Asset types", url: rootUrl("types-url") },
                    { label: "Asset categories", url: rootUrl("categories-url") }
                ]);
        }

        if (hasAny(text, ["purchase", "procure", "invoice", "po", "purchase order"])) {
            return buildResponse(
                "To record a purchase:\n1. Create the supplier first if it does not already exist.\n2. Open Purchases and create a new purchase record.\n3. Enter the supplier, purchase date, currency, invoice, purchase order number, and totals.\n4. Save the record and review the details page.\nThis gives you procurement traceability that can support asset registration.",
                [
                    { label: "Create purchase", url: rootUrl("purchases-create-url") },
                    { label: "Purchases", url: rootUrl("purchases-url") },
                    { label: "Suppliers", url: rootUrl("suppliers-url") }
                ]);
        }

        if (hasAny(text, ["supplier", "vendor"])) {
            return buildResponse(
                "Suppliers are managed from the Suppliers screen.\nCreate the supplier record there before using that supplier on purchase or asset records.\nThat helps keep procurement and asset data consistent.",
                [
                    { label: "Suppliers", url: rootUrl("suppliers-url") },
                    { label: "Purchases", url: rootUrl("purchases-url") }
                ]);
        }

        if (hasAny(text, ["user", "employee", "staff"])) {
            return buildResponse(
                "To create a user:\n1. Open Users and choose Create.\n2. Enter the staff details, department, role, and password.\n3. Save the user, then review the details page.\nUsers should normally be created before you assign assets so the receiving person and access level already exist.",
                [
                    { label: "Create user", url: rootUrl("users-create-url") },
                    { label: "Users", url: rootUrl("users-url") },
                    { label: "Roles", url: rootUrl("roles-url") }
                ]);
        }

        if (hasAny(text, ["role", "permission", "access"])) {
            return buildResponse(
                "To manage roles and permissions:\n1. Open Roles and Permissions.\n2. Create a role or edit an existing one.\n3. Tick the permissions the role should have.\n4. Save the role, review its details, then assign that role to users.\nRoles control which screens and actions each user can access in the module.",
                [
                    { label: "Roles & permissions", url: rootUrl("roles-url") },
                    { label: "Users", url: rootUrl("users-url") }
                ]);
        }

        if (hasAny(text, ["department"])) {
            return buildResponse(
                "Departments are managed from the Departments screen.\nCreate or update departments there before linking them to users, assets, assignments, or transfers.\nKeeping departments current helps the workflow screens stay accurate.",
                [
                    { label: "Departments", url: rootUrl("departments-url") },
                    { label: "Users", url: rootUrl("users-url") }
                ]);
        }

        if (hasAny(text, ["report", "dashboard", "summary", "analytics"])) {
            return buildResponse(
                "Use the reporting area for a high-level summary of the module and the dashboard for quick operational visibility.\nFrom there you can move into assets, purchases, or notifications depending on what needs follow-up.",
                [
                    { label: "Reports", url: rootUrl("reports-url") },
                    { label: "Dashboard", url: rootUrl("dashboard-url") },
                    { label: "Notifications", url: rootUrl("notifications-url") }
                ]);
        }

        if (hasAny(text, ["notification", "alert", "reminder"])) {
            return buildResponse(
                "Notifications are listed on the Notifications screen.\nThe system can generate operational notifications there, and users can review the latest items to see what needs attention.",
                [
                    { label: "Notifications", url: rootUrl("notifications-url") },
                    { label: "Reports", url: rootUrl("reports-url") }
                ]);
        }

        if (hasAny(text, ["setting", "configuration"])) {
            return buildResponse(
                "Module-wide configuration is handled from Settings.\nUse that area for administrative setup, while Asset Categories is the area for categories and asset types.",
                [
                    { label: "Settings", url: rootUrl("settings-url") },
                    { label: "Asset categories", url: rootUrl("categories-url") }
                ]);
        }

        return buildResponse(
            "I did not find an exact process match for that question yet.\nI can answer tasks like creating an asset, assigning, transferring, returning, logging maintenance, reporting incidents, raising claims, recording purchases, creating users, managing roles, and managing categories or asset types.\nIf you rephrase the task in that form, I can guide you step by step.",
            [
                { label: "Assets", url: rootUrl("assets-url") },
                { label: "Users", url: rootUrl("users-url") },
                { label: "Roles", url: rootUrl("roles-url") }
            ]
        );
    }

    function handlePrompt(text) {
        if (!text) {
            return;
        }

        createMessage(text, "user");

        window.setTimeout(function () {
            var response = getProcessResponse(text);
            createMessage(response.text, "bot", response.actions);
        }, 180);
    }

    toggle.addEventListener("click", function () {
        setOpenState(!root.classList.contains("am-helpbot-open"));
    });

    closeButton.addEventListener("click", function () {
        setOpenState(false);
    });

    form.addEventListener("submit", function (event) {
        event.preventDefault();

        var value = input.value.replace(/\s+/g, " ").trim();
        if (!value) {
            return;
        }

        input.value = "";
        handlePrompt(value);
    });

    for (var i = 0; i < chips.length; i++) {
        chips[i].addEventListener("click", function () {
            handlePrompt(this.getAttribute("data-helpbot-prompt"));
        });
    }

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape" && root.classList.contains("am-helpbot-open")) {
            setOpenState(false);
        }
    });
})();
