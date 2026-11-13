using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.Avatars;
using Nox.Avatars.Controllers;
using Nox.Avatars.Parameters;
using Nox.Avatars.Players;
using Nox.CCK.Utils;
using Nox.Controllers;

namespace Nox.Offline.Runtime {
	/// <summary>
	/// Avatar binding of the offline player.
	/// <para>
	/// The local controller owns the avatar, so the player does not load anything: it follows the
	/// avatar of the controller and publishes the parameters of the avatar's
	/// <see cref="IParameterModule"/> as entity properties
	/// (<see cref="AvatarParameterProperty"/>), the same view the relay player gives for its own
	/// avatar. That is what the editor panels list.
	/// </para>
	/// </summary>
	public partial class Player : IPlayerAvatar {
		/// <summary>Controller the parts are bound to, or null while none is current.</summary>
		private IControllerAvatar _controller;

		/// <summary>Avatar currently followed (the controller's), or null while none is loaded.</summary>
		private IRuntimeAvatar _avatar;

		/// <summary>Parameter module of <see cref="_avatar"/>, or null when it carries none.</summary>
		private IParameterModule _module;

		/// <summary>
		/// Identifier of the avatar currently loaded on the local controller, or
		/// <see cref="Identifier.Invalid"/> while none is.
		/// </summary>
		public Identifier GetAvatar()
			=> _avatar?.Identifier ?? Identifier.Invalid;

		/// <summary>Loads the given avatar on the local controller.</summary>
		public async UniTask<bool> SetAvatar(Identifier identifier)
			=> _controller != null && await _controller.SetAvatar(identifier) != null;

		/// <summary>
		/// Binds the controller the player is attached to: the avatar followed by the player is the
		/// one of this controller.
		/// </summary>
		internal void SetController(IController controller) {
			_controller = controller as IControllerAvatar;
			SyncAvatar();
		}

		/// <summary>
		/// Follows the avatar of the controller: rebinds the parameter module when the avatar or its
		/// module changed. Called every frame (cheap: one reference comparison in the steady state)
		/// because an avatar can be replaced on the same controller, which raises no event here.
		/// </summary>
		internal void SyncAvatar() {
			var controller = _controller;

			if (controller == null) {
				// Resolved lazily: the controller can be set before the session creates its player,
				// so the first binding would otherwise be missed.
				controller = _controller = Main.ControllerAPI?.Current as IControllerAvatar;
				if (controller == null)
					return;
			}

			var avatar = controller.GetAvatar();
			if (ReferenceEquals(avatar, _avatar))
				return;

			_avatar = avatar;
			Bind(avatar?.Descriptor?.GetModules<IParameterModule>().FirstOrDefault());
		}

		/// <summary>Binds a parameter module: its parameters become properties of the player.</summary>
		private void Bind(IParameterModule module) {
			// Same module: keep it, its parameters are already published.
			if (module != null && ReferenceEquals(_module, module))
				return;

			Unbind();

			if (module == null)
				return;

			_module = module;
			module.OnRegistred.AddListener(OnParameterRegistered);
			module.OnUnRegistred.AddListener(OnParameterUnregistered);

			foreach (var parameter in module.GetParameters() ?? Array.Empty<IParameter>())
				OnParameterRegistered(parameter);
		}

		/// <summary>Releases the module and the properties it published.</summary>
		private void Unbind() {
			_module?.OnRegistred.RemoveListener(OnParameterRegistered);
			_module?.OnUnRegistred.RemoveListener(OnParameterUnregistered);
			_module = null;

			// Their IParameter dies with the avatar: keeping them would read a disposed playable.
			Properties.RemoveAll(property => property is AvatarParameterProperty);
		}

		private void OnParameterRegistered(IParameter parameter) {
			if (parameter == null)
				return;

			for (var i = 0; i < Properties.Count; i++) {
				var existing = Properties[i];
				if (existing.Key != parameter.Key)
					continue;

				// The key is stable across avatars, the IParameter instance is not: a property still
				// bound to the previous avatar reads its disposed playable.
				if (existing is AvatarParameterProperty avatarProperty && !ReferenceEquals(avatarProperty.Parameter, parameter))
					Properties[i] = new AvatarParameterProperty(parameter);

				// A data entry owns this key (the same name is a value of the player): leave it.
				return;
			}

			Properties.Add(new AvatarParameterProperty(parameter));
		}

		private void OnParameterUnregistered(IParameter parameter) {
			if (parameter == null)
				return;

			Properties.RemoveAll(property => property is AvatarParameterProperty avatarProperty
				&& ReferenceEquals(avatarProperty.Parameter, parameter));
		}
	}
}
