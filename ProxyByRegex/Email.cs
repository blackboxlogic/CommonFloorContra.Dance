using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RazorLight;
using System;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Text;

namespace CalendarFunctions;

// TODO
// Set for multiple dance series (put config in json list, include email template maybe as a link hosted on their site)

// Schedule a preview event email sent to me with a link to send the email to all mailchimp subscribers.
// https://mailchimp.com/help/use-email-beamer-to-create-a-campaign/
public class Email(IHttpClientFactory httpClientFactory, ILogger<Proxy> logger, IMemoryCache cache, IConfiguration configuration)
	: Base(httpClientFactory, logger, cache, configuration)
{

	[Function("SendSampleEmailTimer")] // At Noon-oh-five every 1st day of the month
	public async Task SendSampleEmailTimer([TimerTrigger("0 05 17 1 * *")] TimerInfo myTimer)
	{
		try
		{
			var email = new EmailModel
			{
				SeriesName = GetConfigOrThrow("SeriesName"),
				SeriesWebsite = GetConfigOrThrow("SeriesWebsite"),
				CalendarUrl = GetConfigOrThrow("SeriesCalendarUrl"),
				LightColor = GetConfigOrNull("SeriesLightColor") ?? "#FFFFFF",
				DarkColor = GetConfigOrNull("SeriesDarkColor") ?? "#000000",
				PopColor = GetConfigOrNull("SeriesPopColor") ?? "#333333",
				ToAddress = GetConfigOrThrow("SeriesEmailDestination"),
				FromAddress = GetConfigOrThrow("SeriesGmailSender"),
				FromAddressAppUser = GetConfigOrThrow("SeriesGmailSenderUser"),
				FromAddressAppPassword = GetConfigOrThrow("SeriesGmailSenderAppPassword"),
				Build = BuildTime + Configuration["Environment"]
			};

			(email.Events, _, _) = await GetNextEvents(email.CalendarUrl, [], 1);
			email.FaviconUrl = await GetFaviconUrlFromWebsite(email.SeriesWebsite);
			email.Subject = $"{email.SeriesName} ~ {DateTime.Now:MMMM} Events";

			if (email.Events.Length > 0)
			{
				await SendEmailFromGmail(email);
			}
			else
			{
				Logger.LogInformation($"No events to email in {DateTime.Now:MMMM}.");
			}
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error");
		}
	}

	private async Task SendEmailFromGmail(EmailModel email)
	{
		var textBody = GenerateTextBody(email);
		var textView = AlternateView.CreateAlternateViewFromString(textBody, Encoding.UTF8, "text/plain");
		textView.TransferEncoding = System.Net.Mime.TransferEncoding.QuotedPrintable;

		var htmlBody = await GenerateHtmlBody(email);
		var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html");
		htmlView.TransferEncoding = System.Net.Mime.TransferEncoding.QuotedPrintable;

		using var message = new MailMessage
		{
			From = new MailAddress(email.FromAddress, email.SeriesName),
			Subject = "[PREVIEW] " + email.Subject
		};
		message.AlternateViews.Add(textView);
		message.AlternateViews.Add(htmlView); // Email clients show the last view first, put htmlView second.

		// Send a preview
		message.To.Add(email.FromAddress);

		using var smtp = new SmtpClient
		{
			Host = "smtp.gmail.com",
			Port = 587,
			EnableSsl = true,
			DeliveryMethod = SmtpDeliveryMethod.Network,
			UseDefaultCredentials = false,
			Credentials = new NetworkCredential(email.FromAddressAppUser, email.FromAddressAppPassword)
		};
		await smtp.SendMailAsync(message);

		// Send the live one
		if (Configuration["Environment"] == "PROD")
		{
			message.Subject = email.Subject;
			message.To.Clear();
			message.To.Add(new MailAddress(email.ToAddress));
			await smtp.SendMailAsync(message);
		}
	}

	private static string GenerateTextBody(EmailModel email)
	{
		var plainTextBody = new StringBuilder();
		plainTextBody.AppendLine($"{email.Events.Length} Upcoming {email.SeriesName} Events:");
		plainTextBody.AppendLine();

		int i = 1;
		foreach (var ev in email.Events)
		{
			plainTextBody.AppendLine($"Event {i++}: {ev.summary}");
			if(ev.startLocal.Date == ev.endLocal.Date)
			{
				plainTextBody.AppendLine($"When: {ev.startLocal.ToString("MMMM d")}, {ev.startLocal.ToString("h:mm tt")} to {ev.endLocal.ToString("h:mm tt")}.");
			}
			else
			{
				plainTextBody.AppendLine($"When: {ev.startLocal.ToString("MMMM d")} {ev.startLocal.ToString("h:mm tt")} to {ev.endLocal.ToString("MMMM d")} {ev.endLocal.ToString("h:mm tt")}.");
			}
			plainTextBody.AppendLine($"Where: {ev.location}");
			plainTextBody.AppendLine("Details:");
			plainTextBody.AppendLine(HtmlConverter.ToPlainText(ev.description));
			plainTextBody.AppendLine();
		}

		plainTextBody.AppendLine($"Get more info about {email.SeriesName} at {email.SeriesWebsite}.");
		return plainTextBody.ToString();
	}

	private static async Task<string> GenerateHtmlBody(EmailModel model)
	{
		var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
		var engine = new RazorLightEngineBuilder()
			.UseFileSystemProject(assemblyPath) // root of where to find templates
			.UseMemoryCachingProvider()
			.Build();
		return await engine.CompileRenderAsync("EmailTemplate.cshtml", model);
	}

	private static async Task<string> GetFaviconUrlFromWebsite(string siteUrl)
	{
		try
		{
			using var client = new HttpClient();
			var html = await client.GetStringAsync(siteUrl);
			var match = System.Text.RegularExpressions.Regex.Match(html,
				@"<link[^>]+rel=""[^""]*icon[^""]*""[^>]+href=""([^""]+)""",
				System.Text.RegularExpressions.RegexOptions.IgnoreCase);
			if (match.Success)
			{
				var href = match.Groups[1].Value;
				return href.StartsWith("http") ? href : new Uri(new Uri(siteUrl), href).ToString();
			}
		}
		catch { }
		return new Uri(new Uri(siteUrl), "/favicon.ico").ToString();
	}

	public class EmailModel
	{
		public DanceEvent[] Events = [];
		public required string SeriesName;
		public required string SeriesWebsite;
		public required string CalendarUrl;
		public string FaviconUrl = "";
		public string Subject = "";
		public required string LightColor;
		public required string DarkColor;
		public required string PopColor;
		public required string ToAddress;
		public required string FromAddress;
		public required string FromAddressAppUser;
		public required string FromAddressAppPassword;
		public required string Build;
	}
}