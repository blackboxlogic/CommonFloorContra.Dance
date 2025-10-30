using System;
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CalendarFunctions;

// Send preview to me, click link to send all.
public class Email : Base
{
	public Email(IHttpClientFactory httpClientFactory, ILogger<Proxy> logger, IMemoryCache cache, Microsoft.Extensions.Configuration.IConfiguration configuration)
		: base(httpClientFactory, logger, cache, configuration)
	{
	}

	[Function("SendEmailToSubscribersApi")]
	public async Task<IActionResult> SendEmailToSubscribersApi([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req)
	{
		var isProd = Environment.GetEnvironmentVariable("Environment") == "PROD"; // DEV, TEST, PROD

		if (!isProd)
		{
			Logger.LogInformation("Not in production slot, skipping email.");
			return new ForbidResult();
		}

		var subscribers = await GetMailchimpSubscribers(
			GetConfigOrThrow("MailChimpApiKey"),
			GetConfigOrThrow("MailChimpAudienceId"));

		//await SendEmailFromGmail(subscribers, req.Query["CalendarUrl"].ToString());

		return new OkResult();
	}

	[Function("SendSampleEmailTimer")] // At 10:10 AM on day 15 of every month (after second sundays)
	public async Task SendSampleEmailTimer([TimerTrigger("0 10 10 15 * *")] TimerInfo myTimer)
	{
		var domain = Environment.GetEnvironmentVariable("Domain") ?? throw new Exception("Missing Domain env var");
		var calendarUrl = GetConfigOrThrow("CalendarUrl");
		var subscribers = new[] { (GetConfigOrThrow("EmailApprover"), $"<a href='https://{domain}/api/SendMonthlyEventEmailApi?CalendarUrl={calendarUrl}'>Send to all subscribers</a>") };
		await SendEmailFromGmail(subscribers, calendarUrl);
	}

	private async Task SendEmailFromGmail((string emailAddress, string footer)[] subscribers, string calendarUrl)
	{
		var nextEvents = await GetNextEvents(calendarUrl, ["contra"], 1);

		var content = nextEvents.Select(e => $"{e.date:MMMM dd}: {e.summary} at {e.location}<br>{e.description}").Aggregate((a, b) => a + "<br><br>" + b);

		var fromAddress = new MailAddress(GetConfigOrThrow("GmailSender"), GetConfigOrThrow("SeriesName"));

		using (var smtp = new SmtpClient
		{
			Host = "smtp.gmail.com",
			Port = 587,
			EnableSsl = true,
			DeliveryMethod = SmtpDeliveryMethod.Network,
			UseDefaultCredentials = false,
			Credentials = new NetworkCredential(fromAddress.Address, GetConfigOrThrow("GmailSenderAppPassword"))
		})
		{
			foreach (var subscriber in subscribers)
			{
				var personalizedContent = content + subscriber.footer;
				using (var message = new MailMessage(fromAddress, new MailAddress(subscriber.emailAddress))
				{
					Subject = "Upcoming Dances",
					Body = personalizedContent,
					IsBodyHtml = true
				})
				{
					await smtp.SendMailAsync(message);
				}
				await Task.Delay(1000); // 1 second delay
			}
		}
	}

	// unsubscribe link like: https://dance.us5.list-manage.com/profile?u=a1f02de7f307d697cbefdaf35&id=befe34b442&e=920e49ee2d
	private async Task<(string emailAddress, string footer)[]> GetMailchimpSubscribers(string apiKey, string listId)
	{
		var mailChimpManager = new MailChimp.Net.MailChimpManager(apiKey);
		var apiInfoTask = mailChimpManager.Api.GetInfoAsync();
		var membersTask = mailChimpManager.Members.GetAllAsync(listId);
		await Task.WhenAll(apiInfoTask, membersTask);
		var apiInfo = await apiInfoTask;
		var members = await membersTask;

		return members
			.Where(m => m.Status == MailChimp.Net.Models.Status.Subscribed)
			.Select(m => (m.EmailAddress, "<br><br>" + $"<a href='https://dance.us5.list-manage.com/profile?u={apiInfo.AccountId}&id={m.ListId}&e={m.UniqueEmailId}'>Manage your subscription</a>."))
			.ToArray();
	}
}