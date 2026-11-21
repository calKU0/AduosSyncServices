using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Allegro.Aduos.Gaska.ProductsService.Helpers
{
    /// <summary>
    /// Tekst od dostawcy (opis, "pasuje do", zamienniki) bywa sformatowany HTML-em: &lt;b&gt;, &lt;br /&gt;,
    /// &lt;/br&gt;, nowe linie, myślniki jako punkty listy. Zakodowanie go w całości (HtmlEncode) pokazuje
    /// kupującemu surowe tagi, a wklejenie bez zmian łamie zasady opisu Allegro. Ten konwerter
    /// zamienia go na bloki dozwolone w opisie Allegro : akapity &lt;p&gt;, listy &lt;ul&gt;&lt;li&gt;
    /// i pogrubienia &lt;b&gt; - cała reszta tagów znika, a tekst jest poprawnie zakodowany.
    /// </summary>
    internal static class SupplierHtml
    {
        /// <summary>
        /// Tylko znane tagi HTML są traktowane jak znaczniki. Inny tekst w nawiasach ostrych
        /// (np. "&lt;M10&gt;", "&lt;P1&gt;") to treść techniczna i zostaje w opisie jako tekst.
        /// </summary>
        private static readonly Regex Token = new(
            @"<\s*/?\s*(?:b|strong|br|p|div|span|ul|ol|li|i|em|u|h[1-6]|font|a|img|table|thead|tbody|tr|td|th|sup|sub|small|center|hr|section|article|header|footer|label|s|strike|del|ins|code|pre|blockquote)\b(?:\s+[a-zA-Z_:][-a-zA-Z0-9_:.]*(?:\s*=\s*(?:""[^""]*""|'[^']*'|[^\s""'<>]+))?)*\s*/?\s*>|[^<]+|<",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TagName = new(@"^<\s*(/?)\s*([a-zA-Z0-9]+)", RegexOptions.Compiled);
        private static readonly Regex Spaces = new(@"[ \t ]+", RegexOptions.Compiled);

        /// <summary>Tagi zakodowane jako tekst (&amp;lt;b&amp;gt;) - tak wracają opisy ofert wystawionych z błędem.</summary>
        private static readonly Regex EscapedTag = new(
            @"&lt;\s*(/?)\s*(b|strong|br|p|div|span|ul|ol|li|i|em|u|h[1-6])\b[^&]*?/?\s*&gt;",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Punkt listy: myślnik albo kropka. Numerowanych linii nie ruszamy - numer jest częścią treści.</summary>
        private static readonly Regex ListBullet = new(@"^\s*[-–•*]\s+", RegexOptions.Compiled);

        /// <summary>Tekst dostawcy jako bloki HTML (&lt;p&gt; / &lt;ul&gt;). Pusty tekst = pusty wynik.</summary>
        /// <param name="decodeEscapedTags">
        /// Czy traktować zakodowane tagi (&amp;lt;b&amp;gt;) jak prawdziwe - dla opisów już zapisanych z tym błędem.
        /// </param>
        public static string ToBlocks(string? text, bool decodeEscapedTags = false)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            if (decodeEscapedTags)
                text = EscapedTag.Replace(text, m => $"<{m.Groups[1].Value}{m.Groups[2].Value.ToLowerInvariant()}>");

            var builder = new BlockBuilder();

            foreach (Match token in Token.Matches(text.Replace("\r\n", "\n").Replace('\r', '\n')))
            {
                var value = token.Value;
                var tag = value.StartsWith('<') ? TagName.Match(value) : null;

                if (tag == null || !tag.Success)
                {
                    builder.Text(value);
                    continue;
                }

                var closing = tag.Groups[1].Value == "/";

                switch (tag.Groups[2].Value.ToLowerInvariant())
                {
                    case "b":
                    case "strong":
                        builder.Bold(!closing);
                        break;

                    case "br":
                    case "p":
                    case "div":
                        builder.LineBreak();
                        break;

                    case "h1":
                    case "h2":
                    case "h3":
                    case "h4":
                    case "h5":
                    case "h6":
                        builder.LineBreak();
                        builder.Bold(!closing);
                        break;

                    case "li":
                        if (closing)
                            builder.LineBreak();
                        else
                            builder.ListItem();
                        break;

                    case "ul":
                    case "ol":
                        builder.LineBreak();
                        break;

                    // span, i, em, u, font, a... - zostaje sam tekst.
                }
            }

            return builder.Build();
        }

        /// <summary>
        /// Jak <see cref="ToBlocks"/>, ale z etykietą przed treścią ("Opis: ") - w pierwszym akapicie
        /// albo w osobnym akapicie, gdy treść zaczyna się od listy.
        /// </summary>
        public static string ToLabelledBlocks(string label, string? text)
        {
            // Dostawca potrafi przysłać tagi już zakodowane (&lt;/b&gt;) - w tekście dostawcy to zawsze tagi, nie treść.
            var blocks = ToBlocks(text, decodeEscapedTags: true);

            if (blocks.Length == 0)
                return string.Empty;

            var encodedLabel = $"<b>{BlockBuilder.Encode(label)} </b>";

            return blocks.StartsWith("<p>", StringComparison.Ordinal)
                ? "<p>" + encodedLabel + blocks[3..]
                : $"<p>{encodedLabel}</p>{blocks}";
        }

        /// <summary>Koduje zwykły tekst do wstawienia w opis (tylko znaki specjalne HTML, bez encji dla polskich liter).</summary>
        public static string Encode(string? text) =>
            string.IsNullOrEmpty(text)
                ? string.Empty
                : BlockBuilder.Encode(new string(text.Where(c => c >= 32).ToArray()).Trim());

        /// <summary>Składa akapity i listy, pilnując domknięcia pogrubień w obrębie bloku.</summary>
        private sealed class BlockBuilder
        {
            private readonly StringBuilder _output = new();
            private readonly StringBuilder _line = new();
            private bool _bold;
            private bool _lineIsListItem;
            private bool _inList;

            public void Text(string raw)
            {
                var parts = raw.Split('\n');

                for (var i = 0; i < parts.Length; i++)
                {
                    if (i > 0)
                        LineBreak();

                    var decoded = Spaces.Replace(WebUtility.HtmlDecode(parts[i]), " ");

                    if (decoded.Length == 0)
                        continue;

                    if (_line.Length == 0)
                    {
                        decoded = decoded.TrimStart();

                        // Linia zaczynająca się od myślnika / punktu to element listy.
                        var bullet = ListBullet.Match(decoded);
                        if (bullet.Success && decoded.Length > bullet.Length)
                        {
                            _lineIsListItem = true;
                            decoded = decoded[bullet.Length..];
                        }

                        if (decoded.Length == 0)
                            continue;

                        if (_bold)
                            _line.Append("<b>");
                    }

                    _line.Append(Encode(decoded));
                }
            }

            public void Bold(bool on)
            {
                if (on == _bold)
                    return;

                if (_line.Length > 0)
                    _line.Append(on ? "<b>" : "</b>");

                _bold = on;
            }

            public void ListItem()
            {
                LineBreak();
                _lineIsListItem = true;
            }

            public void LineBreak()
            {
                var content = _line.ToString().TrimEnd();

                if (_bold && content.Length > 0)
                    content += "</b>";

                content = content.Replace("<b></b>", string.Empty);

                if (StripTags(content).Trim().Length > 0)
                {
                    if (_lineIsListItem)
                    {
                        if (!_inList)
                        {
                            _output.Append("<ul>");
                            _inList = true;
                        }

                        _output.Append("<li>").Append(content).Append("</li>");
                    }
                    else
                    {
                        CloseList();
                        _output.Append("<p>").Append(content).Append("</p>");
                    }
                }

                _line.Clear();
                _lineIsListItem = false;
            }

            public string Build()
            {
                LineBreak();
                CloseList();
                return _output.ToString();
            }

            private void CloseList()
            {
                if (!_inList)
                    return;

                _output.Append("</ul>");
                _inList = false;
            }

            /// <summary>
            /// Kodujemy tylko znaki specjalne HTML. WebUtility.HtmlEncode zamienia też "ó" na "&amp;#243;" -
            /// poprawne, ale nieczytelne przy porównywaniu opisów i niepotrzebne w UTF-8.
            /// </summary>
            internal static string Encode(string text) =>
                text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

            private static string StripTags(string html) => Regex.Replace(html, "<[^>]+>", string.Empty);
        }
    }
}
