using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.Entities;
using Nox.Players;
using Nox.Sessions;
using UnityEngine.Events;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.Offline.Runtime {
	/// <summary>
	/// Ownership of the entities of an offline session. Offline there is no relay and no other client:
	/// the local player is the authority, so the same flow as a relay session is kept (entity and modules
	/// are asked, then the ownership is applied and broadcast) but the answer is local and immediate.
	/// </summary>
	public sealed partial class Session : ISessionOwnership {
		/// <summary>Invoked when the ownership of an entity changed (entity, new owner, previous owner).</summary>
		public UnityEvent<IEntity, IPlayer, IPlayer> OnOwnershipChanged { get; } = new();

		/// <summary>Invoked when the ownership of an entity is asked (entity, requester, request id).</summary>
		public UnityEvent<IEntity, IPlayer, ushort> OnOwnershipRequested { get; } = new();

		/// <summary>
		/// Change the ownership of an entity. Offline the decision is taken here instead of being
		/// forwarded to a master: the entity and the modules decide, then it is applied straight away.
		/// </summary>
		public UniTask<OwnershipChangeResult> ChangeOwnership(
			uint entityId,
			uint playerId = 0,
			bool wait = false,
			CancellationToken token = default
		) {
			var entity = InterEntities.GetEntity(entityId);
			if (entity == null)
				return UniTask.FromResult(OwnershipChangeResult.Failed);

			var requester = InterEntities.LocalPlayer;
			var receiver = playerId == 0 ? requester : InterEntities.GetEntity<IPlayer>(playerId);

			if (receiver == null)
				return UniTask.FromResult(OwnershipChangeResult.Failed);

			if (!DecideOwnership(entity, requester, receiver))
				return UniTask.FromResult(OwnershipChangeResult.Refused);

			ApplyOwnership(entity, receiver, requester);

			return UniTask.FromResult(OwnershipChangeResult.Granted);
		}

		/// <summary>Nothing to refuse offline: a request never leaves the client.</summary>
		public UniTask<bool> RefuseOwnership(ushort request)
			=> UniTask.FromResult(false);

		/// <summary>Ask the entity and the session modules whether they accept the new owner.</summary>
		private bool DecideOwnership(IEntity entity, IPlayer requester, IPlayer receiver) {
			if (entity is IOwnedEntity owned
				&& owned.TryDecideOwnership(requester ?? receiver, receiver, out var answer)
				&& !answer)
				return false;

			OnOwnershipRequested.Invoke(entity, requester, 0);

			foreach (var module in Modules) {
				if (module is IOwnershipListener listener)
					listener.OnOwnershipRequested(entity, requester, receiver);

				if (module is IOwnershipDecision decision
					&& decision.TryDecideOwnership(entity, requester, receiver, out var moduleAnswer)
					&& !moduleAnswer)
					return false;
			}

			return true;
		}

		/// <summary>Apply an ownership decided locally and notify everybody watching the entity.</summary>
		internal void ApplyOwnership(IEntity entity, IPlayer receiver, IPlayer requester) {
			if (entity is not IOwnedEntity owned)
				return;

			Logger.LogDebug(
				$"Entity {entity.Id} is now owned by {receiver?.Id ?? 0} (requested by {requester?.Id ?? 0})",
				tag: Tag
			);

			var previous = owned.Owner;

			owned.ApplyOwner(receiver, requester);

			NotifyOwnership(entity, receiver, previous, requester);
		}

		/// <summary>Announce an ownership: the scripts, the modules and everybody listening to the entity.</summary>
		private void NotifyOwnership(
			IEntity entity,
			IPlayer receiver,
			IPlayer previous,
			IPlayer requester
		) {
			Main.CoreAPI.EventAPI.Emit(
				"session_entity_ownership_changed",
				this,
				entity,
				receiver,
				previous,
				requester
			);

			foreach (var module in Modules)
				if (module is IOwnershipListener listener)
					listener.OnOwnershipChanged(entity, receiver, previous);

			if (entity is IOwnedEntity owned)
				owned.OnOwnershipChanged.Invoke(receiver, previous);

			OnOwnershipChanged.Invoke(entity, receiver, previous);
		}

		/// <summary>
		/// The authority changed: the entities that follow it belong to whoever holds it now, so their owner
		/// changed too (nothing is transferred, the owner is resolved live): the events are raised again.
		/// </summary>
		private void RefreshMasterOwnership(IPlayer master, IPlayer previous) {
			foreach (var entity in InterEntities.GetEntities())
				if (entity is IOwnedEntity owned && FollowsMaster(entity, owned))
					NotifyOwnership(entity, master, previous, null);
		}

		/// <summary>
		/// Whether an entity follows the authority: it has no explicit owner, or the player it was granted
		/// to is gone (the ownership falls back to the master in both cases).
		/// </summary>
		private bool FollowsMaster(IEntity entity, IOwnedEntity owned)
			=> entity is Entity local
				? local.OwnerId == IOwnedEntity.MASTER_OWNER_ID
					|| InterEntities.GetEntity<IPlayer>(local.OwnerId) == null
				: owned.Owner == null;
	}
}
