using UnityEngine;

namespace RyLib
{
    internal sealed class Driver : MonoBehaviour
    {
        internal RyLibPlugin Owner;

        internal static Driver Attach(RyLibPlugin owner)
        {
            GameObject host = new GameObject("RyLib_Driver");
            DontDestroyOnLoad(host);

            Driver driver = host.AddComponent<Driver>();
            driver.Owner = owner;
            return driver;
        }

        private void Update()
        {
            if (!ReferenceEquals(Owner, null)) Owner.Tick();
        }
    }
}
