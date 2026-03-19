using System.Collections.Generic;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using Nox.Worlds;
using UnityEngine;

namespace Nox.Offline.Runtime {
	public class Options {
		public static Options From(Dictionary<string, object> dict) {
			var options = new Options();
			if (dict == null) return options;
			if (dict.TryGetValue("title", out var titleObj) && titleObj is string titleStr)
				options.Title = titleStr;
			if (dict.TryGetValue("thumbnail", out var thumbnailObj) && thumbnailObj is Texture2D thumbnailTex)
				options.Thumbnail = thumbnailTex;
			if (dict.TryGetValue("dispose_on_change", out var disposeObj) && disposeObj is bool disposeBool)
				options.DisposeOnChange = disposeBool;
			if (dict.TryGetValue("short_name", out var shortNameObj) && shortNameObj is string shortNameStr)
				options.ShortName = shortNameStr;
			if (dict.TryGetValue("change_current", out var changeCurrentObj) && changeCurrentObj is bool changeCurrentBool)
				options.ChangeCurrent = changeCurrentBool;
			if (dict.TryGetValue("world", out var worldObj))
				switch (worldObj) {
					case WorldIdentifier worldId:
						options.WorldType = 1;
						options.WorldIdentifier = worldId;
						break;
					case ResourceIdentifier resId:
						options.WorldType = 2;
						options.WorldResource = resId;
						break;
				}

			// WorldType=3 : chargement direct depuis le cache via hash, sans aucun fetch réseau
			if (dict.TryGetValue("world_hash", out var worldHashObj) && worldHashObj is string worldHashStr && !string.IsNullOrEmpty(worldHashStr)) {
				options.WorldType = 3;
				options.WorldHash = worldHashStr;
				if (dict.TryGetValue("world_identifier", out var worldIdObj))
					switch (worldIdObj) {
						case WorldIdentifier worldId:
							options.WorldIdentifier = worldId;
							break;
						case IWorldIdentifier iWorldId:
							options.WorldIdentifier = CCK.Worlds.WorldIdentifier.From(iWorldId);
							break;
						case string worldIdStr2 when !string.IsNullOrEmpty(worldIdStr2):
							options.WorldIdentifier = CCK.Worlds.WorldIdentifier.From(worldIdStr2);
							break;
					}
			}

			return options;
		}

		public int WorldType = 0;

		public IWorldIdentifier WorldIdentifier = Nox.CCK.Worlds.WorldIdentifier.Invalid;
		public ResourceIdentifier WorldResource = ResourceIdentifier.Invalid;

		/// <summary>
		/// Hash du fichier world à charger depuis le cache local (WorldType=3).
		/// Aucun appel réseau n'est effectué.
		/// </summary>
		public string WorldHash = null;

		public string Title = "Offline Session";
		public Texture2D Thumbnail = null;
		public bool DisposeOnChange = true;
		public string ShortName = null;
		public bool ChangeCurrent = true;
	}
}