using System.Collections.Generic;
using System.Linq;
using Nox.CCK.Utils;
using Nox.Entities;

namespace Nox.Offline.Runtime {
	public class Entities : IEntities {
		public const int LocalPlayerId   = 0;
		public const int InvalidEntityId = -1;

		internal Entities(Session context)
			=> Context = context;

		internal readonly Session Context;

		public int MasterId { get; set; } = InvalidEntityId;
		public int LocalId  { get; set; } = InvalidEntityId;


		private readonly Dictionary<int, Entity> _entities = new();

		internal void RegisterEntity(Entity entity) {
			if (!_entities.TryAdd(entity.Id, entity)) {
				Logger.LogWarning($"Entity with ID {entity.Id} is already registered.", tag: Context.Tag);
				return;
			}

			Context.HandleEntityRegistered(entity);
		}

		internal void UnregisterEntity(Entity entity) {
			if (!_entities.Remove(entity.Id)) {
				Logger.LogWarning($"Entity with ID {entity.Id} is already not registered.", tag: Context.Tag);
				return;
			}

			Context.HandleEntityUnregistered(entity);
		}

		public IEntity GetEntity(int id)
			=> _entities.GetValueOrDefault(id);

		public T GetEntity<T>(int id) where T : IEntity
			=> GetEntity(id) is T e ? e : default;

		public IEntity[] GetEntities()
			=> _entities.Values.ToArray<IEntity>();

		public T[] GetEntities<T>() where T : IEntity
			=> _entities.Values.OfType<T>().ToArray();

		public bool HasEntity(int id)
			=> _entities.ContainsKey(id);

		public bool HasEntity<T>(int id) where T : IEntity
			=> GetEntity(id) is T;

		public int GetCount()
			=> _entities.Count;

		public int GetCount<T>() where T : IEntity
			=> _entities.Values.OfType<T>().Count();

		// ReSharper disable Unity.PerformanceAnalysis
		public void Dispose() {
			foreach (var entity in _entities.Values.ToArray())
				entity.Dispose();
			_entities.Clear();
		}

		private int NextEntityId()
			=> _entities.Keys.Count != 0
				? _entities.Keys.Max() + 1
				: 1;

		public Player NewPlayer() {
			var player = GetEntity<Player>(LocalPlayerId);

			if (player == null) {
				player  = new Player(this, LocalPlayerId);
				LocalId = player.Id;
			} else {
				Logger.LogWarning($"Player with ID {player.Id} is already registered.", tag: Context.Tag);
			}

			return player;
		}
	}
}