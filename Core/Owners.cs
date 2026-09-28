using System;

namespace RyLib
{
    internal static class Owners
    {
        public static bool Valid(string owner, string call)
        {
            if (!string.IsNullOrEmpty(owner)) return true;

            Log.Warn(call + " was called without an owner GUID, so it was ignored. Pass your plugin's GUID.");
            return false;
        }

        public static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        public static void Run(string owner, string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                Log.Error(owner + ": " + what + " threw " + error.GetType().Name + ": " + error.Message);
            }
        }

        public static string Say(string owner, string what, Func<string> text, string fallback)
        {
            if (text == null) return fallback;

            try
            {
                return text() ?? fallback;
            }
            catch (Exception error)
            {
                Log.Error(owner + ": " + what + " threw " + error.GetType().Name + ": " + error.Message);
                return fallback;
            }
        }

        public static bool Ask(string owner, string what, Func<bool> question, bool fallback)
        {
            if (question == null) return fallback;

            try
            {
                return question();
            }
            catch (Exception error)
            {
                Log.Error(owner + ": " + what + " threw " + error.GetType().Name + ": " + error.Message);
                return fallback;
            }
        }
    }
}
