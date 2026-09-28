using Nox.CCK.Entities;
using Nox.Entities;
using Nox.Sessions;

namespace Nox.Offline.Runtime {
	/// <summary>
	/// <see cref="ITeamEntity"/> state of an offline player: only the team identifier lives in the
	/// entity data container (key <c>team</c>, <c>0</c> meaning none); the team itself is resolved from
	/// the session registry (<see cref="ITeamSession"/>), which the offline session implements.
	/// </summary>
	public partial class Player {
		#region ITeamEntity

		/// <inheritdoc />
		public ITeam Team {
			get {
				var id = Data.Get("team", EntityConstants.NoTeam);
				return id == EntityConstants.NoTeam
					? null
					: Context.Context?.GetTeam(id);
			}
			set => Data.Set("team", value?.Id ?? EntityConstants.NoTeam);
		}

		#endregion
	}
}
