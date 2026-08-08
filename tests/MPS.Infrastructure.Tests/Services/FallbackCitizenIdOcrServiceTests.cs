using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Options;
using MPS.Domain.Exceptions;
using MPS.Infrastructure.Services;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Tests cho FallbackCitizenIdOcrService — điểm mấu chốt là fallback CHỈ xảy ra khi
/// Claude không gọi được, không phải khi Claude kết luận ảnh sai.
/// </summary>
public sealed class FallbackCitizenIdOcrServiceTests : IDisposable
{
    private const string ApiKeyEnvVar = "MPS_TEST_FALLBACK_OCR_KEY";

    public FallbackCitizenIdOcrServiceTests()
        => Environment.SetEnvironmentVariable(ApiKeyEnvVar, "test-key");

    public void Dispose()
        => Environment.SetEnvironmentVariable(ApiKeyEnvVar, null);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _respond;
        public int CallCount { get; private set; }

        public StubHandler(Func<HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(_respond());
        }
    }

    private static StubHandler Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(() => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

    private static string ClaudeEnvelope(string modelJson)
        => JsonSerializer.Serialize(new
        {
            type = "message",
            stop_reason = "end_turn",
            content = new[] { new { type = "text", text = modelJson } },
        });

    private const string ClaudeFrontOk =
        """{"type":"chip_front","name":"CLAUDE RESULT","id":"111","dob":"","sex":"NAM","nationality":"","home":"","address":""}""";

    private const string FptFrontOk =
        """{"errorCode":0,"data":[{"type":"chip_front","name":"FPT RESULT","id":"222"}]}""";

    private static FallbackCitizenIdOcrService Build(
        StubHandler claudeHandler,
        StubHandler fptHandler,
        bool claudeEnabled = true,
        bool fallbackEnabled = true,
        string fptApiKey = "fpt-key")
    {
        var anthropicOptions = Options.Create(new AnthropicOcrOptions
        {
            Enabled = claudeEnabled,
            ApiKeyEnvVar = ApiKeyEnvVar,
            BaseUrl = "http://localhost:8317",
            TimeoutSeconds = 10,
            FallbackToFptAi = fallbackEnabled,
        });

        var primary = new AnthropicCitizenIdOcrService(
            new HttpClient(claudeHandler), anthropicOptions, NullLogger<AnthropicCitizenIdOcrService>.Instance);

        var fallback = new FptAiCitizenIdOcrService(
            new HttpClient(fptHandler),
            Options.Create(new FptAiOcrOptions
            {
                ApiKey = fptApiKey,
                BaseUrl = "https://api.fpt.ai",
                TimeoutSeconds = 10,
            }));

        return new FallbackCitizenIdOcrService(
            primary, fallback, anthropicOptions, NullLogger<FallbackCitizenIdOcrService>.Instance);
    }

    private static Stream JpegStream() => new MemoryStream([0xFF, 0xD8, 0xFF]);

    [Fact]
    public async Task Uses_Claude_And_Never_Calls_Fpt_On_Success()
    {
        var claude = Json(ClaudeEnvelope(ClaudeFrontOk));
        var fpt = Json(FptFrontOk);

        var result = await Build(claude, fpt).RecognizeAsync(JpegStream(), "image/jpeg");

        result.FullName.Should().Be("CLAUDE RESULT");
        fpt.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Falls_Back_To_Fpt_When_Claude_Unavailable()
    {
        var claude = Json("{}", HttpStatusCode.ServiceUnavailable);
        var fpt = Json(FptFrontOk);

        var result = await Build(claude, fpt).RecognizeAsync(JpegStream(), "image/jpeg");

        result.FullName.Should().Be("FPT RESULT");
        fpt.CallCount.Should().Be(1);
    }

    /// <summary>
    /// Claude phán "không phải CCCD" là kết luận hợp lệ về tấm ảnh — hỏi lại FPT chỉ tốn thời gian
    /// và có thể ra kết quả mâu thuẫn. Lỗi phải nổi thẳng lên controller thành 422.
    /// </summary>
    [Fact]
    public async Task Does_Not_Fall_Back_When_Claude_Rejects_Document_Type()
    {
        var claude = Json(ClaudeEnvelope(
            """{"type":"not_id_card","name":"","id":"","dob":"","sex":"","nationality":"","home":"","address":""}"""));
        var fpt = Json(FptFrontOk);

        var act = async () => await Build(claude, fpt).RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrInvalidDocumentTypeException>();
        fpt.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Does_Not_Fall_Back_When_Claude_Refuses()
    {
        var claude = Json(JsonSerializer.Serialize(new
        {
            type = "message",
            stop_reason = "refusal",
            content = Array.Empty<object>(),
        }));
        var fpt = Json(FptFrontOk);

        var act = async () => await Build(claude, fpt).RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrFailedException>();
        fpt.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Uses_Fpt_Directly_When_Claude_Disabled()
    {
        var claude = Json(ClaudeEnvelope(ClaudeFrontOk));
        var fpt = Json(FptFrontOk);

        var result = await Build(claude, fpt, claudeEnabled: false).RecognizeAsync(JpegStream(), "image/jpeg");

        result.FullName.Should().Be("FPT RESULT");
        claude.CallCount.Should().Be(0);
    }

    /// <summary>FallbackToFptAi=false: lỗi Claude phải nổi lên nguyên vẹn, không im lặng chuyển provider.</summary>
    [Fact]
    public async Task Propagates_Claude_Failure_When_Fallback_Disabled()
    {
        var claude = Json("{}", HttpStatusCode.ServiceUnavailable);
        var fpt = Json(FptFrontOk);

        var act = async () => await Build(claude, fpt, fallbackEnabled: false).RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrUnavailableException>();
        fpt.CallCount.Should().Be(0);
    }

    /// <summary>FPT không có key (tình trạng hiện tại) — đừng gọi rồi trả lỗi FPT khó hiểu.</summary>
    [Fact]
    public async Task Propagates_Claude_Failure_When_Fpt_Not_Configured()
    {
        var claude = Json("{}", HttpStatusCode.ServiceUnavailable);
        var fpt = Json(FptFrontOk);

        var act = async () => await Build(claude, fpt, fptApiKey: "").RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrUnavailableException>();
        fpt.CallCount.Should().Be(0);
    }

    /// <summary>Composite buffer ảnh một lần — provider thứ hai vẫn phải nhận đủ byte, không phải stream rỗng.</summary>
    [Fact]
    public async Task Replays_Image_Bytes_To_Fallback_Provider()
    {
        var claude = Json("{}", HttpStatusCode.ServiceUnavailable);
        var fpt = Json("""{"errorCode":0,"data":[{"type":"chip_back","issue_date":"01/01/2024","issue_loc":"CỤC CSQLHC"}]}""");

        var result = await Build(claude, fpt).RecognizeBackAsync(JpegStream(), "image/jpeg");

        result.CitizenIssuedDate.Should().Be(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        result.CitizenIssuedPlace.Should().Be("CỤC CSQLHC");
    }
}
