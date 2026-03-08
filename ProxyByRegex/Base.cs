using System;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Calendar = Ical.Net.Calendar;

namespace CalendarFunctions;

public abstract class Base(IHttpClientFactory httpClientFactory, ILogger<Proxy> logger, IMemoryCache cache, IConfiguration configuration)
{
	internal static readonly Regex GoogleRedirectRegex = new("https://www\\.google\\.com/url\\?q=|&(amp;)?sa=D&(amp;)?source=editors&(amp;)?ust=\\d*&(amp;)?usg=[^\"]*", RegexOptions.Compiled);
	internal static readonly Regex HtmlHeadElementRegex = new("<head>.*</head>", RegexOptions.Compiled | RegexOptions.Singleline);
	internal readonly IHttpClientFactory HttpClientFactory = httpClientFactory;
	internal readonly ILogger Logger = logger;
	internal readonly IMemoryCache Cache = cache;
	internal readonly IConfiguration Configuration = configuration;

	internal string GetConfigOrThrow(string key)
	{
		return Configuration[key] ?? throw new Exception("Missing config: " + key);
	}

	internal async Task<(DanceEvent[] nextEvents, HttpContentHeaders headers, bool cached)> GetNextEvents(string urlString, string[] containsFilters, int months = 12)
	{
		(var remoteContentString, var headers, var cached) = await Fetch(urlString);
		var cal = Calendar.Load(remoteContentString) ?? throw new Exception("Failed to load calendar at " + remoteContentString);
		var start = CalDateTime.UtcNow;
		var end = start.AddMonths(months);
		var events = cal.GetOccurrences<CalendarEvent>(start).TakeWhileBefore(end).ToArray();

		// get X-WR-TIMEZONE
		var defaultTimeZoneLocation = cal.Properties["X-WR-TIMEZONE"]!.Value as string;
		//var defaultTimeZone = cal.TimeZones.First(tz => tz.Location == defaultTimeZoneLocation);
		var defaultTzi = TimeZoneInfo.FindSystemTimeZoneById(defaultTimeZoneLocation!);

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
				// TODO: Test "all day" events, multi-day events, zero-duration events, early morning, late night, night to morning.
				start = new DateTimeOffset(e.period.StartTime.AsUtc),
				end = new DateTimeOffset(e.period.EffectiveEndTime?.AsUtc ?? e.period.StartTime.AsUtc),
				// If the event time is UTC (no TzId or TzId=="UTC"), convert to the calendar's default timezone
				startLocal = string.IsNullOrEmpty(e.period.StartTime.TzId) || e.period.StartTime.TzId == "UTC"
					? TimeZoneInfo.ConvertTimeFromUtc(e.period.StartTime.AsUtc, defaultTzi)
					: e.period.StartTime.Value,
				endLocal = string.IsNullOrEmpty(e.period.EffectiveEndTime?.TzId) || e.period.EffectiveEndTime?.TzId == "UTC"
					? TimeZoneInfo.ConvertTimeFromUtc(e.period.EffectiveEndTime?.AsUtc ?? e.period.StartTime.AsUtc, defaultTzi)
					: e.period.EffectiveEndTime?.Value ?? e.period.StartTime.Value,
				summary = Configuration["Environment"] == "PROD"
					? e.source.Summary ?? ""
					: $"[{Configuration["Environment"]}] " + e.source.Summary ?? "",
				description = e.source.Description, // Might be HTML, might be plain text. Client should expect '\n' chars.
				location = e.source.Location,
			})
			.ToArray();

		return (nextEvents, headers, cached);
	}

	private MemoryCacheEntryOptions FetchCacheOptions => new()
	{
		AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(double.Parse(Configuration["CacheDurationMinutes"] ?? "10"))
	};

	internal async Task<(string content, HttpContentHeaders headers, bool cached)> Fetch(string url, bool useCache = true)
	{
		if (useCache && Cache.TryGetValue("content%" + url, out string? cachedContent) && cachedContent != null
			&& Cache.TryGetValue("headers%" + url, out HttpContentHeaders? cachedHeaders) && cachedHeaders != null)
		{
			//Logger.LogInformation("Cache hit for {url}", url);
			return (cachedContent, cachedHeaders, true);
		}

		//Logger.LogInformation("Cache miss for {url}", url);

		using (var httpClient = HttpClientFactory.CreateClient())
		using (HttpResponseMessage remoteResponse = await httpClient.GetAsync(url))
		using (HttpContent remoteContent = remoteResponse.Content)
		{
			var result = await remoteContent.ReadAsStringAsync() ?? throw new Exception("Received null from " + url);
			Cache.Set("content%" + url, result, FetchCacheOptions);
			Cache.Set("headers%" + url, remoteContent.Headers, FetchCacheOptions);
			return (result, remoteContent.Headers, false);
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
		public DateTimeOffset start { get; set; }
		public DateTimeOffset end { get; set; }
		public DateTime startLocal { get; set; }
		public DateTime endLocal { get; set; }
		public required string summary { get; set; }
		public string? description { get; set; }
		public string? location { get; set; }
	}
}
