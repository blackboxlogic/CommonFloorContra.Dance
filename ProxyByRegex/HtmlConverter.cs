using System.IO;
using System.Text;
using HtmlAgilityPack;

namespace CalendarFunctions;

public static class HtmlConverter
{
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(html);

        StringWriter sw = new StringWriter();
        ConvertTo(doc.DocumentNode, sw);
        sw.Flush();
        return sw.ToString();
    }

    private static void ConvertContentTo(HtmlNode node, TextWriter outText)
    {
        foreach (HtmlNode subnode in node.ChildNodes)
        {
            ConvertTo(subnode, outText);
        }
    }

    private static void ConvertTo(HtmlNode node, TextWriter outText)
    {
        string html;
        switch (node.NodeType)
        {
            case HtmlNodeType.Comment:
                // don't output comments
                break;

            case HtmlNodeType.Document:
                ConvertContentTo(node, outText);
                break;

            case HtmlNodeType.Text:
                string parentName = node.ParentNode.Name;
                if ((parentName == "script") || (parentName == "style"))
                    break;

                html = ((HtmlTextNode)node).Text;

                if (HtmlNode.IsOverlappedClosingElement(html))
                    break;

                if (html.Trim().Length > 0)
                {
                    outText.Write(HtmlEntity.DeEntitize(html));
                }
                break;

            case HtmlNodeType.Element:
                switch (node.Name)
                {
                    case "p":
                    case "br":
                        outText.Write("\r\n");
                        break;
                    case "li":
                        outText.Write("• ");
                        break;
                }

                if (node.HasChildNodes)
                {
                    ConvertContentTo(node, outText);
                }
                break;
        }
    }
}
