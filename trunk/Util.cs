using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler
{
    static class Utils
    {
	    public static readonly Random Rnd = new Random();

	    static TimeCachedValue<uint> _wowPing;
        /// <summary>
        /// Returns WoW's ping, refreshed every 30 seconds.
        /// </summary>
        public static uint WoWPing
        {
            get
            {
	            return _wowPing ??
						(_wowPing = new TimeCachedValue<uint>(TimeSpan.FromSeconds(30), () => Lua.GetReturnVal<uint>("return GetNetStats()", 3)));
            }
        }

        public static bool IsItemInBag(uint entry)
        {
			return StyxWoW.Me.BagItems.Any(i => i.Entry == entry);
        }

        public static WoWItem GetIteminBag(uint entry)
        {
            return StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == entry);
        }

        public static void EquipWeapon()
        {
	        if (StyxWoW.Me.ChanneledCastingSpellId != 0)
				SpellManager.StopCasting();

            bool is2Hand = false;
            // equip right hand weapon
			uint mainHandID = AutoAnglerBot.Instance.MySettings.MainHand;
            WoWItem mainHand = StyxWoW.Me.Inventory.Equipped.MainHand;
            if (mainHand == null || (mainHand.Entry != mainHandID && IsItemInBag(mainHandID)))
            {
				var weapon = StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == AutoAnglerBot.Instance.MySettings.MainHand);
                is2Hand = weapon.ItemInfo.InventoryType == InventoryType.TwoHandWeapon || weapon.ItemInfo.InventoryType == InventoryType.Ranged;
				EquipItemByID(AutoAnglerBot.Instance.MySettings.MainHand);
            }

            // equip left hand weapon
			uint offhandID = AutoAnglerBot.Instance.MySettings.OffHand;
            WoWItem offhand = StyxWoW.Me.Inventory.Equipped.OffHand;

            if ((!is2Hand && offhandID > 0 &&
                 (offhand == null || (offhand.Entry != offhandID && IsItemInBag(offhandID)))))
            {
				EquipItemByID(AutoAnglerBot.Instance.MySettings.OffHand);
            }
        }

		public static bool EquipMainHat()
		{
			if (StyxWoW.Me.Combat)
				return false;

			if (StyxWoW.Me.ChanneledCastingSpellId != 0)
				SpellManager.StopCasting();

			WoWItem hat = StyxWoW.Me.Inventory.Equipped.Head;

			// if not wearing a fishing hat then return
			if (hat != null && !FishingHatIds.Contains(hat.Entry))
			{
				return false;
			}

			// try to find a hat to wear automatically
			if (AutoAnglerSettings.Instance.Hat == 0)
			{
				var bestHat = StyxWoW.Me.BagItems.Where(i => i != null && i.IsValid && i.ItemInfo.EquipSlot == InventoryType.Head)
					.OrderByDescending(i => i.ItemInfo.Level)
					.FirstOrDefault();
				if (bestHat != null)
				{
					AutoAnglerSettings.Instance.Hat = bestHat.Entry;
					AutoAnglerSettings.Instance.Save();
				}
			}
			
			// if regular hat is already equipped or not in bags then return
			if ((hat != null && hat.Entry == AutoAnglerSettings.Instance.Hat) || !IsItemInBag(AutoAnglerSettings.Instance.Hat))
			{
				return false;
			}

			EquipItemByID(AutoAnglerSettings.Instance.Hat);
			return true;
		}

        public static void UseItemByID(int id)
        {
            Lua.DoString("UseItemByName(\"" + id + "\")");
        }

        public static void EquipItemByName(String name)
        {
            Lua.DoString("EquipItemByName (\"" + name + "\")");
        }

        public static void EquipItemByID(uint id)
        {
            Lua.DoString("EquipItemByName ({0})", id);
        }

        public static void BlacklistPool(WoWGameObject pool, TimeSpan time, string reason)
        {
            Blacklist.Add(pool.Guid, time);
			AutoAnglerBot.Instance.Log("Blacklisting {0} for {1} Reason: {2}", pool.Name, time, reason);
            BotPoi.Current = new BotPoi(PoiType.None);
        }

	    internal static readonly uint[] FishingHatIds =
	    {
			33820, // Weather-Beaten Fishing Hat
			88710 , // Nat's Hat
		};

    }
}