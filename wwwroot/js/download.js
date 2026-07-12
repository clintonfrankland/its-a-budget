window.budgetApp = window.budgetApp || {};

window.budgetApp.setBudgetItemMenu = (trigger, menu, open) => {
    if (!trigger || !menu) return;

    if (!open) {
        if (menu.matches(':popover-open')) menu.hidePopover();
        return;
    }

    if (!menu.matches(':popover-open')) menu.showPopover();

    const triggerRect = trigger.getBoundingClientRect();
    const menuRect = menu.getBoundingClientRect();
    const gap = 4;
    const edge = 8;
    const roomBelow = window.innerHeight - triggerRect.bottom;
    const openUp = roomBelow < menuRect.height + gap && triggerRect.top > roomBelow;
    const top = openUp
        ? Math.max(edge, triggerRect.top - menuRect.height - gap)
        : Math.min(window.innerHeight - menuRect.height - edge, triggerRect.bottom + gap);
    const left = Math.min(
        Math.max(edge, triggerRect.right - menuRect.width),
        window.innerWidth - menuRect.width - edge);

    menu.style.top = `${Math.max(edge, top)}px`;
    menu.style.left = `${Math.max(edge, left)}px`;
};

window.budgetApp.downloadFileFromBase64 = (fileName, contentType, base64Data) => {
    const link = document.createElement("a");
    link.href = `data:${contentType};base64,${base64Data}`;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
};
