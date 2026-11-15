window.showModal = function (modalId) {
    var el = document.getElementById(modalId);
    if (el) {
        // Bootstrap focuses the dialog itself; put focus on the first field instead, and give it back on close.
        var opener = document.activeElement;
        el.addEventListener('shown.bs.modal', function () {
            var field = el.querySelector('input:not([disabled]), select:not([disabled]), textarea:not([disabled])');
            if (field) {
                field.focus();
            }
        }, { once: true });
        el.addEventListener('hidden.bs.modal', function () {
            if (opener && document.contains(opener) && typeof opener.focus === 'function') {
                opener.focus();
            }
        }, { once: true });
        var modal = bootstrap.Modal.getOrCreateInstance(el);
        modal.show();
    }
};

window.hideModal = function (modalId) {
    var el = document.getElementById(modalId);
    if (el) {
        var instance = bootstrap.Modal.getInstance(el);
        if (instance) {
            instance.hide();
        }
    }
};
