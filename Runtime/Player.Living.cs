using Nox.CCK.Entities;
using Nox.Entities;

namespace Nox.Offline.Runtime {
	/// <summary>
	/// <see cref="ILivingEntity"/> state of an offline player, stored in the entity data container.
	/// </summary>
	public partial class Player {
		#region ILivingEntity

		/// <inheritdoc />
		public float MaxHealth {
			get => Data.Get("max_health", EntityConstants.DefaultMaxHealth);
			set => Data.Set("max_health", value);
		}

		/// <inheritdoc />
		public float Health {
			get => Data.Get("health", EntityConstants.DefaultHealth);
			set => Data.Set("health", value);
		}

		/// <inheritdoc />
		public bool IsInvisible {
			get => Data.Get("invisible", EntityConstants.DefaultInvisible);
			set => Data.Set("invisible", value);
		}

		#endregion
	}
}
