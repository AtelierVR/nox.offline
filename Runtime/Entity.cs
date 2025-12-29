using System.Collections.Generic;
using Nox.CCK.Utils;
using Nox.Entities;

namespace Nox.Offline.Runtime {
	public class Entity : IEntity {
		internal readonly Entities        Context;
		private           Physical        _physical;
		internal readonly List<IProperty> Properties = new();

		protected Entity(Entities context, int id) {
			Id     = id;
			Context = context;
			context.RegisterEntity(this);
		}

		public int Id { get; }

		public IProperty[] GetProperties()
			=> Properties.ToArray();

		public bool TryGetProperty(int key, out IProperty property) {
			foreach (var prop in Properties) {
				if (prop.Key != key) continue;
				property = prop;
				return true;
			}

			property = null;
			return false;
		}

		protected virtual Physical InstantiatePhysical() {
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

		public bool MakePhysical() {
			if (_physical) return true;
			_physical = InstantiatePhysical();
			return _physical;
		}

		public void DestroyPhysical() {
			if (!_physical) return;
			_physical.Destroy();
			_physical = null;
		}

		public void Dispose() {
			DestroyPhysical();
			Context.UnregisterEntity(this);
		}

		public override string ToString()
			=> $"{GetType().Name}[Id={Id}]";
	}
}