namespace DivineWorld.Simulation.UI
{
    public enum HudWindowId
    {
        None = 0,
        Observer = 1,
        History = 2,
        Politics = 3
    }

    /// <summary>
    /// Exclusive open/close state for large function windows.
    /// Persistent chrome (clock / time / tabs) is not stored here.
    /// </summary>
    public sealed class HudWindowState
    {
        public HudWindowId OpenWindow { get; private set; }

        public HudWindowState(HudWindowId initial = HudWindowId.Observer)
        {
            OpenWindow = initial;
        }

        public bool IsOpen(HudWindowId id)
        {
            return id != HudWindowId.None && OpenWindow == id;
        }

        public void Open(HudWindowId id)
        {
            if (id == HudWindowId.None)
            {
                Close();
                return;
            }

            OpenWindow = id;
        }

        public void Close()
        {
            OpenWindow = HudWindowId.None;
        }

        /// <summary>
        /// Clicking the active tab hides it. Clicking another tab switches to that window.
        /// </summary>
        public void Toggle(HudWindowId id)
        {
            if (id == HudWindowId.None)
            {
                return;
            }

            OpenWindow = OpenWindow == id ? HudWindowId.None : id;
        }
    }
}
