using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net;
using Ical.Net;
using System.Linq;
using Ical.Net.CalendarComponents;
using System.Collections.Generic;
using System.Text.Json;
using Ical.Net.Serialization;

namespace ProxyByRegex
{
	public static class Proxy
	{
		// TODO: Cache requests in azure reddis (after azure non-profit grant gives $2,000/y)

		static int CallCount = 0;

		// Google docs embeded have a link replacement scheme. Undo that.
		const string GoogleRedirect = "https://www\\.google\\.com/url\\?q=|&(amp;)?sa=D&(amp;)?source=editors&(amp;)?ust=\\d*&(amp;)?usg=[^\"]*";
		const string HtmlHeadElement = "<head>.*</head>";

		[FunctionName("Hello")]
		public static async Task<IActionResult> Hello(
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

		[FunctionName("Proxy")]
		public static async Task<IActionResult> ProxyDoc(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
			ILogger log)
		{
			var url = req.Query["url"].ToString();

			if (url.StartsWith(@"https://docs.google.com/document")) { }
			if (url.StartsWith(@"https://calendar.google.com/calendar/ical")) { }
			else
			{
				throw new Exception("Unsupported URL");
			}

			using (HttpClient client = new HttpClient())
			{
				//req.Headers["Accepted"]
				client.DefaultRequestHeaders.Add("Accept", @"*/*");
				using (HttpResponseMessage remoteResponse = await client.GetAsync(url))
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
		}

		[FunctionName("GetNextEventsJSON")]
		public static async Task<IActionResult> GetNextEventsJSON(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
			HttpRequest req)
		{
			using (HttpClient client = new HttpClient())
			{
				client.DefaultRequestHeaders.Add("Accept", @"*/*");
				using (HttpResponseMessage remoteResponse = await client.GetAsync(req.Query["url"]))
				using (HttpContent remoteContent = remoteResponse.Content)
				{
					var remoteContentString = await remoteContent.ReadAsStringAsync();
					var cal = Calendar.Load(remoteContentString);

					var months = int.Parse(req.Query["months"].FirstOrDefault("12"));

					var events = cal.GetOccurrences<CalendarEvent>(DateTime.Now.Date, DateTime.Now.AddMonths(months));
					var nextEvents = events
						.OrderBy(e => e.Period.StartTime)
						.Select(e => e.Source)
						.OfType<CalendarEvent>();

					foreach (var contains in req.Query["contains"])
					{
						nextEvents = nextEvents.Where(e => e.Summary.Contains(contains, StringComparison.InvariantCultureIgnoreCase)
						|| e.Description?.Contains(contains, StringComparison.InvariantCultureIgnoreCase) == true).ToArray();
					}

					foreach (var e in nextEvents)
					{
						e.Description = e.Description?.Replace("<ul>", "<ul style='list-style: inside'>").Replace("<b>", "<b style='font-weight: bolder'>"); // carrd has list-style:none on <ul>.
					}

					var result = JsonSerializer.Serialize(nextEvents.Select(e => new DanceEvent() { date = e.Start.AsDateTimeOffset, summary = e.Summary, description = e.Description, location = e.Location }).ToArray());

					var proxyResponse = new ContentResult
					{
						Content = result,
						ContentType = "application/json; charset=utf-8",
						StatusCode = (int)HttpStatusCode.OK
					};

					AddStandardResponseHeaders(req.HttpContext.Response, req);

					return proxyResponse;
				}
			}
		}

		//[FunctionName("GetOtherDances")]
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

		//[FunctionName("GetFilterCalendar")]
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
			public string summary { get; set; }
			public string description { get; set; }
			public string location { get; set; }
		}
	}
}
