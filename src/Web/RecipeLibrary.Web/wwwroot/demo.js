window.RecipeLibraryDemo = {
    submit: function (id) { document.getElementById(id)?.requestSubmit(); },
    showHint: function (userId) {
        try {
            const key = "RecipeLibrary.DemoHint." + userId;
            if (sessionStorage.getItem(key)) return false;
            sessionStorage.setItem(key, "shown");
            return true;
        } catch { return true; }
    }
};

(function () {
    if (window.location.pathname !== "/demo") return;
    function start() {
        const form = document.getElementById("demo-start");
        if (form?.dataset.autoStart === "true") {
            observer.disconnect();
            form.dataset.autoStart = "false";
            form.requestSubmit();
        }
    }
    const observer = new MutationObserver(start);
    observer.observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ["data-auto-start"] });
    document.addEventListener("DOMContentLoaded", start, { once: true });
    start();
})();
