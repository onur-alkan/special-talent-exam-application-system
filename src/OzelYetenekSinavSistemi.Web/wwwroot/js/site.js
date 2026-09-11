// Ortak istemci tarafı yardımcılar (CSP: harici same-origin script; eval yok)
window.dtLangTR = {
    decimal: ",",
    thousands: ".",
    emptyTable: "Tabloda veri bulunmuyor",
    info: "_TOTAL_ kayıttan _START_ - _END_ arası gösteriliyor",
    infoEmpty: "Kayıt yok",
    infoFiltered: "(_MAX_ kayıt içinden filtrelendi)",
    lengthMenu: "_MENU_ kayıt göster",
    loadingRecords: "Yükleniyor...",
    processing: "İşleniyor...",
    search: "Ara:",
    zeroRecords: "Eşleşen kayıt bulunamadı",
    paginate: {
        first: "İlk",
        last: "Son",
        next: "Sonraki",
        previous: "Önceki"
    },
    buttons: {
        copy: "Kopyala",
        print: "Yazdır",
        colvis: "Sütunlar",
        // Buttons 1.2.3: i18n("buttons.copyTitle" / "buttons.copySuccess", …)
        copyTitle: "Panoya Kopyalandı",
        copySuccess: {
            1: "1 satır panoya kopyalandı",
            _: "%d satır panoya kopyalandı"
        }
    }
};

function oysReadDatasetText(el, key) {
    if (!el || !el.dataset) { return ""; }
    var value = el.dataset[key];
    return typeof value === "string" ? value : "";
}

function oysShowToastsFromContainer() {
    var box = document.getElementById("js-toast-messages");
    if (!box || typeof toastr === "undefined") { return; }

    toastr.options = {
        closeButton: true,
        progressBar: true,
        positionClass: "toast-top-right",
        timeOut: 5000,
        escapeHtml: true
    };

    var success = oysReadDatasetText(box, "success");
    var error = oysReadDatasetText(box, "error");
    var warning = oysReadDatasetText(box, "warning");
    var info = oysReadDatasetText(box, "info");

    if (success) { toastr.success(success); }
    if (error) { toastr.error(error); }
    if (warning) { toastr.warning(warning); }
    if (info) { toastr.info(info); }
}

function oysBindPrintCloseAndImages() {
    document.querySelectorAll(".js-print-document").forEach(function (btn) {
        btn.addEventListener("click", function () { window.print(); });
    });

    document.querySelectorAll(".js-close-window").forEach(function (link) {
        link.addEventListener("click", function (e) {
            e.preventDefault();
            window.close();
        });
    });

    document.querySelectorAll(".js-hide-on-image-error").forEach(function (img) {
        img.addEventListener("error", function () {
            this.classList.add("is-hidden-by-error");
            this.style.display = "none";
        });
    });
}

function oysIsSafeCaptchaUrl(url) {
    if (typeof url !== "string") { return false; }
    var trimmed = url.trim();
    if (!trimmed || trimmed.charAt(0) !== "/") { return false; }
    if (trimmed.indexOf("//") === 0) { return false; }
    if (/^[a-zA-Z][a-zA-Z0-9+.-]*:/i.test(trimmed)) { return false; }
    return true;
}

function oysFindCaptchaInput(root) {
    if (!root) { return null; }

    var input = root.querySelector("[data-oys-captcha-input]");
    if (!input) {
        input = root.querySelector("#CaptchaInput");
    }

    if (!input || input.name !== "CaptchaInput") {
        return null;
    }

    return input;
}

function oysSnapshotLoginFormFieldValues(form) {
    if (!form) { return null; }

    var fields = [];
    var login = form.querySelector(
        "[data-oys-login-identifier], input[name='LoginIdentifier'], input[name='TcNoOrEmail']");
    if (login) {
        fields.push({ el: login, value: login.value });
    }

    var password = form.querySelector("input[type='password'][name='Password']");
    if (password) {
        fields.push({ el: password, value: password.value });
    }

    return fields.length ? { fields: fields } : null;
}

function oysRestoreLoginFormFieldValues(snapshot) {
    if (!snapshot || !snapshot.fields) { return; }

    for (var i = 0; i < snapshot.fields.length; i++) {
        var item = snapshot.fields[i];
        if (!item.el || typeof item.value !== "string") { continue; }
        if (item.el.value !== item.value) {
            item.el.value = item.value;
        }
    }
}

function oysScheduleLoginFormFieldRestore(snapshot) {
    if (!snapshot) { return; }

    oysRestoreLoginFormFieldValues(snapshot);
    if (typeof window.requestAnimationFrame === "function") {
        window.requestAnimationFrame(function () {
            oysRestoreLoginFormFieldValues(snapshot);
        });
    }
}

function oysClearCaptchaInput(root) {
    var input = oysFindCaptchaInput(root);
    if (!input) { return; }
    input.value = "";
    input.classList.remove("input-validation-error");
    if (typeof input.setCustomValidity === "function") {
        input.setCustomValidity("");
    }

    var message = root.querySelector("[data-valmsg-for='CaptchaInput'], [data-valmsg-for=\"CaptchaInput\"]");
    if (message) {
        message.textContent = "";
        message.classList.remove("field-validation-error");
        message.classList.add("field-validation-valid");
    }

    if (typeof jQuery !== "undefined") {
        var $input = jQuery(input);
        var $form = $input.closest("form");
        var validator = $form.data("validator");
        if (validator) {
            $input.removeData("previousValue");
            if (validator.invalid && validator.invalid[input.name]) {
                delete validator.invalid[input.name];
            }
            if (validator.submitted && validator.submitted[input.name]) {
                delete validator.submitted[input.name];
            }
        }
    }
}

function oysSetCaptchaStatus(root, text) {
    var status = root.querySelector("[data-oys-captcha-status]");
    if (!status) { return; }
    status.textContent = text || "";
}

function oysBindCaptchaRefresh() {
    if (window.oysCaptchaRefreshBound) { return; }
    window.oysCaptchaRefreshBound = true;

    document.addEventListener("click", function (event) {
        var target = event.target;
        if (!target || typeof target.closest !== "function") { return; }

        var btn = target.closest(".js-captcha-refresh, [data-oys-captcha-refresh]");
        if (!btn) { return; }

        event.preventDefault();
        event.stopPropagation();

        if (btn.disabled || btn.getAttribute("aria-busy") === "true") { return; }

        var root = btn.closest("[data-oys-captcha-root]") || btn.closest("form") || document;
        var form = root.tagName === "FORM" ? root : root.closest("form");
        var img = root.querySelector("[data-oys-captcha-image], #captchaImage");
        if (!img) { return; }

        var clean = img.getAttribute("data-captcha-url");
        if (!oysIsSafeCaptchaUrl(clean)) { return; }

        var separator = clean.indexOf("?") >= 0 ? "&" : "?";
        var nextUrl = clean + separator + "t=" + Date.now().toString();
        var fieldSnapshot = oysSnapshotLoginFormFieldValues(form);

        btn.disabled = true;
        btn.setAttribute("aria-busy", "true");
        oysClearCaptchaInput(root);
        oysRestoreLoginFormFieldValues(fieldSnapshot);
        oysScheduleLoginFormFieldRestore(fieldSnapshot);
        oysSetCaptchaStatus(root, "Güvenlik kodu yenileniyor");

        var settled = false;
        var finish = function (ok) {
            if (settled) { return; }
            settled = true;
            btn.disabled = false;
            btn.removeAttribute("aria-busy");
            oysRestoreLoginFormFieldValues(fieldSnapshot);
            oysScheduleLoginFormFieldRestore(fieldSnapshot);
            oysSetCaptchaStatus(root, ok ? "Güvenlik kodu yenilendi" : "Güvenlik kodu yenilenemedi");
        };

        var onLoad = function () {
            img.removeEventListener("load", onLoad);
            img.removeEventListener("error", onError);
            finish(true);
        };
        var onError = function () {
            img.removeEventListener("load", onLoad);
            img.removeEventListener("error", onError);
            finish(false);
        };

        img.addEventListener("load", onLoad);
        img.addEventListener("error", onError);
        img.setAttribute("src", nextUrl);

        window.setTimeout(function () {
            if (!settled) { finish(false); }
        }, 8000);
    });
}

function oysInitSelect2() {
    if (typeof jQuery === "undefined" || !jQuery.fn || !jQuery.fn.select2) { return; }
    var $ = jQuery;

    $(".js-select2, .select2-years, .select2-manager").each(function () {
        var $el = $(this);
        if ($el.hasClass("select2-hidden-accessible")) { return; }
        $el.select2({ width: $el.data("width") || "100%" });
    });
}

function oysBindPreferenceOrderingForms() {
    document.querySelectorAll("form[data-oys-preference-form]").forEach(function (form) {
        if (form.dataset.oysPreferenceBound === "true") { return; }
        form.dataset.oysPreferenceBound = "true";

        var max = parseInt(form.getAttribute("data-max-preferences"), 10);
        if (!max || max < 1) { max = 1; }

        var list = form.querySelector("[data-oys-preference-list]");
        var empty = form.querySelector("[data-oys-preference-empty]");
        var hidden = form.querySelector("[data-oys-preference-hidden]");
        var error = form.querySelector("[data-oys-preference-error]");
        var live = form.querySelector("[data-oys-preference-live]");
        if (!list || !empty || !hidden) { return; }

        function announce(message) {
            if (!live) { return; }
            live.textContent = "";
            window.setTimeout(function () { live.textContent = message; }, 20);
        }

        function clearError() {
            if (!error) { return; }
            error.textContent = "";
        }

        function showError(message) {
            if (!error) { return; }
            error.textContent = message;
        }

        function getItems() {
            return Array.prototype.slice.call(list.querySelectorAll("[data-oys-preference-item]"));
        }

        function syncHiddenInputs() {
            while (hidden.firstChild) {
                hidden.removeChild(hidden.firstChild);
            }
            getItems().forEach(function (item) {
                var input = document.createElement("input");
                input.type = "hidden";
                input.name = "SelectedPreferenceOptionIds";
                input.value = item.getAttribute("data-preference-id") || "";
                hidden.appendChild(input);
            });
        }

        function refreshRanksAndButtons() {
            var items = getItems();
            var hasItems = items.length > 0;
            empty.hidden = hasItems;
            list.hidden = !hasItems;

            items.forEach(function (item, index) {
                var rank = item.querySelector("[data-oys-preference-rank]");
                if (rank) { rank.textContent = String(index + 1); }

                var up = item.querySelector("[data-oys-preference-move-up]");
                var down = item.querySelector("[data-oys-preference-move-down]");
                if (up) { up.disabled = index === 0 || items.length === 1; }
                if (down) { down.disabled = index === items.length - 1 || items.length === 1; }
            });

            syncHiddenInputs();
        }

        function findCheckbox(optionId) {
            return form.querySelector('[data-oys-preference-option][data-preference-id="' + optionId + '"]');
        }

        function createItem(optionId, preferenceName) {
            var li = document.createElement("li");
            li.className = "oys-preference-selected-item";
            li.setAttribute("data-oys-preference-item", "");
            li.setAttribute("data-preference-id", optionId);
            li.setAttribute("data-preference-name", preferenceName);

            var row = document.createElement("div");
            row.className = "oys-preference-selected-row";

            var copy = document.createElement("div");
            copy.className = "oys-preference-selected-copy";

            var title = document.createElement("strong");
            var rank = document.createElement("span");
            rank.setAttribute("data-oys-preference-rank", "");
            title.appendChild(rank);
            title.appendChild(document.createTextNode(". Tercihiniz"));

            var nameEl = document.createElement("div");
            nameEl.setAttribute("data-oys-preference-name", "");
            nameEl.textContent = preferenceName;

            copy.appendChild(title);
            copy.appendChild(nameEl);

            var actions = document.createElement("div");
            actions.className = "oys-preference-selected-actions btn-group";

            function createActionButton(attrName, label) {
                var button = document.createElement("button");
                button.type = "button";
                button.className = "btn btn-white btn-sm";
                button.setAttribute(attrName, "");
                button.textContent = label;
                return button;
            }

            actions.appendChild(createActionButton("data-oys-preference-move-up", "Yukarı Taşı"));
            actions.appendChild(createActionButton("data-oys-preference-move-down", "Aşağı Taşı"));
            actions.appendChild(createActionButton("data-oys-preference-remove", "Kaldır"));

            row.appendChild(copy);
            row.appendChild(actions);
            li.appendChild(row);
            return li;
        }

        function addPreference(optionId, preferenceName) {
            if (list.querySelector('[data-oys-preference-item][data-preference-id="' + optionId + '"]')) {
                return false;
            }
            if (getItems().length >= max) {
                showError("En fazla " + max + " tercih seçebilirsiniz.");
                return false;
            }
            clearError();
            list.appendChild(createItem(optionId, preferenceName));
            refreshRanksAndButtons();
            announce(preferenceName + " " + getItems().length + ". tercihe eklendi.");
            return true;
        }

        function removePreference(optionId, announceRemoval) {
            var item = list.querySelector('[data-oys-preference-item][data-preference-id="' + optionId + '"]');
            if (!item) { return; }
            var name = item.getAttribute("data-preference-name") || "Tercih";
            item.parentNode.removeChild(item);

            var checkbox = findCheckbox(optionId);
            if (checkbox) { checkbox.checked = false; }

            refreshRanksAndButtons();
            clearError();
            if (announceRemoval !== false) {
                announce(name + " tercihlerden kaldırıldı.");
            }
        }

        function movePreference(item, direction, focusButton) {
            var items = getItems();
            var index = items.indexOf(item);
            if (index < 0) { return; }
            var targetIndex = index + direction;
            if (targetIndex < 0 || targetIndex >= items.length) { return; }

            var swapWith = items[targetIndex];
            if (direction < 0) {
                list.insertBefore(item, swapWith);
            } else {
                list.insertBefore(swapWith, item);
            }

            refreshRanksAndButtons();
            clearError();

            var name = item.getAttribute("data-preference-name") || "Tercih";
            var newRank = getItems().indexOf(item) + 1;
            announce(name + " " + newRank + ". tercihe taşındı.");

            if (focusButton && typeof focusButton.focus === "function") {
                window.setTimeout(function () { focusButton.focus(); }, 0);
            }
        }

        form.addEventListener("change", function (event) {
            var checkbox = event.target;
            if (!checkbox || !checkbox.matches || !checkbox.matches("[data-oys-preference-option]")) {
                return;
            }

            var optionId = checkbox.getAttribute("data-preference-id") || checkbox.value;
            var preferenceName = checkbox.getAttribute("data-preference-name") || checkbox.value;

            if (checkbox.checked) {
                if (!addPreference(optionId, preferenceName)) {
                    checkbox.checked = false;
                }
            } else {
                removePreference(optionId, true);
            }
        });

        form.addEventListener("click", function (event) {
            var button = event.target.closest("button");
            if (!button || !form.contains(button)) { return; }

            var item = button.closest("[data-oys-preference-item]");
            if (!item) { return; }

            if (button.hasAttribute("data-oys-preference-remove")) {
                event.preventDefault();
                removePreference(item.getAttribute("data-preference-id"), true);
                return;
            }

            if (button.hasAttribute("data-oys-preference-move-up")) {
                event.preventDefault();
                movePreference(item, -1, button);
                return;
            }

            if (button.hasAttribute("data-oys-preference-move-down")) {
                event.preventDefault();
                movePreference(item, 1, button);
            }
        });

        form.addEventListener("submit", function () {
            refreshRanksAndButtons();
            if (getItems().length === 0) {
                showError("En az bir tercih seçiniz.");
            }
        });

        refreshRanksAndButtons();
    });
}

function oysBindDisabilityToggle() {
    var toggles = document.querySelectorAll(".js-disability-toggle");
    if (!toggles.length) { return; }

    toggles.forEach(function (checkbox) {
        var containerSelector = checkbox.getAttribute("data-disability-container");
        var fieldSelector = checkbox.getAttribute("data-disability-field");
        if (!containerSelector || !fieldSelector) { return; }

        var container = document.querySelector(containerSelector);
        var field = document.querySelector(fieldSelector);
        if (!container || !field) { return; }

        function updateDisabilityDescriptionState() {
            var enabled = checkbox.checked;

            container.hidden = !enabled;
            container.setAttribute("aria-hidden", enabled ? "false" : "true");
            container.classList.remove("site-hidden", "d-none", "hidden");
            field.disabled = !enabled;

            if (!enabled) {
                field.value = "";
                if (typeof field.setCustomValidity === "function") {
                    field.setCustomValidity("");
                }
                field.classList.remove("input-validation-error");
                container.querySelectorAll(".field-validation-error, [data-valmsg-for]").forEach(function (msg) {
                    msg.textContent = "";
                    msg.classList.remove("field-validation-error");
                    msg.classList.add("field-validation-valid");
                });
            }
        }

        checkbox.addEventListener("change", updateDisabilityDescriptionState);
        updateDisabilityDescriptionState();
    });
}

function oysBindAttendanceScoreToggle() {
    if (typeof jQuery === "undefined") { return; }
    var $ = jQuery;
    var $status = $("#attendanceStatus");
    var $score = $("#ExamScore");
    if (!$status.length || !$score.length) { return; }

    var attendedValue = String($status.data("attended-value") || "1");

    function toggleScore() {
        var attended = String($status.val()) === attendedValue;
        $score.prop("disabled", !attended);
        if (!attended) { $score.val(""); }
    }

    $status.on("change", toggleScore);
    toggleScore();
}

/**
 * Older DataTables Buttons 1.2.x does not reliably honor
 * jQuery column selectors like ":not(.no-export)". Resolve numeric indexes
 * from data-export-columns or thead th:not(.no-export) at init time.
 */
function oysResolveExportColumnIndexes($table) {
    var $ = jQuery;
    var attr = $table.attr("data-export-columns");
    if (attr) {
        return attr.split(",").map(function (s) {
            return parseInt($.trim(s), 10);
        }).filter(function (n) {
            return !isNaN(n);
        });
    }

    var indexes = [];
    $table.children("thead").find("th").each(function (i) {
        if (!$(this).hasClass("no-export")) {
            indexes.push(i);
        }
    });
    return indexes;
}

/**
 * Older Buttons 1.2.3 omits nothing critical for arrays, but
 * DataTables maps HTML data-buttons onto the buttons init option as a STRING,
 * which overrides JS config and forces Buttons.defaults without exportOptions.
 * Use data-oys-buttons for our button list, and always pass { buttons: [...] }.
 *
 * CSV: do not use Buttons 1.2.3 bom/charset (they double-encode the BOM and
 * Excel still opens as ANSI). Build CSV ourselves with ';' and a real UTF-8 BOM.
 */
function oysEscapeCsvField(value, fieldBoundary, escapeChar) {
    var text = value == null ? "" : String(value);
    var needsQuotes = text.indexOf(fieldBoundary) !== -1
        || text.indexOf(";") !== -1
        || text.indexOf("\n") !== -1
        || text.indexOf("\r") !== -1;
    if (text.indexOf(fieldBoundary) !== -1) {
        text = text.split(fieldBoundary).join(escapeChar + fieldBoundary);
        needsQuotes = true;
    }
    return needsQuotes ? fieldBoundary + text + fieldBoundary : text;
}

function oysBuildSemicolonCsv(exportData) {
    var fieldBoundary = '"';
    var escapeChar = '"';
    var sep = ";";
    var lines = [];

    function rowToLine(cells) {
        return cells.map(function (cell) {
            return oysEscapeCsvField(cell, fieldBoundary, escapeChar);
        }).join(sep);
    }

    if (exportData.header && exportData.header.length) {
        lines.push(rowToLine(exportData.header));
    }
    if (exportData.body && exportData.body.length) {
        for (var i = 0; i < exportData.body.length; i++) {
            lines.push(rowToLine(exportData.body[i]));
        }
    }
    if (exportData.footer && exportData.footer.length) {
        lines.push(rowToLine(exportData.footer));
    }
    return lines.join("\r\n");
}

function oysDownloadUtf8Csv(filename, csvText) {
    var safeName = (filename || "export").replace(/[\\/:*?"<>|]+/g, "_");
    if (!/\.csv$/i.test(safeName)) {
        safeName += ".csv";
    }

    // BOM as raw bytes — avoids Buttons 1.2.3's "ï»¿" string that Excel shows as Ã¯Â»Â¿
    var bom = new Uint8Array([0xEF, 0xBB, 0xBF]);
    var blob = new Blob([bom, csvText], { type: "text/csv;charset=utf-8;" });
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = safeName;
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
}

function oysBuildPrintCustomize() {
    // Injected last into the about:blank print window after DataTables copies
    // app stylesheets (html/body height:100% can cause a blank 2nd page).
    var css = [
        "html, body {",
        "  height: auto !important;",
        "  min-height: 0 !important;",
        "  margin: 0 !important;",
        "  padding: 0 !important;",
        "  overflow: visible !important;",
        "  background: #fff !important;",
        "}",
        "body.dt-print-view > h1 {",
        "  margin: 0 0 12px 0 !important;",
        "  padding: 0 !important;",
        "}",
        "body.dt-print-view > div:empty {",
        "  display: none !important;",
        "  margin: 0 !important;",
        "  padding: 0 !important;",
        "  height: 0 !important;",
        "}",
        "body.dt-print-view table {",
        "  margin: 0 !important;",
        "  page-break-after: avoid;",
        "  break-after: avoid-page;",
        "}",
        "body.dt-print-view > *:last-child {",
        "  margin-bottom: 0 !important;",
        "  padding-bottom: 0 !important;",
        "}",
        "@media print {",
        "  html, body {",
        "    height: auto !important;",
        "    min-height: 0 !important;",
        "    margin: 0 !important;",
        "    padding: 0 !important;",
        "  }",
        "  body.dt-print-view table {",
        "    page-break-after: avoid;",
        "    break-after: avoid-page;",
        "  }",
        "  body.dt-print-view > *:last-child {",
        "    margin-bottom: 0 !important;",
        "    padding-bottom: 0 !important;",
        "  }",
        "}"
    ].join("\n");

    return function (win) {
        if (!win || !win.document || !win.document.head) { return; }
        var style = win.document.createElement("style");
        style.type = "text/css";
        style.setAttribute("data-oys-dt-print-fix", "1");
        style.appendChild(win.document.createTextNode(css));
        win.document.head.appendChild(style);
    };
}

function oysBuildButtonsConfig(buttonsSpec, exportColumns, exportTitle) {
    var exportOptions = exportColumns.length
        ? { columns: exportColumns.slice() }
        : {};
    var csvExportColumns = exportColumns.slice();

    return {
        buttons: buttonsSpec.split(",").map(function (name) {
            name = name.trim();
            if (name === "csv" || name === "csvHtml5") {
                return {
                    extend: "csvHtml5",
                    text: "CSV",
                    title: exportTitle,
                    exportOptions: jQuery.extend({}, exportOptions),
                    action: function (e, dt) {
                        var exportData = dt.buttons.exportData({
                            columns: csvExportColumns.length ? csvExportColumns : undefined
                        });
                        var csvText = oysBuildSemicolonCsv(exportData);
                        var title = exportTitle || "export";
                        oysDownloadUtf8Csv(title, csvText);
                    }
                };
            }

            if (name === "print") {
                return {
                    extend: "print",
                    exportOptions: jQuery.extend({}, exportOptions),
                    customize: oysBuildPrintCustomize()
                };
            }

            var button = { extend: name, exportOptions: jQuery.extend({}, exportOptions) };
            if (name === "excel" || name === "pdf") {
                button.title = exportTitle;
            }
            return button;
        })
    };
}

/**
 * DataTables lengthMenu için CSV/string → sayı dizisi.
 * Ham string verilirse DataTables karakter karakter option üretir; bu yüzden parse zorunlu.
 * @param {string|null|undefined} raw
 * @returns {number[]}
 */
function oysParseDataTablesLengthMenu(raw) {
    var fallback = [10, 25, 50, 100];
    if (raw == null) { return fallback.slice(); }
    var text = String(raw).trim();
    if (!text) { return fallback.slice(); }

    var parts = text.split(",");
    var lengths = [];
    for (var i = 0; i < parts.length; i++) {
        var n = parseInt(String(parts[i]).trim(), 10);
        if (!isNaN(n) && n > 0 && lengths.indexOf(n) === -1) {
            lengths.push(n);
        }
    }
    return lengths.length > 0 ? lengths : fallback.slice();
}

function oysInitDataTables() {
    if (typeof jQuery === "undefined" || !jQuery.fn || !jQuery.fn.DataTable) { return; }
    var $ = jQuery;

    $(".js-datatable").each(function () {
        var $table = $(this);
        if ($.fn.DataTable.isDataTable($table)) { return; }

        var pageLength = parseInt($table.attr("data-page-length"), 10) || 25;
        var orderCol = parseInt($table.attr("data-order-col"), 10);
        var orderDir = $table.attr("data-order-dir") || "asc";
        var secondaryOrderCol = parseInt($table.attr("data-secondary-order-col"), 10);
        var paging = String($table.attr("data-paging")) !== "false";
        var exportTitle = $table.attr("data-export-title") || "Export";
        var buttonsSpec = String($table.attr("data-oys-buttons") || "").trim();
        var searchPlaceholder = String($table.attr("data-search-placeholder") || "").trim();
        var searchDelay = parseInt($table.attr("data-search-delay"), 10);
        var serverSide = String($table.attr("data-oys-server-side") || "").toLowerCase() === "true";
        var ajaxUrl = String($table.attr("data-ajax-url") || "").trim();
        // data-length-menu DataTables HTML5 config ile string olarak okunur ve karakter karakter
        // option üretir. Yalnız data-oys-length-menu kullan; init öncesi her ikisini de kaldır.
        var lengthMenuRaw = String(
            $table.attr("data-oys-length-menu") || $table.attr("data-length-menu") || ""
        ).trim();

        var language = window.dtLangTR;
        if (searchPlaceholder) {
            language = Object.assign({}, window.dtLangTR, {
                search: "",
                searchPlaceholder: searchPlaceholder
            });
        }

        var options = {
            pageLength: pageLength,
            language: language,
            paging: paging
        };

        if (lengthMenuRaw) {
            var lengths = oysParseDataTablesLengthMenu(lengthMenuRaw);
            options.lengthMenu = [lengths, lengths.map(String)];
            if (lengths.indexOf(pageLength) === -1) {
                options.pageLength = lengths[0];
            }
        }

        if (!isNaN(orderCol)) {
            options.order = [[orderCol, orderDir]];
            if (!isNaN(secondaryOrderCol) && !serverSide) {
                options.order.push([secondaryOrderCol, "asc"]);
            }
        }

        if (buttonsSpec) {
            var exportColumns = oysResolveExportColumnIndexes($table);
            options.dom = '<"html5buttons"B>lTfgitp';
            options.buttons = oysBuildButtonsConfig(buttonsSpec, exportColumns, exportTitle);
        }

        if (serverSide && ajaxUrl) {
            options.processing = true;
            options.serverSide = true;
            options.searchDelay = !isNaN(searchDelay) && searchDelay > 0 ? searchDelay : 400;
            options.ajax = {
                url: ajaxUrl,
                type: "GET",
                dataSrc: function (json) {
                    if (json && json.error) {
                        if (window.toastr) {
                            window.toastr.error(json.error);
                        }
                    }
                    return (json && json.data) ? json.data : [];
                },
                error: function () {
                    if (window.toastr) {
                        window.toastr.error("Tercih seçenekleri yüklenirken bir hata oluştu. Lütfen yeniden deneyiniz.");
                    }
                }
            };
            options.columns = oysBuildPreferenceOptionsServerColumns($table[0]);
            options.columnDefs = [
                { targets: 3, orderable: false, searchable: false }
            ];
            options.createdRow = function (row, data) {
                oysEnhancePreferenceOptionsRow(row, data, $table[0]);
            };
        }

        // Prevent DataTables from treating a leftover data-buttons attr as buttons config.
        $table.removeAttr("data-buttons");
        $table.removeData("buttons");
        // Prevent DataTables HTML5 data-* auto-map of lengthMenu as a raw CSV string.
        $table.removeAttr("data-length-menu");
        $table.removeAttr("data-oys-length-menu");
        $table.removeData("lengthMenu");
        $table.removeData("oysLengthMenu");

        var dataTable = $table.DataTable(options);
        if (serverSide) {
            $table.data("oysPreferenceDataTable", dataTable);
        }
    });
}

function oysGetAntiforgeryTokenValue() {
    var input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : "";
}

function oysBuildPreferenceOptionsServerColumns(table) {
    return [
        { data: "displayOrder" },
        { data: "preferenceName" },
        {
            data: "isActive",
            render: function (data) {
                var active = data === true || data === "true" || data === 1;
                return active ? "Aktif" : "Pasif";
            }
        },
        {
            data: "id",
            orderable: false,
            searchable: false,
            className: "text-right text-nowrap",
            defaultContent: ""
        }
    ];
}

function oysEnhancePreferenceOptionsRow(row, data, table) {
    if (!row || !data) { return; }
    var cells = row.querySelectorAll("td");
    if (!cells || cells.length < 4) { return; }

    var active = data.isActive === true || data.isActive === "true" || data.isActive === 1;
    var statusCell = cells[2];
    while (statusCell.firstChild) { statusCell.removeChild(statusCell.firstChild); }
    var badge = document.createElement("span");
    badge.className = active ? "badge badge-primary" : "badge badge-secondary";
    badge.textContent = active ? "Aktif" : "Pasif";
    statusCell.appendChild(badge);

    var actionsCell = cells[3];
    while (actionsCell.firstChild) { actionsCell.removeChild(actionsCell.firstChild); }
    actionsCell.className = "text-right text-nowrap";

    var id = String(data.id || "");
    var name = String(data.preferenceName || "");
    var editTemplate = table.getAttribute("data-edit-url-template") || "";
    var deleteUrl = table.getAttribute("data-delete-url") || "";

    var edit = document.createElement("a");
    edit.className = "btn btn-xs btn-info";
    edit.href = editTemplate.split("__ID__").join(encodeURIComponent(id));
    edit.title = "Düzenle";
    edit.setAttribute("aria-label", "Düzenle: " + name);
    var editIcon = document.createElement("i");
    editIcon.className = "fa fa-pencil";
    editIcon.setAttribute("aria-hidden", "true");
    var editSr = document.createElement("span");
    editSr.className = "sr-only";
    editSr.textContent = "Düzenle";
    edit.appendChild(editIcon);
    edit.appendChild(editSr);
    actionsCell.appendChild(edit);
    actionsCell.appendChild(document.createTextNode(" "));

    var form = document.createElement("form");
    form.method = "post";
    form.action = deleteUrl;
    form.className = "d-inline";
    form.setAttribute(
        "data-confirm",
        "“" + name + "” tercih seçeneğini silmek istediğinize emin misiniz? Kullanılmış tercihler silinemez."
    );

    var token = document.createElement("input");
    token.type = "hidden";
    token.name = "__RequestVerificationToken";
    token.value = oysGetAntiforgeryTokenValue();
    form.appendChild(token);

    var optionId = document.createElement("input");
    optionId.type = "hidden";
    optionId.name = "optionId";
    optionId.value = id;
    form.appendChild(optionId);

    var del = document.createElement("button");
    del.type = "submit";
    del.className = "btn btn-xs btn-danger";
    del.title = "Sil";
    del.setAttribute("aria-label", "Sil: " + name);
    var delIcon = document.createElement("i");
    delIcon.className = "fa fa-times";
    delIcon.setAttribute("aria-hidden", "true");
    var delSr = document.createElement("span");
    delSr.className = "sr-only";
    delSr.textContent = "Sil";
    del.appendChild(delIcon);
    del.appendChild(delSr);
    form.appendChild(del);
    actionsCell.appendChild(form);
}

// SweetAlert2 onaylı POST formu (data-confirm nitelikli formlar için)
document.addEventListener("submit", function (e) {
    var form = e.target;
    if (!form.matches || !form.matches("form[data-confirm]")) { return; }
    if (form.dataset.confirmed === "true") { return; }
    e.preventDefault();
    var message = form.getAttribute("data-confirm") || "Bu işlemi yapmak istediğinize emin misiniz?";
    if (typeof Swal === "undefined") {
        if (window.confirm(message)) { form.dataset.confirmed = "true"; form.submit(); }
        return;
    }
    Swal.fire({
        title: "Onay",
        text: message,
        icon: "warning",
        showCancelButton: true,
        confirmButtonText: "Evet",
        cancelButtonText: "Vazgeç",
        confirmButtonColor: "#1ab394"
    }).then(function (result) {
        if (result.isConfirmed) { form.dataset.confirmed = "true"; form.submit(); }
    });
});

function oysBasenameFromFileName(name) {
    if (typeof name !== "string" || !name) { return ""; }
    var normalized = name.replace(/\\/g, "/");
    var parts = normalized.split("/");
    return parts[parts.length - 1] || name;
}

function oysFormatSelectedFileSize(bytes) {
    if (typeof bytes !== "number" || !isFinite(bytes) || bytes < 0) { return "0 bayt"; }
    if (bytes < 1024) { return bytes + " bayt"; }
    var kb = bytes / 1024;
    var text = kb >= 10 ? String(Math.round(kb)) : kb.toFixed(1).replace(".", ",");
    return text + " KB";
}

function oysIsFormClientValid(form) {
    if (typeof jQuery !== "undefined" && jQuery.fn.validate) {
        var $form = jQuery(form);
        oysEnsureUnobtrusiveFormValidator(form);
        if ($form.data("validator")) {
            return $form.valid();
        }
    }
    if (typeof form.checkValidity === "function") {
        return form.checkValidity();
    }
    return true;
}

function oysEnsureUnobtrusiveFormValidator(form) {
    if (!form || typeof jQuery === "undefined" || !jQuery.fn.validate) { return; }

    var $form = jQuery(form);
    if ($form.data("validator")) { return; }

    // DOMContentLoaded (site.js) jQuery ready'den (unobtrusive parse) önce çalışabilir.
    // Burada düz $form.validate() çağırmak unobtrusive kurallarını yutar ve yalnızca
    // sonradan .rules() eklenen alanlar doğrulanır.
    if (jQuery.validator && jQuery.validator.unobtrusive) {
        jQuery.validator.unobtrusive.parse(form);
        return;
    }

    $form.validate();
}

function oysFormHasRenderedServerErrors(form) {
    if (!form) { return false; }

    var summary = form.querySelector(".validation-summary-errors");
    if (summary && summary.textContent.trim()) { return true; }

    var fieldErrors = form.querySelectorAll(".field-validation-error");
    for (var i = 0; i < fieldErrors.length; i++) {
        if (fieldErrors[i].textContent.trim()) { return true; }
    }

    return form.querySelectorAll(
        "input.input-validation-error, select.input-validation-error, textarea.input-validation-error"
    ).length > 0;
}

function oysMarkServerValidationForms() {
    document.querySelectorAll("form").forEach(function (form) {
        if (oysFormHasRenderedServerErrors(form)) {
            form.dataset.oysServerValidation = "true";
        }
    });
}

function oysHasServerValidationErrors(form) {
    return !!(form && form.dataset.oysServerValidation === "true");
}

function oysResetClientValidationState(form) {
    if (!form || oysHasServerValidationErrors(form)) { return; }

    form.querySelectorAll("input, select, textarea").forEach(function (el) {
        el.classList.remove("input-validation-error");
        el.removeAttribute("aria-invalid");
    });

    form.querySelectorAll(".field-validation-error").forEach(function (msg) {
        msg.textContent = "";
        msg.classList.remove("field-validation-error");
        msg.classList.add("field-validation-valid");
    });

    var summary = form.querySelector(".validation-summary-errors");
    if (summary) {
        summary.classList.remove("validation-summary-errors");
        summary.classList.add("validation-summary-valid");
    }

    if (typeof jQuery !== "undefined") {
        var $form = jQuery(form);
        var validator = $form.data("validator");
        if (validator) {
            if (typeof validator.hideErrors === "function") {
                validator.hideErrors();
            }
            validator.submitted = {};
            validator.invalid = {};
            validator.errorList = [];
            if (form.elements) {
                validator.successList = Array.prototype.slice.call(form.elements);
            }
        }
    }
}

function oysApplyFormValidationStatePolicy() {
    document.querySelectorAll("form").forEach(function (form) {
        oysResetClientValidationState(form);
    });
}

function oysBindFormValidationStatePolicy() {
    if (window.oysFormValidationStatePageshowBound) { return; }
    window.oysFormValidationStatePageshowBound = true;

    window.addEventListener("pageshow", function (event) {
        if (!event.persisted) { return; }
        document.querySelectorAll("form").forEach(function (form) {
            oysResetClientValidationState(form);
            oysResetSingleSubmitForm(form);
        });
    });
}

function oysEnsureSubmitOverlay(form) {
    var overlay = form.querySelector("[data-oys-submit-overlay]");
    if (overlay) { return overlay; }

    overlay = document.createElement("div");
    overlay.setAttribute("data-oys-submit-overlay", "");
    overlay.className = "oys-submit-overlay";
    overlay.hidden = true;
    overlay.setAttribute("aria-hidden", "true");
    form.appendChild(overlay);
    return overlay;
}

function oysGetSubmitButtons(form) {
    return Array.prototype.slice.call(
        form.querySelectorAll("button[type='submit'], input[type='submit']"));
}

function oysResetSingleSubmitForm(form) {
    if (!form || form.dataset.oysSubmitting !== "true") { return; }

    delete form.dataset.oysSubmitting;
    form.removeAttribute("aria-busy");
    form.classList.remove("oys-is-submitting");

    var overlay = form.querySelector("[data-oys-submit-overlay]");
    if (overlay) {
        overlay.hidden = true;
        overlay.setAttribute("aria-hidden", "true");
    }

    oysGetSubmitButtons(form).forEach(function (submitButton) {
        submitButton.disabled = false;
        submitButton.classList.remove("oys-submit-loading");
        submitButton.removeAttribute("aria-disabled");

        var spinner = submitButton.querySelector(".oys-submit-spinner");
        if (spinner) { spinner.hidden = true; }

        var label = submitButton.querySelector(".oys-submit-label");
        if (label && submitButton.dataset.oysSubmitLabel) {
            label.textContent = submitButton.dataset.oysSubmitLabel;
        } else if (submitButton.tagName === "INPUT" && submitButton.dataset.oysSubmitLabel) {
            submitButton.value = submitButton.dataset.oysSubmitLabel;
        }
    });

    var statusRegion = form.querySelector("[data-oys-submit-status]");
    if (statusRegion) { statusRegion.textContent = ""; }
}

function oysLockSingleSubmitForm(form) {
    var submitButtons = oysGetSubmitButtons(form);
    if (submitButtons.length === 0) { return; }

    form.dataset.oysSubmitting = "true";
    form.setAttribute("aria-busy", "true");
    form.classList.add("oys-is-submitting");

    var overlay = oysEnsureSubmitOverlay(form);
    overlay.hidden = false;
    overlay.setAttribute("aria-hidden", "false");

    if (document.activeElement && form.contains(document.activeElement) && typeof document.activeElement.blur === "function") {
        document.activeElement.blur();
    }

    submitButtons.forEach(function (submitButton) {
        if (!submitButton.dataset.oysSubmitLabel) {
            var labelEl = submitButton.querySelector(".oys-submit-label");
            submitButton.dataset.oysSubmitLabel = labelEl
                ? labelEl.textContent
                : (submitButton.tagName === "INPUT" ? submitButton.value : submitButton.textContent);
        }

        var loadingText = submitButton.getAttribute("data-oys-loading-label")
            || "Yükleniyor, lütfen bekleyiniz…";

        var label = submitButton.querySelector(".oys-submit-label");
        if (label) {
            label.textContent = loadingText;
        } else if (submitButton.tagName === "INPUT") {
            submitButton.value = loadingText;
        } else {
            submitButton.textContent = loadingText;
        }

        var spinner = submitButton.querySelector(".oys-submit-spinner");
        if (spinner) { spinner.hidden = false; }

        submitButton.disabled = true;
        submitButton.classList.add("oys-submit-loading");
        submitButton.setAttribute("aria-disabled", "true");
    });

    var statusRegion = form.querySelector("[data-oys-submit-status]");
    if (statusRegion) {
        var explicitStatus = form.getAttribute("data-oys-loading-status");
        if (explicitStatus) {
            statusRegion.textContent = explicitStatus;
        } else if (form.querySelector("[data-oys-photo-upload]")) {
            statusRegion.textContent = "Fotoğraf yükleniyor, lütfen bekleyiniz…";
        } else {
            statusRegion.textContent = "Yükleniyor, lütfen bekleyiniz…";
        }
    }
}

function oysBindSingleSubmitForms() {
    document.querySelectorAll("form[data-oys-single-submit]").forEach(function (form) {
        if (form.dataset.oysSingleSubmitBound === "true") { return; }
        form.dataset.oysSingleSubmitBound = "true";

        form.addEventListener("submit", function (e) {
            if (form.dataset.oysSubmitting === "true") {
                e.preventDefault();
                e.stopPropagation();
                e.stopImmediatePropagation();
                return;
            }

            if (!oysIsFormClientValid(form)) {
                return;
            }

            oysLockSingleSubmitForm(form);
        });

        form.addEventListener("keydown", function (e) {
            if (e.key !== "Enter" || form.dataset.oysSubmitting !== "true") { return; }
            var target = e.target;
            if (target && target.tagName === "TEXTAREA") { return; }
            e.preventDefault();
        });
    });

    if (!window.oysSingleSubmitPageshowBound) {
        window.oysSingleSubmitPageshowBound = true;
        window.addEventListener("pageshow", function (event) {
            if (!event.persisted) { return; }
            document.querySelectorAll("form[data-oys-single-submit]").forEach(function (form) {
                oysResetSingleSubmitForm(form);
            });
        });
    }
}

function oysBindPhotoFileFeedback() {
    document.querySelectorAll("[data-oys-photo-upload]").forEach(function (root) {
        if (root.dataset.oysPhotoBound === "true") { return; }

        var input = root.querySelector("[data-oys-photo-input]");
        if (!input || input.tagName !== "INPUT" || input.type !== "file") { return; }
        // Kayıt: name="Photo"; profil: name="Photo" (eski name="photo" ile de bağlanır).
        var fieldName = (input.name || "").toLowerCase();
        if (fieldName !== "photo") { return; }

        var status = root.querySelector("[data-oys-photo-status]");
        var validation = root.querySelector("[data-oys-photo-validation]");
        var pickButton = root.querySelector("[data-oys-photo-pick]");
        var previewWrap = root.querySelector("[data-oys-photo-preview-wrap]");
        var preview = root.querySelector("[data-oys-photo-preview]");
        if (!status || !validation) { return; }

        root.dataset.oysPhotoBound = "true";

        var maxBytes = parseInt(input.getAttribute("data-oys-photo-max-bytes") || "0", 10);
        if (!maxBytes || maxBytes < 1) { maxBytes = 2097152; }

        var messages = {
            required: "Vesikalık fotoğrafınızı seçiniz.",
            empty: "Seçilen fotoğraf dosyası boş olamaz.",
            unsupported: "Yalnızca geçerli JPG, JPEG veya PNG fotoğraf yükleyiniz.",
            unreadable: "Seçilen fotoğraf dosyası okunamadı. Lütfen geçerli bir fotoğraf seçiniz.",
            tooLarge: "Fotoğraf dosyası en fazla 2 MB olabilir.",
            noneSelected: "Fotoğraf seçilmedi",
            changeLabel: "Fotoğrafı Değiştir",
            pickLabel: "Fotoğraf Seç"
        };

        var previewUrl = null;
        var validationToken = 0;
        var previewToken = 0;
        var loading = root.querySelector("[data-oys-photo-preview-loading]");

        function setPickLabel(hasFile) {
            if (!pickButton) { return; }
            pickButton.textContent = hasFile ? messages.changeLabel : messages.pickLabel;
        }

        function setLoadingVisible(visible) {
            if (!loading) { return; }
            loading.hidden = !visible;
        }

        function clearPreview() {
            previewToken += 1;
            setLoadingVisible(false);
            if (preview) {
                preview.onload = null;
                preview.onerror = null;
                preview.removeAttribute("src");
                preview.hidden = true;
            }
            if (previewWrap) { previewWrap.hidden = true; }
            previewUrl = null;
        }

        function showPreview(file, selectionToken) {
            if (!preview || !previewWrap || !file) { return; }

            var token = ++previewToken;
            clearPreview();
            // clearPreview artırdığı için token'ı yeniden hizala
            previewToken = token;
            setLoadingVisible(true);
            previewWrap.hidden = false;
            preview.hidden = true;

            var reader = new FileReader();
            reader.onload = function () {
                if (token !== previewToken || selectionToken !== validationToken) { return; }

                var dataUrl = typeof reader.result === "string" ? reader.result : "";
                if (!dataUrl || dataUrl.indexOf("data:image/") !== 0) {
                    clearPreview();
                    setPhotoError(messages.unreadable);
                    return;
                }

                previewUrl = dataUrl;
                preview.onload = function () {
                    if (token !== previewToken || selectionToken !== validationToken) { return; }
                    setLoadingVisible(false);
                    preview.hidden = false;
                    previewWrap.hidden = false;
                };
                preview.onerror = function () {
                    if (token !== previewToken || selectionToken !== validationToken) { return; }
                    clearPreview();
                    setPhotoError(messages.unreadable);
                };
                preview.src = dataUrl;
            };
            reader.onerror = function () {
                if (token !== previewToken || selectionToken !== validationToken) { return; }
                clearPreview();
                setPhotoError(messages.unreadable);
            };
            // CSP img-src yalnızca 'self' ve data: izin verir; object URL kullanılmaz.
            reader.readAsDataURL(file);
        }

        function setPhotoError(message) {
            validation.textContent = message || "";
            validation.classList.remove("field-validation-valid");
            validation.classList.add("field-validation-error");
            input.setAttribute("aria-invalid", message ? "true" : "false");
            if (typeof input.setCustomValidity === "function") {
                input.setCustomValidity(message || "");
            }
        }

        function clearPhotoError() {
            // Sunucu hatası varken istemci temizlemesin; yeniden seçimde kalkar.
            if (root.closest("form") && root.closest("form").dataset.oysServerValidation === "true"
                && validation.classList.contains("field-validation-error")
                && validation.textContent
                && !(input.files && input.files.length)) {
                return;
            }
            validation.textContent = "";
            validation.classList.remove("field-validation-error");
            validation.classList.add("field-validation-valid");
            input.setAttribute("aria-invalid", "false");
            if (typeof input.setCustomValidity === "function") {
                input.setCustomValidity("");
            }
        }

        function resetToEmpty(message) {
            input.value = "";
            status.textContent = messages.noneSelected;
            setPickLabel(false);
            clearPreview();
            if (message) { setPhotoError(message); }
            else { clearPhotoError(); }
        }

        function extensionOf(fileName) {
            var base = oysBasenameFromFileName(fileName).toLowerCase();
            var dot = base.lastIndexOf(".");
            return dot >= 0 ? base.substring(dot) : "";
        }

        function hasCompoundExtension(fileName) {
            var base = oysBasenameFromFileName(fileName);
            var withoutFinal = base.replace(/\.[^.]+$/, "");
            return withoutFinal.indexOf(".") >= 0;
        }

        function isAllowedMime(type) {
            if (!type) { return false; }
            var normalized = String(type).split(";")[0].trim().toLowerCase();
            return normalized === "image/jpeg" || normalized === "image/png";
        }

        function matchesSignature(bytes, extension) {
            if (!bytes || bytes.length < 3) { return false; }
            var isJpeg = bytes[0] === 0xFF && bytes[1] === 0xD8 && bytes[2] === 0xFF;
            var isPng = bytes.length >= 8
                && bytes[0] === 0x89 && bytes[1] === 0x50 && bytes[2] === 0x4E && bytes[3] === 0x47
                && bytes[4] === 0x0D && bytes[5] === 0x0A && bytes[6] === 0x1A && bytes[7] === 0x0A;

            if (extension === ".jpg" || extension === ".jpeg") { return isJpeg; }
            if (extension === ".png") { return isPng; }
            return false;
        }

        function readHeader(file, count) {
            return new Promise(function (resolve, reject) {
                var reader = new FileReader();
                reader.onload = function () {
                    resolve(new Uint8Array(reader.result || new ArrayBuffer(0)));
                };
                reader.onerror = function () { reject(reader.error || new Error("read")); };
                reader.readAsArrayBuffer(file.slice(0, count));
            });
        }

        function applyValidSelection(file, selectionToken) {
            var form = input.form;
            if (form) { delete form.dataset.oysServerValidation; }
            clearPhotoError();
            status.textContent = oysBasenameFromFileName(file.name);
            setPickLabel(true);
            showPreview(file, selectionToken);
        }

        async function validateSelectedFile() {
            var token = ++validationToken;
            var file = input.files && input.files.length > 0 ? input.files[0] : null;
            if (!file) {
                status.textContent = messages.noneSelected;
                setPickLabel(false);
                clearPreview();
                clearPhotoError();
                return;
            }

            if (file.size === 0) {
                resetToEmpty(messages.empty);
                return;
            }

            if (file.size > maxBytes) {
                resetToEmpty(messages.tooLarge);
                return;
            }

            if (hasCompoundExtension(file.name)) {
                resetToEmpty(messages.unsupported);
                return;
            }

            var extension = extensionOf(file.name);
            if (extension !== ".jpg" && extension !== ".jpeg" && extension !== ".png") {
                resetToEmpty(messages.unsupported);
                return;
            }

            if (file.type && !isAllowedMime(file.type)) {
                resetToEmpty(messages.unsupported);
                return;
            }

            try {
                var header = await readHeader(file, 8);
                if (token !== validationToken) { return; }
                if (!matchesSignature(header, extension)) {
                    resetToEmpty(messages.unsupported);
                    return;
                }
                applyValidSelection(file, token);
            } catch (e) {
                if (token !== validationToken) { return; }
                resetToEmpty(messages.unreadable);
            }
        }

        if (pickButton) {
            pickButton.addEventListener("click", function () {
                // Aynı dosyanın yeniden seçiminde change olayının tetiklenmesi için.
                input.value = "";
                input.click();
            });
        }

        input.addEventListener("change", function () {
            validateSelectedFile();
        });

        // İlk yüklemede sunucu hatası yoksa boş durum metni göster.
        if (!(input.files && input.files.length)) {
            status.textContent = messages.noneSelected;
            setPickLabel(false);
            clearPreview();
        }
    });
}

function oysParseBoolParam(value) {
    if (typeof value !== "string") { return false; }
    var normalized = value.trim().toLowerCase();
    return normalized === "true" || normalized === "1";
}

function oysBindInputTextValidation() {
    if (typeof jQuery === "undefined" || !jQuery.validator || typeof window.oysInputText === "undefined") {
        return;
    }
    var $ = jQuery;
    var rules = window.oysInputText;

    if (!$.validator.methods.humanname) {
        $.validator.addMethod("humanname", function (value, element) {
            if (this.optional(element)) { return true; }
            return rules.isValidHumanName(value);
        });
    }

    if (!$.validator.methods.meaningfultitle) {
        $.validator.addMethod("meaningfultitle", function (value, element) {
            if (this.optional(element)) { return true; }
            if (typeof value === "string" && value.trim().length === 0) { return true; }
            return rules.isValidMeaningfulTitle(value);
        });
    }

    if (!$.validator.methods.meaningfultext) {
        $.validator.addMethod("meaningfultext", function (value, element, params) {
            var optional = params && params.optional;
            var multiline = params && params.multiline;
            if (optional && (value == null || String(value).trim().length === 0)) {
                return true;
            }
            if (optional) {
                return rules.isValidOptionalMeaningfulText(value, multiline);
            }
            return rules.isValidRequiredMeaningfulText(value, multiline);
        });
    }

    if ($.validator.unobtrusive && !window.oysInputTextAdaptersBound) {
        window.oysInputTextAdaptersBound = true;
        $.validator.unobtrusive.adapters.addBool("humanname");
        $.validator.unobtrusive.adapters.addBool("meaningfultitle");
        $.validator.unobtrusive.adapters.add("meaningfultext", ["optional", "multiline"], function (options) {
            options.rules[options.name] = {
                optional: oysParseBoolParam(options.params.optional),
                multiline: oysParseBoolParam(options.params.multiline)
            };
            options.messages[options.name] = options.message;
        });
    }
}

function oysBindTurkishIdentityValidation() {
    if (typeof jQuery === "undefined" || !jQuery.validator) { return; }
    var $ = jQuery;

    $.validator.addMethod("turkishidentity", function (value, element) {
        if (this.optional(element)) { return true; }
        return oysIsValidTurkishIdentityNumber(value);
    });

    if ($.validator.unobtrusive) {
        $.validator.unobtrusive.adapters.addBool("turkishidentity");
    }
}

function oysNormalizePassportNumber(value) {
    if (typeof value !== "string") { return ""; }
    var stripped = value.replace(/[\s-]+/g, "").toUpperCase();
    if (!stripped) { return ""; }
    for (var i = 0; i < stripped.length; i++) {
        var ch = stripped.charAt(i);
        var isDigit = ch >= "0" && ch <= "9";
        var isUpper = ch >= "A" && ch <= "Z";
        if (!isDigit && !isUpper) { return ""; }
    }
    return stripped;
}

function oysIsValidTurkishIdentityNumber(value) {
    if (typeof value !== "string") { return false; }
    var tcNo = value.trim();
    if (!/^[1-9][0-9]{10}$/.test(tcNo)) { return false; }

    var digits = [];
    for (var i = 0; i < 11; i++) {
        digits.push(tcNo.charCodeAt(i) - 48);
    }

    var oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
    var evenSum = digits[1] + digits[3] + digits[5] + digits[7];
    var digit10 = ((oddSum * 7) - evenSum) % 10;
    if (digit10 < 0) { digit10 += 10; }
    if (digit10 !== digits[9]) { return false; }

    var sum10 = 0;
    for (var j = 0; j < 10; j++) { sum10 += digits[j]; }
    return (sum10 % 10) === digits[10];
}

function oysIsValidForeignIdentityNumber(value) {
    if (typeof value !== "string") { return false; }
    var digits = value.trim();
    if (!/^\d{11}$/.test(digits)) { return false; }
    return digits.charAt(0) === "9";
}

function oysIsValidPassportNumber(value) {
    var normalized = oysNormalizePassportNumber(value);
    return normalized.length >= 5 && normalized.length <= 20;
}

function oysIsValidCountryCode(value) {
    if (typeof value !== "string") { return false; }
    return /^[A-Za-z]{2}$/.test(value.trim());
}

function oysIsValidForeignNationalityCode(value) {
    if (typeof value !== "string") { return false; }
    var code = value.trim().toUpperCase();
    return /^[A-Z]{2}$/.test(code) && code !== "TR";
}

function oysIsValidPassportExpiry(value) {
    if (!value) { return false; }
    var selected = new Date(value + "T00:00:00");
    if (isNaN(selected.getTime())) { return false; }
    var today = new Date();
    today.setHours(0, 0, 0, 0);
    return selected >= today;
}

function oysBirthDatePartsValidationResult(dayValue, monthValue, yearValue) {
    var day = typeof dayValue === "string" ? dayValue.trim() : "";
    var month = typeof monthValue === "string" ? monthValue.trim() : "";
    var year = typeof yearValue === "string" ? yearValue.trim() : "";
    var any = day || month || year;
    var all = day && month && year;
    if (!any || !all) {
        return { ok: false, message: "Doğum tarihinizi gün, ay ve yıl olarak seçiniz." };
    }

    var dayNum = parseInt(day, 10);
    var monthNum = parseInt(month, 10);
    var yearNum = parseInt(year, 10);
    if (!dayNum || !monthNum || !yearNum) {
        return { ok: false, message: "Geçerli bir doğum tarihi seçiniz." };
    }

    var selected = new Date(yearNum, monthNum - 1, dayNum);
    if (isNaN(selected.getTime())
        || selected.getFullYear() !== yearNum
        || selected.getMonth() !== monthNum - 1
        || selected.getDate() !== dayNum) {
        return { ok: false, message: "Geçerli bir doğum tarihi seçiniz." };
    }

    var today = new Date();
    today.setHours(0, 0, 0, 0);
    selected.setHours(0, 0, 0, 0);
    if (selected.getTime() > today.getTime()) {
        return { ok: false, message: "Doğum tarihi gelecekte olamaz." };
    }
    return { ok: true, message: "" };
}

function oysBirthDateValidationResult(value) {
    if (typeof value !== "string" || !value) {
        return { ok: false, message: "Doğum tarihinizi gün, ay ve yıl olarak seçiniz." };
    }
    var trimmed = value.trim();
    if (!/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) {
        return { ok: false, message: "Geçerli bir doğum tarihi seçiniz." };
    }
    var parts = trimmed.split("-");
    return oysBirthDatePartsValidationResult(parts[2], parts[1], parts[0]);
}

function oysIsValidBirthDate(value) {
    return oysBirthDateValidationResult(value).ok;
}

function oysBindBirthDateParts() {
    if (typeof jQuery === "undefined" || !jQuery.validator) { return; }
    var $ = jQuery;

    if (!$.validator.methods.birthdateparts) {
        $.validator.addMethod("birthdateparts", function (value, element) {
            var root = element.closest("[data-oys-birth-date-root]");
            if (!root) { return true; }
            var day = root.querySelector("[data-oys-birth-day]");
            var month = root.querySelector("[data-oys-birth-month]");
            var year = root.querySelector("[data-oys-birth-year]");
            var result = oysBirthDatePartsValidationResult(
                day ? day.value : "",
                month ? month.value : "",
                year ? year.value : "");
            if (!result.ok) {
                $.validator.messages.birthdateparts = result.message;
            }
            return result.ok;
        }, function () {
            return $.validator.messages.birthdateparts
                || "Doğum tarihinizi gün, ay ve yıl olarak seçiniz.";
        });
    }

    if ($.validator.unobtrusive && !window.oysBirthDatePartsAdapterBound) {
        window.oysBirthDatePartsAdapterBound = true;
        $.validator.unobtrusive.adapters.addBool("birthdateparts");
    }

    document.querySelectorAll("[data-oys-birth-date-root]").forEach(function (root) {
        if (root.dataset.oysBirthDateBound === "true") { return; }
        root.dataset.oysBirthDateBound = "true";

        var day = root.querySelector("[data-oys-birth-day]");
        var month = root.querySelector("[data-oys-birth-month]");
        var year = root.querySelector("[data-oys-birth-year]");
        var validation = root.querySelector("[data-oys-birth-date-validation]");
        if (!day || !month || !year) { return; }

        function setInvalid(isInvalid) {
            [day, month, year].forEach(function (el) {
                el.setAttribute("aria-invalid", isInvalid ? "true" : "false");
            });
        }

        function revalidate() {
            var result = oysBirthDatePartsValidationResult(day.value, month.value, year.value);
            if (typeof jQuery !== "undefined" && year.form) {
                oysEnsureUnobtrusiveFormValidator(year.form);
                var $year = jQuery(year);
                if ($year.length && typeof $year.valid === "function") {
                    $year.valid();
                }
            }
            if (result.ok) {
                setInvalid(false);
                if (validation && validation.classList.contains("field-validation-error")
                    && !(year.form && year.form.dataset.oysServerValidation === "true")) {
                    validation.textContent = "";
                    validation.classList.remove("field-validation-error");
                    validation.classList.add("field-validation-valid");
                }
            } else if (day.value || month.value || year.value) {
                setInvalid(true);
            }
        }

        [day, month, year].forEach(function (el) {
            el.addEventListener("change", revalidate);
        });
    });
}

function oysBindIdentityDocumentValidation() {
    if (typeof jQuery === "undefined" || !jQuery.validator) { return; }
    var $ = jQuery;

    $.validator.addMethod("foreignidentity", function (value, element) {
        if (this.optional(element)) { return true; }
        return oysIsValidForeignIdentityNumber(value);
    });

    $.validator.addMethod("passportidentity", function (value, element) {
        if (this.optional(element)) { return true; }
        return oysIsValidPassportNumber(value);
    });

    $.validator.addMethod("foreigncountrycode", function (value, element) {
        if (this.optional(element)) { return true; }
        return oysIsValidForeignNationalityCode(value);
    });

    $.validator.addMethod("countrycode", function (value, element) {
        if (this.optional(element)) { return true; }
        return oysIsValidCountryCode(value);
    });

    $.validator.addMethod("passportexpiry", function (value, element) {
        if (this.optional(element)) { return true; }
        return oysIsValidPassportExpiry(value);
    });

    $.validator.addMethod("birthdate", function (value, element) {
        if (this.optional(element)) { return true; }
        var result = oysBirthDateValidationResult(value);
        if (!result.ok) {
            $.validator.messages.birthdate = result.message;
        }
        return result.ok;
    }, function () {
        return $.validator.messages.birthdate || "Geçerli bir doğum tarihi giriniz.";
    });

    if ($.validator.unobtrusive) {
        $.validator.unobtrusive.adapters.addBool("birthdate");
    }
}

function oysGetIdentityDocumentTypeValue(form) {
    var typeSelect = form.querySelector("[data-oys-identity-type]");
    if (!typeSelect) { return 1; }
    var parsed = parseInt(typeSelect.value, 10);
    return isNaN(parsed) ? 1 : parsed;
}

function oysSetFieldDisabled(field, disabled) {
    if (!field) { return; }
    field.disabled = disabled;
    if (disabled) {
        field.setAttribute("disabled", "disabled");
    } else {
        field.removeAttribute("disabled");
    }
}

function oysClearFieldValue(field) {
    if (!field) { return; }
    field.value = "";
    if (typeof field.setCustomValidity === "function") {
        field.setCustomValidity("");
    }
    field.classList.remove("input-validation-error");
}

function oysClearIdentityNumberClientError(form, field) {
    oysClearFieldValue(field);
    if (!field) { return; }

    field.setAttribute("aria-invalid", "false");

    if (typeof jQuery === "undefined") { return; }
    var $form = jQuery(form);
    var validator = $form.data("validator");
    var name = field.name;
    if (validator) {
        if (validator.submitted) { delete validator.submitted[name]; }
        if (validator.invalid) { delete validator.invalid[name]; }
        if (validator.errorMap) { delete validator.errorMap[name]; }
        validator.errorList = (validator.errorList || []).filter(function (item) {
            return item.element !== field;
        });
    }

    $form.find("[data-valmsg-for=\"" + name + "\"]")
        .empty()
        .removeClass("field-validation-error")
        .addClass("field-validation-valid");
}

function oysIdentityNumberRequiredMessage(type) {
    if (type === 2) { return "Yabancı kimlik numaranızı giriniz."; }
    if (type === 3) { return "Pasaport numaranızı giriniz."; }
    return "T.C. Kimlik Numaranızı giriniz.";
}

function oysRefreshIdentityValidationRules(form, type) {
    if (typeof jQuery === "undefined" || !jQuery.fn.validate) { return; }
    // site.js DOMContentLoaded, unobtrusive parse (jQuery ready) öncesinde çalışabilir.
    oysEnsureUnobtrusiveFormValidator(form);
    var $form = jQuery(form);
    var validator = $form.data("validator");
    if (!validator) { return; }

    var $identity = $form.find("[data-oys-identity-number]");
    var $nationality = $form.find("[data-oys-nationality-select]");
    var $issuing = $form.find("[data-oys-issuing-select]");
    var $expiry = $form.find("[data-oys-passport-expiry]");
    var requiredMessage = oysIdentityNumberRequiredMessage(type);

    $identity.rules("remove");
    $nationality.rules("remove");
    $issuing.rules("remove");
    $expiry.rules("remove");

    if (type === 1) {
        $identity.rules("add", {
            required: true,
            turkishidentity: true,
            messages: {
                required: requiredMessage,
                turkishidentity: "Geçerli bir T.C. Kimlik Numarası giriniz."
            }
        });
        return;
    }

    if (type === 2) {
        $identity.rules("add", {
            required: true,
            foreignidentity: true,
            messages: {
                required: requiredMessage,
                foreignidentity: "Yabancı Kimlik Numarası 9 ile başlayan 11 haneli olmalıdır."
            }
        });
        $nationality.rules("add", {
            required: true,
            foreigncountrycode: true,
            messages: {
                required: "Uyruğunuzu seçiniz.",
                foreigncountrycode: "Yabancı adaylar için uyruk TR olamaz."
            }
        });
        return;
    }

    $identity.rules("add", {
        required: true,
        passportidentity: true,
        messages: {
            required: requiredMessage,
            passportidentity: "Pasaport numarası 5-20 karakter olmalıdır."
        }
    });
    $nationality.rules("add", {
        required: true,
        foreigncountrycode: true,
        messages: {
            required: "Uyruğunuzu seçiniz.",
            foreigncountrycode: "Yabancı adaylar için uyruk TR olamaz."
        }
    });
    $issuing.rules("add", {
        required: true,
        countrycode: true,
        messages: {
            required: "Pasaportu düzenleyen ülkeyi seçiniz.",
            countrycode: "Geçerli bir ülke seçiniz."
        }
    });
    $expiry.rules("add", {
        required: true,
        passportexpiry: true,
        messages: {
            required: "Pasaport geçerlilik tarihini giriniz.",
            passportexpiry: "Pasaport geçerlilik tarihi geçmiş olamaz."
        }
    });
}

function oysApplyIdentityDocumentVisibility(form, type, announce) {
    var identityInput = form.querySelector("[data-oys-identity-number]");
    var identityLabel = form.querySelector("[data-oys-identity-number-label]");
    var identityHelp = form.querySelector("[data-oys-identity-number-help]");
    var turkishGroup = form.querySelector("[data-oys-turkish-nationality-group]");
    var turkishCode = form.querySelector("[data-oys-turkish-nationality-code]");
    var nationalityGroup = form.querySelector("[data-oys-nationality-group]");
    var nationalitySelect = form.querySelector("[data-oys-nationality-select]");
    var issuingGroup = form.querySelector("[data-oys-issuing-group]");
    var issuingSelect = form.querySelector("[data-oys-issuing-select]");
    var expiryGroup = form.querySelector("[data-oys-passport-expiry-group]");
    var expiryInput = form.querySelector("[data-oys-passport-expiry]");
    var liveRegion = form.querySelector("[data-oys-identity-live]");

    if (identityInput) {
        identityInput.maxLength = type === 1 || type === 2 ? 11 : 32;
        identityInput.inputMode = type === 3 ? "text" : "numeric";
    }

    if (type === 1) {
        if (identityLabel) { identityLabel.textContent = "T.C. Kimlik Numarası"; }
        if (identityHelp) { identityHelp.textContent = "11 haneli T.C. Kimlik Numaranızı giriniz."; }
    } else if (type === 2) {
        if (identityLabel) { identityLabel.textContent = "Yabancı Kimlik Numarası"; }
        if (identityHelp) { identityHelp.textContent = "Yabancı Kimlik Numarası 9 ile başlayan 11 haneli olmalıdır."; }
    } else {
        if (identityLabel) { identityLabel.textContent = "Pasaport Numarası"; }
        if (identityHelp) { identityHelp.textContent = "Pasaport numarası 5-20 karakter; yalnız harf ve rakam kullanılabilir."; }
    }

    var showTurkish = type === 1;
    var showNationality = type === 2 || type === 3;
    var showIssuing = type === 3;
    var showExpiry = type === 3;

    if (turkishGroup) {
        turkishGroup.hidden = !showTurkish;
        turkishGroup.setAttribute("aria-hidden", showTurkish ? "false" : "true");
    }
    if (turkishCode) {
        oysSetFieldDisabled(turkishCode, !showTurkish);
        if (showTurkish) { turkishCode.value = "TR"; }
    }

    if (nationalityGroup) {
        nationalityGroup.hidden = !showNationality;
        nationalityGroup.setAttribute("aria-hidden", showNationality ? "false" : "true");
    }
    if (nationalitySelect) {
        oysSetFieldDisabled(nationalitySelect, !showNationality);
        if (!showNationality) {
            oysClearFieldValue(nationalitySelect);
        } else if (nationalitySelect.value === "TR") {
            oysClearFieldValue(nationalitySelect);
        }
    }

    if (issuingGroup) {
        issuingGroup.hidden = !showIssuing;
        issuingGroup.setAttribute("aria-hidden", showIssuing ? "false" : "true");
    }
    if (issuingSelect) {
        oysSetFieldDisabled(issuingSelect, !showIssuing);
        if (!showIssuing) { oysClearFieldValue(issuingSelect); }
    }

    if (expiryGroup) {
        expiryGroup.hidden = !showExpiry;
        expiryGroup.setAttribute("aria-hidden", showExpiry ? "false" : "true");
    }
    if (expiryInput) {
        oysSetFieldDisabled(expiryInput, !showExpiry);
        if (!showExpiry) { oysClearFieldValue(expiryInput); }
    }

    oysRefreshIdentityValidationRules(form, type);

    if (announce && liveRegion) {
        if (type === 1) {
            liveRegion.textContent = "Kimlik belgesi türü T.C. Kimlik Numarası olarak seçildi.";
        } else if (type === 2) {
            liveRegion.textContent = "Kimlik belgesi türü Yabancı Kimlik Numarası olarak seçildi.";
        } else {
            liveRegion.textContent = "Kimlik belgesi türü Pasaport olarak seçildi.";
        }
    }
}

function oysBindIdentityDocumentForms() {
    document.querySelectorAll("form[data-oys-identity-form]").forEach(function (form) {
        if (form.dataset.oysIdentityFormBound === "true") { return; }
        form.dataset.oysIdentityFormBound = "true";

        var typeSelect = form.querySelector("[data-oys-identity-type]");
        if (!typeSelect) { return; }

        var previousType = oysGetIdentityDocumentTypeValue(form);

        function handleTypeChange() {
            var nextType = oysGetIdentityDocumentTypeValue(form);
            if (nextType !== previousType) {
                var identityInput = form.querySelector("[data-oys-identity-number]");
                oysClearIdentityNumberClientError(form, identityInput);
                previousType = nextType;
            }
            oysApplyIdentityDocumentVisibility(form, nextType, true);
        }

        typeSelect.addEventListener("change", handleTypeChange);
        oysApplyIdentityDocumentVisibility(form, oysGetIdentityDocumentTypeValue(form), false);
    });

    if (!window.oysIdentityFormPageshowBound) {
        window.oysIdentityFormPageshowBound = true;
        window.addEventListener("pageshow", function (event) {
            if (!event.persisted) { return; }
            document.querySelectorAll("form[data-oys-identity-form]").forEach(function (form) {
                oysApplyIdentityDocumentVisibility(form, oysGetIdentityDocumentTypeValue(form), false);
            });
        });
    }
}

function oysLooksLikeLoginEmail(value) {
    if (typeof value !== "string") { return false; }
    var trimmed = value.trim();
    var atIndex = trimmed.indexOf("@");
    if (atIndex <= 0 || atIndex >= trimmed.length - 1) { return false; }
    return trimmed.indexOf(".", atIndex + 1) > atIndex;
}

function oysIsValidLoginEmail(value) {
    if (typeof value !== "string") { return false; }
    var trimmed = value.trim();
    if (!trimmed || trimmed.length > 254) { return false; }
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmed);
}

function oysIsValidLoginIdentifier(value) {
    if (typeof value !== "string") { return false; }
    var trimmed = value.trim();
    if (!trimmed || trimmed.length > 254) { return false; }

    if (oysLooksLikeLoginEmail(trimmed)) {
        return oysIsValidLoginEmail(trimmed);
    }

    if (/^\d{11}$/.test(trimmed) && trimmed.charAt(0) === "9") { return false; }
    if (/[A-Za-z]/.test(trimmed)) { return false; }

    return oysIsValidTurkishIdentityNumber(trimmed);
}

function oysBindLoginIdentifierValidation() {
    if (typeof jQuery === "undefined" || !jQuery.validator) { return; }
    var $ = jQuery;

    if (!$.validator.methods.loginidentifier) {
        $.validator.addMethod("loginidentifier", function (value, element) {
            if (this.optional(element)) { return true; }
            return oysIsValidLoginIdentifier(value);
        }, "Geçerli bir e-posta adresi veya T.C. Kimlik Numarası giriniz.");
    }

    document.querySelectorAll("[data-oys-login-identifier]").forEach(function (input) {
        if (input.dataset.oysLoginIdentifierBound === "true") { return; }

        var form = input.form;
        if (!form || typeof jQuery === "undefined" || !jQuery.fn.validate) { return; }

        var $form = jQuery(form);
        oysEnsureUnobtrusiveFormValidator(form);
        if (!$form.data("validator")) { return; }

        input.dataset.oysLoginIdentifierBound = "true";

        var $input = jQuery(input);
        $input.rules("remove", "loginidentifier turkishidentity");
        $input.rules("add", {
            required: true,
            loginidentifier: true,
            messages: {
                required: input.name === "TcNoOrEmail"
                    ? "E-posta veya T.C. kimlik numaranızı giriniz."
                    : "E-posta veya T.C. kimlik numaranızı giriniz.",
                loginidentifier: "Geçerli bir e-posta adresi veya T.C. Kimlik Numarası giriniz."
            }
        });
    });
}

function oysPhoneDigitsOnly(value) {
    if (typeof value !== "string") { return ""; }
    return value.replace(/\D+/g, "");
}

function oysIsSafePhoneInput(value) {
    if (typeof value !== "string") { return false; }
    return /^[0-9+\-\s().\u00A0\u202F]*$/.test(value);
}

function oysNormalizeTrNationalDigits(raw) {
    var digits = oysPhoneDigitsOnly(raw);
    if (digits.indexOf("90") === 0 && digits.length >= 12) {
        digits = digits.substring(2);
    }
    if (digits.charAt(0) === "0") {
        digits = digits.substring(1);
    }
    if (digits.length > 10) {
        digits = digits.substring(0, 10);
    }
    return digits;
}

function oysFormatTrNationalDisplay(digits) {
    var d = digits || "";
    if (d.length <= 3) { return d; }
    if (d.length <= 6) { return d.substring(0, 3) + " " + d.substring(3); }
    if (d.length <= 8) {
        return d.substring(0, 3) + " " + d.substring(3, 6) + " " + d.substring(6);
    }
    return d.substring(0, 3) + " " + d.substring(3, 6) + " " + d.substring(6, 8) + " " + d.substring(8, 10);
}

function oysCountDigitsBefore(value, caret) {
    var count = 0;
    var limit = Math.min(caret || 0, value.length);
    for (var i = 0; i < limit; i++) {
        if (/\d/.test(value.charAt(i))) { count++; }
    }
    return count;
}

function oysCaretForDigitIndex(formatted, digitIndex) {
    if (digitIndex <= 0) { return 0; }
    var seen = 0;
    for (var i = 0; i < formatted.length; i++) {
        if (/\d/.test(formatted.charAt(i))) {
            seen++;
            if (seen >= digitIndex) { return i + 1; }
        }
    }
    return formatted.length;
}

function oysClientValidateMobilePhone(value, countryCode) {
    if (typeof value !== "string" || !value.trim()) {
        return { ok: false, message: "Cep telefonu numaranızı giriniz." };
    }
    if (value.length > 32 || !oysIsSafePhoneInput(value)) {
        return { ok: false, message: "Geçerli bir cep telefonu numarası giriniz." };
    }

    var region = (countryCode || "TR").toString().trim().toUpperCase();
    if (!/^[A-Z]{2}$/.test(region)) {
        return { ok: false, message: "Telefon için ülke seçiniz." };
    }

    if (value.indexOf("+") >= 0) {
        var intlDigits = oysPhoneDigitsOnly(value);
        if (intlDigits.length < 8 || intlDigits.length > 15) {
            return { ok: false, message: "Geçerli bir cep telefonu numarası giriniz." };
        }
        if (region === "TR") {
            var trDigits = oysNormalizeTrNationalDigits(value);
            if (trDigits.length !== 10 || trDigits.charAt(0) !== "5") {
                return { ok: false, message: "Seçtiğiniz ülkeye uygun geçerli bir cep telefonu numarası giriniz." };
            }
        }
        return { ok: true };
    }

    if (region === "TR") {
        var national = oysNormalizeTrNationalDigits(value);
        if (national.length !== 10 || national.charAt(0) !== "5") {
            return { ok: false, message: "Geçerli bir cep telefonu numarası giriniz." };
        }
        return { ok: true };
    }

    var foreignDigits = oysPhoneDigitsOnly(value);
    if (foreignDigits.length < 6 || foreignDigits.length > 15) {
        return { ok: false, message: "Geçerli bir cep telefonu numarası giriniz." };
    }
    return { ok: true };
}

function oysBindMobilePhoneValidation() {
    if (typeof jQuery === "undefined" || !jQuery.validator) { return; }
    var $ = jQuery;

    if (!$.validator.methods.mobilephone) {
        $.validator.addMethod("mobilephone", function (value, element) {
            if (this.optional(element)) { return true; }
            var form = element.form;
            var country = form
                ? form.querySelector("[data-oys-phone-country], [name='PhoneCountryCode']")
                : null;
            var result = oysClientValidateMobilePhone(value, country ? country.value : "TR");
            if (!result.ok && result.message) {
                $.validator.messages.mobilephone = result.message;
            }
            return result.ok;
        }, "Geçerli bir cep telefonu numarası giriniz.");
    }

    if ($.validator.unobtrusive && !window.oysMobilePhoneAdapterBound) {
        window.oysMobilePhoneAdapterBound = true;
        $.validator.unobtrusive.adapters.add("mobilephone", ["country"], function (options) {
            options.rules.mobilephone = true;
            options.messages.mobilephone = options.message;
        });
    }
}

function oysBindMobilePhoneInputs() {
    oysBindMobilePhoneValidation();

    document.querySelectorAll("[data-oys-phone-root]").forEach(function (root) {
        if (root.dataset.oysPhoneBound === "true") { return; }
        root.dataset.oysPhoneBound = "true";

        var countrySelect = root.querySelector("[data-oys-phone-country]");
        var phoneInput = root.querySelector("[data-oys-phone-input]");
        if (!countrySelect || !phoneInput) { return; }

        var help = root.querySelector("#phone-help");
        var validation = root.querySelector("#phone-validation");
        if (help && validation) {
            phoneInput.setAttribute("aria-describedby", "phone-help phone-validation");
        }

        function currentRegion() {
            return (countrySelect.value || "TR").toUpperCase();
        }

        function setAriaInvalid(isInvalid) {
            phoneInput.setAttribute("aria-invalid", isInvalid ? "true" : "false");
        }

        function clearClientErrorIfFixed() {
            if (typeof jQuery === "undefined" || !phoneInput.form) { return; }
            var $form = jQuery(phoneInput.form);
            var validator = $form.data("validator");
            if (!validator) { return; }
            var result = oysClientValidateMobilePhone(phoneInput.value, currentRegion());
            if (result.ok) {
                validator.element(phoneInput);
                setAriaInvalid(false);
            } else if (phoneInput.value && phoneInput.value.trim()) {
                setAriaInvalid(true);
            }
        }

        function applyTrFormatting(preserveCaret) {
            if (currentRegion() !== "TR") { return; }
            var previous = phoneInput.value;
            var caret = phoneInput.selectionStart || 0;
            var digitIndex = oysCountDigitsBefore(previous, caret);
            var digits = oysNormalizeTrNationalDigits(previous);
            var formatted = oysFormatTrNationalDisplay(digits);
            if (formatted === previous) { return; }
            phoneInput.value = formatted;
            if (preserveCaret && typeof phoneInput.setSelectionRange === "function") {
                var nextCaret = oysCaretForDigitIndex(formatted, digitIndex);
                phoneInput.setSelectionRange(nextCaret, nextCaret);
            }
        }

        function tryApplyInternationalPaste(raw) {
            var trimmed = (raw || "").trim();
            if (trimmed.indexOf("+") !== 0) { return false; }
            var digits = oysPhoneDigitsOnly(trimmed);
            if (digits.indexOf("90") === 0 && (digits.length === 12 || digits.length === 13)) {
                var option = countrySelect.querySelector('option[value="TR"]');
                if (option) {
                    countrySelect.value = "TR";
                    phoneInput.value = oysFormatTrNationalDisplay(oysNormalizeTrNationalDigits(trimmed));
                    updatePlaceholder();
                    return true;
                }
            }
            return false;
        }

        function updatePlaceholder() {
            if (currentRegion() === "TR") {
                phoneInput.placeholder = "5XX XXX XX XX";
            } else {
                var dial = countrySelect.options[countrySelect.selectedIndex];
                var code = dial ? dial.getAttribute("data-dial-code") : "";
                phoneInput.placeholder = code ? ("+" + code + " ...") : "Telefon numarası";
            }
        }

        phoneInput.addEventListener("input", function () {
            if (tryApplyInternationalPaste(phoneInput.value)) {
                clearClientErrorIfFixed();
                return;
            }
            applyTrFormatting(true);
            clearClientErrorIfFixed();
        });

        phoneInput.addEventListener("paste", function (event) {
            var text = "";
            if (event.clipboardData) {
                text = event.clipboardData.getData("text") || "";
            } else if (window.clipboardData) {
                text = window.clipboardData.getData("Text") || "";
            }
            if (!text) { return; }
            if (tryApplyInternationalPaste(text)) {
                event.preventDefault();
                clearClientErrorIfFixed();
            }
        });

        countrySelect.addEventListener("change", function () {
            // Ülke değişiminde numarayı başka ülkeye dönüştürme; yalnızca biçim/placeholder güncelle.
            updatePlaceholder();
            if (currentRegion() === "TR") {
                applyTrFormatting(false);
            }
            clearClientErrorIfFixed();
            if (typeof jQuery === "undefined" || !phoneInput.form) { return; }

            oysEnsureUnobtrusiveFormValidator(phoneInput.form);
            var $form = jQuery(phoneInput.form);
            var validator = $form.data("validator");
            if (!validator) { return; }

            // Ülke select'inin kendi required/regex hatasını temizle veya göster.
            var $country = jQuery(countrySelect);
            if (typeof $country.valid === "function") {
                $country.valid();
            } else {
                validator.element(countrySelect);
            }
            if (phoneInput.value) {
                validator.element(phoneInput);
            }
            countrySelect.setAttribute(
                "aria-invalid",
                countrySelect.value && /^[A-Z]{2}$/i.test(countrySelect.value) ? "false" : "true");
        });

        updatePlaceholder();
        if (currentRegion() === "TR" && phoneInput.value) {
            applyTrFormatting(false);
        }

        if (phoneInput.getAttribute("aria-invalid") !== "true") {
            setAriaInvalid(false);
        }
    });
}

function oysBindDefaultValidationMessages() {
    if (typeof jQuery === "undefined" || !jQuery.validator) { return; }

    // data-val-* / data-msg-* alan mesajları önceliklidir; bunlar yalnızca İngilizce
    // jQuery Validate varsayılanlarına karşı güvenlik ağıdır.
    jQuery.extend(jQuery.validator.messages, {
        required: "Bu bilgiyi giriniz.",
        email: "Geçerli bir e-posta adresi giriniz.",
        number: "Geçerli bir sayı giriniz.",
        digits: "Geçerli bir sayı giriniz.",
        date: "Tarih bilgisini kontrol ediniz.",
        dateISO: "Tarih bilgisini kontrol ediniz.",
        maxlength: jQuery.validator.format("En fazla {0} karakter giriniz."),
        minlength: jQuery.validator.format("En az {0} karakter giriniz."),
        rangelength: jQuery.validator.format("{0}-{1} karakter aralığında giriniz."),
        range: jQuery.validator.format("{0}-{1} aralığında bir değer giriniz."),
        max: jQuery.validator.format("{0} veya daha küçük bir değer giriniz."),
        min: jQuery.validator.format("{0} veya daha büyük bir değer giriniz."),
        equalTo: "Değerler eşleşmiyor.",
        step: "Geçerli bir sayı giriniz."
    });
}

function oysInitializePage() {
    // Bildirimler önce gösterilir; sonraki bir başlatıcı hata verse bile kaybolmaz.
    oysShowToastsFromContainer();
    // Binder'lar DOM'u değiştirmeden önce sunucu hatalarını işaretle.
    oysMarkServerValidationForms();

    var binders = [
        oysBindDefaultValidationMessages,
        oysBindInputTextValidation,
        oysBindTurkishIdentityValidation,
        oysBindIdentityDocumentValidation,
        oysBindLoginIdentifierValidation,
        oysBindPrintCloseAndImages,
        oysBindCaptchaRefresh,
        oysInitSelect2,
        oysBindPreferenceOrderingForms,
        oysBindDisabilityToggle,
        oysBindAttendanceScoreToggle,
        oysBindPhotoFileFeedback,
        oysBindIdentityDocumentForms,
        oysBindBirthDateParts,
        oysBindMobilePhoneInputs,
        oysBindSingleSubmitForms,
        oysBindFormValidationStatePolicy,
        oysInitDataTables
    ];

    for (var i = 0; i < binders.length; i++) {
        try {
            binders[i]();
        } catch (e) {
            if (window.console && typeof window.console.error === "function") {
                window.console.error("OYS init binder failed", e);
            }
        }
    }

    oysApplyFormValidationStatePolicy();
}

// CAPTCHA yenileme document-level delegation kullanır; DOMContentLoaded beklemeden
// kurulur. Böylece sonraki binder hataları yenilemeyi engelleyemez.
oysBindCaptchaRefresh();

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", oysInitializePage, { once: true });
} else {
    oysInitializePage();
}
