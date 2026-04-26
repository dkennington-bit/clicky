using Clicky.Windows.Services;
using Xunit;

namespace Clicky.Windows.Tests;

public sealed class OpenAIApiKeyStoreContractTests
{
    [Fact]
    public void ApiKeyStoreCanSaveReadAndDeleteKey()
    {
        IOpenAIApiKeyStore apiKeyStore = new InMemoryOpenAIApiKeyStore();

        apiKeyStore.SaveApiKey("sk-test");

        Assert.Equal("sk-test", apiKeyStore.ReadApiKey());

        apiKeyStore.DeleteApiKey();

        Assert.Null(apiKeyStore.ReadApiKey());
    }

    private sealed class InMemoryOpenAIApiKeyStore : IOpenAIApiKeyStore
    {
        private string? apiKey;

        public string? ReadApiKey()
        {
            return apiKey;
        }

        public void SaveApiKey(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public void DeleteApiKey()
        {
            apiKey = null;
        }
    }
}
