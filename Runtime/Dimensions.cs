using System.Linq;
using Cysharp.Threading.Tasks;
using Nox.Sessions;
using Nox.Worlds;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Offline.Runtime {
	public class Dimensions : IDimensions {

		private const int UnloadedIndex = -1;
		public const int MainIndex = 0;

		internal Dimensions(Session session, IRuntimeWorld world) {
			_world = world;
			_session = session;
			_ids = new int[world.Dimensions.Count()];
			for (var i = 0; i < _ids.Length; i++)
				_ids[i] = UnloadedIndex;
		}

		/// <summary>
		/// The world associated with this dimension.
		/// </summary>
		private readonly IRuntimeWorld _world;
		private readonly Session _session;

		/// <summary>
		/// Redirect IRuntimeWorld instances ids, by local index.
		/// </summary>
		private readonly int[] _ids;

		/// <summary>
		/// Creates an instance at the given index if it is missing.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public async UniTask<bool> CreateIfMissing(int index) {
			if (index < 0 || index >= _ids.Length) {
				Logger.LogWarning($"Tried to create dimension instance at invalid index {index} for world {_world.Identifier}", tag: nameof(Dimensions));
				return false;
			}

			if (IsLoaded(index)) return true;
			if (!TryDimension(index, out var dimension)) {
				Logger.LogWarning($"Tried to create dimension instance at index {index} for world {_world.Identifier}, but dimension not found", tag: nameof(Dimensions));
				return false;
			}

			_ids[index] = await dimension.MakeInstance();
			Logger.LogDebug($"Created dimension instance at index {index}", tag: nameof(Dimensions));
			var descriptor = dimension.GetDescriptor(_ids[index]);
			var anchor = dimension.GetAnchor(_ids[index]);
			_session.OnSceneLoaded(index, descriptor, anchor);
			return true;
		}

		/// <summary>
		/// Sets the active state of the instance at the given index.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="isActive"></param>
		public void SetActive(int index, bool isActive) {
			if (!TryDimension(index, out var instance)) return;
			instance.SetVisibleInstance(_ids[index], isActive, isActive);
		}

		/// <summary>
		/// Tries to get the <see cref="IRuntimeWorldDimension"/> at the given index.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="dimension"></param>
		/// <returns></returns>
		private bool TryDimension(int index, out IRuntimeWorldDimension dimension) {
			dimension = null;
			if (index < 0 || index >= _ids.Length) {
				Logger.LogWarning($"Tried to get dimension at invalid index {index} for world {_world.Identifier}", tag: nameof(Dimensions));
				return false;
			}

			dimension = _world.GetDimension(index);
			if (dimension == null) {
				Logger.LogWarning($"Tried to get dimension at index {index} for world {_world.Identifier}, but dimension not found", tag: nameof(Dimensions));
				return false;
			}

			return true;
		}

		/// <summary>
		/// Gets the world identifier associated with this dimension.
		/// </summary>
		public IWorldIdentifier Identifier
			=> _world.Identifier;

		/// <summary>
		/// Indicates if the instance at the given index is loaded.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public bool IsLoaded(int index)
			=> index >= 0 && index < _ids.Length && _ids[index] != UnloadedIndex;

		/// <summary>
		/// Gets the world descriptor for the instance at the given index.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public IWorldDescriptor GetDescriptor(int index)
			=> TryDimension(index, out var dimension)
				? dimension.GetDescriptor(_ids[index])
				: null;

		/// <summary>
		/// Gets the anchor GameObject for the instance at the given index.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public GameObject GetAnchor(int index)
			=> TryDimension(index, out var dimension)
				? dimension.GetAnchor(_ids[index])
				: null;

		/// <summary>
		/// Gets the scene for the instance at the given index.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public Scene GetScene(int index)
			=> TryDimension(index, out var dimension)
				? dimension.GetScene()
				: default;

		/// <summary>
		/// Sets the world as the current active world.
		/// </summary>
		public void SetCurrent()
			=> _world.IsCurrent = true;

		/// <summary>
		/// Disposes the dimension
		/// and unloads all instances.
		/// </summary>
		public void Dispose() {
			for (var i = 0; i < _ids.Length; i++) {
				if (_ids[i] == UnloadedIndex) continue;
				if (TryDimension(i, out var instance))
					instance.RemoveInstance(_ids[i]);
				_ids[i] = UnloadedIndex;
				_session.OnSceneUnloaded(i);
			}
		}

		public IWorldDescriptor[] GetDescriptors() {
			var descriptors = new IWorldDescriptor[_ids.Length];
			for (var i = 0; i < _ids.Length; i++)
				descriptors[i] = GetDescriptor(i);
			return descriptors;
		}
	}
}