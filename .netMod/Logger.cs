using REFrameworkNET;

namespace RE3DotNet_CC
{
    public static class Logger
    {
        public static bool IsEnabled { get; private set; } = true;

        public static void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        public static void LogInfo(string message)
        {
            if (IsEnabled)
                API.LogInfo(message);
        }

        public static void LogWarning(string message)
        {
            if (IsEnabled)
                API.LogWarning(message);
        }

        public static void LogError(string message)
        {
            if (IsEnabled)
                API.LogError(message);
        }
    }
}


