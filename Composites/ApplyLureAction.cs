using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Styx;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler.Composites
{
    public class ApplyLureAction : Action
    {

        private readonly Stopwatch _lureRecastSW = new Stopwatch();

        protected override RunStatus Run(object context)
        {
            if (!StyxWoW.Me.IsCasting && !IsLureOnPole && Applylure())
                return RunStatus.Success;
            return RunStatus.Failure;
        }

        // does nothing if no lures are in bag
        private bool Applylure()
        {
            if (_lureRecastSW.IsRunning && _lureRecastSW.ElapsedMilliseconds < 10000)
                return false;
            _lureRecastSW.Reset();
            _lureRecastSW.Start();
			if (StyxWoW.Me.Inventory.Equipped.MainHand != null &&
				StyxWoW.Me.Inventory.Equipped.MainHand.ItemInfo.WeaponClass != WoWItemWeaponClass.FishingPole)
                return false;

			// Ancient Pandaren Fishing Charm
			WoWItem ancientPandarenFishingCharm = StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == AncientPandarenFishingCharmItemId);
			if (ancientPandarenFishingCharm != null && !StyxWoW.Me.HasAura(AncientPandarenFishingCharmAuraId))
            {
				AutoAnglerBot.Instance.Log("Appling Ancient Pandaren Fishing Charm lure");
				Utils.UseItemByID(AncientPandarenFishingCharmItemId);
                return true;
            }

            // Fishing Hats
	        WoWItem head = StyxWoW.Me.Inventory.Equipped.Head;// StyxWoW.Me.Inventory.GetItemBySlot((uint)WoWEquipSlot.Head);
	        if (head == null || !Utils.FishingHatIds.Contains(head.Entry))
	        {
		        var fishingHat = StyxWoW.Me.BagItems.FirstOrDefault(i =>i != null && i.IsValid && Utils.FishingHatIds.Contains(i.Entry));
		        if (fishingHat != null)
		        {
			        if (head != null)
			        {
				        AutoAnglerSettings.Instance.Hat = head.Entry;
						AutoAnglerSettings.Instance.Save();
				        AutoAnglerBot.Instance.Log("Replacing {0} with {1}", head.Name, fishingHat.Name);
			        }
			        else
			        {
				        AutoAnglerBot.Instance.Log("Equipping {0}", fishingHat.Name);
			        }
					fishingHat.UseContainerItem();
		        }
	        }
			if (head != null && Utils.FishingHatIds.Contains(head.Entry))
            {
				AutoAnglerBot.Instance.Log("Appling Fishing Hat lure to fishing pole");
                Utils.UseItemByID((int)head.Entry);
                return true;
            }

            foreach (var kv in Lures)
            {
                WoWItem lureInBag = Utils.GetIteminBag(kv.Key);
                if (lureInBag != null && lureInBag.Use())
                {
					AutoAnglerBot.Instance.Log("Appling {0} to fishing pole", kv.Value);
                    return true;
                }
            }
            return false;
        }

		public static bool IsLureOnPole
		{
			get
			{
				bool useHatLure = false;

				var head = StyxWoW.Me.Inventory.GetItemBySlot((uint)WoWEquipSlot.Head);
				if (head != null && Utils.FishingHatIds.Contains(head.Entry))
					useHatLure = true;

				var ancientPandarenFishingCharm = StyxWoW.Me.BagItems.FirstOrDefault(r => r.Entry == AncientPandarenFishingCharmItemId);
				if (AutoAnglerBot.Instance.MySettings.Poolfishing && ancientPandarenFishingCharm != null && !StyxWoW.Me.HasAura(AncientPandarenFishingCharmAuraId))
				{
					return false;
				}

				//if poolfishing, dont need lure say we have one
				if (AutoAnglerBot.Instance.MySettings.Poolfishing && !useHatLure && !AutoAnglerBot.FishAtHotspot)
					return true;

				var ret = Lua.GetReturnValues("return GetWeaponEnchantInfo()");
				return ret != null && ret.Count > 0 && ret[0] == "1";
			}
		}

		#region Static members

		private static readonly Dictionary<uint, string> Lures = new Dictionary<uint, string>
																{
																	{68049, "Heat-Treated Spinning Lure"},
																	{62673, "Feathered Lure"},
																	{34861, "Sharpened Fish Hook"},
																	{46006, "Glow Worm"},
																	{6533, "Aquadynamic Fish Attractor"},
																	{7307, "Flesh Eating Worm"},
																	{6532, "Bright Baubles"},
																	{6530, "Nightcrawlers"},
																	{6811, "Aquadynamic Fish Lens"},
																	{6529, "Shiny Bauble"},
																	{67404, "Glass Fishing Bobber"},
																};


	    private const int AncientPandarenFishingCharmItemId = 85973;

		private const int AncientPandarenFishingCharmAuraId = 125167;	

	    #endregion
    }
}