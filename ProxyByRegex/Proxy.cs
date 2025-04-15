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
		// TODO: Cache requests?

		static int CallCount = 0;

		// Google docs embeded have a link replacement scheme. Undo that.
		const string GoogleRedirect = "https://www\\.google\\.com/url\\?q=|&(amp;)?sa=D&(amp;)?source=editors&(amp;)?ust=\\d*&(amp;)?usg=[^\"]*";
		const string HtmlHeadElement = "<head>.*</head>";

		[FunctionName("Proxy")]
		public static async Task<IActionResult> ProxyDoc(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
			ILogger log)
		{
			using (HttpClient client = new HttpClient())
			{
				//req.Headers["Accepted"]
				client.DefaultRequestHeaders.Add("Accept", @"*/*");
				using (HttpResponseMessage remoteResponse = await client.GetAsync(req.Query["url"]))
				using (HttpContent remoteContent = remoteResponse.Content)
				{
					var remoteContentString = await remoteContent.ReadAsStringAsync();

					if (req.Query["url"].ToString().StartsWith(@"https://docs.google.com/document"))
					{
						remoteContentString = new Regex(GoogleRedirect).Replace(remoteContentString, "");
						remoteContentString = new Regex(HtmlHeadElement).Replace(remoteContentString, "");
					}
					else
					{
						throw new Exception("Unknown URL type");
					}

					var proxyResponse = new ContentResult
					{
						Content = remoteContentString,
						ContentType = remoteContent.Headers.ContentType.ToString(),
						StatusCode = (int)HttpStatusCode.OK
					};

					AddStandardResponseHeaders(req.HttpContext.Response);

					return proxyResponse;
				}
			}
		}

		[FunctionName("GetNextEvent")]
		public static async Task<IActionResult> GetNextEvent(
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

					var events = cal.GetOccurrences<CalendarEvent>(DateTime.Now, DateTime.Now.AddYears(1));
					var nextEvents = events
						.OrderBy(e => e.Period.StartTime)
						.Select(e => e.Source)
						.OfType<CalendarEvent>();

					foreach (var contains in req.Query["contains"])
					{
						nextEvents = nextEvents.Where(e => e.Summary.Contains(contains, StringComparison.InvariantCultureIgnoreCase)
						|| e.Description.Contains(contains, StringComparison.InvariantCultureIgnoreCase)).ToArray();
					}

					var nextEvent = nextEvents.FirstOrDefault();

					var description = "<span>no upcoming events found :(</span>";

					if (nextEvent != null)
					{
						description = $"<h1 id='eventDateId' class='eventDateClass'>{nextEvent.Start.Date.ToString("dddd, MMMM d, yyyy")}</h1><h2 id='eventSummaryId' class='eventSummaryClass'>{nextEvent.Summary}</h2><div id='eventDescriptionId' class='eventDescriptionClass' style='eventDescriptionStyle'>{nextEvent.Description}</div>";
						description = description.Replace("<ul>", "<ul style='list-style: inside'>"); // carrd has list-style:none on <ul>.
					}

					var proxyResponse = new ContentResult
					{
						Content = description,
						ContentType = "text/html; charset=utf-8",
						StatusCode = (int)HttpStatusCode.OK
					};

					AddStandardResponseHeaders(req.HttpContext.Response);

					return proxyResponse;
				}
			}
		}

		[FunctionName("GetNextEventsSummaries")]
		public static async Task<IActionResult> GetNextEventsSummaries(
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

					var events = cal.GetOccurrences<CalendarEvent>(DateTime.Now.Date.AddDays(1), DateTime.Now.AddMonths(months));
					var nextEvents = events.OrderBy(e => e.Period.StartTime).ToArray();

					foreach (var contains in req.Query["contains"])
					{
						nextEvents = nextEvents.Where(e => (e.Source as CalendarEvent).Summary.Contains(contains, StringComparison.InvariantCultureIgnoreCase)
						|| (e.Source as CalendarEvent).Description.Contains(contains, StringComparison.InvariantCultureIgnoreCase)).ToArray();
					}

					var html = "<span>no upcoming events found :(</span>";

					if (nextEvents.Any())
					{
						html = "<ul>" + string.Concat(nextEvents.Select(e => $"<li><b>{e.Period.StartTime.Date.ToString("ddd, MMM d, yyyy")}</b> {(e.Source as CalendarEvent).Summary}</li>")) + "</ul>";
						html = html.Replace("TBD", "<b>TBD</b>");
					}

					var proxyResponse = new ContentResult
					{
						Content = html,
						ContentType = "text/html; charset=utf-8",
						StatusCode = (int)HttpStatusCode.OK
					};

					AddStandardResponseHeaders(req.HttpContext.Response);

					return proxyResponse;
				}
			}
		}

		[FunctionName("GetOtherDances")]
		public static async Task<IActionResult> GetOtherDances(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
			HttpRequest req)
		{
			var state = " " + req.Query["state"]; // like "ME"

			using (HttpClient client = new HttpClient())
			{
				client.DefaultRequestHeaders.Add("Accept", @"*/*");
				using (HttpResponseMessage remoteResponse = await client.GetAsync("https://www.trycontra.com/dances_locs.json"))
				using (HttpContent remoteContent = remoteResponse.Content)
				{
					var remoteContentString = await remoteContent.ReadAsStringAsync();
					var dances = JsonSerializer.Deserialize<List<DanceSeries>>(remoteContentString)
						.Where(d => d.city.EndsWith(state) && !d.inactive)
						.OrderBy(d=> d.lat + d.lon) // south east to north west
						.ToArray();

					var html = "<div id='otherDancesListId' class='otherDancesListClass'>" + string.Join(", ", dances
						.Select(d => $"<a href=\"{d.url}\">{d.city.Replace(state, "").TrimEnd(',')}</a>")) + "</div>";

					var proxyResponse = new ContentResult
					{
						Content = html,
						ContentType = "text/html; charset=utf-8",
						StatusCode = (int)HttpStatusCode.OK
					};

					AddStandardResponseHeaders(req.HttpContext.Response);

					return proxyResponse;
				}
			}
		}

		[FunctionName("GetFilterCalendar")]
		public static async Task<IActionResult> GetFilterCalendar(
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

					//var months = int.Parse(req.Query["months"].FirstOrDefault("12"));

					foreach (var e in cal.Events.ToArray())
					{
						if (req.Query["contains"].Any(contains => !e.Summary.Contains(contains, StringComparison.InvariantCultureIgnoreCase)
							&& !e.Description.Contains(contains, StringComparison.InvariantCultureIgnoreCase)))
						{
							cal.Events.Remove(e);
						}
					}

					string result = new CalendarSerializer().SerializeToString(cal);

					var proxyResponse = new ContentResult
					{
						Content = result,
						ContentType = "text/calendar; charset=UTF-8",
						StatusCode = (int)HttpStatusCode.OK
					};

					AddStandardResponseHeaders(req.HttpContext.Response);

					return proxyResponse;
				}
			}
		}

		private static void AddStandardResponseHeaders(HttpResponse response)
		{
			response.Headers["Access-Control-Allow-Origin"] = "*";
			response.Headers["X-CallCount"] = CallCount++.ToString();
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
	}
}
