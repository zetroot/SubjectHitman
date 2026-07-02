namespace SubjectHitman.IntegrationTests;

/// <summary>
/// Test collection sharing a single application host and container set across test classes.
/// </summary>
[CollectionDefinition(nameof(AppCollection))]
public sealed class AppCollection : ICollectionFixture<IntegrationTestFixture>;
