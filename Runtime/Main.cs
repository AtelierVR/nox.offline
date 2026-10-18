using System;
using System.Collections.Generic;
using Nox.Audio;
using Nox.CCK.Audio;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Events;
using Nox.Controllers;
using Nox.Sessions;
using Nox.Users;
using Nox.Worlds;

namespace Nox.Offline.Runtime {
	public class Main : ISessionRegister {
		public bool TryMakeSession(string name, Dictionary<string, object> options, out ISession session) {
			if (name != "offline") {
				session = null;
				return false;
			}

			session = Helper.Create(Options.From(options));
			return true;
		}

		internal static IMainModCoreAPI     CoreAPI;
		private        EventSubscription[] _events = Array.Empty<EventSubscription>();

		/// <summary>Voice volume/mute channel of the offline session (same one the relay uses).</summary>
		static internal ChannelRegister VoiceRegister;

		public void OnInitializeMain(IMainModCoreAPI api) {
			CoreAPI = api;

			// The voice channel is session-scoped: offline must register it too, otherwise the
			// deafen key, the deafen state and every listener of "voice" find no channel at all.
			VoiceRegister = new ChannelRegister("voice", new[] { "general" }, api);

			_events = new[] {
				api.EventAPI.Subscribe("controller_changed", OnControllerChanged),
				api.EventAPI.Subscribe("user_update", OnUserUpdate)
			};
			SessionAPI.Register(this);
		}

		private static void OnControllerChanged(EventData context) {
			if (!context.TryGet(0, out IController controller)) return;
			if (!SessionAPI.TryGet(SessionAPI.Current, out var s)) return;
			if (s is not Session session) return;
			session.OnControllerChanged(controller);
		}

		/// <summary>Refresh the plate when the local profile changes (login, rename...).</summary>
		private static void OnUserUpdate(EventData context) {
			if (!context.TryGet(0, out IUser user)) return;
			if (!SessionAPI.TryGet(SessionAPI.Current, out var s)) return;
			if (s is not Session session) return;
			session.OnUserUpdated(user);
		}

		public void OnDisposeMain() {
			SessionAPI.Unregister(this);
			foreach (var ev in _events)
				CoreAPI.EventAPI.Unsubscribe(ev);
			_events  = Array.Empty<EventSubscription>();

			VoiceRegister?.Dispose();
			VoiceRegister = null;

			CoreAPI = null;
		}

		static internal IWorldAPI WorldAPI
			=> CoreAPI.ModAPI
				.GetMod("worlds")
				.GetInstance<IWorldAPI>();

		static internal ISessionAPI SessionAPI
			=> CoreAPI.ModAPI
				.GetMod("session")
				.GetInstance<ISessionAPI>();

		static internal IControllerAPI ControllerAPI
			=> CoreAPI.ModAPI
				.GetMod("controllers")
				.GetInstance<IControllerAPI>();

		static internal IUserAPI UserAPI
			=> CoreAPI.ModAPI
				.GetMod("users")
				.GetInstance<IUserAPI>();

		/// <summary>Audio API of the audio mod (deafen state).</summary>
		static internal IAudioAPI AudioAPI
			=> CoreAPI?.ModAPI?
				.GetMod("audio")
				?.GetInstance<IAudioAPI>();

		/// <summary>Microphone API of the audio mod (the device the local player captures).</summary>
		static internal IMicrophoneAPI MicrophoneAPI
			=> CoreAPI?.ModAPI?
				.GetMod("audio")
				?.GetInstance<IMicrophoneAPI>();
	}
}