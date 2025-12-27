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

    private static void ConvertContentTo(HtmlNode node, TextWriter outText, int listDepth = 0)
    {
        foreach (HtmlNode subnode in node.ChildNodes)
        {
            ConvertTo(subnode, outText, listDepth);
        }
    }

    private static void ConvertTo(HtmlNode node, TextWriter outText, int listDepth = 0)
    {
        string html;
        switch (node.NodeType)
        {
            case HtmlNodeType.Comment:
                // don't output comments
                break;

            case HtmlNodeType.Document:
                ConvertContentTo(node, outText, listDepth);
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
                int nextListDepth = listDepth;
                switch (node.Name)
                {
                    case "p":
                    case "br":
                        outText.Write("\r\n");
                        break;
                    case "ul":
                    case "ol":
                        nextListDepth++; // Increment depth for nested lists
                        break;
                    case "li":
                        outText.Write("\r\n" + new string('\t', listDepth) + "• ");
                        break;
                }

                if (node.HasChildNodes)
                {
                    ConvertContentTo(node, outText, nextListDepth);
                }
                break;
        }
    }
}
