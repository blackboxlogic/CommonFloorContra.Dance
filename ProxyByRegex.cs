using System.Net;
using System.Text;
using System.Text.RegularExpressions;

const string GoogleRedirect = "https://www\\.google\\.com/url\\?q=|&(amp;)?sa=D&(amp;)?source=editors&(amp;)?ust=\\d*&(amp;)?usg=[^\"]*";

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, ILogger log)
{
    var d = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query);
    var html = await Fetch(d["url"]);
    html = BaseTargetTop(html);
    html = RunRegexRemove(html, d.Get("regex") ?? GoogleRedirect);
    var response = req.CreateResponse(HttpStatusCode.OK);
    response.Content = new StringContent(html, Encoding.UTF8, "text/html");

    return response;
}

public static async Task<string> Fetch(string url)
{
    using (HttpClient client = new HttpClient())
    using (HttpResponseMessage response = await client.GetAsync(url))
    using (HttpContent content = response.Content)
    {
        return await content.ReadAsStringAsync();
    }
}

public static string RunRegexRemove(string html, string regexParam)
{
    return new Regex(regexParam).Replace(html, "");
}

public static string BaseTargetTop(string html)
{
    return html.Replace("<head>", "<head><base target=\"_top\">");
}
