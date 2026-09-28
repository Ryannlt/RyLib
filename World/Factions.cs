using HoldfastGame;
using UnityEngine;

namespace RyLib
{
    public static class Factions
    {
        public static readonly Color British = new Color(0.9f, 0.12f, 0.1f);
        public static readonly Color French = new Color(0.15f, 0.35f, 1f);
        public static readonly Color Prussian = new Color(0.85f, 0.87f, 0.9f);
        public static readonly Color Russian = new Color(0.05f, 0.45f, 0.15f);
        public static readonly Color Austrian = new Color(1f, 0.85f, 0.1f);
        public static readonly Color Italian = new Color(0.55f, 0.95f, 0.45f);
        public static readonly Color Spanish = new Color(1f, 0.55f, 0.1f);
        public static readonly Color Privateer = new Color(0.04f, 0.04f, 0.05f);
        public static readonly Color Allied = new Color(0.5f, 0.8f, 1f);
        public static readonly Color Central = new Color(1f, 0.7f, 0.35f);
        public static readonly Color Neutral = new Color(0.75f, 0.75f, 0.78f);

        public static Color Colour(FactionCountry faction)
        {
            switch (faction)
            {
                case FactionCountry.British:
                case FactionCountry.ARBritish:
                    return British;
                case FactionCountry.French:
                case FactionCountry.ARAmerican:
                    return French;
                case FactionCountry.Prussian: return Prussian;
                case FactionCountry.Russian: return Russian;
                case FactionCountry.Austrian: return Austrian;
                case FactionCountry.Italian: return Italian;
                case FactionCountry.Spanish: return Spanish;
                case FactionCountry.Privateer: return Privateer;
                case FactionCountry.Allied: return Allied;
                case FactionCountry.Central: return Central;
                default: return Neutral;
            }
        }

        public static string Name(FactionCountry faction)
        {
            switch (faction)
            {
                case FactionCountry.ARBritish: return "AR British";
                case FactionCountry.ARAmerican: return "AR American";
                case FactionCountry.None: return "No faction";
                default: return faction.ToString();
            }
        }

        public static string ClassName(PlayerClass type)
        {
            switch (type)
            {
                case PlayerClass.ArmyLineInfantry: return "Line Infantry";
                case PlayerClass.ArmyInfantryOfficer: return "Officer";
                case PlayerClass.FlagBearer: return "Flag Bearer";
                case PlayerClass.LightInfantry: return "Light Infantry";
                case PlayerClass.NavalMarine: return "Marine";
                case PlayerClass.NavalCaptain: return "Captain";
                case PlayerClass.NavalSailor: return "Sailor";
                case PlayerClass.NavalSailor2: return "Midshipman";
                case PlayerClass.CoastGuard: return "Coast Guard";
                default: return type.ToString();
            }
        }
    }
}
