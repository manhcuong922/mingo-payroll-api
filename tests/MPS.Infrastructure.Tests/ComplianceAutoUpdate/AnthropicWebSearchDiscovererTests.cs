using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Watcher;
using MPS.Application.Options;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Infrastructure.ComplianceAutoUpdate.Ai;

namespace MPS.Infrastructure.Tests.ComplianceAutoUpdate;

/// <summary>Stub HTTP cho AnthropicWebSearchDiscoverer — không gọi mạng thật.</summary>
public sealed class AnthropicWebSearchDiscovererTests
{
    private const string KeyVar = "TEST_WS_KEY";

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _code;
        private readonly string _body;
        public StubHandler(HttpStatusCode code, string body) { _code = code; _body = body; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(_code)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
    }

    private static AnthropicWebSearchDiscoverer Create(HttpStatusCode code, string body)
    {
        Environment.SetEnvironmentVariable(KeyVar, "test-key");
        var options = new ComplianceAutoUpdateOptions { Llm = new LlmOptions { ApiKeyEnvVar = KeyVar, BaseUrl = "" } };
        var http = new HttpClient(new StubHandler(code, body));
        return new AnthropicWebSearchDiscoverer(http, options, NullLogger<AnthropicWebSearchDiscoverer>.Instance);
    }

    private static string Wrap(string innerJson)
        => $$"""{ "content": [ { "type": "text", "text": {{System.Text.Json.JsonSerializer.Serialize(innerJson)}} } ] }""";

    [Fact]
    public async Task Discover_Http400_ThrowsWebSearchUnavailable()
    {
        var sut = Create(HttpStatusCode.BadRequest, "{\"error\":\"tool not supported\"}");

        var act = () => sut.DiscoverNewerAsync(TargetEntityTypes.MinimumWageVersion, "38/2022/NĐ-CP", CancellationToken.None);

        await act.Should().ThrowAsync<WebSearchUnavailableException>();
    }

    [Fact]
    public async Task Discover_HasNewerFalse_ReturnsNull()
    {
        var body = Wrap("""{ "hasNewer": false, "documentNo": null, "effectiveDate": null, "sourceUrl": null, "summary": "Không có bản mới." }""");
        var sut = Create(HttpStatusCode.OK, body);

        var result = await sut.DiscoverNewerAsync(TargetEntityTypes.MinimumWageVersion, "38/2022/NĐ-CP", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Discover_HasNewerTrue_ReturnsDiscoveryWithUrl()
    {
        var body = Wrap("""{ "hasNewer": true, "documentNo": "74/2024/NĐ-CP", "effectiveDate": "2024-07-01", "sourceUrl": "https://example.gov/74", "summary": "Có nghị định mới." }""");
        var sut = Create(HttpStatusCode.OK, body);

        var result = await sut.DiscoverNewerAsync(TargetEntityTypes.MinimumWageVersion, "38/2022/NĐ-CP", CancellationToken.None);

        result.Should().NotBeNull();
        result!.DocumentNo.Should().Be("74/2024/NĐ-CP");
        result.SourceUrl.Should().Be("https://example.gov/74");
        result.EffectiveDate.Should().Be(new DateOnly(2024, 7, 1));
    }
}
