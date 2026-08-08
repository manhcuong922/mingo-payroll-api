using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Options;
using MPS.Domain.Exceptions;
using Polly;
using Polly.Retry;

namespace MPS.Infrastructure.Services;

/// <summary>
/// OCR CCCD qua FPT.AI. Từ 2026 giữ lại làm FALLBACK cho
/// <see cref="AnthropicCitizenIdOcrService"/> (FPT ngừng cấp dịch vụ cho tài khoản cá nhân).
///
/// Logic map dữ liệu nằm ở <see cref="CitizenIdOcrParsing"/>, dùng chung với provider Claude.
/// </summary>
public sealed class FptAiCitizenIdOcrService : ICitizenIdOcrService
{
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

    /// <summary>Không có API key thì đừng gọi — dùng để quyết định có fallback sang đây hay không.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<CitizenIdOcrResult> RecognizeAsync(Stream imageStream, string contentType, CancellationToken cancellationToken)
    {
        using var doc = await CallFptAsync(imageStream, contentType, cancellationToken);
        return CitizenIdOcrParsing.ParseFront(FirstData(doc));
    }

    public async Task<CitizenIdBackOcrResult> RecognizeBackAsync(Stream imageStream, string contentType, CancellationToken cancellationToken)
    {
        using var doc = await CallFptAsync(imageStream, contentType, cancellationToken);
        return CitizenIdOcrParsing.ParseBack(FirstData(doc));
    }

    private async Task<JsonDocument> CallFptAsync(Stream imageStream, string contentType, CancellationToken cancellationToken)
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

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new OcrUnavailableException(new HttpRequestException($"HTTP {(int)response.StatusCode}"));

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonDocument.Parse(json);
        }
    }

    /// <summary>Bóc phần tử data[0] từ envelope FPT, ném <see cref="OcrFailedException"/> nếu API báo lỗi.</summary>
    private static JsonElement FirstData(JsonDocument doc)
    {
        var root = doc.RootElement;

        if (root.TryGetProperty("errorCode", out var errCode) && errCode.GetInt32() != 0)
        {
            var errMsg = root.TryGetProperty("errorMessage", out var em) ? em.GetString() : "Unknown error";
            throw new OcrFailedException(errMsg ?? "Unknown error");
        }

        if (!root.TryGetProperty("data", out var dataArr) || dataArr.ValueKind != JsonValueKind.Array || dataArr.GetArrayLength() == 0)
            throw new OcrFailedException("Empty data in OCR response.");

        return dataArr[0];
    }
}
