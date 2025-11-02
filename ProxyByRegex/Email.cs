using System;
using System.Net;
using System.Net.Mail;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CalendarFunctions;

// Schedule a preview event email sent to me with a link to send the email to all mailchimp subscribers.
// https://mailchimp.com/help/use-email-beamer-to-create-a-campaign/
public class Email : Base
{
	public Email(IHttpClientFactory httpClientFactory, ILogger<Proxy> logger, IMemoryCache cache, Microsoft.Extensions.Configuration.IConfiguration configuration)
		: base(httpClientFactory, logger, cache, configuration)
	{
	}

	[Function("SendSampleEmailTimer")] // At 10:10 AM on day 15 of every month (after second sundays)
	public async Task SendSampleEmailTimer([TimerTrigger("0 10 10 15 * *")] TimerInfo myTimer)
	{
		if (GetConfigOrThrow("Environment") == "PROD")
		{
			await SendEmailFromGmail();
		}
	}

	private async Task SendEmailFromGmail()
	{
		var nextEvents = await GetNextEvents(GetConfigOrThrow("CalendarUrl"), ["contra"], 1);
		var fromAddress = new MailAddress(GetConfigOrThrow("GmailSender"), GetConfigOrThrow("SeriesName"));
		var fromAddressAppPassword = GetConfigOrThrow("GmailSenderAppPassword");
		var emailSubject = $"{GetConfigOrThrow("SeriesName")} - Upcoming Events";
		// TODO: format location as map link. Add MoreInfo link to bottom of email. Link to add-calendar to yours. Larger font for title.
		var emailBody = nextEvents.Select(e => $"{e.date:MMMM dd}: {e.summary} at {e.location}<br>{e.description}").Aggregate((a, b) => a + "<br><br>" + b);
		var toAddress = new MailAddress(GetConfigOrThrow("MailchimpBeamerAddress"));

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
			var personalizedContent = emailBody;
			using (var message = new MailMessage
			{
				From = fromAddress,
				Subject = "Upcoming Dances",
				Body = personalizedContent,
				IsBodyHtml = true
			})
			{
				message.To.Add(toAddress);
				message.To.Add(fromAddress); // So sender can preview the email body.
				await smtp.SendMailAsync(message);
			}
		}
	}
}