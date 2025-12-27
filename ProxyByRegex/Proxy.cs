using System;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Cors;
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
		var useCache = req.Query["cache"].FirstOrDefault()?.ToLower() != "false";

		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
		{
			return new BadRequestObjectResult("Invalid URL.");
		}

		bool isGoogleDoc = uri.Host.Equals("docs.google.com", StringComparison.OrdinalIgnoreCase);
		bool isGoogleCalendar = uri.Host.Equals("calendar.google.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.Contains("/ical");
		bool looksLikeOtherCalendar = uri.AbsoluteUri.EndsWith(".ics", StringComparison.OrdinalIgnoreCase)
			|| uri.AbsoluteUri.Contains("ical", StringComparison.OrdinalIgnoreCase)
			|| uri.AbsoluteUri.Contains("calendar", StringComparison.OrdinalIgnoreCase);

		if (!isGoogleDoc && !isGoogleCalendar && !looksLikeOtherCalendar)
		{
			return new BadRequestObjectResult("Unsupported URL.");
		}

		(var remoteContentString, var headers, var cached) = await Fetch(url, useCache);

		req.HttpContext.Response.Headers.Append("X-Proxy-Cache", cached ? "HIT" : "MISS");

		if (isGoogleDoc)
		{
			remoteContentString = GoogleRedirectRegex.Replace(remoteContentString, "");
			remoteContentString = HtmlHeadElementRegex.Replace(remoteContentString, "");
		}

		var proxyResponse = new ContentResult
		{
			Content = remoteContentString,
			ContentType = headers.ContentType?.ToString(),
			StatusCode = (int)HttpStatusCode.OK
		};

		// CORS is failing on 3 of 5?
		if(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == "Development")
		{
			req.HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "*");
		}
		
		return proxyResponse;
	}

	// takes 'url', 'months' and 'contains' query parameters
	[Function("GetNextEventsJSON")]
	public async Task<IActionResult> GetNextEventsJSON(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req)
	{
		var urlString = req.Query["url"].FirstOrDefault() ?? throw new ArgumentNullException("url");
		var months = int.Parse(req.Query["months"].FirstOrDefault("12") ?? throw new ArgumentNullException("url"));
		var containsFilters = req.Query["contains"].OfType<string>().Where(c => c != "").ToArray();
		(var nextEvents, var headers, var cached) = await GetNextEvents(urlString, containsFilters, months);
		req.HttpContext.Response.Headers.Append("X-Proxy-Cache", cached ? "HIT" : "MISS");
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