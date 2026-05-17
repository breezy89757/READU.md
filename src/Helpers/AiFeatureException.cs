// READU.md — Licensed under the MIT License.

using System;

namespace ReadU.Helpers;

public enum AiFeatureErrorKind
{
    InvalidConfiguration,
    EmptyDocument,
    NetworkFailure,
    EmptyResponse,
    ProviderError
}

public sealed class AiFeatureException : Exception
{
    public AiFeatureErrorKind ErrorKind { get; }
    public string ProviderDetail { get; }

    public AiFeatureException(
        AiFeatureErrorKind errorKind,
        string providerDetail = "",
        Exception innerException = null)
        : base(providerDetail, innerException)
    {
        ErrorKind = errorKind;
        ProviderDetail = providerDetail ?? string.Empty;
    }
}