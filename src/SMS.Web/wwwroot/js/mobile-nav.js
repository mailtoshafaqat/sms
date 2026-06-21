(function () {
    function closeSidebar() {
        var toggle = document.getElementById('sidebar-toggle');
        if (toggle) {
            toggle.checked = false;
        }
    }

    document.addEventListener('click', function (event) {
        if (event.target.closest('.nav-menu a')) {
            closeSidebar();
        }
    });
})();
