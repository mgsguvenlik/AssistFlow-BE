using Business.Interfaces.Helpdesk;
using Microsoft.Extensions.Logging;
using Model.Concrete.Helpdesk;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Business.Services.Helpdesk;

public sealed class EwsHelpdeskMailboxClient(ILogger<EwsHelpdeskMailboxClient> logger) : IHelpdeskMailboxClient
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Messages = "http://schemas.microsoft.com/exchange/services/2006/messages";
    private static readonly XNamespace Types = "http://schemas.microsoft.com/exchange/services/2006/types";

    public async Task ProcessUnreadAsync(HelpdeskMailbox mailbox, string password,
        Func<HelpdeskInboundMail, CancellationToken, Task<bool>> handler, CancellationToken cancellationToken = default)
    {
        var endpoint = new Uri(mailbox.EwsUrl, UriKind.Absolute);
        using var httpHandler = new HttpClientHandler
        {
            Credentials = CreateCredential(mailbox.Username, password),
            PreAuthenticate = false,
            UseDefaultCredentials = false,
            AllowAutoRedirect = false
        };
        using var client = new HttpClient(httpHandler) { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AssistFlow-Helpdesk/1.0");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-AnchorMailbox", mailbox.Address);

        var items = await FindUnreadAsync(client, endpoint, mailbox.Address, cancellationToken);
        logger.LogInformation("Helpdesk EWS okunmamış mail taraması tamamlandı. MailboxId={MailboxId}, MailCount={MailCount}",
            mailbox.Id, items.Count);
        foreach (var item in items)
        {
            try
            {
                var mail = await GetMailAsync(client, endpoint, item.Id, cancellationToken);
                if (await handler(mail, cancellationToken))
                {
                    await MarkAsReadAsync(client, endpoint, item.Id, item.ChangeKey, cancellationToken);
                    logger.LogInformation("Helpdesk EWS maili işlendi ve okundu olarak işaretlendi. MailboxId={MailboxId}", mailbox.Id);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Helpdesk EWS maili işlenemedi. MailboxId={MailboxId}", mailbox.Id);
            }
        }
    }

    private static async Task<List<EwsItem>> FindUnreadAsync(HttpClient client, Uri endpoint, string address, CancellationToken ct)
    {
        var body = new XElement(Messages + "FindItem", new XAttribute("Traversal", "Shallow"),
            new XElement(Messages + "ItemShape", new XElement(Types + "BaseShape", "IdOnly")),
            new XElement(Messages + "IndexedPageItemView", new XAttribute("MaxEntriesReturned", "100"), new XAttribute("Offset", "0"), new XAttribute("BasePoint", "Beginning")),
            new XElement(Messages + "Restriction", new XElement(Types + "IsEqualTo",
                Field("message:IsRead"), new XElement(Types + "FieldURIOrConstant", new XElement(Types + "Constant", new XAttribute("Value", "false"))))),
            new XElement(Messages + "ParentFolderIds", new XElement(Types + "DistinguishedFolderId", new XAttribute("Id", "inbox"),
                new XElement(Types + "Mailbox", new XElement(Types + "EmailAddress", address)))));
        var document = await SendAsync(client, endpoint, body, ct);
        return document.Descendants(Types + "Message").Select(x => x.Element(Types + "ItemId")).Where(x => x is not null)
            .Select(x => new EwsItem((string)x!.Attribute("Id")!, (string?)x.Attribute("ChangeKey") ?? string.Empty)).ToList();
    }

    private static async Task<HelpdeskInboundMail> GetMailAsync(HttpClient client, Uri endpoint, string itemId, CancellationToken ct)
    {
        var body = new XElement(Messages + "GetItem",
            new XElement(Messages + "ItemShape", new XElement(Types + "BaseShape", "IdOnly"), new XElement(Types + "BodyType", "Best"),
                new XElement(Types + "AdditionalProperties", Field("item:Subject"), Field("item:Body"), Field("item:DateTimeReceived"),
                    Field("message:InternetMessageId"), Field("item:InternetMessageHeaders"), Field("message:From"),
                    Field("message:ToRecipients"), Field("message:CcRecipients"), Field("message:BccRecipients"))),
            new XElement(Messages + "ItemIds", new XElement(Types + "ItemId", new XAttribute("Id", itemId))));
        var document = await SendAsync(client, endpoint, body, ct);
        var message = document.Descendants(Types + "Message").FirstOrDefault()
            ?? throw new InvalidOperationException("EWS GetItem yanıtında Message bulunamadı.");
        var from = message.Element(Types + "From")?.Element(Types + "Mailbox");
        var received = DateTimeOffset.TryParse(message.Element(Types + "DateTimeReceived")?.Value, out var parsedDate) ? parsedDate : DateTimeOffset.Now;
        return new HelpdeskInboundMail
        {
            MessageId = NormalizeMessageId(message.Element(Types + "InternetMessageId")?.Value) ?? CreateFallbackMessageId(itemId),
            InReplyTo = NormalizeMessageId(Header(message, "In-Reply-To")), References = Header(message, "References"),
            FromName = from?.Element(Types + "Name")?.Value ?? string.Empty,
            FromAddress = from?.Element(Types + "EmailAddress")?.Value ?? string.Empty,
            ToRecipients = Recipients(message, "ToRecipients"), CcRecipients = Recipients(message, "CcRecipients"),
            BccRecipients = Recipients(message, "BccRecipients"),
            Subject = message.Element(Types + "Subject")?.Value ?? string.Empty,
            Body = message.Element(Types + "Body")?.Value ?? string.Empty, MailDate = received
        };
    }

    private static async Task MarkAsReadAsync(HttpClient client, Uri endpoint, string itemId, string changeKey, CancellationToken ct)
    {
        var itemIdElement = new XElement(Types + "ItemId", new XAttribute("Id", itemId));
        if (!string.IsNullOrWhiteSpace(changeKey)) itemIdElement.Add(new XAttribute("ChangeKey", changeKey));
        var body = new XElement(Messages + "UpdateItem", new XAttribute("ConflictResolution", "AutoResolve"), new XAttribute("MessageDisposition", "SaveOnly"),
            new XElement(Messages + "ItemChanges", new XElement(Types + "ItemChange", itemIdElement,
                new XElement(Types + "Updates", new XElement(Types + "SetItemField", Field("message:IsRead"),
                    new XElement(Types + "Message", new XElement(Types + "IsRead", true)))))));
        await SendAsync(client, endpoint, body, ct);
    }

    private static async Task<XDocument> SendAsync(HttpClient client, Uri endpoint, XElement operation, CancellationToken ct)
    {
        var envelope = new XDocument(new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soap", Soap), new XAttribute(XNamespace.Xmlns + "m", Messages), new XAttribute(XNamespace.Xmlns + "t", Types),
            new XElement(Soap + "Header", new XElement(Types + "RequestServerVersion", new XAttribute("Version", "Exchange2010_SP2"))),
            new XElement(Soap + "Body", operation)));
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml")
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        XDocument? document = null;
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            try { document = XDocument.Parse(responseText, LoadOptions.None); }
            catch (System.Xml.XmlException) when (!response.IsSuccessStatusCode) { }
        }
        var error = document?.Descendants().FirstOrDefault(x => x.Name.LocalName.EndsWith("ResponseMessage", StringComparison.Ordinal)
            && (string?)x.Attribute("ResponseClass") == "Error");
        var fault = document?.Descendants().FirstOrDefault(x => x.Name.LocalName == "Fault");
        if (error is not null || fault is not null)
        {
            var source = error ?? fault!;
            var code = source.DescendantsAndSelf().FirstOrDefault(x => x.Name.LocalName is "ResponseCode" or "faultcode")?.Value;
            var text = source.DescendantsAndSelf().FirstOrDefault(x => x.Name.LocalName is "MessageText" or "faultstring")?.Value;
            throw new InvalidOperationException($"EWS isteği başarısız oldu: {code} - {text}");
        }
        response.EnsureSuccessStatusCode();
        return document ?? throw new InvalidOperationException("EWS boş veya geçersiz XML yanıtı döndürdü.");
    }

    private static XElement Field(string fieldUri) => new(Types + "FieldURI", new XAttribute("FieldURI", fieldUri));
    private static string Recipients(XElement message, string elementName) => string.Join(';',
        message.Element(Types + elementName)?.Elements(Types + "Mailbox").Select(x => x.Element(Types + "EmailAddress")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x)) ?? []);
    private static string? Header(XElement message, string name) => message.Element(Types + "InternetMessageHeaders")?
        .Elements(Types + "InternetMessageHeader").FirstOrDefault(x => string.Equals((string?)x.Attribute("HeaderName"), name, StringComparison.OrdinalIgnoreCase))?.Value;
    private static string? NormalizeMessageId(string? value) { if (string.IsNullOrWhiteSpace(value)) return null; var trimmed = value.Trim(); return trimmed.StartsWith('<') ? trimmed : $"<{trimmed.Trim('<', '>')}>"; }
    private static string CreateFallbackMessageId(string itemId) => $"<ews-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(itemId))).ToLowerInvariant()}@assistflow.local>";
    private static NetworkCredential CreateCredential(string username, string password)
    {
        var separator = username.IndexOf('\\');
        return separator > 0
            ? new NetworkCredential(username[(separator + 1)..], password, username[..separator])
            : new NetworkCredential(username, password);
    }
    private sealed record EwsItem(string Id, string ChangeKey);
}
