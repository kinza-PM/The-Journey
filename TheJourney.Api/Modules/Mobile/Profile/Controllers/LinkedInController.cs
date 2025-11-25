using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using TheJourney.Api.Modules.Mobile.Profile.Services;
using TheJourney.Api.Modules.Mobile.Assessment.Controllers;

namespace TheJourney.Api.Modules.Mobile.Profile.Controllers;

[Route("api/mobile/auth/linkedin")]
public class LinkedInController : StudentAuthorizedController
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IMemoryCache _cache;
    private readonly IProfileService _profileService;
    private readonly ILogger<LinkedInController> _logger;

    public LinkedInController(
        IConfiguration config,
        IHttpClientFactory httpFactory,
        IMemoryCache cache,
        IProfileService profileService,
        ILogger<LinkedInController> logger)
    {
        _config = config;
        _httpFactory = httpFactory;
        _cache = cache;
        _profileService = profileService;
        _logger = logger;
    }

    // =========================
    // Step 1: Start LinkedIn OAuth
    // =========================
    [HttpGet("start")]
    public IActionResult Start()
    {
        // Use base helper to get authenticated student id (returns null if not present)
        var studentId = GetStudentId();
        if (studentId == null)
            return Unauthorized(new { message = "Student ID not found in token." });

        // 2️⃣ Return URL from environment variable
        var returnUrl = _config["LINKEDIN_REDIRECT_URI"];
        if (string.IsNullOrWhiteSpace(returnUrl))
            return BadRequest("Return URL not configured on server");

        // 3️⃣ Generate state
        var state = Guid.NewGuid().ToString("N");
        var stateObj = new { StudentId = studentId, ReturnUrl = returnUrl };
        _cache.Set(state, JsonSerializer.Serialize(stateObj), TimeSpan.FromMinutes(10));

        // 4️⃣ Build LinkedIn auth URL
        var clientId = _config["LINKEDIN_CLIENT_ID"];
        var redirectUri = _config["LINKEDIN_REDIRECT_URI"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
            return BadRequest("LinkedIn configuration missing");

        var scope = "r_liteprofile r_emailaddress";
        var url = $"https://www.linkedin.com/oauth/v2/authorization" +
                  $"?response_type=code" +
                  $"&client_id={Uri.EscapeDataString(clientId)}" +
                  $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&state={Uri.EscapeDataString(state)}" +
                  $"&scope={Uri.EscapeDataString(scope)}";

        return Redirect(url);
    }

    // =========================
    // Step 2: LinkedIn callback
    // =========================
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            return BadRequest("Missing code or state");

        if (!_cache.TryGetValue(state, out string? stateJson) || string.IsNullOrWhiteSpace(stateJson))
            return BadRequest("Invalid or expired state");

        var stateObj = JsonSerializer.Deserialize<JsonElement>(stateJson);
        int? studentId = null;
        string? returnUrl = null;
        try
        {
            if (stateObj.TryGetProperty("StudentId", out var s) && s.ValueKind != JsonValueKind.Null)
                if (s.TryGetInt32(out var sid)) studentId = sid;

            if (stateObj.TryGetProperty("ReturnUrl", out var r) && r.ValueKind == JsonValueKind.String)
                returnUrl = r.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed parsing state JSON");
        }

        var clientId = _config["LINKEDIN_CLIENT_ID"];
        var clientSecret = _config["LINKEDIN_CLIENT_SECRET"];
        var redirectUri = _config["LINKEDIN_REDIRECT_URI"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(redirectUri))
            return BadRequest("LinkedIn configuration missing");

        try
        {
            var client = _httpFactory.CreateClient();
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            };

            // Exchange code for access token
            var tokenResp = await client.PostAsync("https://www.linkedin.com/oauth/v2/accessToken", new FormUrlEncodedContent(form));
            tokenResp.EnsureSuccessStatusCode();
            var tokenText = await tokenResp.Content.ReadAsStringAsync();
            using var tokenDoc = JsonDocument.Parse(tokenText);
            var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();
            if (string.IsNullOrWhiteSpace(accessToken)) throw new Exception("No access token returned");

            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Fetch profile
            var meUrl = "https://api.linkedin.com/v2/me?projection=(id,localizedFirstName,localizedLastName,profilePicture(displayImage~:playableStreams))";
            var meResp = await client.GetAsync(meUrl);
            meResp.EnsureSuccessStatusCode();
            var meText = await meResp.Content.ReadAsStringAsync();
            using var meDoc = JsonDocument.Parse(meText);

            // Fetch email
            var emailUrl = "https://api.linkedin.com/v2/emailAddress?q=members&projection=(elements*(handle~))";
            var emailResp = await client.GetAsync(emailUrl);
            emailResp.EnsureSuccessStatusCode();
            var emailText = await emailResp.Content.ReadAsStringAsync();
            using var emailDoc = JsonDocument.Parse(emailText);

            // Map to DTO
            var profileDto = new LinkedInProfileDto();
            if (meDoc.RootElement.TryGetProperty("id", out var idEl)) profileDto.Id = idEl.GetString();
            if (meDoc.RootElement.TryGetProperty("localizedFirstName", out var fn)) profileDto.FirstName = fn.GetString();
            if (meDoc.RootElement.TryGetProperty("localizedLastName", out var ln)) profileDto.LastName = ln.GetString();

            // Profile picture
            try
            {
                if (meDoc.RootElement.TryGetProperty("profilePicture", out var pic) && pic.TryGetProperty("displayImage~", out var display))
                    if (display.TryGetProperty("elements", out var elements) && elements.GetArrayLength() > 0)
                        foreach (var el in elements.EnumerateArray())
                            if (el.TryGetProperty("identifiers", out var ids) && ids.GetArrayLength() > 0)
                                if (ids[0].TryGetProperty("identifier", out var ident))
                                {
                                    profileDto.ProfilePictureUrl = ident.GetString();
                                    break;
                                }
            }
            catch { }

            // Email
            try
            {
                if (emailDoc.RootElement.TryGetProperty("elements", out var elems) && elems.GetArrayLength() > 0)
                    if (elems[0].TryGetProperty("handle~", out var handleTilde) && handleTilde.TryGetProperty("emailAddress", out var ema))
                        profileDto.Email = ema.GetString();
            }
            catch { }

            // Persist to DB
            if (studentId.HasValue)
                await _profileService.ImportLinkedInProfileAsync(studentId.Value, profileDto);

            // Redirect to return URL (mobile deep link)
            if (!string.IsNullOrWhiteSpace(returnUrl))
                return Redirect(returnUrl);

            // Fallback: simple HTML
            var successHtml = "<html><body><script>window.close();</script><p>Import successful. You can close this window.</p></body></html>";
            return Content(successHtml, "text/html", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LinkedIn callback error");
            var errHtml = $"<html><body><p>Import failed: {System.Net.WebUtility.HtmlEncode(ex.Message)}</p></body></html>";
            return Content(errHtml, "text/html", Encoding.UTF8);
        }
    }
}
