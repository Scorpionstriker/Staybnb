// =====================================================================
// Booking box on the property details page.
// Calls the JSON API  GET /api/properties/{id}/quote  whenever the dates
// or number of guests change, and shows the live price breakdown:
//   Total = (PricePerNight x Nights) + CleaningFee + ServiceFee
// The server recalculates the price again when the booking is created.
// =====================================================================
(function () {
    "use strict";

    const box = document.getElementById("bookingBox");
    if (!box) return;

    const propertyId = box.dataset.propertyId;
    const maxGuests = parseInt(box.dataset.maxGuests || "1", 10);

    const checkIn = document.getElementById("checkIn");
    const checkOut = document.getElementById("checkOut");
    const guestsInput = document.getElementById("guests");
    const guestCount = document.getElementById("guestCount");
    const guestLabel = document.getElementById("guestLabel");
    const minus = document.getElementById("guestMinus");
    const plus = document.getElementById("guestPlus");
    const reserve = document.getElementById("reserveButton");
    const errorBox = document.getElementById("quoteError");
    const breakdown = document.getElementById("priceBreakdown");

    const money = new Intl.NumberFormat("en-ZA", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

    function isoDate(date) {
        return date.toISOString().slice(0, 10);
    }

    function addDays(value, days) {
        const d = new Date(value + "T00:00:00Z");
        d.setUTCDate(d.getUTCDate() + days);
        return isoDate(d);
    }

    // ---------- Guests counter ----------
    function setGuests(n) {
        n = Math.max(1, Math.min(maxGuests, n));
        guestsInput.value = n;
        guestCount.textContent = n;
        guestLabel.textContent = n + (n === 1 ? " guest" : " guests");
        minus.disabled = n <= 1;
        plus.disabled = n >= maxGuests;
    }

    minus.addEventListener("click", () => { setGuests(parseInt(guestsInput.value, 10) - 1); requestQuote(); });
    plus.addEventListener("click", () => { setGuests(parseInt(guestsInput.value, 10) + 1); requestQuote(); });

    // ---------- Dates ----------
    checkIn.addEventListener("change", () => {
        if (!checkIn.value) return;
        checkOut.min = addDays(checkIn.value, 1);
        if (!checkOut.value || checkOut.value <= checkIn.value) {
            checkOut.value = addDays(checkIn.value, 1);
        }
        requestQuote();
    });
    checkOut.addEventListener("change", requestQuote);

    // ---------- Live quote from the API ----------
    let latestRequest = 0;

    async function requestQuote() {
        if (!checkIn.value || !checkOut.value) return;

        const requestId = ++latestRequest;
        const url = `/api/properties/${propertyId}/quote?checkIn=${checkIn.value}&checkOut=${checkOut.value}&guests=${guestsInput.value}`;

        breakdown.style.opacity = "0.5";
        try {
            const response = await fetch(url, { headers: { "Accept": "application/json" } });
            if (!response.ok) throw new Error("Could not get a price right now.");
            const quote = await response.json();
            if (requestId !== latestRequest) return; // a newer request is on its way

            render(quote);
        } catch (err) {
            showError(err.message);
        } finally {
            breakdown.style.opacity = "1";
        }
    }

    function render(quote) {
        breakdown.querySelector('[data-q="pricePerNight"]').textContent = money.format(quote.pricePerNight);
        breakdown.querySelector('[data-q="nights"]').textContent = quote.nights;
        breakdown.querySelector('[data-q="subtotal"]').textContent = money.format(quote.subtotal);
        breakdown.querySelector('[data-q="cleaningFee"]').textContent = money.format(quote.cleaningFee);
        breakdown.querySelector('[data-q="serviceFee"]').textContent = money.format(quote.serviceFee);
        breakdown.querySelector('[data-q="total"]').textContent = money.format(quote.total);

        if (quote.available) {
            showError("");
        } else {
            showError(quote.message || "These dates are not available.");
        }
    }

    function showError(message) {
        errorBox.textContent = message;
        errorBox.classList.toggle("d-none", !message);
        if (reserve) reserve.disabled = !!message;
    }

    setGuests(parseInt(guestsInput.value, 10) || 1);
    requestQuote();
})();
