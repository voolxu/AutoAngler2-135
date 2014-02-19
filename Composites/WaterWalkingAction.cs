﻿using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler.Composites
{
    public class WaterWalkingAction : Action
    {
        private readonly LocalPlayer _me = StyxWoW.Me;

        protected override RunStatus Run(object context)
        {
            // refresh water walking if needed
            if (!_me.Mounted && WaterWalking.CanCast && (!WaterWalking.IsActive || _me.IsSwimming))
            {
                WaterWalking.Cast();
                return RunStatus.Success;
            }
            return RunStatus.Failure;
        }
    }
}