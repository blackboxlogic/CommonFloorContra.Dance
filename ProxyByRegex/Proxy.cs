using System;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Calendar = Ical.Net.Calendar;

namespace CalendarFunctions;

public class Proxy
{
	// Booking tool:
	//https://github.com/kewisch/ical.js/
	//https://fullcalendar.io/
	//https://ui.toast.com/tui-calendar
	//https://styledcalendar.com/

	private static int CallCount = 0;

	// Google docs embeded have a link replacement scheme. Undo that.
	const string GoogleRedirect = "https://www\\.google\\.com/url\\?q=|&(amp;)?sa=D&(amp;)?source=editors&(amp;)?ust=\\d*&(amp;)?usg=[^\"]*";
	const string HtmlHeadElement = "<head>.*</head>";

	private readonly HttpClient HttpClient;
	private readonly ILogger Logger;
	private readonly IMemoryCache Cache;

	public Proxy(HttpClient httpClient, ILogger<Proxy> logger, IMemoryCache cache)
	{
		HttpClient = httpClient;
		HttpClient.DefaultRequestHeaders.Add("Accept", @"*/*");
		Logger = logger;
		Cache = cache;
	}

	[Function("Hello")]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
	public static async Task<IActionResult> Hello(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
		ILogger log)
	{
		var proxyResponse = new ContentResult
		{
			Content = "hi!",
			ContentType = "text/plain",
			StatusCode = (int)HttpStatusCode.OK
		};

		AddStandardResponseHeaders(req.HttpContext.Response, req);

		return proxyResponse;
	}

	//What's this being used for? I guess if I want to get an ical file, and google doesn't have cors header for it.
	[Function("Proxy")]
	public async Task<IActionResult> ProxyDoc(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
		ILogger log)
	{
		var url = req.Query["url"].FirstOrDefault();

		if (url.StartsWith(@"https://docs.google.com/document")) { }
		if (url.StartsWith(@"https://calendar.google.com/calendar/ical")) { }
		else
		{
			throw new Exception("Unsupported URL");
		}

		using (HttpResponseMessage remoteResponse = await HttpClient.GetAsync(url))
		using (HttpContent remoteContent = remoteResponse.Content)
		{
			var remoteContentString = await remoteContent.ReadAsStringAsync();

			if (url.StartsWith(@"https://docs.google.com/document"))
			{
				remoteContentString = new Regex(GoogleRedirect).Replace(remoteContentString, "");
				remoteContentString = new Regex(HtmlHeadElement).Replace(remoteContentString, "");
			}

			var proxyResponse = new ContentResult
			{
				Content = remoteContentString,
				ContentType = remoteContent.Headers.ContentType.ToString(),
				StatusCode = (int)HttpStatusCode.OK
			};

			AddStandardResponseHeaders(req.HttpContext.Response, req);

			return proxyResponse;
		}
	}

	// takes 'url', 'months' and 'contains' query parameters
	[Function("GetNextEventsJSON")]
	public async Task<IActionResult> GetNextEventsJSON(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
			HttpRequest req)
	{
		var remoteContentString = await Fetch(req.Query["url"]);
		var cal = Calendar.Load(remoteContentString);
		var months = int.Parse(req.Query["months"].FirstOrDefault("12"));
		var start = CalDateTime.UtcNow;
		var end = start.AddMonths(months);

		// after https://github.com/ical-org/ical.net/issues/871
		var events = ICalNetHelper.MyGetOccurrences<CalendarEvent>(cal, start).TakeWhileBefore(end).ToArray();
		//var events = cal.GetOccurrences<CalendarEvent>(start).TakeWhileBefore(end).ToArray();

		var containsFilters = req.Query["contains"].Where(c => !string.IsNullOrEmpty(c)).ToArray();

		var nextEvents = events
			.OrderBy(e => e.Period.StartTime) // StartTime could be null? // OR e.start?
			.Where(e => e.Source is CalendarEvent)
			.Select(e => new {period = e.Period, source = e.Source as CalendarEvent}) // is source the initial reoccuring event?
			//.OfType<CalendarEvent>()
			.Where(e =>
				containsFilters.Length == 0 ||
				containsFilters.Any(contains =>
					e.source.Summary?.Contains(contains, StringComparison.InvariantCultureIgnoreCase) == true ||
					e.source.Description?.Contains(contains, StringComparison.InvariantCultureIgnoreCase) == true)
			)
			.Select(e => new DanceEvent() {
				date = new DateTimeOffset(e.period.StartTime.AsUtc), // timezone???
				start = new DateTimeOffset(e.period.StartTime.AsUtc), // timezone???
				end = new DateTimeOffset(e.period.EffectiveEndTime.AsUtc), // timezone??? Null?
				summary = e.source.Summary,
				description = e.source.Description?.Replace("<ul>", "<ul style='list-style: inside'>").Replace("<b>", "<b style='font-weight: bolder'>"), // carrd has list-style:none on <ul>.
				location = e.source.Location })
			.ToArray();

		var result = JsonSerializer.Serialize(nextEvents);

		var proxyResponse = new ContentResult
		{
			Content = result,
			ContentType = "application/json; charset=utf-8",
			StatusCode = (int)HttpStatusCode.OK
		};

		AddStandardResponseHeaders(req.HttpContext.Response, req);

		return proxyResponse;
	}

	private async Task<string> Fetch(string url, bool useCache = true)
	{
		if (useCache && Cache.TryGetValue(url, out string cachedContent))
		{
			return cachedContent;
		}

		using (HttpResponseMessage remoteResponse = await HttpClient.GetAsync(url))
		using (HttpContent remoteContent = remoteResponse.Content)
		{
			var result = await remoteContent.ReadAsStringAsync();
			Cache.Set(url, result, new MemoryCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
			});
			return result;
		}
	}

	//[Function("GetOtherDances")]
	//public static async Task<IActionResult> GetOtherDances(
	//	[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
	//	HttpRequest req)
	//{
	//	var state = " " + req.Query["state"]; // like "ME"

	//	using (HttpClient client = new HttpClient())
	//	{
	//		client.DefaultRequestHeaders.Add("Accept", @"*/*");
	//		using (HttpResponseMessage remoteResponse = await client.GetAsync("https://www.trycontra.com/dances_locs.json"))
	//		using (HttpContent remoteContent = remoteResponse.Content)
	//		{
	//			var remoteContentString = await remoteContent.ReadAsStringAsync();
	//			var dances = JsonSerializer.Deserialize<List<DanceSeries>>(remoteContentString)
	//				.Where(d => d.city.EndsWith(state) && !d.inactive)
	//				.OrderBy(d=> d.lat + d.lon) // south east to north west
	//				.ToArray();

	//			var html = "<div id='otherDancesListId' class='otherDancesListClass'>" + string.Join(", ", dances
	//				.Select(d => $"<a href=\"{d.url}\">{d.city.Replace(state, "").TrimEnd(',')}</a>")) + "</div>";

	//			var proxyResponse = new ContentResult
	//			{
	//				Content = html,
	//				ContentType = "text/html; charset=utf-8",
	//				StatusCode = (int)HttpStatusCode.OK
	//			};

	//			AddStandardResponseHeaders(req.HttpContext.Response, req);

	//			return proxyResponse;
	//		}
	//	}
	//}

	//[Function("GetFilterCalendar")]
	//public static async Task<IActionResult> GetFilterCalendar(
	//	[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
	//	HttpRequest req)
	//{
	//	using (HttpClient client = new HttpClient())
	//	{
	//		client.DefaultRequestHeaders.Add("Accept", @"*/*");
	//		using (HttpResponseMessage remoteResponse = await client.GetAsync(req.Query["url"]))
	//		using (HttpContent remoteContent = remoteResponse.Content)
	//		{
	//			var remoteContentString = await remoteContent.ReadAsStringAsync();
	//			var cal = Calendar.Load(remoteContentString);

	//			//var months = int.Parse(req.Query["months"].FirstOrDefault("12"));

	//			foreach (var e in cal.Events.ToArray())
	//			{
	//				if (req.Query["contains"].Any(contains => !e.Summary.Contains(contains, StringComparison.InvariantCultureIgnoreCase)
	//					&& !e.Description.Contains(contains, StringComparison.InvariantCultureIgnoreCase)))
	//				{
	//					cal.Events.Remove(e);
	//				}
	//			}

	//			string result = new CalendarSerializer().SerializeToString(cal);

	//			var proxyResponse = new ContentResult
	//			{
	//				Content = result,
	//				ContentType = "text/calendar; charset=UTF-8",
	//				StatusCode = (int)HttpStatusCode.OK
	//			};

	//			AddStandardResponseHeaders(req.HttpContext.Response, req);

	//			return proxyResponse;
	//		}
	//	}
	//}

	private static void AddStandardResponseHeaders(HttpResponse response, HttpRequest req)
	{
		// Detect cold starts
		response.Headers["X-CallCount"] = CallCount++.ToString();
		// I think CORS is handled by azure: FunctionApp/API/CORS/Allowed Origins/*
		var origin = req.Headers["Origin"].FirstOrDefault();
		response.Headers["Access-Control-Allow-Origin"] = origin == null || origin == "null" ? "*" : origin;
		response.Headers["Access-Control-Allow-Credentials"] = "true";
	}

	public class DanceSeries
	{
		public string city { get; set; }
		public string url { get; set; }
		public bool inactive { get; set; }
		public string[] icals { get; set; }
		public double lat { get; set; }
		public double lon { get; set; }
	}

	public class DanceEvent
	{
		public DateTimeOffset date { get; set; }
		public DateTimeOffset start { get; set; }
		public DateTimeOffset end { get; set; }
		public string summary { get; set; }
		public string description { get; set; }
		public string location { get; set; }
	}
}