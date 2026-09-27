using Nox.CCK.Nameplate;
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
		/// <summary>
		/// Pushes the level of the captured audio (<see cref="Audio"/>, the microphone bound by the
		/// controller's connector) onto the controller's plate (<c>Keys.VOICE</c>), driving the alpha
		/// of the plate's voice image. Called every frame by <see cref="OnUpdate"/>: the plate ignores
		/// an unchanged value.
		/// </summary>
		internal void UpdateNameplate() {
			if (Main.ControllerAPI?.Current is not INameplateHolder holder)
				return;

			var plate = holder.Nameplate;
			if (!plate.IsAlive())
				return;

			plate.Set(Keys.VOICE, Audio?.Level ?? 0f);
		}
	}
}
