using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.WhatsApp;

namespace StayFlow.Api.Services;

public sealed class WhatsAppCloudClient(
    IHttpClientFactory httpClientFactory,
    IOptions<WhatsAppCloudOptions> options,
    IWhatsAppOutboundSendGate outboundSendGate,
    IWhatsAppProviderTelemetry telemetry,
    ILogger<WhatsAppCloudClient> logger) : IWhatsAppCloudClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<WhatsAppSendTextMessageResult> SendTextMessageAsync(WhatsAppSendTextMessageRequest request, CancellationToken cancellationToken)
    {
        var gate = outboundSendGate.EvaluateRealProviderSend(request.IsIntegrationProductionEnabled);
        if (!gate.Success)
        {
            return CreateSendFailure(gate.FailureCode!, gate.FailureSummary!, "AuthenticationOrConfigurationIssue", null, null, null, false);
        }

        var context = CreateSendContext(request.CompanyId, request.IntegrationId, request.PhoneNumberId, request.GraphApiVersion, request.AccessToken);
        if (context is null)
        {
            return CreateSendFailure("Configuration", "WhatsApp configuration is incomplete.", "Configuration", null, null, null, false);
        }

        var stopwatch = Stopwatch.StartNew();
        var endpoint = $"{context.GraphApiVersion}/{context.PhoneNumberId}/messages";
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "text",
            text = new
            {
                body = request.Body,
                preview_url = false
            }
        };

        var sendResult = await SendWithRetriesAsync(
            context,
            operation: "send_text",
            maxAttempts: Math.Max(1, options.Value.MaxPostRetryAttempts + 1),
            allowTimeoutRetry: false,
            createRequest: () => CreateJsonPost(context, endpoint, payload),
            cancellationToken);

        stopwatch.Stop();
        telemetry.RecordSendResult(
            context.CompanyId,
            context.IntegrationId,
            "text",
            sendResult.Success,
            sendResult.FailureCategory,
            sendResult.HttpStatusCode,
            sendResult.Attempts,
            stopwatch.ElapsedMilliseconds,
            ShortenSupportReference(sendResult.ProviderRequestId));

        return new WhatsAppSendTextMessageResult
        {
            Success = sendResult.Success,
            IsTransientFailure = sendResult.IsTransientFailure,
            ExternalMessageId = sendResult.ExternalMessageId,
            ProviderRequestId = sendResult.ProviderRequestId,
            ProviderTraceId = sendResult.ProviderTraceId,
            HttpStatusCode = sendResult.HttpStatusCode,
            FailureCategory = sendResult.FailureCategory,
            FailureCode = sendResult.FailureCode,
            FailureReason = sendResult.FailureReason
        };
    }

    public async Task<WhatsAppSendTemplateMessageResult> SendTemplateMessageAsync(WhatsAppTemplateSendRequest request, CancellationToken cancellationToken)
    {
        var gate = outboundSendGate.EvaluateRealProviderSend(request.IsIntegrationProductionEnabled);
        if (!gate.Success)
        {
            return CreateTemplateFailure(gate.FailureCode!, gate.FailureSummary!, "AuthenticationOrConfigurationIssue", null, null, null, false);
        }

        var context = CreateSendContext(request.CompanyId, request.IntegrationId, request.PhoneNumberId, request.GraphApiVersion, request.AccessToken);
        if (context is null)
        {
            return CreateTemplateFailure("Configuration", "WhatsApp configuration is incomplete.", "Configuration", null, null, null, false);
        }

        var components = new List<object>();
        if (request.Variables.Count > 0)
        {
            components.Add(new
            {
                type = "body",
                parameters = request.Variables.Select(value => new { type = "text", text = value }).ToArray()
            });
        }

        var endpoint = $"{context.GraphApiVersion}/{context.PhoneNumberId}/messages";
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "template",
            template = new
            {
                name = request.TemplateName,
                language = new { code = request.LanguageCode },
                components = components.ToArray()
            }
        };

        var stopwatch = Stopwatch.StartNew();
        var sendResult = await SendWithRetriesAsync(
            context,
            operation: "send_template",
            maxAttempts: Math.Max(1, options.Value.MaxPostRetryAttempts + 1),
            allowTimeoutRetry: false,
            createRequest: () => CreateJsonPost(context, endpoint, payload),
            cancellationToken);

        stopwatch.Stop();
        telemetry.RecordSendResult(
            context.CompanyId,
            context.IntegrationId,
            "template",
            sendResult.Success,
            sendResult.FailureCategory,
            sendResult.HttpStatusCode,
            sendResult.Attempts,
            stopwatch.ElapsedMilliseconds,
            ShortenSupportReference(sendResult.ProviderRequestId));

        return new WhatsAppSendTemplateMessageResult
        {
            Success = sendResult.Success,
            IsTransientFailure = sendResult.IsTransientFailure,
            ExternalMessageId = sendResult.ExternalMessageId,
            ProviderRequestId = sendResult.ProviderRequestId,
            ProviderTraceId = sendResult.ProviderTraceId,
            HttpStatusCode = sendResult.HttpStatusCode,
            FailureCategory = sendResult.FailureCategory,
            FailureCode = sendResult.FailureCode,
            FailureReason = sendResult.FailureReason
        };
    }

    public async Task<WhatsAppGetTemplatesResult> GetTemplatesAsync(WhatsAppGetTemplatesRequest request, CancellationToken cancellationToken)
    {
        var context = CreateTemplateContext(request.CompanyId, request.IntegrationId, request.WhatsAppBusinessAccountId, request.GraphApiVersion, request.AccessToken);
        if (context is null)
        {
            return new WhatsAppGetTemplatesResult
            {
                Success = false,
                FailureCode = "Configuration",
                FailureReason = "WhatsApp configuration is incomplete.",
                IsTransientFailure = false
            };
        }

        var stopwatch = Stopwatch.StartNew();
        var maxPages = options.Value.MaxTemplateSyncPages;
        var maxItems = options.Value.MaxTemplateSyncItems;
        var baseUri = GetBaseUri();
        var nextPath = $"{context.GraphApiVersion}/{context.WhatsAppBusinessAccountId}/message_templates?limit=50";
        var pageCount = 0;
        var collected = new List<WhatsAppProviderTemplate>();
        var attempts = 0;

        while (!string.IsNullOrWhiteSpace(nextPath) && pageCount < maxPages && collected.Count < maxItems)
        {
            pageCount++;

            var pageResult = await ExecuteWithRetriesAsync(
                context,
                operation: "template_sync_get",
                maxAttempts: Math.Max(1, options.Value.MaxRetryAttempts + 1),
                allowTimeoutRetry: true,
                createRequest: () => CreateGet(context, nextPath),
                cancellationToken);

            attempts += pageResult.Attempts;
            if (!pageResult.Success)
            {
                var mappedFailure = WhatsAppFailureMapper.Map(
                    pageResult.FailureCode,
                    pageResult.FailureReason,
                    pageResult.HttpStatusCode,
                    pageResult.ProviderCode,
                    pageResult.ProviderSubcode,
                    pageResult.IsTransientFailure,
                    pageResult.Exception);

                logger.LogWarning(
                    "WhatsApp template sync request failed. CompanyId={CompanyId} IntegrationId={IntegrationId} HttpStatus={HttpStatus} Category={Category}",
                    context.CompanyId,
                    context.IntegrationId,
                    pageResult.HttpStatusCode,
                    mappedFailure.Category);

                stopwatch.Stop();
                telemetry.RecordTemplateSyncResult(context.CompanyId, context.IntegrationId, false, mappedFailure.Category, attempts, stopwatch.ElapsedMilliseconds);
                return new WhatsAppGetTemplatesResult
                {
                    Success = false,
                    FailureCode = pageResult.FailureCode,
                    FailureReason = mappedFailure.Summary,
                    IsTransientFailure = pageResult.IsTransientFailure
                };
            }

            var body = pageResult.TemplatesResponse;
            if (body?.Data is { Count: > 0 })
            {
                foreach (var item in body.Data)
                {
                    collected.Add(MapTemplate(item));
                    if (collected.Count >= maxItems)
                    {
                        break;
                    }
                }
            }

            nextPath = ResolveSafeNextPath(baseUri, context.GraphApiVersion, body?.Paging?.Next);
        }

        stopwatch.Stop();
        telemetry.RecordTemplateSyncResult(context.CompanyId, context.IntegrationId, true, null, attempts, stopwatch.ElapsedMilliseconds);

        return new WhatsAppGetTemplatesResult
        {
            Success = true,
            Templates = collected
        };
    }

    public async Task<WhatsAppValidateIntegrationResult> ValidateIntegrationAsync(WhatsAppValidateIntegrationRequest request, CancellationToken cancellationToken)
    {
        var result = await GetTemplatesAsync(new WhatsAppGetTemplatesRequest
        {
            CompanyId = request.CompanyId,
            IntegrationId = request.IntegrationId,
            AccessToken = request.AccessToken,
            GraphApiVersion = request.GraphApiVersion,
            WhatsAppBusinessAccountId = request.WhatsAppBusinessAccountId
        }, cancellationToken);

        var mapped = WhatsAppFailureMapper.Map(result.FailureCode, result.FailureReason, null, null, null, result.IsTransientFailure, null);

        return new WhatsAppValidateIntegrationResult
        {
            Success = result.Success,
            IsTransientFailure = result.IsTransientFailure,
            FailureCategory = result.Success ? null : mapped.Category,
            FailureCode = result.FailureCode,
            FailureReason = result.Success ? null : mapped.Summary
        };
    }

    private async Task<SendExecutionResult> SendWithRetriesAsync(
        WhatsAppProviderContext context,
        string operation,
        int maxAttempts,
        bool allowTimeoutRetry,
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        var executeResult = await ExecuteWithRetriesAsync(context, operation, maxAttempts, allowTimeoutRetry, createRequest, cancellationToken);
        if (!executeResult.Success)
        {
            var mapped = WhatsAppFailureMapper.Map(
                executeResult.FailureCode,
                executeResult.FailureReason,
                executeResult.HttpStatusCode,
                executeResult.ProviderCode,
                executeResult.ProviderSubcode,
                executeResult.IsTransientFailure,
                executeResult.Exception);

            return new SendExecutionResult
            {
                Success = false,
                Attempts = executeResult.Attempts,
                ProviderRequestId = executeResult.ProviderRequestId,
                ProviderTraceId = executeResult.ProviderTraceId,
                HttpStatusCode = executeResult.HttpStatusCode,
                FailureCode = executeResult.FailureCode,
                FailureReason = mapped.Summary,
                FailureCategory = mapped.Category,
                IsTransientFailure = IsTransientCategory(mapped.Category)
            };
        }

        return new SendExecutionResult
        {
            Success = true,
            Attempts = executeResult.Attempts,
            ExternalMessageId = executeResult.SendResponse?.Messages?.FirstOrDefault()?.Id,
            ProviderRequestId = executeResult.ProviderRequestId,
            ProviderTraceId = executeResult.ProviderTraceId,
            HttpStatusCode = executeResult.HttpStatusCode
        };
    }

    private async Task<ProviderExecutionResult> ExecuteWithRetriesAsync(
        WhatsAppProviderContext context,
        string operation,
        int maxAttempts,
        bool allowTimeoutRetry,
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(WhatsAppCloudClient));
        var attempts = 0;

        while (attempts < maxAttempts)
        {
            attempts++;
            using var request = createRequest();

            try
            {
                using var response = await client.SendAsync(request, cancellationToken);
                var providerRequestId = GetHeaderValue(response.Headers, "x-request-id") ?? GetHeaderValue(response.Headers, "x-fb-request-id");
                var providerTraceId = GetHeaderValue(response.Headers, "x-fb-trace-id");

                if (response.IsSuccessStatusCode)
                {
                    WhatsAppCloudSendResponse? sendBody = null;
                    WhatsAppCloudTemplatesResponse? templatesBody = null;

                    if (operation.StartsWith("send_", StringComparison.Ordinal))
                    {
                        sendBody = await response.Content.ReadFromJsonAsync<WhatsAppCloudSendResponse>(SerializerOptions, cancellationToken);
                    }
                    else
                    {
                        templatesBody = await response.Content.ReadFromJsonAsync<WhatsAppCloudTemplatesResponse>(SerializerOptions, cancellationToken);
                    }

                    return new ProviderExecutionResult
                    {
                        Success = true,
                        Attempts = attempts,
                        HttpStatusCode = (int)response.StatusCode,
                        ProviderRequestId = providerRequestId,
                        ProviderTraceId = providerTraceId,
                        SendResponse = sendBody,
                        TemplatesResponse = templatesBody
                    };
                }

                var errorBody = await response.Content.ReadFromJsonAsync<WhatsAppCloudErrorEnvelope>(SerializerOptions, cancellationToken);
                var providerCode = errorBody?.Error?.Code;
                var providerSubcode = errorBody?.Error?.ErrorSubcode;
                var isTransient = errorBody?.Error?.IsTransient == true;
                var mapped = WhatsAppFailureMapper.Map(
                    ((int)response.StatusCode).ToString(),
                    errorBody?.Error?.Message,
                    (int)response.StatusCode,
                    providerCode,
                    providerSubcode,
                    isTransient,
                    null);

                if ((int)response.StatusCode == 429)
                {
                    telemetry.RecordRateLimit(context.CompanyId, context.IntegrationId, operation, ParseRetryAfterSeconds(response.Headers));
                }

                var canRetry = attempts < maxAttempts && IsRetryableStatus(response.StatusCode) && IsTransientCategory(mapped.Category);
                if (canRetry)
                {
                    telemetry.RecordRetry(context.CompanyId, context.IntegrationId, operation, attempts, (int)response.StatusCode);
                    await DelayForRetryAsync(response.Headers, attempts, cancellationToken);
                    continue;
                }

                return new ProviderExecutionResult
                {
                    Success = false,
                    Attempts = attempts,
                    HttpStatusCode = (int)response.StatusCode,
                    ProviderRequestId = providerRequestId,
                    ProviderTraceId = providerTraceId,
                    FailureCategory = mapped.Category,
                    FailureCode = providerCode?.ToString() ?? ((int)response.StatusCode).ToString(),
                    FailureReason = errorBody?.Error?.Message,
                    ProviderCode = providerCode,
                    ProviderSubcode = providerSubcode,
                    IsTransientFailure = isTransient
                };
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                var mapped = WhatsAppFailureMapper.Map("Timeout", "Request timed out.", 408, null, null, true, ex);
                var shouldRetry = allowTimeoutRetry && attempts < maxAttempts;
                if (shouldRetry)
                {
                    telemetry.RecordRetry(context.CompanyId, context.IntegrationId, operation, attempts, 408);
                    await DelayForRetryAsync(null, attempts, cancellationToken);
                    continue;
                }

                return new ProviderExecutionResult
                {
                    Success = false,
                    Attempts = attempts,
                    HttpStatusCode = 408,
                    FailureCategory = mapped.Category,
                    FailureCode = "Timeout",
                    FailureReason = mapped.Summary,
                    IsTransientFailure = true,
                    Exception = ex
                };
            }
            catch (HttpRequestException ex)
            {
                var mapped = WhatsAppFailureMapper.Map("TransportFailure", ex.Message, null, null, null, true, ex);
                if (attempts < maxAttempts)
                {
                    telemetry.RecordRetry(context.CompanyId, context.IntegrationId, operation, attempts, null);
                    await DelayForRetryAsync(null, attempts, cancellationToken);
                    continue;
                }

                return new ProviderExecutionResult
                {
                    Success = false,
                    Attempts = attempts,
                    FailureCategory = mapped.Category,
                    FailureCode = "TransportFailure",
                    FailureReason = mapped.Summary,
                    IsTransientFailure = true,
                    Exception = ex
                };
            }
        }

        return new ProviderExecutionResult
        {
            Success = false,
            Attempts = maxAttempts,
            FailureCategory = "ProviderUnavailable",
            FailureCode = "ProviderUnavailable",
            FailureReason = "WhatsApp provider is temporarily unavailable.",
            IsTransientFailure = true
        };
    }

    private static HttpRequestMessage CreateJsonPost(WhatsAppProviderContext context, string endpoint, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
        return request;
    }

    private static HttpRequestMessage CreateGet(WhatsAppProviderContext context, string endpoint)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
        return request;
    }

    private static bool IsRetryableStatus(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    private static bool IsTransientCategory(string? category)
    {
        return category is "RateLimited" or "TemporaryProviderFailure" or "ProviderUnavailable";
    }

    private async Task DelayForRetryAsync(HttpResponseHeaders? headers, int attempt, CancellationToken cancellationToken)
    {
        var retryAfter = ParseRetryAfterSeconds(headers);
        if (retryAfter.HasValue)
        {
            await Task.Delay(TimeSpan.FromSeconds(retryAfter.Value), cancellationToken);
            return;
        }

        var baseDelayMs = options.Value.RetryBaseDelayMilliseconds;
        var cappedDelayMs = options.Value.RetryMaxDelaySeconds * 1000;
        var jitterMs = Random.Shared.Next(0, 200);
        var exponentialMs = Math.Min(cappedDelayMs, baseDelayMs * (int)Math.Pow(2, Math.Max(0, attempt - 1)) + jitterMs);
        await Task.Delay(TimeSpan.FromMilliseconds(exponentialMs), cancellationToken);
    }

    private static int? ParseRetryAfterSeconds(HttpResponseHeaders? headers)
    {
        if (headers is null)
        {
            return null;
        }

        if (headers.RetryAfter?.Delta is { } delta)
        {
            return (int)Math.Ceiling(Math.Max(0, delta.TotalSeconds));
        }

        if (headers.RetryAfter?.Date is { } retryDate)
        {
            return (int)Math.Ceiling(Math.Max(0, (retryDate - DateTimeOffset.UtcNow).TotalSeconds));
        }

        return null;
    }

    private WhatsAppProviderContext? CreateSendContext(Guid companyId, Guid integrationId, string phoneNumberId, string graphApiVersion, string accessToken)
    {
        if (companyId == Guid.Empty
            || integrationId == Guid.Empty
            || string.IsNullOrWhiteSpace(phoneNumberId)
            || string.IsNullOrWhiteSpace(graphApiVersion)
            || string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        return new WhatsAppProviderContext
        {
            CompanyId = companyId,
            IntegrationId = integrationId,
            PhoneNumberId = phoneNumberId.Trim(),
            GraphApiVersion = graphApiVersion.Trim('/'),
            AccessToken = accessToken.Trim()
        };
    }

    private WhatsAppProviderContext? CreateTemplateContext(Guid companyId, Guid integrationId, string wabaId, string graphApiVersion, string accessToken)
    {
        if (companyId == Guid.Empty
            || integrationId == Guid.Empty
            || string.IsNullOrWhiteSpace(wabaId)
            || string.IsNullOrWhiteSpace(graphApiVersion)
            || string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        return new WhatsAppProviderContext
        {
            CompanyId = companyId,
            IntegrationId = integrationId,
            WhatsAppBusinessAccountId = wabaId.Trim(),
            GraphApiVersion = graphApiVersion.Trim('/'),
            AccessToken = accessToken.Trim()
        };
    }

    private Uri GetBaseUri()
    {
        var currentOptions = options.Value;
        var client = httpClientFactory.CreateClient(nameof(WhatsAppCloudClient));
        if (client.BaseAddress is not null)
        {
            return client.BaseAddress;
        }

        return new Uri(currentOptions.GraphApiBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
    }

    private static string? ResolveSafeNextPath(Uri baseUri, string graphApiVersion, string? next)
    {
        if (string.IsNullOrWhiteSpace(next))
        {
            return null;
        }

        if (!Uri.TryCreate(next, UriKind.Absolute, out var nextUri))
        {
            return null;
        }

        if (!string.Equals(nextUri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(nextUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            || nextUri.Port != baseUri.Port)
        {
            return null;
        }

        var versionPrefix = "/" + graphApiVersion.Trim('/') + "/";
        if (!nextUri.AbsolutePath.StartsWith(versionPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return nextUri.PathAndQuery.TrimStart('/');
    }

    private static string? GetHeaderValue(HttpResponseHeaders headers, string name)
    {
        if (headers.TryGetValues(name, out var values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    private static string? ShortenSupportReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 12 ? trimmed : trimmed[..12];
    }

    private static WhatsAppProviderTemplate MapTemplate(WhatsAppCloudTemplate template)
    {
        var body = template.Components?.FirstOrDefault(component => string.Equals(component.Type, "BODY", StringComparison.OrdinalIgnoreCase));
        var header = template.Components?.FirstOrDefault(component => string.Equals(component.Type, "HEADER", StringComparison.OrdinalIgnoreCase));
        var footer = template.Components?.FirstOrDefault(component => string.Equals(component.Type, "FOOTER", StringComparison.OrdinalIgnoreCase));
        var bodyText = body?.Text ?? string.Empty;
        var placeholders = ExtractPlaceholders(bodyText);

        return new WhatsAppProviderTemplate
        {
            ExternalTemplateId = template.Id ?? string.Empty,
            Name = template.Name ?? string.Empty,
            LanguageCode = template.Language ?? string.Empty,
            Category = template.Category ?? "UNKNOWN",
            Status = template.Status ?? "UNKNOWN",
            HeaderType = header?.Format,
            BodyText = bodyText,
            FooterText = footer?.Text,
            Placeholders = placeholders,
            ComponentsJson = JsonSerializer.Serialize(template.Components ?? [], SerializerOptions)
        };
    }

    private static IReadOnlyCollection<string> ExtractPlaceholders(string bodyText)
    {
        var matches = Regex.Matches(bodyText, "\\{\\{\\d+\\}\\}");
        return matches.Select(match => match.Value).Distinct(StringComparer.Ordinal).ToList();
    }

    private static WhatsAppSendTextMessageResult CreateSendFailure(
        string? code,
        string? reason,
        string category,
        int? httpStatusCode,
        string? providerRequestId,
        string? providerTraceId,
        bool isTransient)
    {
        return new WhatsAppSendTextMessageResult
        {
            Success = false,
            IsTransientFailure = isTransient,
            FailureCode = code,
            FailureReason = reason,
            FailureCategory = category,
            HttpStatusCode = httpStatusCode,
            ProviderRequestId = providerRequestId,
            ProviderTraceId = providerTraceId
        };
    }

    private static WhatsAppSendTemplateMessageResult CreateTemplateFailure(
        string? code,
        string? reason,
        string category,
        int? httpStatusCode,
        string? providerRequestId,
        string? providerTraceId,
        bool isTransient)
    {
        return new WhatsAppSendTemplateMessageResult
        {
            Success = false,
            IsTransientFailure = isTransient,
            FailureCode = code,
            FailureReason = reason,
            FailureCategory = category,
            HttpStatusCode = httpStatusCode,
            ProviderRequestId = providerRequestId,
            ProviderTraceId = providerTraceId
        };
    }

    private sealed class SendExecutionResult
    {
        public bool Success { get; init; }
        public int Attempts { get; init; }
        public bool IsTransientFailure { get; init; }
        public string? ExternalMessageId { get; init; }
        public string? ProviderRequestId { get; init; }
        public string? ProviderTraceId { get; init; }
        public int? HttpStatusCode { get; init; }
        public string? FailureCategory { get; init; }
        public string? FailureCode { get; init; }
        public string? FailureReason { get; init; }
    }

    private sealed class ProviderExecutionResult
    {
        public bool Success { get; init; }
        public int Attempts { get; init; }
        public int? HttpStatusCode { get; init; }
        public string? ProviderRequestId { get; init; }
        public string? ProviderTraceId { get; init; }
        public string? FailureCode { get; init; }
        public string? FailureReason { get; init; }
        public string? FailureCategory { get; init; }
        public int? ProviderCode { get; init; }
        public int? ProviderSubcode { get; init; }
        public bool IsTransientFailure { get; init; }
        public Exception? Exception { get; init; }
        public WhatsAppCloudSendResponse? SendResponse { get; init; }
        public WhatsAppCloudTemplatesResponse? TemplatesResponse { get; init; }
    }

    private sealed class WhatsAppCloudSendResponse
    {
        [JsonPropertyName("messages")]
        public IReadOnlyCollection<WhatsAppCloudSendMessage>? Messages { get; init; }

        [JsonPropertyName("contacts")]
        public IReadOnlyCollection<WhatsAppCloudContact>? Contacts { get; init; }
    }

    private sealed class WhatsAppCloudContact
    {
        [JsonPropertyName("wa_id")]
        public string? WhatsAppId { get; init; }
    }

    private sealed class WhatsAppCloudTemplatesResponse
    {
        [JsonPropertyName("data")]
        public IReadOnlyCollection<WhatsAppCloudTemplate>? Data { get; init; }

        [JsonPropertyName("paging")]
        public WhatsAppCloudPaging? Paging { get; init; }
    }

    private sealed class WhatsAppCloudPaging
    {
        [JsonPropertyName("next")]
        public string? Next { get; init; }
    }

    private sealed class WhatsAppCloudTemplate
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("language")]
        public string? Language { get; init; }

        [JsonPropertyName("category")]
        public string? Category { get; init; }

        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("components")]
        public IReadOnlyCollection<WhatsAppCloudTemplateComponent>? Components { get; init; }
    }

    private sealed class WhatsAppCloudTemplateComponent
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("format")]
        public string? Format { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    private sealed class WhatsAppCloudSendMessage
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }
    }

    private sealed class WhatsAppCloudErrorEnvelope
    {
        [JsonPropertyName("error")]
        public WhatsAppCloudError? Error { get; init; }
    }

    private sealed class WhatsAppCloudError
    {
        [JsonPropertyName("message")]
        public string? Message { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("code")]
        public int? Code { get; init; }

        [JsonPropertyName("error_subcode")]
        public int? ErrorSubcode { get; init; }

        [JsonPropertyName("is_transient")]
        public bool? IsTransient { get; init; }

        [JsonPropertyName("fbtrace_id")]
        public string? FbTraceId { get; init; }
    }
}
