using System;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace HelloWorldWeb.Services
{
    public class EmailService
    {
        private readonly string _emailTo;
        private readonly string _emailFrom;
        private readonly string _brevoApiKey;

        public bool IsConfigured { get; }

        public EmailService(IConfiguration configuration)
        {
            Console.WriteLine("[EmailService] Initializing EmailService (Brevo)...");

            _emailTo = Environment.GetEnvironmentVariable("EMAIL_TO") ?? configuration["Email:To"];
            _emailFrom = Environment.GetEnvironmentVariable("EMAIL_FROM") ?? configuration["Email:From"];
            _brevoApiKey = Environment.GetEnvironmentVariable("BREVO_API_KEY");

            IsConfigured = !string.IsNullOrWhiteSpace(_brevoApiKey)
                           && !string.IsNullOrWhiteSpace(_emailFrom)
                           && !string.IsNullOrWhiteSpace(_emailTo);

            Console.WriteLine($"[EmailService] Configuration loaded:");
            Console.WriteLine($"  - EmailTo: {(_emailTo ?? "NULL")}");
            Console.WriteLine($"  - EmailFrom: {(_emailFrom ?? "NULL")}");
            Console.WriteLine($"  - BrevoApiKey: {(string.IsNullOrWhiteSpace(_brevoApiKey) ? "NOT SET" : "***SET***")}");
            Console.WriteLine($"  - IsConfigured: {IsConfigured}");

            if (!IsConfigured)
            {
                Console.WriteLine("[EmailService] ❌ WARNING: EmailService is NOT properly configured!");
                Console.WriteLine("[EmailService] Missing configuration:");
                if (string.IsNullOrWhiteSpace(_brevoApiKey)) Console.WriteLine("  - BREVO_API_KEY is missing");
                if (string.IsNullOrWhiteSpace(_emailFrom)) Console.WriteLine("  - EMAIL_FROM is missing");
                if (string.IsNullOrWhiteSpace(_emailTo)) Console.WriteLine("  - EMAIL_TO is missing");
            }
            else
            {
                Console.WriteLine("[EmailService] ✅ EmailService is properly configured (Brevo)");
            }
        }

        public bool Send(string subject, string htmlBody)
        {
            Console.WriteLine($"[EmailService] Send() called");
            Console.WriteLine($"  - Subject: {subject}");
            Console.WriteLine($"  - Body length: {htmlBody?.Length ?? 0} chars");
            Console.WriteLine($"  - IsConfigured: {IsConfigured}");

            if (!IsConfigured)
            {
                Console.WriteLine("[EmailService] ❌ Cannot send - EmailService is NOT configured");
                return false;
            }

            try
            {
                Console.WriteLine("[EmailService] Sending via Brevo API...");
                var ok = SendViaBrevo(_emailFrom, _emailTo, subject, htmlBody, _brevoApiKey);
                if (ok)
                {
                    Console.WriteLine("[EmailService] ✅ Email sent successfully via Brevo!");
                }
                return ok;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EmailService] ❌ Send failed!");
                Console.WriteLine($"  - Exception type: {ex.GetType().Name}");
                Console.WriteLine($"  - Message: {ex.Message}");
                Console.WriteLine($"  - StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  - Inner Exception: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        private bool SendViaBrevo(string fromEmail, string toEmail, string subject, string htmlBody, string apiKey)
        {
            try
            {
                using var http = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(15)
                };
                http.DefaultRequestHeaders.Add("api-key", apiKey);
                http.DefaultRequestHeaders.Add("accept", "application/json");

                var payload = new
                {
                    sender = new { email = fromEmail, name = "WinterNET" },
                    to = new[] { new { email = toEmail } },
                    subject = subject,
                    htmlContent = htmlBody
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var url = "https://api.brevo.com/v3/smtp/email";

                Console.WriteLine($"[EmailService] POST {url}");
                var resp = http.PostAsync(url, content).GetAwaiter().GetResult();
                var respBody = resp.Content != null ? resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() : string.Empty;
                Console.WriteLine($"[EmailService] Brevo response: {(int)resp.StatusCode} {resp.ReasonPhrase}");

                if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode <= 299)
                {
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(respBody))
                {
                    Console.WriteLine($"[EmailService] Brevo error body: {respBody}");
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailService] Brevo error: {ex.Message}");
                return false;
            }
        }
    }
}
