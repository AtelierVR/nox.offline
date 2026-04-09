using System.Collections.Generic;
using Nox.CCK.Utils;
using Nox.CCK.Worlds;
using Nox.Worlds;
using UnityEngine;

namespace Nox.Offline.Runtime {
	public class Options {
		public static Options From(Dictionary<string, object> dict) {
			var options = new Options();
			if (dict == null)
				return options;
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
					case Identifier w:
						options.WorldType  = 1;
						options.Identifier = w;
						break;
					case ResourceIdentifier r:
						options.WorldType     = 2;
						options.WorldResource = r;
						break;
				}

			// WorldType=3 : chargement direct depuis le cache via hash, sans aucun fetch réseau
			if (dict.TryGetValue("world_hash", out var o) && o is string s && !string.IsNullOrEmpty(s)) {
				options.WorldType = 3;
				options.WorldHash = s;
				if (dict.TryGetValue("world_identifier", out var i))
					options.Identifier = i switch {
						Identifier w                           => w,
						string w when !string.IsNullOrEmpty(w) => Identifier.Parse(w),
						_                                      => options.Identifier
					};
			}

			return options;
		}

		public int WorldType = 0;

		public Identifier Identifier = Identifier.Invalid;
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