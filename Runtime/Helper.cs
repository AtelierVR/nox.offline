using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.CCK.Sessions;
using Nox.CCK.Utils;
using Nox.Sessions;
using Nox.Worlds;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Offline.Runtime {

	public static class Helper {
		public const string IdFormat = "offline_{0}";

		public static ISession Create(Options options) {
			var session = new Session(string.Format(IdFormat, System.Guid.NewGuid()));
			session.SetTitle(options.Title);
			session.SetThumbnail(options.Thumbnail);
			session.SetDisposeOnChange(options.DisposeOnChange);
			session.UpdateState(Status.Pending, "Preparing...", 0f);
			session.Prepare(options).Forget();
			return session;
		}

		private static async UniTask Prepare(this Session session, Options options) {
			IRuntimeWorld scene;

			if (options.WorldType == 1) {
				session.UpdateState(Status.Pending, "Fetching world data...", 0.05f);
				
				var asset = (await Main.WorldAPI.SearchAssets(
						options.WorldIdentifier.ToString(),
						Main.WorldAPI.MakeAssetSearchRequest()
							.SetEngines(new[] { EngineExtensions.CurrentEngine.GetEngineName() })
							.SetPlatforms(new[] { PlatformExtensions.CurrentPlatform.GetPlatformName() })
							.SetVersions(new[] { options.WorldIdentifier.Version })
							.SetLimit(1)
					)).GetAssets()
					.FirstOrDefault();

				if (asset == null) {
					Logger.LogError($"Failed to find asset for world {options.WorldIdentifier.ToString()} with version {options.WorldIdentifier.Version}");
					session.UpdateState(Status.Error, $"World '{options.WorldIdentifier.ToString()}' not found", 1f);
					return;
				}

				session.UpdateState(Status.Pending, $"Preparing world '{options.WorldIdentifier.ToString()}'...", 0.1f);

				if (!Main.WorldAPI.HasSceneInCache(asset.GetHash())) {
					session.UpdateState(Status.Pending, $"Downloading world '{options.WorldIdentifier.ToString()}'...", 0.15f);
					var download = Main.WorldAPI.DownloadSceneToCache(
						asset.GetUrl(),
						hash: asset.GetHash(),
						progress: arg0 => session.UpdateState(Status.Pending, $"Downloading world '{options.WorldIdentifier.ToString()}'...", 0.15f + arg0 * 0.45f)
					);
					await download.Start();
				}

				session.UpdateState(Status.Pending, $"Loading world '{options.WorldIdentifier}'...", 0.6f);
				scene = await Main.WorldAPI.LoadFromCache(asset.GetHash());
			} else if (options.WorldType == 2) {
				session.UpdateState(Status.Pending, "Loading world resource...", 0.1f);
				
				scene = await Main.WorldAPI.LoadFromAssets(
					options.WorldResource,
					progress: arg0 => session.UpdateState(Status.Pending, $"Loading world '{options.WorldIdentifier.ToString()}'...", 0.1f + arg0 * 0.5f)
				);
			} else {
				Logger.LogError("No valid world specified for offline session.");
				session.UpdateState(Status.Error, "No valid world specified", 1f);
				return;
			}

			if (scene == null) {
				Logger.LogError($"Failed to load scene for world {options.WorldIdentifier.ToString()} with version {options.WorldIdentifier.Version}");
				session.UpdateState(Status.Error, $"Failed to load world '{options.WorldIdentifier.ToString()}'", 1f);
				return;
			}

			session.UpdateState(Status.Pending, $"World '{options.WorldIdentifier.ToString()}' loaded successfully", 0.65f);

			scene.SetIdentifier(options.WorldIdentifier);
			session.SetDimension(scene);

			if (options.ChangeCurrent) {
				session.UpdateState(Status.Pending, $"Setting world '{options.WorldIdentifier.ToString()}' as current", 0.8f);
				await Main.SessionAPI.SetCurrent(session.Id);
			}

			session.UpdateState(Status.Ready, $"World '{options.WorldIdentifier.ToString()}' is ready", 1f);
		}
	}
}