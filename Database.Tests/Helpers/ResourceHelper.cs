using System.Reflection;

namespace Database.Tests.Helpers;

public class ResourceHelper
{
    public static string GetContent(string resourceName)
    {
        return GetContent(Assembly.GetExecutingAssembly().GetType(), resourceName);
    }

    public static string GetContent(Type type, string resourceName)
    {
        using var resourceStream = type.Assembly.GetManifestResourceStream(resourceName);
        if (resourceStream == null)
        {
            throw new Exception(resourceName);
        }
        using var reader = new StreamReader(resourceStream);
        var resourceContent = reader.ReadToEnd();

        return resourceContent;
    }
}