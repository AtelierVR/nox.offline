using System.Collections.Generic;
using Nox.CCK.Utils;
using Nox.Entities;

namespace Nox.Offline.Runtime {
	public class Entity : IEntity {
		internal readonly Entities Context;
		private Physical _physical;
		internal readonly List<IProperty> Properties = new();

		protected Entity(Entities context, int id) {
			Id      = id;
			Context = context;
			context.RegisterEntity(this);
		}

		public int Id { get; }

		public IProperty[] GetProperties()
			=> Properties.ToArray();

		public bool TryGetProperty(int key, out IProperty property) {
			foreach (var prop in Properties) {
				if (prop.Key != key)
					continue;
				property = prop;
				return true;
			}

			property = null;
			return false;
		}

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
			_physical.Destroy();
			_physical = null;
			OnPhysicalDestroyed();
		}

		public void Dispose() {
			DestroyPhysical();
			Context.UnregisterEntity(this);
		}

		public override string ToString()
			=> $"{GetType().Name}[Id={Id}]";
	}
}