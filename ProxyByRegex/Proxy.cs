using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Net;
using Ical.Net;
using System.Linq;
using System.Net.Mime;

namespace ProxyByRegex
{
	public static class Proxy
	{
		// Google docs embeded have a link replacement scheme. Undo that.
		const string GoogleRedirect = "https://www\\.google\\.com/url\\?q=|&(amp;)?sa=D&(amp;)?source=editors&(amp;)?ust=\\d*&(amp;)?usg=[^\"]*";
		//https://www.google.com/url?q=https://goo.gl/maps/wiKvCqQXJuvENgxu9&amp;sa=D&amp;source=editors&amp;ust=1673497522098420&amp;usg=AOvVaw2q6BTu7yL_ro96iMBJHUzL
		//https://www.google.com/url?q=https://www.commonfloorcontra.dance/details&sa=D&source=editors&ust=1673327554932203&usg=AOvVaw2L4qtl85v3uaFNOAe7A-hv

		[FunctionName("Proxy")]
		public static async Task<IActionResult> ProxyDoc(
			[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
			ILogger log)
		{
			using (HttpClient client = new HttpClient())
			{
				client.DefaultRequestHeaders.Add("Accept", @"*/*");
				using (HttpResponseMessage remoteResponse = await client.GetAsync(req.Query["url"]))
				using (HttpContent remoteContent = remoteResponse.Content)
				{
					var remoteContentString = await remoteContent.ReadAsStringAsync();

					if (req.Query["url"].ToString().StartsWith(@"https://docs.google.com/document"))
					{
						remoteContentString = new Regex(GoogleRedirect).Replace(remoteContentString, "");
						remoteContentString = remoteContentString.Replace("<head>", "<head><base target=\"_top\">"); // I forget why?
					}
					else
					{
						throw new Exception("Unknown URL type");
					}

					var proxyResponse = new ContentResult
					{
						Content = remoteContentString,
						ContentType = remoteContent.Headers.ContentType.MediaType,
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
					var nextEvent = cal.Events.OrderBy(e => e.Start).FirstOrDefault(e => e.Start.AsDateTimeOffset > DateTimeOffset.Now);

					var proxyResponse = new ContentResult
					{
						Content = nextEvent?.Description ?? "<span>no upcoming events found :(</span>",
						ContentType = "text/html",
						StatusCode = (int)HttpStatusCode.OK
					};

					req.HttpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";

					return proxyResponse;
				}
			}
		}
	}
}
