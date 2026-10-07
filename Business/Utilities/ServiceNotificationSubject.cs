namespace Business.Utilities;

public static class ServiceNotificationSubject
{
    public static string WithSubscriberName(string subject, string? subscriberName)
    {
        // Customer names may contain pasted line breaks; mail subjects must stay on one line.
        var name = string.Join(" ", (subscriberName ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return name.Length == 0 ? subject : $"{subject} - {name}";
    }
}
