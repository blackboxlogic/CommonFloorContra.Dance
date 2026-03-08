using System;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RazorLight;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace CalendarFunctions;

// TODO
// Format the email
// Test timezone issues
// Set for multiple dance series

// Schedule a preview event email sent to me with a link to send the email to all mailchimp subscribers.
// https://mailchimp.com/help/use-email-beamer-to-create-a-campaign/
public class Email(IHttpClientFactory httpClientFactory, ILogger<Proxy> logger, IMemoryCache cache, IConfiguration configuration)
	: Base(httpClientFactory, logger, cache, configuration)
{
	[Function("SendSampleEmailAPI")]
	public async Task SendSampleEmailApi([HttpTrigger(AuthorizationLevel.Function, "get", "post")]
		Microsoft.Azure.Functions.Worker.Http.HttpRequestData req)
	{
		await SendSampleEmailTimer(new());
	}

	[Function("SendSampleEmailTimer")] // At Noon-oh-five every 1th day of the month
	public async Task SendSampleEmailTimer([TimerTrigger("0 05 17 1 * *", RunOnStartup = true)] TimerInfo myTimer)
	{
		try
		{
			var seriesName = GetConfigOrThrow("SeriesName");
			var seriesWebsite = GetConfigOrThrow("SeriesWebsite");
			var calendarUrl = GetConfigOrThrow("SeriesCalendarUrl");
			(var nextEvents, var headers, var cached) = await GetNextEvents(calendarUrl, [], 1);
			var toAddress = new MailAddress(GetConfigOrThrow("SeriesEmailDestination"));
			var fromAddress = new MailAddress(GetConfigOrThrow("SeriesGmailSender"), seriesName);
			var fromAddressAppPassword = GetConfigOrThrow("SeriesGmailSenderAppPassword");
			var emailSubject = $"{seriesName} ~ {DateTime.Now:MMMM} Events";
			await SendEmailFromGmail(calendarUrl, nextEvents, toAddress, fromAddress, fromAddressAppPassword, emailSubject, seriesName, seriesWebsite);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "Error");
		}
	}

	private static async Task SendEmailFromGmail(string calendarUrl, IEnumerable<DanceEvent> nextEvents,
		MailAddress toAddress, MailAddress fromAddress, string fromAddressAppPassword, string emailSubject,
		string seriesName, string seriesWebsite)
	{
		if(!nextEvents.Any())
		{
			return;
		}

		var textBody = GenerateTextBody(calendarUrl, nextEvents, seriesName, seriesWebsite, emailSubject);
		var textView = AlternateView.CreateAlternateViewFromString(textBody, Encoding.UTF8, "text/plain");
		textView.TransferEncoding = System.Net.Mime.TransferEncoding.QuotedPrintable;

		var htmlBody = await GenerateHtmlBody(calendarUrl, nextEvents, seriesName, seriesWebsite, emailSubject);
		var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html");
		htmlView.TransferEncoding = System.Net.Mime.TransferEncoding.QuotedPrintable;

		using var smtp = new SmtpClient
		{
			Host = "smtp.gmail.com",
			Port = 587,
			EnableSsl = true,
			DeliveryMethod = SmtpDeliveryMethod.Network,
			UseDefaultCredentials = false,
			Credentials = new NetworkCredential("admin@commonfloorcontra.dance", fromAddressAppPassword)
		};
		using var message = new MailMessage
		{
			From = fromAddress,
			Subject = emailSubject
		};
		message.AlternateViews.Add(textView);
		message.AlternateViews.Add(htmlView); // last one is the preferred view
												//message.ReplyToList.Add(toAddress)
												//message.To.Add(toAddress);
												//message.To.Add(fromAddress); // So sender can preview the email body.
		message.To.Add(new MailAddress("blackboxlogic@gmail.com")); // For testing purposes only.
		await smtp.SendMailAsync(message);
	}

	private static string GenerateTextBody(string calendarUrl, IEnumerable<DanceEvent> nextEvents, string seriesName, string seriesWebsite, string subject)
	{
		var plainTextBody = new StringBuilder();
		plainTextBody.AppendLine($"{nextEvents.Count()} Upcoming {seriesName} Events:");
		plainTextBody.AppendLine();

		int i = 1;
		foreach (var ev in nextEvents)
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

		plainTextBody.AppendLine($"Get more info about {seriesName} at {seriesWebsite}.");
		return plainTextBody.ToString();
	}

	private static async Task<string> GenerateHtmlBody(string calendarUrl, IEnumerable<DanceEvent> nextEvents, string seriesName, string seriesWebsite, string emailSubject)
	{
		var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
		var engine = new RazorLightEngineBuilder()
			.UseFileSystemProject(assemblyPath) // root of where to find templates
			.UseMemoryCachingProvider()
			.Build();

		var model = new EmailModel
		{
			Events = nextEvents,
			SeriesName = seriesName,
			SeriesWebsite = seriesWebsite,
			CalendarUrl = calendarUrl,
			FaviconUrl = await GetFaviconUrl(seriesWebsite),
			Subject = emailSubject
		};

		//try
		//{
			return await engine.CompileRenderAsync("EmailTemplate.cshtml", model);
		//}
		//catch (Exception e)
		//{
		//	return "";
		//}
	}

	private static async Task<string> GetFaviconUrl(string siteUrl)
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
		public required IEnumerable<DanceEvent> Events;
		public required string SeriesName;
		public required string SeriesWebsite;
		public required string CalendarUrl;
		public required string FaviconUrl;
		public required string Subject;
	}
}