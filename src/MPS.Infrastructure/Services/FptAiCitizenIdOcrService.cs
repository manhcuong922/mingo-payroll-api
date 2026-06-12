using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Options;
using MPS.Domain.Exceptions;
using Polly;
using Polly.Retry;

namespace MPS.Infrastructure.Services;

public sealed class FptAiCitizenIdOcrService : ICitizenIdOcrService
{
    private static readonly IReadOnlySet<string> ValidDocTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // CMND (cũ) + CCCD mã vạch
        "cmnd_front", "cmnd_back", "cccd_front", "cccd_back",
        // CCCD gắn chip (2021+) — FPT.AI trả về chip_front/chip_back
        "chip_front", "chip_back",
        // Thẻ Căn cước mới theo Luật Căn cước 2023 (hiệu lực 01/07/2024) — FPT.AI trả về cc_front/cc_back
        "cc_front", "cc_back"
    };

    private static readonly IReadOnlySet<string> BackDocTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "cmnd_back", "cccd_back", "chip_back", "cc_back"
    };

    private readonly HttpClient _http;
    private readonly FptAiOcrOptions _options;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public FptAiCitizenIdOcrService(HttpClient http, IOptions<FptAiOcrOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.BaseAddress = new Uri(_options.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => (int)r.StatusCode >= 500)
            .WaitAndRetryAsync(2, attempt => TimeSpan.FromSeconds(attempt));
    }

    public async Task<CitizenIdOcrResult> RecognizeAsync(Stream imageStream, string contentType, CancellationToken cancellationToken)
        => ParseResponse(await CallFptAsync(imageStream, contentType, cancellationToken));

    public async Task<CitizenIdBackOcrResult> RecognizeBackAsync(Stream imageStream, string contentType, CancellationToken cancellationToken)
        => ParseBackResponse(await CallFptAsync(imageStream, contentType, cancellationToken));

    private async Task<string> CallFptAsync(Stream imageStream, string contentType, CancellationToken cancellationToken)
    {
        // Buffer all bytes once so Polly retries can re-create the body.
        // MultipartFormDataContent.Dispose() closes the StreamContent it owns,
        // which would close imageStream on the first attempt and break any subsequent retry.
        var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms, cancellationToken);
        var imageBytes = ms.ToArray();

        HttpResponseMessage response;
        try
        {
            response = await _retryPolicy.ExecuteAsync(async () =>
            {
                using var form = new MultipartFormDataContent();
                var imageContent = new StreamContent(new MemoryStream(imageBytes));
                imageContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                form.Add(imageContent, "image", "image");

                var req = new HttpRequestMessage(HttpMethod.Post, "/vision/idr/vnm");
                req.Headers.Add("api-key", _options.ApiKey);
                req.Content = form;
                return await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cancellationToken);
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            throw new OcrUnavailableException(ex);
        }

        if (!response.IsSuccessStatusCode)
            throw new OcrUnavailableException(new HttpRequestException($"HTTP {(int)response.StatusCode}"));

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static CitizenIdOcrResult ParseResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("errorCode", out var errCode) && errCode.GetInt32() != 0)
        {
            var errMsg = root.TryGetProperty("errorMessage", out var em) ? em.GetString() : "Unknown error";
            throw new OcrFailedException(errMsg ?? "Unknown error");
        }

        if (!root.TryGetProperty("data", out var dataArr) || dataArr.ValueKind != JsonValueKind.Array || dataArr.GetArrayLength() == 0)
            throw new OcrFailedException("Empty data in OCR response.");

        var data = dataArr[0];

        var docType = data.TryGetProperty("type", out var tp) ? tp.GetString() : null;
        if (docType is null || !ValidDocTypes.Contains(docType))
            throw new OcrInvalidDocumentTypeException(docType);

        DateTime? dob = null;
        if (data.TryGetProperty("dob", out var dobProp) && dobProp.GetString() is { } dobStr)
            dob = TryParseDateVn(dobStr);

        var gender = data.TryGetProperty("sex", out var sexProp) ? MapGender(sexProp.GetString()) : null;

        double? confidence = null;
        if (data.TryGetProperty("confidence", out var confProp) &&
            confProp.TryGetDouble(out var confVal))
            confidence = confVal;

        return new CitizenIdOcrResult(
            FullName: GetString(data, "name"),
            Dob: dob,
            Gender: gender,
            CitizenId: GetString(data, "id"),
            Nationality: GetString(data, "nationality"),
            PermanentAddress: GetString(data, "home"),
            NativePlaceAddress: GetString(data, "home"),      // Quê quán
            ResidenceAddress: GetString(data, "address"),     // Nơi thường trú
            Type: docType,
            RawConfidence: confidence
        );
    }

    private static CitizenIdBackOcrResult ParseBackResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("errorCode", out var errCode) && errCode.GetInt32() != 0)
        {
            var errMsg = root.TryGetProperty("errorMessage", out var em) ? em.GetString() : "Unknown error";
            throw new OcrFailedException(errMsg ?? "Unknown error");
        }

        if (!root.TryGetProperty("data", out var dataArr) || dataArr.ValueKind != JsonValueKind.Array || dataArr.GetArrayLength() == 0)
            throw new OcrFailedException("Empty data in OCR response.");

        var data = dataArr[0];

        var docType = data.TryGetProperty("type", out var tp) ? tp.GetString() : null;
        if (docType is null || !BackDocTypes.Contains(docType))
            throw new OcrInvalidDocumentTypeException(docType);

        DateTime? issuedDate = null;
        if (data.TryGetProperty("issue_date", out var idProp) && idProp.GetString() is { } idStr)
            issuedDate = TryParseDateVn(idStr);

        // FPT đặt tên field không nhất quán giữa các phiên bản (cccd_back / chip_back / cc_back) → thử lần lượt.
        var issuedPlace = GetFirstString(data, "issue_loc", "issue_place", "issued_at", "place_of_issue");

        // Thẻ Căn cước mới (cc_back): "nơi cư trú" và "nơi đăng ký khai sinh" nằm ở mặt sau (Luật Căn cước 2023).
        var residenceAddress = GetFirstString(data, "place_of_residence", "residence", "address", "home");
        var placeOfBirthRegistration = GetFirstString(data, "place_of_birth_registration", "place_of_birth", "birth_place", "pob");

        double? confidence = null;
        if (data.TryGetProperty("confidence", out var confProp) && confProp.TryGetDouble(out var confVal))
            confidence = confVal;

        return new CitizenIdBackOcrResult(issuedDate, issuedPlace, residenceAddress, placeOfBirthRegistration, confidence);
    }

    private static string? GetString(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) ? p.GetString() : null;

    private static string? GetFirstString(JsonElement el, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
            {
                var val = p.GetString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        }
        return null;
    }

    private static DateTime? TryParseDateVn(string s)
    {
        if (DateTime.TryParseExact(s, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        return null;
    }

    private static string? MapGender(string? sex) =>
        sex?.ToUpperInvariant() switch
        {
            "NAM" => "Male",
            "NỮ" or "NU" => "Female",
            _ => sex
        };
}
