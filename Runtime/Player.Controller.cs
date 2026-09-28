using Nox.CCK.Nameplate;
using Nox.CCK.Sessions;
using Nox.Controllers;

namespace Nox.Offline.Runtime {
	/// <summary>
	/// Controller binding of the offline player: pushes the player state (movement abilities,
	/// nameplate visibility) onto the active controller.
	/// </summary>
	public partial class Player {
		/// <summary>
		/// Starts replicating the player data onto the controller: every change is pushed again while
		/// this session is the current one (the list lives as long as the player's data container).
		/// </summary>
		internal void BindDataChanges()
			=> Data.OnChanged.AddListener(OnEntityDataChanged);

		/// <summary>
		/// Pushes the player state onto the controller: the movement abilities
		/// (<see cref="AbilitiesConstants.WalkForce"/>, ...), the team colour and the health bar of the
		/// plate. Called when the session becomes current (<c>ISessionAPI.SetCurrent</c> →
		/// <c>Session.OnSelect</c>) and again on every data change while it is current.
		/// <para>
		/// The plate <b>visibility</b> is deliberately not pushed here: it is client-wide and owned by
		/// the controller itself (its menu provider drives <c>Keys.VISIBLE</c>), so no entity — thus no
		/// script — can influence it.
		/// </para>
		internal void ApplyToController(IController controller) {
			if (controller == null)
				return;

			controller.SetAbilities(AbilitiesConstants.MaxMoveSpeed, WalkSpeed);
			controller.SetAbilities(AbilitiesConstants.MoveAcceleration, MoveAcceleration);
			controller.SetAbilities(AbilitiesConstants.JumpForce, JumpForce);
			controller.SetAbilities(AbilitiesConstants.SprintMultiplier, SprintMultiplier);
			controller.SetAbilities(AbilitiesConstants.AirControl, AirControl);
			controller.SetAbilities(AbilitiesConstants.FlySpeed, FlySpeed);
			controller.SetAbilities(AbilitiesConstants.MayFly, MayFly);
			controller.SetAbilities(AbilitiesConstants.Immobilized, IsImmobilized);
			controller.SetAbilities(AbilitiesConstants.Flying, IsFlying);
			controller.SetAbilities(AbilitiesConstants.Crouching, IsCrouching);
			controller.SetAbilities(AbilitiesConstants.Sprinting, IsSprinting);

			if (controller is INameplateHolder holder && holder.Nameplate.IsAlive()) {
				PushHealthbar(holder.Nameplate);
				PushTeam(holder.Nameplate);
			}
		}

		/// <summary>
		/// Re-applies the player state to the controller when the entity data changes, but only while
		/// this session is the current one.
		/// </summary>
		private void OnEntityDataChanged(string[] key, object @new, object @old) {
			if (!SessionHelper.IsCurrent(Main.SessionAPI, Context.Context))
				return;

			ApplyToController(Main.ControllerAPI?.Current);
		}
	}
}
