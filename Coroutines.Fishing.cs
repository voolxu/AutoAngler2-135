using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using JetBrains.Annotations;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler
{
	partial class Coroutines
	{
		// the radius of any fishing pool where scale is equal to 1
		private const float NormalFishingPoolRadius = 4f;
		// The longest posible cast from player to bobber rounded to nearest hundredths place
		private const float MaxCastDistance2D = 20f;
		// The shortest posible cast from player to bobber rounded to nearest hundredths place
		private const float MinCastDistance2D = 10f;
		// The distance from pool where most cast will land inside pool
		private const float OptimumPoolDistance2D = 15f;
		// the minimum distance inside of cast limits..
		// example: Ignore pool if no spot where player can stand at is found 
		// within (LongestCastDistance2D + PoolRadius - PoolDistTolerance)
		private const float PoolDistTolerance = 3f;

		public static readonly WaitTimer LineRecastTimer = new WaitTimer(TimeSpan.FromSeconds(2));

		private static readonly Stopwatch TimeAtPoolTimer = new Stopwatch();
		private static int _castCounter;
		private static ulong _lastVisitedPoolGuid;
		public static int DelayAfterBobberTriggerMs
		{
			get
			{
				return (Utility.Rnd.Next(1, 100) < 85)
					? Utility.Rnd.Next(300, 700)     // 'normal' delay
					: Utility.Rnd.Next(600, 2400);   // 'outlier' delay
			}
		}

		public static async Task<bool> DoFishing()
		{
			if (AutoAnglerBot.Instance.Profile.FishAtHotspot && !Navigator.AtLocation(AutoAnglerBot.Instance.Profile.CurrentPoint))
				return false;

			if (AutoAnglerSettings.Instance.Poolfishing
				&& BotPoi.Current.Type != PoiType.Harvest)
			{
				return false;
			}

			if (await CheckLootFrame())
				return true;

			// refresh water walking if needed
			if (!Me.Mounted && WaterWalking.CanCast 
				&& (!WaterWalking.IsActive || StyxWoW.Me.IsSwimming)
				&& await WaterWalking.Cast())
			{
				return true;
			}

			if (AutoAnglerSettings.Instance.Poolfishing)
			{
				var pool = BotPoi.Current.AsObject as WoWGameObject;
				if (pool == null || !pool.IsValid)
				{
					BotPoi.Clear();
					return false;
				}

				if (await MoveToPool(pool))
					return true;
			}

			if (await EquipPole())
				return true;

			if (await EquipHat())
				return true;

			if (await Applylure())
				return true;

			if (!AutoAnglerSettings.Instance.Poolfishing 
				&& AutoAnglerBot.Instance.ShouldFaceWaterNow)
			{
				AutoAnglerBot.Instance.ShouldFaceWaterNow = false;
				if (await FaceWater())
					return true;
			}

			// Checks if we got a bite and recasts if needed.
			if (await CheckFishLine())
				return true;

			return false;
		}

		private async static Task<bool> CheckFishLine()
		{
			if (AutoAnglerSettings.Instance.Poolfishing && BotPoi.Current.Type != PoiType.Harvest)
				return false;

			if (Me.Mounted && await CommonCoroutines.Dismount("Fishing"))
				return true;

			if (!await Coroutine.Wait(10000, () => !Me.IsFalling))
			{
				AutoAnglerBot.Log("Falling for 10 seconds; I don't think this will end good.");
				return false;
			}

			if (Me.IsMoving)
			{
				WoWMovement.MoveStop();
				if (!await Coroutine.Wait(4000, () => !Me.IsMoving))
					return false;
			}

			var pool = BotPoi.Current.AsObject as WoWGameObject;
			if (AutoAnglerSettings.Instance.Poolfishing)
			{
				if (await PoolSafetyChecks(pool))
					return true;

				// face pool if not facing it already.
				if (!Me.IsSafelyFacing(pool, 5))
				{
					LineRecastTimer.Reset();
					Me.SetFacing(pool.Location);
					// SetFacing doesn't really update my angle in game.. still tries to fish using prev angle. so I need to move to update in-game angle
					WoWMovement.Move(WoWMovement.MovementDirection.ForwardBackMovement);
					WoWMovement.MoveStop(WoWMovement.MovementDirection.ForwardBackMovement);
					await CommonCoroutines.SleepForLagDuration();
					return true;
				}
			}

			if (Me.IsCasting)
			{
				WoWGameObject bobber = ObjectManager.GetObjectsOfType<WoWGameObject>()
						.FirstOrDefault(o => o.IsValid && o.SubType == WoWGameObjectType.FishingNode &&
											 o.CreatedByGuid == Me.Guid);
				
				if (bobber != null)
				{
					// recast line if it's not close enough to pool
					if (AutoAnglerSettings.Instance.Poolfishing
						&& bobber.Location.Distance2D(pool.Location) >= GetPoolRadius(pool)
						&& await CastLine())
					{
						return true;
					}
					// else lets see if there's a bite
					if (((WoWFishingBobber)bobber.SubObj).IsBobbing)
					{
						if (await Coroutine.Wait(DelayAfterBobberTriggerMs, () => !bobber.IsValid))
							return false;
						_castCounter = 0;
						bobber.SubObj.Use();
						LootTimer.Reset();
						return true;
					}
				}
				return false;
			}

			return await CastLine();
		}

		[ContractAnnotation("null => true")]
		private async static Task<bool> PoolSafetyChecks(WoWGameObject pool)
		{
			if (pool == null || !pool.IsValid)
			{
				BotPoi.Clear();
				return true;
			}

			if (pool.Guid != _lastVisitedPoolGuid)
			{
				_lastVisitedPoolGuid = pool.Guid;
				TimeAtPoolTimer.Restart();
			}

			// safety check. if spending more than 5 mins at pool than black list it.
			if (TimeAtPoolTimer.ElapsedMilliseconds >= AutoAnglerSettings.Instance.MaxTimeAtPool * 60000)
			{
				Utility.BlacklistPool(pool, TimeSpan.FromMinutes(10), "Spend too much time at pool");
				return true;
			}

			// move to another spot if we have too many failed casts
			if (_castCounter >= AutoAnglerSettings.Instance.MaxFailedCasts)
			{
				AutoAnglerBot.Log("Moving to a new fishing location since we have {0} failed casts",
										_castCounter);
				_castCounter = 0;
				RemovePointAtTop(pool);
				return true;
			}
			return false;
		}

		private async static Task<bool> CastLine()
		{
			if (!LineRecastTimer.IsFinished)
				return false;
			LineRecastTimer.Reset();
			_castCounter++;
			SpellManager.Cast("Fishing");
			await CommonCoroutines.SleepForLagDuration();
			StyxWoW.ResetAfk();
			InactivityDetector.Reset();
			return true;
		}

		static float GetPoolRadius(WoWGameObject pool)
		{
			return NormalFishingPoolRadius*pool.Scale;
		}

	}
}
