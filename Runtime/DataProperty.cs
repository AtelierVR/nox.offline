using System;
using Nox.CCK;
using Nox.CCK.Network;
using Nox.Entities;

namespace Nox.Offline.Runtime {
	/// <summary>
	/// An <see cref="IProperty"/> backed by an entry of the entity data container.
	/// <para>
	/// Offline sessions have no avatar parameters to expose, so the properties shown by the editor
	/// panels are the values the player keeps in its <see cref="IDataContainer"/> (health, movement,
	/// team, nameplate...). The value is read from the container, so it always reflects the current
	/// state; <see cref="UpdatedAt"/> is refreshed by <see cref="Touch"/> when the container changes.
	/// </para>
	/// </summary>
	public sealed class DataProperty : IProperty {
		private readonly IDataContainer _data;

		public DataProperty(IDataContainer data, string name) {
			_data = data ?? throw new ArgumentNullException(nameof(data));
			Name  = name ?? throw new ArgumentNullException(nameof(name));
			Key   = name.Hash();
		}

		/// <inheritdoc />
		public int Key { get; }

		/// <inheritdoc />
		public string Name { get; }

		/// <summary>A value of the data container carries no category.</summary>
		public string Category
			=> null;

		/// <inheritdoc />
		public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

		/// <inheritdoc />
		public PropertyFlags Flags
			=> PropertyFlags.None;

		/// <inheritdoc />
		public bool IsDirty { get; set; }

		/// <inheritdoc />
		public object Value {
			get => _data.Get<object>(Name);
			set {
				_data.Set(Name, value);
				Touch();
			}
		}

		/// <summary>
		/// Marks the value as changed: the editors compare <see cref="UpdatedAt"/> to know what to
		/// redraw, and the container raises its change event on every write.
		/// </summary>
		internal void Touch()
			=> UpdatedAt = DateTime.UtcNow;

		/// <inheritdoc />
		public byte[] Serialize()
			=> Serializer.ToBytes(Value);

		/// <inheritdoc />
		public void Deserialize(byte[] data) {
			Value = data;
			Touch();
		}

		public override string ToString()
			=> $"{Name} = {Value ?? "null"}";
	}
}
