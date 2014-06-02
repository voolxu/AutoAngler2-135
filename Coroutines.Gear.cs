using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Styx;
using Styx.CommonBot.Coroutines;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler
{
	static partial class Coroutines
	{
		public async static Task<bool> EquipPole()
		{
			var mainHand = StyxWoW.Me.Inventory.Equipped.MainHand;
			// equip fishing pole if there's none equipped
			if (mainHand != null && mainHand.ItemInfo.WeaponClass == WoWItemWeaponClass.FishingPole)
				return false;

			WoWItem pole = Me.BagItems
				.Where(i => i != null && i.IsValid 
					&& i.ItemInfo.WeaponClass == WoWItemWeaponClass.FishingPole)
				.OrderByDescending(i => i.ItemInfo.Level)
				.FirstOrDefault();

			if (pole == null)
				return false;

			return await EquipItem(pole, WoWInventorySlot.MainHand);
		}

		public async static Task<bool> EquipItem(WoWItem item, WoWInventorySlot slot)
		{
			if (item == null || !item.IsValid)
				return false;

			AutoAnglerBot.Log("Equipping {0}", item.SafeName);
			Lua.DoString("ClearCursor()");
			item.PickUp();
			Lua.DoString(string.Format("PickupInventoryItem({0})", (int)slot + 1));
			await CommonCoroutines.SleepForLagDuration();
			if (!await Coroutine.Wait(4000, () => !item.IsDisabled))
				return false;
			return true;
		}
	}
}
