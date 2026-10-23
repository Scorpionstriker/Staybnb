// =====================================================================
// "Become a Host" form
//  - photo picker with previews, remove buttons and drag & drop
//  - blocks submission when no photo is selected (the server checks this too)
//  - description character counter and example price
// =====================================================================
(function () {
    "use strict";

    const form = document.getElementById("hostApplicationForm");
    if (!form) return;

    const input = document.getElementById("imageInput");
    const dropzone = document.getElementById("dropzone");
    const preview = document.getElementById("photoPreview");
    const countLabel = document.getElementById("photoCount");
    const errorBox = document.getElementById("photoError");
    const submitButton = document.getElementById("submitApplication");

    const maxFiles = parseInt(input.dataset.maxFiles || "10", 10);
    const maxSizeBytes = parseInt(input.dataset.maxSizeMb || "5", 10) * 1024 * 1024;
    const allowedTypes = ["image/jpeg", "image/png", "image/webp"];
    const allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    /** The photos the user has picked so far (kept across several "browse" clicks). */
    let selectedFiles = [];

    // ---------------------------------------------------------------
    // Photos
    // ---------------------------------------------------------------
    function showError(message) {
        errorBox.textContent = message || "";
        dropzone.classList.toggle("is-invalid", !!message);
    }

    function isAllowed(file) {
        const name = file.name.toLowerCase();
        const extensionOk = allowedExtensions.some(ext => name.endsWith(ext));
        const typeOk = !file.type || allowedTypes.includes(file.type);
        return extensionOk && typeOk;
    }

    function addFiles(fileList) {
        const problems = [];

        Array.from(fileList).forEach(file => {
            if (!isAllowed(file)) {
                problems.push(`"${file.name}" is not a JPG, PNG or WEBP image.`);
            } else if (file.size > maxSizeBytes) {
                problems.push(`"${file.name}" is larger than ${input.dataset.maxSizeMb} MB.`);
            } else if (selectedFiles.length >= maxFiles) {
                problems.push(`You can add a maximum of ${maxFiles} photos.`);
            } else if (selectedFiles.some(f => f.name === file.name && f.size === file.size)) {
                // same file picked twice - silently ignore
            } else {
                selectedFiles.push(file);
            }
        });

        syncInput();
        render();
        showError([...new Set(problems)].join(" "));
    }

    function removeFile(index) {
        selectedFiles.splice(index, 1);
        syncInput();
        render();
        showError("");
    }

    /** Copies our list back into the real <input type="file"> so it is posted with the form. */
    function syncInput() {
        const transfer = new DataTransfer();
        selectedFiles.forEach(f => transfer.items.add(f));
        input.files = transfer.files;
    }

    function render() {
        preview.innerHTML = "";
        countLabel.textContent = selectedFiles.length;

        selectedFiles.forEach((file, index) => {
            const tile = document.createElement("div");
            tile.className = "stb-photo";

            const img = document.createElement("img");
            img.alt = file.name;
            img.src = URL.createObjectURL(file);
            img.onload = () => URL.revokeObjectURL(img.src);
            tile.appendChild(img);

            if (index === 0) {
                const cover = document.createElement("span");
                cover.className = "stb-photo-cover";
                cover.textContent = "Cover photo";
                tile.appendChild(cover);
            }

            const remove = document.createElement("button");
            remove.type = "button";
            remove.className = "stb-photo-remove";
            remove.title = "Remove photo";
            remove.innerHTML = '<i class="bi bi-x-lg"></i>';
            remove.addEventListener("click", () => removeFile(index));
            tile.appendChild(remove);

            preview.appendChild(tile);
        });
    }

    input.addEventListener("change", () => {
        // input.files is replaced by syncInput(), so take a copy of the new picks first
        const picked = Array.from(input.files);
        syncInput(); // restore previous selection before adding
        addFiles(picked);
    });

    // Drag & drop
    ["dragenter", "dragover"].forEach(evt =>
        dropzone.addEventListener(evt, e => {
            e.preventDefault();
            dropzone.classList.add("is-dragover");
        }));

    ["dragleave", "drop"].forEach(evt =>
        dropzone.addEventListener(evt, e => {
            e.preventDefault();
            dropzone.classList.remove("is-dragover");
        }));

    dropzone.addEventListener("drop", e => {
        if (e.dataTransfer && e.dataTransfer.files.length) addFiles(e.dataTransfer.files);
    });

    // ---------------------------------------------------------------
    // Submit: an application without a photo is incomplete
    // ---------------------------------------------------------------
    form.addEventListener("submit", e => {
        if (selectedFiles.length === 0) {
            e.preventDefault();
            e.stopImmediatePropagation();
            showError("Your application is incomplete: please add at least one photo of your place.");
            dropzone.scrollIntoView({ behavior: "smooth", block: "center" });
            return;
        }

        // Only show the spinner if jQuery validation is happy too
        const $form = window.jQuery ? window.jQuery(form) : null;
        if ($form && typeof $form.valid === "function" && !$form.valid()) return;

        submitButton.disabled = true;
        submitButton.querySelector(".spinner-border").classList.remove("d-none");
    });

    // ---------------------------------------------------------------
    // Description counter
    // ---------------------------------------------------------------
    const description = document.getElementById("Description");
    const descriptionCount = document.getElementById("descriptionCount");
    if (description && descriptionCount) {
        const updateCount = () => descriptionCount.textContent = description.value.length;
        description.addEventListener("input", updateCount);
        updateCount();
    }

    // ---------------------------------------------------------------
    // Example price: 2 nights + cleaning fee + 12% service fee
    // ---------------------------------------------------------------
    const price = document.getElementById("PricePerNight");
    const cleaning = document.getElementById("CleaningFee");
    const example = document.getElementById("examplePrice");
    if (price && cleaning && example) {
        const formatter = new Intl.NumberFormat("en-ZA", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        const updateExample = () => {
            const nightly = parseFloat(price.value) || 0;
            const clean = parseFloat(cleaning.value) || 0;
            const subtotal = nightly * 2;
            const serviceFee = Math.round(subtotal * 0.12 * 100) / 100;
            example.textContent = "R " + formatter.format(subtotal + clean + serviceFee);
        };
        price.addEventListener("input", updateExample);
        cleaning.addEventListener("input", updateExample);
        updateExample();
    }
})();
