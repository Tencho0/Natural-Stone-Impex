window.showModal = function (modalId) {
    var el = document.getElementById(modalId);
    if (el) {
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
