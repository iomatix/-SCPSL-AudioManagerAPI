namespace AudioManagerAPI
{
    using Log = LabApi.Features.Console.Logger;
    public static class DebugLogger
    {
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Debug(string message) => Log.Debug(message);

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Warn(string message) => Log.Warn(message);


        [System.Diagnostics.Conditional("DEBUG")]
        public static void Info(string message) => Log.Info(message);
    }

}
