// Invite user: cascading Department -> Role picker. Reloads the Role list from
// UserInvitations/RolesForDepartment whenever the department changes (the server applies the same
// scope, active and escalation filters again on submit).
(function () {
    'use strict';
    var dept = document.getElementById('DepartmentId');
    var role = document.getElementById('RoleId');
    if (!dept || !role) {
        return;
    }

    var url = role.getAttribute('data-roles-url');
    var status = document.getElementById('am-invite-role-status');
    if (!url) {
        return;
    }

    function setStatus(text) {
        if (status) {
            status.textContent = text;
        }
    }

    function rebuild(roles, keep) {
        while (role.options.length > 0) {
            role.remove(0);
        }

        var placeholder = document.createElement('option');
        placeholder.value = '';
        placeholder.textContent = 'Select role';
        role.appendChild(placeholder);
        for (var i = 0; i < roles.length; i++) {
            var option = document.createElement('option');
            option.value = String(roles[i].id);
            option.textContent = roles[i].name + (roles[i].scope === 'department' ? ' (this department only)' : '');
            if (option.value === keep) {
                option.selected = true;
            }

            role.appendChild(option);
        }
    }

    dept.addEventListener('change', function () {
        var keep = role.value;
        var requestUrl = url + (dept.value ? (url.indexOf('?') >= 0 ? '&' : '?') + 'departmentId=' + encodeURIComponent(dept.value) : '');
        role.disabled = true;
        setStatus('Loading roles...');
        var xhr = new XMLHttpRequest();
        xhr.open('GET', requestUrl, true);
        xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
        xhr.setRequestHeader('Accept', 'application/json');
        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) {
                return;
            }

            role.disabled = false;
            var data = null;
            try {
                data = JSON.parse(xhr.responseText);
            } catch (e) {
                data = null;
            }

            if (xhr.status !== 200 || !data || !data.roles) {
                rebuild([], '');
                setStatus((data && data.error) || 'Could not load roles for this department.');
                return;
            }

            rebuild(data.roles, keep);
            role.setAttribute('data-loaded-department', dept.value || '');
            setStatus(data.roles.length ? data.roles.length + ' role(s) apply here.' : 'No roles you can grant apply here.');
        };
        xhr.send();
    });
})();