using System.Collections.Generic;
using Styx;
using Styx.Logic.Combat;
using Styx.Logic.POI;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using System.Linq;

namespace HighVoltz.Composites
{
    public class CombatAction : Action
    {
        protected override RunStatus Run(object context)
        {
            if (BotPoi.Current != null && BotPoi.Current.Type == PoiType.Harvest)
            {
                MoveToPoolAction.MoveToPoolSW.Reset();
                MoveToPoolAction.MoveToPoolSW.Start();
            }
            Utils.EquipWeapon();
            return RunStatus.Failure;
        }
    }
}