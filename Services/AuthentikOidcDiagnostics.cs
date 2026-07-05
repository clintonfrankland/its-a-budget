using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ClintonFrankland.Services;

public static class AuthentikOidcDiagnostics
{
    public static readonly EventId MissingRequiredGroup = new(2101, nameof(MissingRequiredGroup));
    public static readonly EventId MissingStableSubject = new(2102, nameof(MissingStableSubject));
    public static readonly EventId UnknownLinkedUser = new(2103, nameof(UnknownLinkedUser));
    public static readonly EventId LinkedUserSuccess = new(2104, nameof(LinkedUserSuccess));
    public static readonly EventId RemoteFailure = new(2105, nameof(RemoteFailure));

    public static void LogMissingRequiredGroup(
        ILogger logger,
        IReadOnlyCollection<string> requiredGroups,
        IReadOnlyCollection<string> receivedGroups)
    {
        logger.LogWarning(
            MissingRequiredGroup,
            "Authentik login denied: required Budget App group missing. RequiredGroups={RequiredGroups}; ReceivedGroupCount={ReceivedGroupCount}; ReceivedGroups={ReceivedGroups}",
            string.Join(",", requiredGroups),
            receivedGroups.Count,
            string.Join(",", receivedGroups));
    }

    public static void LogMissingStableSubject(ILogger logger, string provider)
    {
        logger.LogWarning(
            MissingStableSubject,
            "Authentik login denied: stable subject claim missing. Provider={Provider}",
            provider);
    }

    public static void LogUnknownLinkedUser(ILogger logger, ExternalIdentityProfile profile)
    {
        logger.LogWarning(
            UnknownLinkedUser,
            "Authentik login denied: subject is not linked to an active Budget App user. Provider={Provider}; SubjectFingerprint={SubjectFingerprint}",
            profile.Provider,
            Fingerprint(profile.Subject));
    }

    public static void LogLinkedUserSuccess(
        ILogger logger,
        ExternalIdentityProfile profile,
        int userId,
        IReadOnlyCollection<string> receivedGroups)
    {
        logger.LogInformation(
            LinkedUserSuccess,
            "Authentik login accepted for linked Budget App user. Provider={Provider}; SubjectFingerprint={SubjectFingerprint}; BudgetUserId={BudgetUserId}; ReceivedGroupCount={ReceivedGroupCount}",
            profile.Provider,
            Fingerprint(profile.Subject),
            userId,
            receivedGroups.Count);
    }

    public static void LogRemoteFailure(ILogger logger, Exception? exception)
    {
        logger.LogWarning(
            RemoteFailure,
            exception,
            "Authentik remote login failed before Budget App user mapping completed.");
    }

    public static string Fingerprint(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes, 0, 6).ToLowerInvariant();
    }
}
