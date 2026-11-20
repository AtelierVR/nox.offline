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
		/// of the plate's voice image. Called every frame by <see cref="OnUpdate"/>: the plate ignores an
		/// unchanged value.
		/// <para>
		/// The voice status (<c>Keys.STATUS</c>) is not pushed here: it only changes when the self-mute or the
		/// deafen state changes, which announce themselves (see <see cref="PushNameplateStatus"/>).
		/// </para>
		/// </summary>
		internal void UpdateNameplate() {
			// The controller of the player is the one it was bound to (see SetController): resolving
			// Main.ControllerAPI here walked the mod list again on every frame.
			if (_controller is not INameplateHolder holder)
				return;

			var plate = holder.Nameplate;
			if (!plate.IsAlive())
				return;

			plate.Set(Keys.VOICE, Audio?.Level ?? 0f);
		}

		/// <summary>
		/// Pushes the voice status of the player onto the controller's plate (<c>Keys.STATUS</c>: muted while
		/// self-muted, deafened while the voice channel is muted). Called when the state changes — self-mute,
		/// deafen — and when the plate is taken (see <see cref="ApplyToController"/>), never per frame.
		/// <para>
		/// Offline there is no listener-mute relation to display (the player is alone), so these are
		/// its own announced states, exactly what the relay feeds for a remote player.
		/// </para>
		/// </summary>
		internal void PushNameplateStatus() {
			if (_controller is not INameplateHolder holder)
				return;

			var plate = holder.Nameplate;
			if (!plate.IsAlive())
				return;

			var muted    = IsSelfMuted;
			var deafened = IsDeafened;

			var status = new Dictionary<string, string>(muted ? 2 : deafened ? 1 : 0);
			if (muted)
				status[_muteIcon] = "mute";
			if (deafened)
				status[_deafIcon] = "deaf";

			plate.Set(Keys.STATUS, status);
		}

		/// <summary>Status icon keys, resolved once: <see cref="NameplateExtensions.VoiceIcon"/> builds an id.</summary>
		private static readonly string _muteIcon = NameplateExtensions.VoiceIcon("mute").ToString();
		private static readonly string _deafIcon = NameplateExtensions.StatusIcon("deaf").ToString();
	}
}
