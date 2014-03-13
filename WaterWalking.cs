using System;
using System.Diagnostics;
using System.Linq;
using Styx;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler
{
	public class WaterWalking
	{
		private static readonly Stopwatch RecastSW = new Stopwatch();

		private static string[] _waterWalkingAbilities = { "Levitate", "Water Walking", "Path of Frost" };

		public static bool CanCast
		{
			get
			{
				return AutoAnglerBot.Instance.MySettings.UseWaterWalking &&
					   (SpellManager.HasSpell("Levitate") || // priest levitate
						SpellManager.HasSpell("Water Walking") || // shaman water walking
						SpellManager.HasSpell("Path of Frost") || // Dk Path of frost
						SpellManager.HasSpell("Soulburn") || // Affliction Warlock
						StyxWoW.Me.HasAura("Still Water") || // hunter with water strider pet.
						Utils.IsItemInBag(ElixirOfWaterWalkingId) || //isItemInBag(8827);
						Utils.IsItemInBag(FishingRaftId)); // Anglers Fishing Raft
			}
		}

		private static readonly TimeSpan MinimumWaterWalkingTimeLeft = TimeSpan.FromSeconds(20);

		public static bool IsActive
		{
			get
			{
				// DKs have 2 Path of Frost auras. only one can be stored in WoWAuras at any time. 
				return IsAuraActive("Levitate", MinimumWaterWalkingTimeLeft)
						|| IsAuraActive("Anglers Fishing Raft", MinimumWaterWalkingTimeLeft)
						|| IsAuraActive("Water Walking", MinimumWaterWalkingTimeLeft)
						|| IsAuraActive("Unending Breath", MinimumWaterWalkingTimeLeft)
						|| IsAuraActive("Bipsi's Bobbing Berg", MinimumWaterWalkingTimeLeft)
						|| IsAuraActive("Path of Frost")
						|| IsAuraActive("Surface Trot");
			}
		}

		static bool IsAuraActive(string auraName, TimeSpan? minTimeLeft = null)
		{
			var aura = StyxWoW.Me.GetAuraByName(auraName);
			return aura != null && (minTimeLeft == null || aura.TimeLeft >= minTimeLeft);
		}

		public static bool Cast()
		{
			WoWItem fishingRaft;
			WoWItem waterPot;

			bool casted = false;
			if (!IsActive)
			{
				if (RecastSW.IsRunning && RecastSW.ElapsedMilliseconds < 5000)
					return false;
				RecastSW.Reset();
				RecastSW.Start();
				int waterwalkingSpellID = 0;
				switch (StyxWoW.Me.Class)
				{
					case WoWClass.Priest:
						waterwalkingSpellID = LevitateSpellId;
						break;
					case WoWClass.Shaman:
						waterwalkingSpellID = 546;
						break;
					case WoWClass.DeathKnight:
						waterwalkingSpellID = PathOfFrostSpellId;
						break;
					case WoWClass.Warlock:
						waterwalkingSpellID = UnendingBreathSpellId;
						break;
					case WoWClass.Hunter:
						// cast Surface Trot if Water Strider pet is active.
						if (StyxWoW.Me.HasAura("Still Water"))
							waterwalkingSpellID = SurfaceTrotSpellId;
						break;
				}
				if (waterwalkingSpellID != 0 && (SpellManager.CanCast(waterwalkingSpellID) || StyxWoW.Me.HasAura("Still Water")))
				{
					if (StyxWoW.Me.Class == WoWClass.Warlock)
						SpellManager.Cast(SoulburnSpellId); //cast Soulburn
					// use lua to cast spells because SpellManager.Cast can't handle pet spells.
					Lua.DoString("CastSpellByID ({0})", waterwalkingSpellID);
					casted = true;
				}
				else if ((waterPot = Utils.GetIteminBag(ElixirOfWaterWalkingId)) != null && waterPot.Use())
				{
					casted = true;
				}
				else if ((fishingRaft = Utils.GetIteminBag(FishingRaftId)) != null && fishingRaft.Use())
				{
					casted = true;
				}
				else if ((fishingRaft = Utils.GetIteminBag(BipsisBobbingBergId)) != null && fishingRaft.Use())
				{
					casted = true;
				}
			}
			if (StyxWoW.Me.IsSwimming)
			{
				StyxWoW.ResetAfk();
				WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
			}
			return casted;
		}

		#region Static Members

		private const int BipsisBobbingBergId = 107950;
		private const int FishingRaftId = 85500;
		private const int ElixirOfWaterWalkingId = 8827;
		private const int SoulburnSpellId = 74434;
		private const int SurfaceTrotSpellId = 126311;
		private const int UnendingBreathSpellId = 5697;
		private const int PathOfFrostSpellId = 3714;
		private const int LevitateSpellId = 1706;

		#endregion

	}
}