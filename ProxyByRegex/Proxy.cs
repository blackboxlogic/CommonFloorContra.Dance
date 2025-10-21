using System;
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

	// Google docs embeded have a link replacement scheme. Undo that.
	private static readonly Regex GoogleRedirectRegex = new("https://www\\.google\\.com/url\\?q=|&(amp;)?sa=D&(amp;)?source=editors&(amp;)?ust=\\d*&(amp;)?usg=[^\"]*", RegexOptions.Compiled);
	private static readonly Regex HtmlHeadElementRegex = new("<head>.*</head>", RegexOptions.Compiled | RegexOptions.Singleline);
	private readonly IHttpClientFactory HttpClientFactory;
	private readonly ILogger Logger;
	private readonly IMemoryCache Cache;

	public Proxy(IHttpClientFactory httpClientFactory, ILogger<Proxy> logger, IMemoryCache cache)
	{
		//HttpClient.DefaultRequestHeaders.Add("Accept", @"*/*");
		HttpClientFactory = httpClientFactory;
		Logger = logger;
		Cache = cache;
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
		var remoteContentString = await Fetch(urlString);
		var cal = Calendar.Load(remoteContentString) ?? throw new Exception("Failed to load calendar at " + remoteContentString);
		var months = int.Parse(req.Query["months"].FirstOrDefault("12") ?? throw new ArgumentNullException("url"));
		var start = CalDateTime.UtcNow;
		var end = start.AddMonths(months);
		var events = cal.GetOccurrences<CalendarEvent>(start).TakeWhileBefore(end).ToArray();

		var containsFilters = req.Query["contains"].OfType<string>().Where(c => c != "").ToArray();
		var nextEvents = events
			.OrderBy(e => e.Period.StartTime)
			// source is the INITIAL reoccuring event
			.Where(e => e.Source is CalendarEvent)
			.Select(e => new {period = e.Period, source = (CalendarEvent)e.Source})
			.Where(e =>
				containsFilters.Length == 0 ||
				containsFilters.Any(contains =>
					e.source.Summary?.Contains(contains, StringComparison.InvariantCultureIgnoreCase) == true ||
					e.source.Description?.Contains(contains, StringComparison.InvariantCultureIgnoreCase) == true)
			)
			.Select(e => new DanceEvent() {
				date = new DateTimeOffset(e.period.StartTime.AsUtc),
				start = new DateTimeOffset(e.period.StartTime.AsUtc),
				end = new DateTimeOffset(e.period.EffectiveEndTime?.AsUtc ?? e.period.StartTime.AsUtc),
				summary = e.source.Summary ?? "",
				// carrd has list-style:none on <ul>.
				description = e.source.Description?.Replace("<ul>", "<ul style='list-style: inside'>")?.Replace("<b>", "<b style='font-weight: bolder'>")?.Replace("\n", "<br>"),
				location = e.source.Location})
			.ToArray();

		var result = JsonSerializer.Serialize(nextEvents);

		var proxyResponse = new ContentResult
		{
			Content = result,
			ContentType = "application/json; charset=utf-8",
			StatusCode = (int)HttpStatusCode.OK
		};

		return proxyResponse;
	}

	[Function("DailyCleanup")]
	public async Task DailyCleanup([TimerTrigger("0 0 0 * * *")] TimerInfo myTimer)
	{
		Logger.LogInformation($"C# Timer trigger function 'DailyCleanup' executed at: {DateTime.Now}");
		// Add your daily cleanup or scheduled logic here.
		// For example, you might clear specific cache entries, log statistics, etc.
		await Task.CompletedTask; // Or perform actual async work
		Logger.LogInformation($"Next timer schedule for 'DailyCleanup' at: {myTimer.ScheduleStatus?.Next}");
	}

	private async Task<string> Fetch(string url, bool useCache = true)
	{
		if (useCache && Cache.TryGetValue(url, out string? cachedContent) && cachedContent != null)
		{
			return cachedContent;
		}

		using(var httpClient = HttpClientFactory.CreateClient())
		using (HttpResponseMessage remoteResponse = await httpClient.GetAsync(url))
		using (HttpContent remoteContent = remoteResponse.Content)
		{
			var result = await remoteContent.ReadAsStringAsync() ?? throw new Exception("Received null from " + url);
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
	//			return proxyResponse;
	//		}
	//	}
	//}

	public class DanceSeries
	{
		public required string city { get; set; }
		public required string url { get; set; }
		public bool inactive { get; set; }
		public required string[] icals { get; set; }
		public double lat { get; set; }
		public double lon { get; set; }
	}

	public class DanceEvent
	{
		public DateTimeOffset date { get; set; }
		public DateTimeOffset start { get; set; }
		public DateTimeOffset end { get; set; }
		public required string summary { get; set; }
		public string? description { get; set; }
		public string? location { get; set; }
	}
}