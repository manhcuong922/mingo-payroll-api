using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Options;
using MPS.Domain.Exceptions;
using MPS.Infrastructure.Services;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// Tests for FptAiCitizenIdOcrService — stubs HTTP via a custom handler to avoid real network calls.
/// </summary>
public sealed class FptAiCitizenIdOcrServiceTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_respond(request));
    }

    private static ICitizenIdOcrService BuildService(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        });

        var client = new HttpClient(handler);
        var options = Options.Create(new FptAiOcrOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://api.fpt.ai",
            TimeoutSeconds = 10,
            MaxImageSizeBytes = 5_242_880,
        });
        return new FptAiCitizenIdOcrService(client, options);
    }

    private static ICitizenIdOcrService BuildThrowingService(Exception ex)
    {
        var handler = new StubHandler(_ => throw ex);
        var client = new HttpClient(handler);
        var options = Options.Create(new FptAiOcrOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://api.fpt.ai",
            TimeoutSeconds = 10,
            MaxImageSizeBytes = 5_242_880,
        });
        return new FptAiCitizenIdOcrService(client, options);
    }

    private static Stream JpegStream() => new MemoryStream([0xFF, 0xD8, 0xFF]);

    // ────────────────────────────── Happy path ──────────────────────────────

    [Fact]
    public async Task RecognizeAsync_Maps_FullName_And_CitizenId()
    {
        const string json = """
            {
              "errorCode": 0,
              "data": [{
                "type": "cccd_front",
                "name": "NGUYEN VAN A",
                "id": "012345678901",
                "dob": "01/01/1990",
                "sex": "NAM",
                "nationality": "Viet Nam",
                "home": "456 Nguyen Trai, Ha Noi"
              }]
            }
            """;

        var result = await BuildService(json).RecognizeAsync(JpegStream(), "image/jpeg");

        result.FullName.Should().Be("NGUYEN VAN A");
        result.CitizenId.Should().Be("012345678901");
        result.Type.Should().Be("cccd_front");
        result.PermanentAddress.Should().Be("456 Nguyen Trai, Ha Noi");
    }

    [Fact]
    public async Task RecognizeAsync_Maps_Dob_Vietnamese_Format()
    {
        const string json = """
            {
              "errorCode": 0,
              "data": [{ "type": "cccd_front", "dob": "15/06/1995", "sex": "NỮ" }]
            }
            """;

        var result = await BuildService(json).RecognizeAsync(JpegStream(), "image/jpeg");

        result.Dob.Should().Be(new DateTime(1995, 6, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("NAM", "Male")]
    [InlineData("NỮ", "Female")]
    [InlineData("NU", "Female")]
    public async Task RecognizeAsync_Maps_Gender(string sexValue, string expectedGender)
    {
        var json = $$"""
            { "errorCode": 0, "data": [{ "type": "cccd_front", "sex": "{{sexValue}}" }] }
            """;

        var result = await BuildService(json).RecognizeAsync(JpegStream(), "image/jpeg");

        result.Gender.Should().Be(expectedGender);
    }

    [Theory]
    [InlineData("cccd_front")]
    [InlineData("cmnd_front")]
    [InlineData("chip_front")]
    [InlineData("cc_front")]
    public async Task RecognizeAsync_Accepts_All_Valid_Front_DocumentTypes(string docType)
    {
        var json = $$"""{ "errorCode": 0, "data": [{ "type": "{{docType}}" }] }""";

        var act = async () => await BuildService(json).RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// Đối xứng với <see cref="RecognizeBackAsync_Throws_When_Front_Image_Uploaded"/>:
    /// endpoint mặt trước phải từ chối ảnh mặt sau, thay vì trả về record toàn null.
    /// </summary>
    [Theory]
    [InlineData("cccd_back")]
    [InlineData("cmnd_back")]
    [InlineData("chip_back")]
    [InlineData("cc_back")]
    public async Task RecognizeAsync_Throws_When_Back_Image_Uploaded(string docType)
    {
        var json = $$"""{ "errorCode": 0, "data": [{ "type": "{{docType}}" }] }""";

        var act = async () => await BuildService(json).RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrInvalidDocumentTypeException>();
    }

    [Fact]
    public async Task RecognizeAsync_Returns_Null_Dob_When_Format_Invalid()
    {
        const string json = """
            { "errorCode": 0, "data": [{ "type": "cccd_front", "dob": "not-a-date" }] }
            """;

        var result = await BuildService(json).RecognizeAsync(JpegStream(), "image/jpeg");

        result.Dob.Should().BeNull();
    }

    // ────────────────────────────── Error paths ──────────────────────────────

    [Fact]
    public async Task RecognizeAsync_Throws_OcrFailedException_When_ErrorCode_NonZero()
    {
        const string json = """{ "errorCode": 1, "errorMessage": "Unrecognized image" }""";

        var act = async () => await BuildService(json).RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrFailedException>()
            .WithMessage("*Unrecognized*");
    }

    [Fact]
    public async Task RecognizeAsync_Throws_OcrFailedException_When_Data_Array_Empty()
    {
        const string json = """{ "errorCode": 0, "data": [] }""";

        var act = async () => await BuildService(json).RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrFailedException>();
    }

    [Fact]
    public async Task RecognizeAsync_Throws_OcrInvalidDocumentTypeException_When_Type_Not_Cccd()
    {
        const string json = """{ "errorCode": 0, "data": [{ "type": "passport" }] }""";

        var act = async () => await BuildService(json).RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrInvalidDocumentTypeException>();
    }

    [Fact]
    public async Task RecognizeAsync_Throws_OcrUnavailableException_When_Http_5xx()
    {
        var act = async () => await BuildService("{}", HttpStatusCode.InternalServerError)
            .RecognizeAsync(JpegStream(), "image/jpeg");

        // Polly retries 2x then service wraps in OcrUnavailableException
        await act.Should().ThrowAsync<OcrUnavailableException>();
    }

    [Fact]
    public async Task RecognizeAsync_Throws_OcrUnavailableException_On_HttpRequestException()
    {
        var act = async () => await BuildThrowingService(new HttpRequestException("Connection refused"))
            .RecognizeAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrUnavailableException>();
    }

    // ─────────────────────── Front: quê quán + nơi thường trú ───────────────────────

    [Fact]
    public async Task RecognizeAsync_Maps_NativePlace_And_Residence_Address()
    {
        const string json = """
            {
              "errorCode": 0,
              "data": [{
                "type": "chip_front",
                "home": "Nam Dinh",
                "address": "456 Nguyen Trai, Ha Noi"
              }]
            }
            """;

        var result = await BuildService(json).RecognizeAsync(JpegStream(), "image/jpeg");

        result.NativePlaceAddress.Should().Be("Nam Dinh");
        result.ResidenceAddress.Should().Be("456 Nguyen Trai, Ha Noi");
    }

    // ────────────────────────────── Back side ──────────────────────────────

    [Fact]
    public async Task RecognizeBackAsync_Maps_IssuedDate_And_IssuedPlace()
    {
        const string json = """
            {
              "errorCode": 0,
              "data": [{
                "type": "chip_back",
                "issue_date": "15/03/2021",
                "issue_loc": "Cuc Canh sat QLHC ve TTXH",
                "confidence": 0.97
              }]
            }
            """;

        var result = await BuildService(json).RecognizeBackAsync(JpegStream(), "image/jpeg");

        result.CitizenIssuedDate.Should().Be(new DateTime(2021, 3, 15, 0, 0, 0, DateTimeKind.Utc));
        result.CitizenIssuedPlace.Should().Be("Cuc Canh sat QLHC ve TTXH");
        result.RawConfidence.Should().Be(0.97);
    }

    [Fact]
    public async Task RecognizeBackAsync_Returns_Null_When_Fields_Absent()
    {
        const string json = """{ "errorCode": 0, "data": [{ "type": "cccd_back" }] }""";

        var result = await BuildService(json).RecognizeBackAsync(JpegStream(), "image/jpeg");

        result.CitizenIssuedDate.Should().BeNull();
        result.CitizenIssuedPlace.Should().BeNull();
    }

    [Fact]
    public async Task RecognizeBackAsync_Maps_Residence_And_PlaceOfBirth_From_NewCanCuoc()
    {
        // Thẻ Căn cước mới (Luật Căn cước 2023) — nơi cư trú + nơi đăng ký khai sinh ở mặt sau.
        const string json = """
            {
              "errorCode": 0,
              "data": [{
                "type": "cc_back",
                "issue_date": "01/08/2024",
                "place_of_residence": "123 Tran Phu, Ha Dong, Ha Noi",
                "place_of_birth_registration": "Xa An Khanh, Hoai Duc, Ha Noi"
              }]
            }
            """;

        var result = await BuildService(json).RecognizeBackAsync(JpegStream(), "image/jpeg");

        result.ResidenceAddress.Should().Be("123 Tran Phu, Ha Dong, Ha Noi");
        result.PlaceOfBirthRegistration.Should().Be("Xa An Khanh, Hoai Duc, Ha Noi");
        result.CitizenIssuedDate.Should().Be(new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task RecognizeBackAsync_Throws_When_Front_Image_Uploaded()
    {
        const string json = """{ "errorCode": 0, "data": [{ "type": "chip_front", "issue_date": "15/03/2021" }] }""";

        var act = async () => await BuildService(json).RecognizeBackAsync(JpegStream(), "image/jpeg");

        await act.Should().ThrowAsync<OcrInvalidDocumentTypeException>();
    }
}
