window.kentico = window.kentico || {};

/**
 * Attachment selector module.
 * @param {object} namespace Namespace under which this module operates.
 */
(function (namespace) {
    // Register the initialization function only in page builder
    if (!namespace.pageBuilder) {
        return;
    }

    var init = function (id, filesData) {
        var component = document.getElementById(id);
        component.openDialog = window.kentico.modalDialog.contentSelector.open;
        component.getString = window.kentico.localization.getString;
        component.selectedFiles = JSON.parse(filesData);
    };

    const modalDialogInternal = namespace._modalDialog = namespace._modalDialog || {};
    const attachmentSelector = modalDialogInternal.attachmentSelector = modalDialogInternal.attachmentSelector || {};
    attachmentSelector.init = init;
})(window.kentico);
window.kentico = window.kentico || {};

/**
 * Media file selector module.
 * @param {object} namespace Namespace under which this module operates.
 */
(function (namespace) {
    // Register the initialization function only in page builder
    if (!namespace.pageBuilder) {
        return;
    }

    var init = function (id, filesData) {
        var component = document.getElementById(id);
        component.openDialog = window.kentico.modalDialog.contentSelector.open;
        component.getString = window.kentico.localization.getString;
        component.selectedFiles = JSON.parse(filesData);
    };

    const modalDialogInternal = namespace._modalDialog = namespace._modalDialog || {};
    const mediaFilesSelector = modalDialogInternal.mediaFilesSelector = modalDialogInternal.mediaFilesSelector || {};
    mediaFilesSelector.init = init;
})(window.kentico);
window.kentico = window.kentico || {};

/**
 * Page selector module.
 * @param {object} namespace Namespace under which this module operates.
 */
(function (namespace) {
    // Register the initialization function only in page builder
    if (!namespace.pageBuilder) {
        return;
    }

    var init = function (id, selectedPageData) {
        var component = document.getElementById(id);
        component.getString = window.kentico.localization.getString;
        component.selectedPageData = selectedPageData;
    };

    const modalDialogInternal = namespace._modalDialog = namespace._modalDialog || {};
    const pageSelector = modalDialogInternal.pageSelector = modalDialogInternal.pageSelector || {};
    pageSelector.init = init;
})(window.kentico);
window.kentico = window.kentico || {};

/**
 * Page selector module.
 * @param {object} namespace Namespace under which this module operates.
 */
(function (namespace) {
    // Register the initialization function only in page builder
    if (!namespace.pageBuilder) {
        return;
    }

    var init = function (id, selectedPageData) {
        var component = document.getElementById(id);
        component.getString = window.kentico.localization.getString;
        component.selectedPageData = selectedPageData;
    };

    const modalDialogInternal = namespace._modalDialog = namespace._modalDialog || {};
    const pathSelector = modalDialogInternal.pathSelector = modalDialogInternal.pathSelector || {};
    pathSelector.init = init;
})(window.kentico);