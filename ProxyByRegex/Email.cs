using System;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RazorLight;
using System.Text;

namespace CalendarFunctions;

// Schedule a preview event email sent to me with a link to send the email to all mailchimp subscribers.
// https://mailchimp.com/help/use-email-beamer-to-create-a-campaign/
public class Email : Base
{
	public Email(IHttpClientFactory httpClientFactory, ILogger<Proxy> logger, IMemoryCache cache, Microsoft.Extensions.Configuration.IConfiguration configuration)
		: base(httpClientFactory, logger, cache, configuration)
	{
	}

	[Function("SendSampleEmailAPI")]
	public async Task SendSampleEmailTimer([HttpTrigger(AuthorizationLevel.Function, "get", "post")]
		Microsoft.Azure.Functions.Worker.Http.HttpRequestData req)
	{
		if (GetConfigOrThrow("Environment") == "DEV" || GetConfigOrThrow("Environment") == "TEST")
		{
			var seriesName = GetConfigOrThrow("SeriesName");
			var seriesWebsite = GetConfigOrThrow("SeriesWebsite");
			var calendarUrl = GetConfigOrThrow("CalendarUrl");
			var nextEvents = await GetNextEvents(calendarUrl, ["contra"], 1);
			var toAddress = new MailAddress(GetConfigOrThrow("MailchimpBeamerAddress"));
			var fromAddress = new MailAddress(GetConfigOrThrow("GmailSender"), seriesName);
			var fromAddressAppPassword = GetConfigOrThrow("GmailSenderAppPassword");
			var emailSubject = $"{seriesName} - Upcoming Events";
			await SendEmailFromGmail(calendarUrl, nextEvents, toAddress, fromAddress, fromAddressAppPassword, emailSubject, seriesName, seriesWebsite);
		}
	}

	[Function("SendSampleEmailTimer")] // At 10:10 AM on day 15 of every month (after second sundays)
	public async Task SendSampleEmailTimer([TimerTrigger("0 10 10 15 * *")] TimerInfo myTimer)
	{
		if (GetConfigOrThrow("Environment") == "PROD")
		{
			var seriesName = GetConfigOrThrow("SeriesName");
			var seriesWebsite = GetConfigOrThrow("SeriesWebsite");
			var calendarUrl = GetConfigOrThrow("CalendarUrl");
			var nextEvents = await GetNextEvents(calendarUrl, ["contra"], 1);
			var toAddress = new MailAddress(GetConfigOrThrow("MailchimpBeamerAddress"));
			var fromAddress = new MailAddress(GetConfigOrThrow("GmailSender"), seriesName);
			var fromAddressAppPassword = GetConfigOrThrow("GmailSenderAppPassword");
			var emailSubject = $"{seriesName} - Upcoming Events";
			await SendEmailFromGmail(calendarUrl, nextEvents, toAddress, fromAddress, fromAddressAppPassword, emailSubject, seriesName, seriesWebsite);
		}
	}

	private async Task SendEmailFromGmail(string calendarUrl, IEnumerable<DanceEvent> nextEvents,
		MailAddress toAddress, MailAddress fromAddress, string fromAddressAppPassword, string emailSubject,
		string seriesName, string seriesWebsite)
	{
		var emailBodyHtml = await GenerateHtmlBody(calendarUrl, nextEvents, seriesName, seriesWebsite);
		var plainTextBody = GenerateTextBody(calendarUrl, nextEvents, seriesName, seriesWebsite);

		using (var smtp = new SmtpClient
		{
			Host = "smtp.gmail.com",
			Port = 587,
			EnableSsl = true,
			DeliveryMethod = SmtpDeliveryMethod.Network,
			UseDefaultCredentials = false,
			Credentials = new NetworkCredential(fromAddress.Address, fromAddressAppPassword)
		})
		{
			using (var message = new MailMessage
			{
				From = fromAddress,
				Subject = emailSubject,
				Body = emailBodyHtml,
				IsBodyHtml = true
			})
			{
				message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plainTextBody, Encoding.UTF8, "text/plain"));
				//message.To.Add(toAddress);
				message.To.Add(fromAddress); // So sender can preview the email body.
				await smtp.SendMailAsync(message);
			}
		}
	}

	private string GenerateTextBody(string calendarUrl, IEnumerable<DanceEvent> nextEvents, string seriesName, string seriesWebsite)
	{
		var plainTextBody = new StringBuilder();
		plainTextBody.AppendLine($"{nextEvents.Count()} Upcoming {seriesName} Events:");
		plainTextBody.AppendLine();

		int i = 1;
		foreach (var ev in nextEvents)
		{
			plainTextBody.AppendLine($"Event {i++}: {ev.summary}");
			if(ev.start.Date == ev.end.Date)
			{
				plainTextBody.AppendLine($"When: {ev.start.ToString("MMMM dd")}, {ev.start.ToString("hh:mm tt")} to {ev.end.ToString("hh:mm tt")}");
			}
			else
			{
				plainTextBody.AppendLine($"When: {ev.start.ToString("MMMM dd")} {ev.start.ToString("hh:mm tt")} to {ev.end.ToString("MMMM dd")} {ev.end.ToString("hh:mm tt")}");
			}
			plainTextBody.AppendLine($"Where: {ev.location}");
			plainTextBody.AppendLine("Details:");
			plainTextBody.AppendLine(HtmlConverter.ToPlainText(ev.description));
			plainTextBody.AppendLine();
		}

		plainTextBody.AppendLine($"Get more info about {seriesName} at {seriesWebsite}.");
		return plainTextBody.ToString();
	}

	private async Task<string> GenerateHtmlBody(string calendarUrl, IEnumerable<DanceEvent> nextEvents, string seriesName, string seriesWebsite)
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
			CalendarUrl = calendarUrl
		};

		return await engine.CompileRenderAsync("EmailTemplate.cshtml", model);
	}

	public class EmailModel
	{
		public required IEnumerable<DanceEvent> Events;
		public required string SeriesName;
		public required string SeriesWebsite;
		public required string CalendarUrl;
	}
}