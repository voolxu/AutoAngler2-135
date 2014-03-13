using System.Linq;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Frames;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler.Composites
{
	public class MailAction : Action
	{
		protected override RunStatus Run(object context)
		{
			WoWPoint mboxLoc = BotPoi.Current.Location;
			WoWGameObject mailbox = ObjectManager.GetObjectsOfType<WoWGameObject>().
				FirstOrDefault(
					m => m.SubType == WoWGameObjectType.Mailbox &&
						m.Location.Distance(mboxLoc) < 10);
			WoWPoint loc = mailbox != null ? mailbox.Location : mboxLoc;

			if (StyxWoW.Me.Location.Distance(loc) > 4)
			{
				if (AutoAnglerBot.Instance.MySettings.Fly)
					Flightor.MoveTo(WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, loc, 3));
				else
				{
					if (!StyxWoW.Me.Mounted && Mount.ShouldMount(loc) && Mount.CanMount())
						Mount.MountUp(() => loc);
					Navigator.MoveTo(WoWMathHelper.CalculatePointFrom(StyxWoW.Me.Location, loc, 4));
				}
			}
			else
			{
				if (MailFrame.Instance == null || !MailFrame.Instance.IsVisible)
				{
					if (mailbox == null)
					{
						AutoAnglerBot.Instance.Log("No Mailbox found at location {0}. Vendoring instead", loc);
						Vendor ven = ProfileManager.CurrentOuterProfile.VendorManager.GetClosestVendor();
						BotPoi.Current = ven != null ? new BotPoi(ven, PoiType.Repair) : new BotPoi(PoiType.InnKeeper);
						return RunStatus.Failure;
					}
					mailbox.Interact();
				}
				else
				{
					MailFrame.Instance.SendMailWithManyAttachments(
						CharacterSettings.Instance.MailRecipient,
						0,
						StyxWoW.Me.BagItems.Where(
							i => !i.IsSoulbound && !i.IsConjured && ForceMailManager.Contains(i.Entry)).ToArray());

					Vendor ven = ProfileManager.CurrentOuterProfile.VendorManager.GetClosestVendor();
					BotPoi.Current = ven != null ? new BotPoi(ven, PoiType.Repair) : new BotPoi(PoiType.None);
				}
			}
			return RunStatus.Success;
		}
	}
}