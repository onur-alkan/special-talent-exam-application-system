namespace OzelYetenekSinavSistemi.Tests.Security;

/// <summary>
/// Serializes tests that invoke <c>dotnet publish</c> against the Web project so they cannot
/// race on shared <c>bin/</c>/<c>obj/</c> intermediate outputs.
/// </summary>
[CollectionDefinition(nameof(DotnetPublishIsolationCollection), DisableParallelization = true)]
public sealed class DotnetPublishIsolationCollection;
