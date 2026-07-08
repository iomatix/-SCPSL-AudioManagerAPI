namespace AudioManagerAPI.Logger
{
    using Log = LabApi.Features.Console.Logger;
    public static class ApiLogger
    {
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Debug(string message) => Log.Debug(message);

        public static void Warn(string message) => Log.Warn(message);

        public static void Info(string message) => Log.Info(message);
    }

}
