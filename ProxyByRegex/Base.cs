using System;
using System.Text.RegularExpressions;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Calendar = Ical.Net.Calendar;

namespace CalendarFunctions;

public abstract class Base
{
	internal static readonly Regex GoogleRedirectRegex = new("https://www\\.google\\.com/url\\?q=|&(amp;)?sa=D&(amp;)?source=editors&(amp;)?ust=\\d*&(amp;)?usg=[^\"]*", RegexOptions.Compiled);
	internal static readonly Regex HtmlHeadElementRegex = new("<head>.*</head>", RegexOptions.Compiled | RegexOptions.Singleline);
	internal readonly IHttpClientFactory HttpClientFactory;
	internal readonly ILogger Logger;
	internal readonly IMemoryCache Cache;
	internal readonly Microsoft.Extensions.Configuration.IConfiguration Configuration;

	public Base(IHttpClientFactory httpClientFactory, ILogger<Proxy> logger, IMemoryCache cache, Microsoft.Extensions.Configuration.IConfiguration configuration)
	{
		HttpClientFactory = httpClientFactory;
		Logger = logger;
		Cache = cache;
		Configuration = configuration;
	}

	internal string GetConfigOrThrow(string key)
	{
		return Configuration[key] ?? throw new Exception("Missing config: " + key);
	}

	internal async Task<DanceEvent[]> GetNextEvents(string urlString, string[] containsFilters, int months = 12)
	{
		var remoteContentString = await Fetch(urlString);
		var cal = Calendar.Load(remoteContentString) ?? throw new Exception("Failed to load calendar at " + remoteContentString);
		var start = CalDateTime.UtcNow;
		var end = start.AddMonths(months);
		var events = cal.GetOccurrences<CalendarEvent>(start).TakeWhileBefore(end).ToArray();
		var nextEvents = events
			.OrderBy(e => e.Period.StartTime)
			// source is the INITIAL reoccuring event
			.Where(e => e.Source is CalendarEvent)
			.Select(e => new { period = e.Period, source = (CalendarEvent)e.Source })
			.Where(e =>
				containsFilters.Length == 0 ||
				containsFilters.Any(contains =>
					e.source.Summary?.Contains(contains, StringComparison.InvariantCultureIgnoreCase) == true ||
					e.source.Description?.Contains(contains, StringComparison.InvariantCultureIgnoreCase) == true)
			)
			.Select(e => new DanceEvent()
			{
				date = new DateTimeOffset(e.period.StartTime.AsUtc),
				start = new DateTimeOffset(e.period.StartTime.AsUtc),
				end = new DateTimeOffset(e.period.EffectiveEndTime?.AsUtc ?? e.period.StartTime.AsUtc),
				summary = e.source.Summary ?? "",
				// carrd has list-style:none on <ul>.
				description = e.source.Description?.Replace("<ul>", "<ul style='list-style: inside'>")?.Replace("<b>", "<b style='font-weight: bolder'>")?.Replace("\n", "<br>"),
				location = e.source.Location
			})
			.ToArray();

		return nextEvents;
	}

	internal async Task<string> Fetch(string url, bool useCache = true)
	{
		if (useCache && Cache.TryGetValue(url, out string? cachedContent) && cachedContent != null)
		{
			return cachedContent;
		}

		using (var httpClient = HttpClientFactory.CreateClient())
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
