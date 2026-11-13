using System.Collections.Generic;
using Nox.CCK;
using Nox.CCK.Sessions;
using Nox.CCK.Utils;
using Nox.Entities;

namespace Nox.Offline.Runtime {
	public class Entity : IEntity {
		internal readonly Entities Context;
		private Physical _physical;

		/// <summary>
		/// Properties of the entity, cached per data entry: an offline entity has no avatar parameters
		/// to publish, so what it exposes are the entries of its <see cref="IDataContainer"/>.
		/// </summary>
		internal readonly List<IProperty> Properties = new();

		private readonly DataContainer _data = new();

		protected Entity(Entities context, uint id) {
			Id      = id;
			Context = context;
			_data.OnChanged.AddListener(OnDataChanged);
			context.RegisterEntity(this);
		}

		public uint Id { get; }

		/// <summary>
		/// Values the entity keeps in its data container, as properties (one per entry). Built on
		/// demand, so a key written later shows up without anybody declaring it.
		/// </summary>
		public IProperty[] GetProperties() {
			foreach (var name in _data.Keys)
				GetOrCreateProperty(name);

			return Properties.ToArray();
		}

		/// <summary>Cached property of a data entry, created when it is first seen.</summary>
		private DataProperty GetOrCreateProperty(string name) {
			foreach (var property in Properties)
				if (property is DataProperty data && data.Name == name)
					return data;

			var created = new DataProperty(_data, name);
			Properties.Add(created);
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
				Properties.RemoveAll(property => property is DataProperty data && data.Name == name);
				return;
			}

			GetOrCreateProperty(name).Touch();
		}

		public bool TryGetProperty(int key, out IProperty property) {
			// Makes sure the entries written since the last call are cached.
			GetProperties();

			foreach (var prop in Properties) {
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

		virtual protected Physical InstantiatePhysical() {
			Logger.LogWarning($"Entity {Id} does not implement {nameof(InstantiatePhysical)}, cannot create physical representation.", tag: nameof(Entity));
			return null;
		}

		public bool HasPhysical()
			=> _physical;

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

		public bool MakePhysical() {
			if (_physical)
				return true;
			_physical = InstantiatePhysical();
			if (!_physical)
				return false;
			OnPhysicalCreated();
			return true;
		}

		public void DestroyPhysical() {
			if (!_physical)
				return;
			_physical.gameObject.DestroyImmediate();
			_physical = null;
			OnPhysicalDestroyed();
		}

		public virtual void Dispose() {
			DestroyPhysical();
			_data.Clear();
			Context.UnregisterEntity(this);
		}

		public override string ToString()
			=> $"{GetType().Name}[Id={Id}]";
	}
}