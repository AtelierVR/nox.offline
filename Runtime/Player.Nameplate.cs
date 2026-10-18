using System.Collections.Generic;
using Nox.CCK.Entities;
using Nox.CCK.Nameplate;
using Nox.Nameplate;
using Keys = Nox.CCK.Nameplate.Constants;

namespace Nox.Offline.Runtime {
	/// <summary>
	/// Nameplate binding of the offline player.
	/// <para>
	/// The plate belongs to the active controller (<see cref="INameplateHolder"/>, a handle the
	/// session feeds with the local profile); the player feeds it its voice level, driving the alpha
	/// of the plate's voice image.
	/// </para>
	/// </summary>
	public partial class Player {
		#region INameplateEntity

		/// <inheritdoc />
		public bool NameplateVisible {
			get => Data.Get("nameplate", EntityConstants.DefaultNameplateVisible);
			set => Data.Set("nameplate", value);
		}

		/// <inheritdoc />
		public bool HealthbarVisible {
			get => Data.Get("healthbar", EntityConstants.DefaultHealthbarVisible);
			set => Data.Set("healthbar", value);
		}

		#endregion

		/// <summary>
		/// Pushes the entity health bar onto a plate (<c>Keys.HEARTS_*</c>): hidden unless
		/// <see cref="HealthbarVisible"/> and a <c>heart</c> value is stored in the entity data —
		/// the data container is the source of truth, the plate only displays it.
		/// </summary>
		internal void PushHealthbar(INameplate plate) {
			if (!plate.IsAlive())
				return;

			if (!HealthbarVisible || !Data.Has("heart")) {
				plate.Set(Keys.HEARTS_VISIBLE, false);
				return;
			}

			plate.Set(Keys.HEARTS_MAX, Data.Get("heart.max", 100f));
			plate.Set(Keys.HEARTS_VALUE, Data.Get("heart", 0f));

			// Guards the value/max push above, which makes the bar visible: re-applied last.
			plate.Set(Keys.HEARTS_VISIBLE, true);
		}

		/// <summary>
		/// Pushes the colour of the entity team onto a plate (<c>Keys.COLOR</c>): the colour of the
		/// display name and of the voice image. Cleared back to the default (white) without a team.
		/// </summary>
		internal void PushTeam(INameplate plate) {
			if (!plate.IsAlive())
				return;

			var team = Team;
			plate.Set(Keys.COLOR, team != null ? (object)team.Color : null);
		}

		/// <summary>
		/// Pushes the level of the captured audio (<see cref="Audio"/>, the microphone bound by
		/// <see cref="BindVoiceEvents"/>) onto the controller's plate (<c>Keys.VOICE</c>), driving the alpha
		/// of the plate's voice image, then the voice status of the local player (<c>Keys.STATUS</c>:
		/// muted while self-muted, deafened while the voice channel is muted). Called every frame by
		/// <see cref="OnUpdate"/>: the plate ignores an unchanged value.
		/// <para>
		/// Offline there is no listener-mute relation to display (the player is alone), so these are
		/// its own announced states, exactly what the relay feeds for a remote player.
		/// </para>
		/// </summary>
		internal void UpdateNameplate() {
			if (Main.ControllerAPI?.Current is not INameplateHolder holder)
				return;

			var plate = holder.Nameplate;
			if (!plate.IsAlive())
				return;

			plate.Set(Keys.VOICE, Audio?.Level ?? 0f);

			var status = new Dictionary<string, string>();
			if (IsSelfMuted)
				status[NameplateExtensions.StatusIcon("mute")] = "mute";
			if (IsDeafened)
				status[NameplateExtensions.StatusIcon("deaf")] = "deaf";

			plate.Set(Keys.STATUS, status);
		}
	}
}
