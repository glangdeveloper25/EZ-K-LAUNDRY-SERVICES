using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace EZKLaundrySystem.Services;

/// <summary>Settings for the "Sms" section of appsettings.json.</summary>
public class SmsOptions
{
    public bool Enabled { get; set; }
    /// <summary>
    /// Cloud mode: https://api.sms-gate.app/3rdparty/v1/messages.
    /// Local mode (phone on the same Wi-Fi): http://PHONE-IP:8080/message.
    /// </summary>
    public string ApiUrl { get; set; } = "https://api.sms-gate.app/3rdparty/v1/messages";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public record SmsResult(bool Sent, string Detail);

/// <summary>
/// Staff-initiated SMS (Objective 2) sent through an Android phone running the SMSGate
/// (sms-gate.app) app, so texts go out from the shop's own SIM. When SMS is not configured the
/// callers still save the in-app notification, so the system keeps working without it.
/// </summary>
public class SmsSender(HttpClient http, IOptions<SmsOptions> options, ILogger<SmsSender> logger)
{
    private readonly SmsOptions _options = options.Value;

    public bool IsConfigured =>
        _options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiUrl)
        && !string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password);

    public async Task<SmsResult> SendAsync(IEnumerable<string> phoneNumbers, string message)
    {
        if (!IsConfigured) return new SmsResult(false, "SMS is not set up yet (see the Sms section of appsettings.json)");

        var recipients = phoneNumbers.Select(NormalizePhilippineNumber).OfType<string>().Distinct().ToList();
        if (recipients.Count == 0) return new SmsResult(false, "no valid mobile number");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl.Trim())
            {
                Content = JsonContent.Create(new { textMessage = new { text = message }, phoneNumbers = recipients })
            };
            string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            using var response = await http.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return new SmsResult(true, $"sent to {recipients.Count} number(s)");

            logger.LogWarning("SMS gateway returned {Status}: {Body}", (int)response.StatusCode,
                await response.Content.ReadAsStringAsync());
            return new SmsResult(false, $"the SMS gateway rejected the request ({(int)response.StatusCode})");
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            // No internet, phone offline, or a brownout at the shop: fall back to in-app only.
            logger.LogWarning(error, "SMS gateway could not be reached.");
            return new SmsResult(false, "the SMS gateway could not be reached (check internet and the phone)");
        }
    }

    /// <summary>Converts 09XXXXXXXXX, 9XXXXXXXXX, 639XXXXXXXXX or +639XXXXXXXXX to +639XXXXXXXXX.</summary>
    public static string? NormalizePhilippineNumber(string? phone)
    {
        string digits = new((phone ?? "").Where(char.IsDigit).ToArray());
        string? national = digits switch
        {
            { Length: 11 } when digits.StartsWith("09") => digits[1..],
            { Length: 12 } when digits.StartsWith("639") => digits[2..],
            { Length: 10 } when digits.StartsWith('9') => digits,
            _ => null
        };
        return national == null ? null : "+63" + national;
    }
}
