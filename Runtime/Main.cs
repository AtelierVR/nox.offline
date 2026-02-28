using System;
using System.Collections.Generic;
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

		public void OnInitializeMain(IMainModCoreAPI api) {
			CoreAPI = api;
			_events = new[] {
				api.EventAPI.Subscribe("controller_changed", OnControllerChanged)
			};
			SessionAPI.Register(this);
		}

		private static void OnControllerChanged(EventData context) {
			if (!context.TryGet(0, out IController controller)) return;
			if (!SessionAPI.TryGet(SessionAPI.Current, out var s)) return;
			if (s is not Session session) return;
			session.OnControllerChanged(controller);
		}

		public void OnDisposeMain() {
			SessionAPI.Unregister(this);
			foreach (var ev in _events)
				CoreAPI.EventAPI.Unsubscribe(ev);
			_events  = Array.Empty<EventSubscription>();
			CoreAPI = null;
		}

		internal static IWorldAPI WorldAPI
			=> CoreAPI.ModAPI
				.GetMod("world")
				.GetInstance<IWorldAPI>();

		internal static ISessionAPI SessionAPI
			=> CoreAPI.ModAPI
				.GetMod("session")
				.GetInstance<ISessionAPI>();

		internal static IControllerAPI ControllerAPI
			=> CoreAPI.ModAPI
				.GetMod("controller")
				.GetInstance<IControllerAPI>();

		internal static IUserAPI UserAPI
			=> CoreAPI.ModAPI
				.GetMod("users")
				.GetInstance<IUserAPI>();
	}
}