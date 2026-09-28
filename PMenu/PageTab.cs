using HoldfastGame;

namespace RyLib
{
    internal sealed class PageTab : PMenuPanelTabBase
    {
        internal Page Page;

        public override void _InitializeOnMap(RoundGameDetails roundGameDetails)
        {
            if (Page != null && Page.Map != null) Page.Map.Invalidate();
        }

        public override void _Awake()
        {
        }

        public override void _ResetObject()
        {
            if (Page != null && Page.Map != null) Page.Map.Invalidate();
        }

        public override void _OnDestroy()
        {
        }

        public override void _ToggleShowing(bool show)
        {
            if (Page != null && Page.Map != null) Page.Map.Show(show);
            if (show) Controls.RefreshVisible();
        }
    }
}
