using System;
using System.Collections.Generic;

namespace RyLib
{
    public static class KillLog
    {
        private sealed class Request
        {
            public string Owner;
            public Func<bool> Visible;
        }

        private static readonly List<Request> Requests = new List<Request>();

        public static bool EnableActions(string owner, Func<bool> visible = null)
        {
            if (!Owners.Valid(owner, "KillLog.EnableActions")) return false;

            for (int i = 0; i < Requests.Count; i++)
            {
                if (!Owners.Same(Requests[i].Owner, owner)) continue;

                Log.Warn(owner + " already turned on the kill log actions, so the second call was ignored.");
                return false;
            }

            Request request = new Request();
            request.Owner = owner;
            request.Visible = visible;
            Requests.Add(request);

            Log.Info(owner + " turned on the kill log actions.");
            return true;
        }

        internal static bool Requested
        {
            get { return Requests.Count > 0; }
        }

        internal static bool ActionsOn
        {
            get
            {
                for (int i = 0; i < Requests.Count; i++)
                {
                    if (Owners.Ask(Requests[i].Owner, "kill log actions visibility", Requests[i].Visible, true)) return true;
                }

                return false;
            }
        }
    }
}
