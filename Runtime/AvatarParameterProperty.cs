using System;
using Nox.Avatars.Parameters;
using Nox.CCK.Network;
using Nox.Entities;
using UnityEngine.Events;

namespace Nox.Offline.Runtime {
	/// <summary>
	/// An <see cref="IProperty"/> bound to a live avatar parameter of the local controller.
	/// <para>
	/// The offline session publishes no value over the network, so this property is a plain view on
	/// the parameter: <see cref="Value"/> reads the parameter every time, which is what the editor
	/// panels need to follow a live avatar. <see cref="UpdatedAt"/> is refreshed when the read value
	/// differs from the last one, so a panel polling <see cref="UpdatedAt"/> sees the change on the
	/// next tick.
	/// </para>
	/// </summary>
	public sealed class AvatarParameterProperty : IProperty {
		private readonly IParameter _parameter;
		private object _lastValue;

		public AvatarParameterProperty(IParameter parameter) {
			_parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
			_lastValue = _parameter.Value;
			Key = parameter.Key;
			Name = parameter.Name;
			UpdatedAt = DateTime.UtcNow;
		}

		/// <summary>The parameter this property is bound to.</summary>
		public IParameter Parameter
			=> _parameter;

		/// <inheritdoc />
		public UnityEvent<IProperty> OnUpdated { get; } = new();

		/// <inheritdoc />
		public int Key { get; }

		/// <inheritdoc />
		public string Name { get; }

		/// <summary>Category of the parameter, exposed so the editors can group the properties.</summary>
		public string Category
			=> _parameter.Category;

		/// <inheritdoc />
		public DateTime UpdatedAt { get; private set; }

		/// <summary>A local avatar parameter is never synchronized, so it carries no flag.</summary>
		public PropertyFlags Flags
			=> PropertyFlags.None;

		/// <inheritdoc />
		public bool IsDirty { get; set; }

		/// <inheritdoc />
		public object Value {
			get {
				// Read-through: the parameter is the source of truth (an animator or a module
				// writes it every frame). A change is only reported through UpdatedAt.
				var value = _parameter.Value;

				if (!AreEqual(value, _lastValue)) {
					_lastValue = value;
					UpdatedAt = DateTime.UtcNow;
				}

				return value;
			}
			set {
				_parameter.Value = value;
				_lastValue = _parameter.Value;
				UpdatedAt = DateTime.UtcNow;
			}
		}

		/// <inheritdoc />
		public byte[] Serialize()
			=> Value.ToBytes();

		/// <inheritdoc />
		public void Deserialize(byte[] data)
			=> Value = data;

		private static bool AreEqual(object a, object b) {
			if (ReferenceEquals(a, b))
				return true;

			if (a == null || b == null)
				return false;

			if (a is byte[] bytesA && b is byte[] bytesB) {
				if (bytesA.Length != bytesB.Length)
					return false;

				for (var i = 0; i < bytesA.Length; i++)
					if (bytesA[i] != bytesB[i])
						return false;

				return true;
			}

			return a.Equals(b);
		}

		public override string ToString()
			=> $"{Name} = {Value}";
	}
}
