window.budgetApp = window.budgetApp || {};

window.budgetApp.downloadFileFromBase64 = (fileName, contentType, base64Data) => {
    const link = document.createElement("a");
    link.href = `data:${contentType};base64,${base64Data}`;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
};
