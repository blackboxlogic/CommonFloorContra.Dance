using System;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CalendarFunctions;

public class Proxy : Base
{
	// Booking tool:
	//https://github.com/kewisch/ical.js/
	//https://fullcalendar.io/
	//https://ui.toast.com/tui-calendar
	//https://styledcalendar.com/

	public Proxy(IHttpClientFactory httpClientFactory, ILogger<Proxy> logger, IMemoryCache cache, Microsoft.Extensions.Configuration.IConfiguration configuration)
		: base(httpClientFactory, logger, cache, configuration)
	{
	}

	[Function("Hello")]
	public static IActionResult Hello(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
		ILogger log)
	{
		var proxyResponse = new ContentResult
		{
			Content = "hi!",
			ContentType = "text/plain",
			StatusCode = (int)HttpStatusCode.OK
		};

		return proxyResponse;
	}

	// If you want to get an ical file, and google doesn't have cors header for it.
	[Function("Proxy")]
	public async Task<IActionResult> ProxyDoc(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
		ILogger log)
	{
		var url = req.Query["url"].FirstOrDefault();

		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
		{
			return new BadRequestObjectResult("Invalid URL.");
		}

		bool isGoogleDoc = uri.Host.Equals("docs.google.com", StringComparison.OrdinalIgnoreCase);
		bool isGoogleCalendar = uri.Host.Equals("calendar.google.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.Contains("/ical");

		if (!isGoogleDoc && !isGoogleCalendar)
		{
			return new BadRequestObjectResult("Unsupported URL.");
		}

		using (var httpClient = HttpClientFactory.CreateClient())
		using (HttpResponseMessage remoteResponse = await httpClient.GetAsync(uri))
		using (HttpContent remoteContent = remoteResponse.Content)
		{
			var remoteContentString = await remoteContent.ReadAsStringAsync();

			if (isGoogleDoc)
			{
				remoteContentString = GoogleRedirectRegex.Replace(remoteContentString, "");
				remoteContentString = HtmlHeadElementRegex.Replace(remoteContentString, "");
			}

			var proxyResponse = new ContentResult
			{
				Content = remoteContentString,
				ContentType = remoteContent.Headers.ContentType?.ToString(),
				StatusCode = (int)HttpStatusCode.OK
			};

			return proxyResponse;
		}
	}

	// takes 'url', 'months' and 'contains' query parameters
	[Function("GetNextEventsJSON")]
	public async Task<IActionResult> GetNextEventsJSON(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req)
	{
		var urlString = req.Query["url"].FirstOrDefault() ?? throw new ArgumentNullException("url");
		var months = int.Parse(req.Query["months"].FirstOrDefault("12") ?? throw new ArgumentNullException("url"));
		var containsFilters = req.Query["contains"].OfType<string>().Where(c => c != "").ToArray();
		var nextEvents = await GetNextEvents(urlString, containsFilters, months);
		var result = JsonSerializer.Serialize(nextEvents);

		var proxyResponse = new ContentResult
		{
			Content = result,
			ContentType = "application/json; charset=utf-8",
			StatusCode = (int)HttpStatusCode.OK
		};

		return proxyResponse;
	}
}