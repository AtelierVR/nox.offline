using Cysharp.Threading.Tasks;
using Nox.CCK.Sessions;
using Nox.CCK.Worlds;
using Nox.CCK.Network.Assets;
using Nox.Sessions;
using Nox.Worlds;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Offline.Runtime {

	public static class Helper {
		public const string IdFormat = "offline_{0}";

		public static ISession Create(Options options) {
			var session = new Session(string.Format(IdFormat, System.Guid.NewGuid()));
			session.SetWorld(options.Identifier);
			session.SetTitle(options.Title);
			session.SetThumbnail(options.Thumbnail);
			session.SetDisposeOnChange(options.DisposeOnChange);
			session.NotifyInfoUpdated();
			session.UpdateState(Status.Pending, "Preparing...", 0f);
			session.Prepare(options).Forget();
			return session;
		}

		private static async UniTask Prepare(this Session session, Options options) {
			IRuntimeWorld scene;

			if (options.WorldType == 1) {
				session.UpdateState(Status.Pending, "Fetching world data...", 0.05f);

				var bundle = await Main.WorldAPI.ResolveBundle(options.Identifier);

				if (bundle == null || string.IsNullOrEmpty(bundle.Url)) {
					Logger.LogError($"Failed to find a compatible bundle for world {options.Identifier}", session.Tag);
					session.UpdateState(Status.Error, $"World '{options.Identifier.ToString()}' not found", 1f);
					return;
				}

				var hash = bundle.CacheKey();

				if (string.IsNullOrEmpty(hash)) {
					Logger.LogError($"The bundle of world {options.Identifier} carries no hash", session.Tag);
					session.UpdateState(Status.Error, $"World '{options.Identifier.ToString()}' not found", 1f);
					return;
				}

				session.UpdateState(Status.Pending, $"Preparing world '{options.Identifier.ToString()}'...", 0.1f);

				if (!Main.WorldAPI.HasInCache(hash)) {
					session.UpdateState(Status.Pending, $"Downloading world '{options.Identifier.ToString()}'...", 0.15f);
					var download = Main.WorldAPI.DownloadToCache(
						bundle.Url,
						hash: hash,
						progress: arg0 => session.UpdateState(Status.Pending, $"Downloading world '{options.Identifier.ToString()}'...", 0.15f + arg0 * 0.45f)
					);
					await download.Start();
				}

				session.UpdateState(Status.Pending, $"Loading world '{options.Identifier}'...", 0.6f);
				scene = await Main.WorldAPI.LoadFromCache(hash);
			} else if (options.WorldType == 2) {
				session.UpdateState(Status.Pending, "Loading world resource...", 0.1f);

				scene = await Main.WorldAPI.LoadFromAssets(
					options.WorldResource,
					progress: arg0 => session.UpdateState(Status.Pending, $"Loading world '{options.Identifier.ToString()}'...", 0.1f + arg0 * 0.5f)
				);
			} else if (options.WorldType == 3) {
				// Chargement direct depuis le cache local par hash, sans appel réseau
				session.UpdateState(Status.Pending, $"Loading world from local cache (hash: {options.WorldHash})...", 0.1f);
				scene = await Main.WorldAPI.LoadFromCache(
					options.WorldHash,
					progress: arg0 => session.UpdateState(Status.Pending, "Loading world from local cache...", 0.1f + arg0 * 0.5f)
				);
			} else {
				Logger.LogError("No valid world specified for offline session.", session.Tag);
				session.UpdateState(Status.Error, "No valid world specified", 1f);
				return;
			}

			if (scene == null) {
				Logger.LogError($"Failed to load scene for world {options.Identifier.ToString()} with version {options.Identifier.GetVersion()}", session.Tag);
				session.UpdateState(Status.Error, $"Failed to load world '{options.Identifier.ToString()}'", 1f);
				return;
			}

			session.UpdateState(Status.Pending, $"World '{options.Identifier.ToString()}' loaded successfully", 0.65f);

			scene.Identifier = options.Identifier;
			session.SetDimension(scene);

			if (options.ChangeCurrent) {
				session.UpdateState(Status.Pending, $"Setting world '{options.Identifier.ToString()}' as current", 0.8f);
				await Main.SessionAPI.SetCurrent(session.Id);
			}

			session.UpdateState(Status.Ready, $"World '{options.Identifier.ToString()}' is ready", 1f);
		}
	}
}