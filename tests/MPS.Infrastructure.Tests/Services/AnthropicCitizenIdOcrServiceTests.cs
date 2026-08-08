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
/// Tests cho AnthropicCitizenIdOcrService — stub HTTP để không gọi mạng thật.
/// Trọng tâm: output phải giống hệt provider FPT.AI (cùng record, cùng exception contract).
/// </summary>
public sealed class AnthropicCitizenIdOcrServiceTests : IDisposable
{
    private const string ApiKeyEnvVar = "MPS_TEST_ANTHROPIC_OCR_KEY";

    public AnthropicCitizenIdOcrServiceTests()
        => Environment.SetEnvironmentVariable(ApiKeyEnvVar, "test-key");

    public void Dispose()
        => Environment.SetEnvironmentVariable(ApiKeyEnvVar, null);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_respond(request));
    }

    /// <summary>Bọc payload JSON của model vào envelope Messages API.</summary>
    private static string MessagesEnvelope(string modelText, string stopReason = "end_turn")
        => JsonSerializer.Serialize(new
        {
            id = "msg_test",
            type = "message",
            role = "assistant",
            model = "claude-opus-5",
            stop_reason = stopReason,
            content = new[] { new { type = "text", text = modelText } },
        });

    /// <summary>
    /// Service dùng <c>using var req</c> nên request bị dispose ngay sau khi gửi — phải đọc
    /// body NGAY trong handler, không giữ lại HttpRequestMessage để đọc sau.
    /// </summary>
    private static AnthropicCitizenIdOcrService BuildService(
        string responseBody,
        HttpStatusCode status = HttpStatusCode.OK,
        Action<HttpRequestMessage, string>? captureRequest = null)
    {
        var handler = new StubHandler(req =>
        {
            captureRequest?.Invoke(req, req.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        });
        return BuildWith(new HttpClient(handler));
    }

    private static AnthropicCitizenIdOcrService BuildThrowingService(Exception ex)
        => BuildWith(new HttpClient(new StubHandler(_ => throw ex)));

    private static AnthropicCitizenIdOcrService BuildWith(HttpClient client, bool enabled = true)
        => new(
            client,
            Options.Create(new AnthropicOcrOptions
            {
                Enabled = enabled,
                ApiKeyEnvVar = ApiKeyEnvVar,
                BaseUrl = "http://localhost:8317",
                Model = "claude-opus-5",
                TimeoutSeconds = 10,
            }),
            NullLogger<AnthropicCitizenIdOcrService>.Instance);

    private static Stream JpegStream() => new MemoryStream([0xFF, 0xD8, 0xFF]);

    // ────────────────────────────── Happy path ──────────────────────────────

    [Fact]
    public async Task RecognizeAsync_Maps_All_Front_Fields()
    {
        var payload = """
            {"type":"chip_front","name":"NGUYỄN VĂN A","id":"012345678901","dob":"15/03/1990",
             "sex":"NAM","nationality":"Việt Nam","home":"Nam Định","address":"Số 1, Cầu Giấy, Hà Nội"}
            """;

        var result = await BuildService(MessagesEnvelope(payload)).RecognizeAsync(JpegStream(), "image/jpeg");

        result.FullName.Should().Be("NGUYỄN VĂN A");
        result.CitizenId.Should().Be("012345678901");
        result.Dob.Should().Be(new DateTime(1990, 3, 15, 0, 0, 0, DateTimeKind.Utc));
        result.Gender.Should().Be("Male");
        result.Nationality.Should().Be("Việt Nam");
        result.NativePlaceAddress.Should().Be("Nam Định");
        result.ResidenceAddress.Should().Be("Số 1, Cầu Giấy, Hà Nội");
        result.Type.Should().Be("chip_front");
    }

    [Theory]
    [InlineData("NAM", "Male")]
    [InlineData("NỮ", "Female")]
    public async Task RecognizeAsync_Maps_Gender_Same_As_FptProvider(string sex, string expected)
    {
        var payload = $$"""{"type":"chip_front","name":"A","id":"1","dob":"","sex":"{{sex}}","nationality":"","home":"","address":""}""";

        var result = await BuildService(MessagesEnvelope(payload)).RecognizeAsync(JpegStream(), "image/jpeg");

        result.Gender.Should().Be(expected);
    }

    /// <summary>Claude trả "" cho field không đọc được; API phải phát ra null như FPT bỏ hẳn key.</summary>
    [Fact]
    public async Task RecognizeAsync_Normalizes_Empty_String_To_Null()
    {
        var payload = """{"type":"cccd_front","name":"A","id":"1","dob":"","sex":"NAM","nationality":"","home":"","address":""}""";

        var result = await BuildService(MessagesEnvelope(payload)).RecognizeAsync(JpegStream(), "image/jpeg");

        result.Dob.Should().BeNull();
        result.Nationality.Should().BeNull();
        result.NativePlaceAddress.Should().BeNull();
        result.ResidenceAddress.Should().BeNull();
    }

    /// <summary>Claude không có confidence hiệu chuẩn — trả null, FE đã khai báo nullable.</summary>
    [Fact]
    public async Task RecognizeAsync_Returns_Null_Confidence()
    {
        var payload = """{"type":"chip_front","name":"A","id":"1","dob":"","sex":"NAM","nationality":"","home":"","address":""}""";

        var result = await BuildService(MessagesEnvelope(payload)).RecognizeAsync(JpegStream(), "image/jpeg");

        result.RawConfidence.Should().BeNull();
    }

    [Fact]
    public async Task RecognizeBackAsync_Maps_NewCanCuoc_Fields()
    {
        var payload = """
            {"type":"cc_back","issue_date":"20/07/2024","issue_loc":"CỤC CẢNH SÁT QUẢN LÝ HÀNH CHÍNH",
             "place_of_residence":"Số 5, Ba Đình, Hà Nội","place_of_birth_registration":"Hải Dương"}
            """;

        var result = await BuildService(MessagesEnvelope(payload)).RecognizeBackAsync(JpegStream(), "image/jpeg");

        result.CitizenIssuedDate.Should().Be(new DateTime(2024, 7, 20, 0, 0, 0, DateTimeKind.Utc));
        result.CitizenIssuedPlace.Should().Be("CỤC CẢNH SÁT QUẢN LÝ HÀNH CHÍNH");
        result.ResidenceAddress.Should().Be("Số 5, Ba Đình, Hà Nội");
        result.PlaceOfBirthRegistration.Should().Be("Hải Dương");
    }

    // ────────────────────────── Ảnh sai / model từ chối ──────────────────────────

    [Fact]
    public async Task RecognizeAsync_Throws_InvalidDocumentType_When_Not_IdCard()
    {
        var payload = """{"type":"not_id_card","name":"","id":"","dob":"","sex":"","nationality":"","home":"","address":""}""";

        var act = async () => await BuildService(MessagesEnvelope(payload)).RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrInvalidDocumentTypeException>();
    }

    [Fact]
    public async Task RecognizeAsync_Throws_InvalidDocumentType_When_Back_Image_Uploaded()
    {
        var payload = """{"type":"chip_back","name":"","id":"","dob":"","sex":"","nationality":"","home":"","address":""}""";

        var act = async () => await BuildService(MessagesEnvelope(payload)).RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrInvalidDocumentTypeException>();
    }

    /// <summary>Refusal trả HTTP 200 với content rỗng — phải bắt trước khi đọc content[0].</summary>
    [Fact]
    public async Task RecognizeAsync_Throws_OcrFailed_On_Refusal_Without_Content()
    {
        var body = JsonSerializer.Serialize(new
        {
            type = "message",
            stop_reason = "refusal",
            stop_details = new { type = "refusal", category = "privacy" },
            content = Array.Empty<object>(),
        });

        var act = async () => await BuildService(body).RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrFailedException>();
    }

    [Fact]
    public async Task RecognizeAsync_Throws_OcrFailed_When_Model_Returns_Non_Json()
    {
        var act = async () => await BuildService(MessagesEnvelope("Xin lỗi, tôi không đọc được ảnh này."))
            .RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrFailedException>();
    }

    /// <summary>Lưới an toàn nếu proxy nuốt output_config và model bọc JSON trong markdown fence.</summary>
    [Fact]
    public async Task RecognizeAsync_Strips_Markdown_Fence_Around_Json()
    {
        var fenced = "```json\n{\"type\":\"cc_front\",\"name\":\"TRẦN THỊ B\",\"id\":\"9\",\"dob\":\"\",\"sex\":\"NỮ\",\"nationality\":\"\",\"home\":\"\",\"address\":\"\"}\n```";

        var result = await BuildService(MessagesEnvelope(fenced)).RecognizeAsync(JpegStream(), "image/jpeg");

        result.FullName.Should().Be("TRẦN THỊ B");
        result.Gender.Should().Be("Female");
    }

    // ────────────────────────────── Không khả dụng ──────────────────────────────

    [Fact]
    public async Task RecognizeAsync_Throws_OcrUnavailable_On_Http_5xx()
    {
        var act = async () => await BuildService("{}", HttpStatusCode.InternalServerError)
            .RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrUnavailableException>();
    }

    [Fact]
    public async Task RecognizeAsync_Throws_OcrUnavailable_On_HttpRequestException()
    {
        var act = async () => await BuildThrowingService(new HttpRequestException("connection refused"))
            .RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrUnavailableException>();
    }

    [Fact]
    public async Task RecognizeAsync_Throws_OcrUnavailable_When_ApiKey_Missing()
    {
        Environment.SetEnvironmentVariable(ApiKeyEnvVar, null);

        var act = async () => await BuildService(MessagesEnvelope("{}")).RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrUnavailableException>();
    }

    [Fact]
    public void IsConfigured_False_When_Disabled()
        => BuildWith(new HttpClient(new StubHandler(_ => new HttpResponseMessage())), enabled: false)
            .IsConfigured.Should().BeFalse();

    // ────────────────────────────── Request shape ──────────────────────────────

    [Fact]
    public async Task RecognizeAsync_Sends_Image_And_StructuredOutput_Schema()
    {
        string? apiKey = null, version = null, uri = null, body = null;
        var payload = """{"type":"chip_front","name":"A","id":"1","dob":"","sex":"NAM","nationality":"","home":"","address":""}""";

        await BuildService(MessagesEnvelope(payload), captureRequest: (req, raw) =>
        {
            apiKey = req.Headers.GetValues("x-api-key").Single();
            version = req.Headers.GetValues("anthropic-version").Single();
            uri = req.RequestUri!.ToString();
            body = raw;
        }).RecognizeAsync(JpegStream(), "image/png");

        apiKey.Should().Be("test-key");
        version.Should().Be("2023-06-01");
        uri.Should().Be("http://localhost:8317/v1/messages");

        body.Should().NotBeNull();
        using var dom = JsonDocument.Parse(body!);
        var root = dom.RootElement;

        root.GetProperty("model").GetString().Should().Be("claude-opus-5");
        root.GetProperty("output_config").GetProperty("format").GetProperty("type").GetString().Should().Be("json_schema");

        var imageSource = root.GetProperty("messages")[0].GetProperty("content")[0].GetProperty("source");
        imageSource.GetProperty("type").GetString().Should().Be("base64");
        imageSource.GetProperty("media_type").GetString().Should().Be("image/png");
        imageSource.GetProperty("data").GetString().Should().Be(Convert.ToBase64String([0xFF, 0xD8, 0xFF]));
    }
}
