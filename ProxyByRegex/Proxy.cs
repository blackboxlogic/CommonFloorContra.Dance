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
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Collections.Generic;
using System.Text.Json;

namespace ProxyByRegex
{
	public static class Proxy
	{
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
						//remoteContentString = remoteContentString.Replace("<head>", "<head><base target=\"_top\">"); // I forget why? Maybe it was about links?
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

					req.HttpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";

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
					var nextEvent = events.OrderBy(e => e.Period.StartTime).FirstOrDefault()?.Source as CalendarEvent;
					var description = "<span>no upcoming events found :(</span>";

					if (nextEvent != null)
					{
						description = $"<h1>{nextEvent.Start.Date.ToString("dddd, MMMM d, yyyy")}</h1><h2>{nextEvent.Summary}</h2>{nextEvent.Description}";
					}

					var proxyResponse = new ContentResult
					{
						Content = description,
						ContentType = "text/html; charset=utf-8",
						StatusCode = (int)HttpStatusCode.OK
					};

					req.HttpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";

					return proxyResponse;
				}
			}
		}

		[FunctionName("GetNextYear")]
		public static async Task<IActionResult> GetNextYear(
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

					var events = cal.GetOccurrences<CalendarEvent>(DateTime.Now.Date.AddDays(1), DateTime.Now.AddYears(1));
					var nextEvents = events.OrderBy(e => e.Period.StartTime).ToArray();
					var html = "<span>no upcoming events found :(</span>";

					if (nextEvents.Any())
					{
						html = $"<h1>Future Events:</h1>";
						html += "<ul>" + string.Concat(nextEvents.Select(e => $"<li><b>{e.Period.StartTime.Date.ToString("ddd, MMM d")}</b> {(e.Source as CalendarEvent).Summary}</li>")) + "</ul>";
						html = html.Replace("TBD", "<b>TBD</b>");
					}

					var proxyResponse = new ContentResult
					{
						Content = html,
						ContentType = "text/html; charset=utf-8",
						StatusCode = (int)HttpStatusCode.OK
					};

					req.HttpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";

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
						.ToArray();

					var html = string.Join(", ", dances
						.Select(d => $"<a href=\"{d.url}\">{d.city.Replace(state, "").TrimEnd(',')}</a>"));

					var proxyResponse = new ContentResult
					{
						Content = html,
						ContentType = "text/html; charset=utf-8",
						StatusCode = (int)HttpStatusCode.OK
					};

					req.HttpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";

					return proxyResponse;
				}
			}
		}

		public class DanceSeries
		{
			public string city { get; set; }
			public string url { get; set; }
			public bool inactive { get; set; }
			public string[] icals { get; set; }
		}
	}
}
