using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[CollectionDefinition(nameof(SqlContainerCollection))]
public sealed class SqlContainerCollection : ICollectionFixture<SqlContainerFixture>;
