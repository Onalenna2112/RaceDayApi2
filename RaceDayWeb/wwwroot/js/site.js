// RaceDay site script: small helpers only. No data is handled here.
(function () {
    'use strict';

    // Ask before running any form that has a data-confirm message (delete, cancel).
    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!(form instanceof HTMLFormElement)) return;

        var message = form.getAttribute('data-confirm');
        if (message && !window.confirm(message)) {
            event.preventDefault();
        }
    });

    // On the register page, show the organiser code only when "Organiser" is chosen.
    var organiserRadio = document.getElementById('roleOrg');
    var participantRadio = document.getElementById('rolePart');
    var codeGroup = document.getElementById('organiserCodeGroup');

    if (organiserRadio && participantRadio && codeGroup) {
        var update = function () {
            codeGroup.classList.toggle('d-none', !organiserRadio.checked);
        };
        organiserRadio.addEventListener('change', update);
        participantRadio.addEventListener('change', update);
        update();
    }
})();
