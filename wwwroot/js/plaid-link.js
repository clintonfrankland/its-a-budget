window.plaidLink = {
    open: function (linkToken) {
        return new Promise(function (resolve, reject) {
            if (!window.Plaid) {
                reject(new Error("Plaid Link failed to load. Please refresh and try again."));
                return;
            }
            const handler = window.Plaid.create({
                token: linkToken,
                onSuccess: function (publicToken, metadata) {
                    resolve({ publicToken: publicToken, institutionId: metadata.institution ? metadata.institution.institution_id : null, institutionName: metadata.institution ? metadata.institution.name : null });
                },
                onExit: function (error) {
                    if (error) reject(new Error(error.display_message || error.error_message || "Plaid Link was not completed."));
                    else resolve(null);
                }
            });
            handler.open();
        });
    }
};
