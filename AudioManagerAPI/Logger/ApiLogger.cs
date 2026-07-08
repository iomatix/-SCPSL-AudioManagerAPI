namespace AudioManagerAPI.Logger
{
    using Log = LabApi.Features.Console.Logger;
    public static class ApiLogger
    {
        private static string _prefix = "[AudioManagerAPI]";
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Debug(string message) => Log.Debug($"{_prefix} {message}");

        public static void Warn(string message) => Log.Warn($"{_prefix} {message}");

        public static void Error(string message) => Log.Error($"{_prefix} {message}");

        public static void Info(string message) => Log.Info($"{_prefix} {message}");
    }

}
