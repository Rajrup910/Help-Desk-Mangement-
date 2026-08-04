// Ask the user to confirm before deleting something.
function confirmDelete(message) {
    return confirm(message);
}

// Submit the filter form automatically when a dropdown is changed.
window.onload = function () {
    var dropdowns = document.getElementsByClassName("auto-submit");

    for (var i = 0; i < dropdowns.length; i++) {
        dropdowns[i].onchange = function () {
            this.form.submit();
        };
    }
};
