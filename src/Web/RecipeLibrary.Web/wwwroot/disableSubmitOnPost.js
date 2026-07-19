// Disable submit buttons on static SSR forms after the first submit.
document.addEventListener(
    "submit",
    (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        if (!form.hasAttribute("data-disable-on-submit")) {
            return;
        }

        form.querySelector("[type=submit]")?.setAttribute("disabled", "disabled");
    },
    true);
