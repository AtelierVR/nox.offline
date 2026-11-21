using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Nox.CCK;
using Nox.CCK.Sessions;
using Nox.CCK.Utils;
using Nox.Entities;
using Nox.Players;
using Nox.Sessions;
using UnityEngine.Events;

namespace Nox.Offline.Runtime {
	public class Entity : IPropertiesEntity, IPhysicalEntity, IOwnedEntity, IDisposable {
		internal readonly Entities Context;
		private Physical _physical;
		private PhysicalState _physicalState;

		/// <summary>
		/// Properties of the entity, cached per data entry: an offline entity has no avatar parameters
		/// to publish, so what it exposes are the entries of its <see cref="IDataContainer"/>.
		/// </summary>
		internal readonly List<IProperty> PropertyList = new();

		private readonly DataContainer _data = new();

		protected Entity(Entities context, uint id) {
			Id = id;
			Context = context;
			_data.OnChanged.AddListener(OnDataChanged);
			context.RegisterEntity(this);
		}

		public uint Id { get; set; }

		/// <summary>
		/// Entity id of the player owning this entity. Offline there is a single player, and an entity
		/// nobody claimed belongs to the session master (<see cref="Entities.MasterOwnerId"/>, the default).
		/// </summary>
		public uint OwnerId { get; internal set; } = Entities.MasterOwnerId;

		/// <summary>
		/// The player owning this entity: the session master while it has no explicit owner, and the master
		/// again when the player it was granted to is gone.
		/// </summary>
		public virtual IPlayer Owner
			=> OwnerId == Entities.MasterOwnerId
				? Context?.Context?.MasterPlayer
				: Context?.GetEntity<IPlayer>(OwnerId)
					?? Context?.Context?.MasterPlayer;

		/// <summary>The local player owns this entity: it drives its state.</summary>
		public bool IsOwner
			=> Owner is { IsLocal: true };

		/// <summary>Invoked when the ownership of this entity changes (new owner, previous owner).</summary>
		public virtual UnityEvent<IPlayer, IPlayer> OnOwnershipChanged { get; } = new();

		/// <summary>Apply an ownership decided by the session (offline the session grants everything).</summary>
		public virtual void ApplyOwner(IPlayer receiver, IPlayer requester)
			=> OwnerId = receiver?.Id
				?? Entities.MasterOwnerId;

		/// <summary>
		/// Values the entity keeps in its data container, as properties (one per entry). Built on
		/// demand, so a key written later shows up without anybody declaring it.
		/// </summary>
		public IProperty[] Properties {
			get {
				foreach (var name in _data.Keys)
					GetOrCreateProperty(name);

				return PropertyList.ToArray();
			}
		}

		/// <summary>Cached property of a data entry, created when it is first seen.</summary>
		private DataProperty GetOrCreateProperty(string name) {
			foreach (var property in PropertyList)
				if (property is DataProperty data && data.Name == name)
					return data;

			var created = new DataProperty(_data, name);
			PropertyList.Add(created);
			return created;
		}

		/// <summary>
		/// Keeps the cached properties in step with the container: a changed entry is flagged (the
		/// editors compare <see cref="IProperty.UpdatedAt"/>), a removed one leaves the list.
		/// </summary>
		private void OnDataChanged(string[] key, object @new, object old) {
			if (key is not { Length: > 0 })
				return;

			var name = string.Join(".", key);

			if (@new == null) {
				PropertyList.RemoveAll(property => property is DataProperty data && data.Name == name);
				return;
			}

			GetOrCreateProperty(name).Touch();
		}

		public bool TryGetProperty(int key, out IProperty property) {
			// Makes sure the entries written since the last call are cached.
			foreach (var name in _data.Keys)
				GetOrCreateProperty(name);

			foreach (var prop in PropertyList) {
				if (prop.Key != key)
					continue;
				property = prop;
				return true;
			}

			property = null;
			return false;
		}

		public IDataContainer Data
			=> _data;

		virtual protected UniTask<Physical> InstantiatePhysical() {
			Logger.LogWarning($"Entity {Id} does not implement {nameof(InstantiatePhysical)}, cannot create physical representation.", tag: nameof(Entity));
			return UniTask.FromResult<Physical>(null);
		}

		public PhysicalState PhysicalState
			=> _physicalState;

		public bool TryGetPhysical<T>(out T physical) where T : Physical {
			if (_physical is T p) {
				physical = p;
				return true;
			}

			physical = null;
			return false;
		}


		/// <summary>
		/// Called after the physical representation is successfully created.
		/// Override this in derived classes to perform additional initialization.
		/// </summary>
		virtual protected void OnPhysicalCreated() {
			// Base implementation does nothing
		}

		/// <summary>
		/// Called after the physical representation is destroyed.
		/// Override this in derived classes to perform additional cleanup.
		/// </summary>
		virtual protected void OnPhysicalDestroyed() {
			// Base implementation does nothing
		}

		public async UniTask<bool> MakePhysical() {
			if (_physical)
				return true;

			if (_physicalState != PhysicalState.None)
				return false;

			_physicalState = PhysicalState.Creating;
			try {
				_physical = await InstantiatePhysical();
			} finally {
				_physicalState = PhysicalState.None;
			}

			if (!_physical)
				return false;

			OnPhysicalCreated();
			return true;
		}

		public UniTask DestroyPhysical() {
			if (!_physical)
				return UniTask.CompletedTask;

			_physicalState = PhysicalState.Destroying;
			_physical.gameObject.DestroyImmediate();
			_physical = null;
			_physicalState = PhysicalState.None;
			OnPhysicalDestroyed();

			return UniTask.CompletedTask;
		}

		public virtual void Dispose() {
			DestroyPhysical().Forget();
			_data.Clear();
			Context.UnregisterEntity(this);
		}

		public override string ToString()
			=> $"{GetType().Name}[Id={Id}]";
	}
}
