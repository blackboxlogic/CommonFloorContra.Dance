using System;
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CalendarFunctions;

public class Email : Base
{
	public Email(IHttpClientFactory httpClientFactory, ILogger<Proxy> logger, IMemoryCache cache, Microsoft.Extensions.Configuration.IConfiguration configuration)
		: base(httpClientFactory, logger, cache, configuration)
	{
	}

	[Function("SendMonthlyEventEmail")]
	public async Task<IActionResult> SendMonthlyEventEmail(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req)
	//public async Task SendMonthlyEventEmail([TimerTrigger("0 40 15 1 * *")] TimerInfo myTimer)
	{
		var isProduction = Convert.ToBoolean(Environment.GetEnvironmentVariable("IsProduction"));

		if (!isProduction)
		{
			Logger.LogInformation("Not in production slot, skipping email.");
			return new OkResult();
		}

		var nextEvents = await GetNextEvents("https://calendar.google.com/calendar/ical/0d91bca8eebb5bf2b86e7ea2ef26a3f6f1729ee3c73b87985a0407204e00dbc4%40group.calendar.google.com/public/basic.ics", ["contra"], 1);
		var content = nextEvents.Select(e => $"{e.date:MMMM dd}: {e.summary} at {e.location}<br>{e.description}").Aggregate((a, b) => a + "<br><br>" + b);
		var subscribers = await GetMailchimpSubscribers(
			Configuration["MailChimpApiKey"] ?? throw new Exception("Missing MailChimpApiKey"),
			Configuration["MailChimpAudienceId"] ?? throw new Exception("Missing MailChimpAudienceId"));
		//test to one person:
		subscribers = new string[] { "blackboxlogic@gmail.com" };
		foreach (var subscriber in subscribers)
		{
			await SendEmailAsync(subscriber, "Upcoming Dances", content);
		}
		return new OkResult();
	}

	private async Task SendEmailAsync(string recipient, string subject, string body)
	{
		var fromAddress = new MailAddress(Configuration["GmailUser"] ?? throw new Exception("Missing GmailUser"), "Common Floor Contra Dance");
		var toAddress = new MailAddress(recipient);
		var smtp = new SmtpClient
		{
			Host = "smtp.gmail.com",
			Port = 587,
			EnableSsl = true,
			DeliveryMethod = SmtpDeliveryMethod.Network,
			UseDefaultCredentials = false,
			Credentials = new NetworkCredential(fromAddress.Address, Configuration["GmailAppPassword"] ?? throw new Exception("Missing GmailAppPassword"))
		};
		using (var message = new MailMessage(fromAddress, toAddress)
		{
			Subject = subject,
			Body = body,
			IsBodyHtml = true
		})
		{
			await smtp.SendMailAsync(message);
		}
	}

	private async Task<IEnumerable<string>> GetMailchimpSubscribers(string apiKey, string listId)
	{
		var mailChimpManager = new MailChimp.Net.MailChimpManager(apiKey);
		var members = await mailChimpManager.Members.GetAllAsync(listId);
		return members
			.Where(m => m.Status == MailChimp.Net.Models.Status.Subscribed)
			.Select(m => m.EmailAddress)
			.ToArray();
	}
}