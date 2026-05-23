using System.Diagnostics;

namespace Utils;

public static class Logger
{
    public static void Log(Source src, Level lvl, string msg)
    {
        switch (src)
        {
            case Source.Server:
                Console.WriteLine($">>> [{DateTime.Now}][{lvl}] (thread #{Environment.CurrentManagedThreadId}): " + msg);
                break;
            case Source.Client:
                Debug.WriteLine($">>> [{DateTime.Now}][{lvl}] (thread #{Environment.CurrentManagedThreadId}): " + msg);
                break;
        }
    }
}

public enum Source
{
    Server,
    Client,
}

public enum Level
{
    TRACE, DEBUG, INFO, WARN, ERROR
}
